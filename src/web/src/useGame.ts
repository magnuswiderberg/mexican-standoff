import { useCallback, useEffect, useReducer, useRef } from 'react'
import type { HubConnection } from '@microsoft/signalr'
import { createConnection, friendlyError } from './gameClient'
import { clearSeat, getSeat, saveSeat } from './session'
import type {
  ActionDto,
  GameSnapshot,
  GameView,
  JoinResult,
  LobbyView,
  PlayerLockedView,
  RoundResolvedView,
  RoundStartedView,
} from './types'

export type UiPhase =
  | 'connecting'
  | 'joining' // player page only: no seat yet, show the join form
  | 'lobby'
  | 'selecting'
  | 'revealing'
  | 'gameover'
  | 'fatal'

/** One queued reveal playback: the script plus the snapshot it starts from. */
export interface RevealJob {
  steps: RoundResolvedView['reveal']
  prev: GameSnapshot
  view: RoundResolvedView
}

interface State {
  phase: UiPhase
  fatalError: string | null
  actionError: string | null
  lobby: LobbyView | null
  snapshot: GameSnapshot | null
  deadline: string | null
  locked: PlayerLockedView | null
  hasSubmitted: boolean
  winnerIds: string[] | null
  winReason: string | null
  playerId: string | null
  reveal: RevealJob | null
}

const initial: State = {
  phase: 'connecting',
  fatalError: null,
  actionError: null,
  lobby: null,
  snapshot: null,
  deadline: null,
  locked: null,
  hasSubmitted: false,
  winnerIds: null,
  winReason: null,
  playerId: null,
  reveal: null,
}

type Msg =
  | { type: 'fatal'; error: string }
  | { type: 'actionError'; error: string | null }
  | { type: 'needJoin' }
  | { type: 'joined'; playerId: string; lobby: LobbyView }
  | { type: 'hydrate'; view: GameView }
  | { type: 'lobbyUpdated'; lobby: LobbyView }
  | { type: 'roundStarted'; view: RoundStartedView }
  | { type: 'playerLocked'; view: PlayerLockedView }
  | { type: 'submitted' }
  | { type: 'startReveal'; job: RevealJob }
  | { type: 'revealDone'; view: RoundResolvedView; nextJob: RevealJob | null }

function reduce(state: State, msg: Msg): State {
  switch (msg.type) {
    case 'fatal':
      return { ...state, phase: 'fatal', fatalError: msg.error }
    case 'actionError':
      return { ...state, actionError: msg.error }
    case 'needJoin':
      return { ...state, phase: 'joining' }
    case 'joined':
      return { ...state, phase: 'lobby', playerId: msg.playerId, lobby: msg.lobby }
    case 'hydrate': {
      const v = msg.view
      const phase: UiPhase =
        v.phase === 'Lobby' ? 'lobby' : v.phase === 'Selecting' ? 'selecting' : 'gameover'
      return {
        ...state,
        phase,
        lobby: v.lobby,
        snapshot: v.snapshot,
        deadline: v.deadline,
        playerId: v.playerId ?? state.playerId,
        hasSubmitted: v.hasSubmitted,
        winnerIds: v.winnerIds,
        winReason: v.winReason,
        locked: null,
        reveal: null,
        actionError: null,
      }
    }
    case 'lobbyUpdated':
      return { ...state, lobby: msg.lobby }
    case 'roundStarted':
      // Fires on game start and on rematch.
      return {
        ...state,
        phase: 'selecting',
        snapshot: msg.view.snapshot,
        deadline: msg.view.deadline,
        locked: null,
        hasSubmitted: false,
        winnerIds: null,
        winReason: null,
        reveal: null,
        actionError: null,
      }
    case 'playerLocked':
      return { ...state, locked: msg.view }
    case 'submitted':
      return { ...state, hasSubmitted: true, actionError: null }
    case 'startReveal':
      return { ...state, phase: 'revealing', reveal: msg.job, locked: null, actionError: null }
    case 'revealDone': {
      const v = msg.view
      if (msg.nextJob) return { ...state, snapshot: v.snapshot, reveal: msg.nextJob }
      const over = v.winnerIds !== null && v.winnerIds.length > 0
      return {
        ...state,
        phase: over ? 'gameover' : 'selecting',
        snapshot: v.snapshot,
        deadline: v.nextDeadline,
        winnerIds: v.winnerIds,
        winReason: v.winReason,
        hasSubmitted: false,
        locked: null,
        reveal: null,
      }
    }
  }
}

export interface GameApi extends State {
  join(name: string): Promise<void>
  start(): Promise<void>
  submitAction(action: ActionDto): Promise<void>
  submitSequence(sequence: ActionDto[]): Promise<void>
  rematch(): Promise<void>
  finishReveal(): void
}

/**
 * One SignalR connection per mounted page. `player` mode reconnects a stored
 * seat (or asks to join); `monitor` mode watches without a seat. Reveal scripts
 * are queued so a server-side timeout resolving the next round mid-playback
 * can never skip a reveal.
 */
export function useGame(code: string, mode: 'player' | 'monitor'): GameApi {
  const [state, dispatch] = useReducer(reduce, initial)
  const connRef = useRef<HubConnection | null>(null)
  // The seat token lives in memory once known; localStorage is only the
  // cross-refresh backup (two tabs on one device must not steal each other's seat).
  const tokenRef = useRef<string | null>(null)
  // Latest snapshot the server told us about (applied or not yet played).
  const snapshotRef = useRef<GameSnapshot | null>(null)
  const queueRef = useRef<RevealJob[]>([])
  const playingRef = useRef(false)

  useEffect(() => {
    let disposed = false
    const conn = createConnection()
    connRef.current = conn
    snapshotRef.current = null
    queueRef.current = []
    playingRef.current = false

    conn.on('LobbyUpdated', (lobby: LobbyView) => {
      if (!disposed) dispatch({ type: 'lobbyUpdated', lobby })
    })

    conn.on('RoundStarted', (view: RoundStartedView) => {
      if (disposed) return
      snapshotRef.current = view.snapshot
      queueRef.current = []
      playingRef.current = false
      dispatch({ type: 'roundStarted', view })
    })

    conn.on('PlayerLocked', (view: PlayerLockedView) => {
      if (!disposed) dispatch({ type: 'playerLocked', view })
    })

    conn.on('RoundResolved', (view: RoundResolvedView) => {
      if (disposed) return
      const prev = snapshotRef.current ?? view.snapshot
      snapshotRef.current = view.snapshot
      const job: RevealJob = { steps: view.reveal, prev, view }
      if (playingRef.current) {
        queueRef.current.push(job)
      } else {
        playingRef.current = true
        dispatch({ type: 'startReveal', job })
      }
    })

    async function hydrate() {
      if (mode === 'monitor') {
        const view = await conn.invoke<GameView>('WatchGame', code)
        if (disposed) return
        snapshotRef.current = view.snapshot
        dispatch({ type: 'hydrate', view })
        return
      }

      const token = tokenRef.current ?? getSeat(code)?.token
      if (!token) {
        dispatch({ type: 'needJoin' })
        return
      }
      try {
        const view = await conn.invoke<GameView>('Reconnect', code, token)
        if (disposed) return
        tokenRef.current = token
        snapshotRef.current = view.snapshot
        dispatch({ type: 'hydrate', view })
      } catch {
        // Stale seat (server restarted, game gone) — join fresh.
        if (disposed) return
        tokenRef.current = null
        clearSeat(code)
        dispatch({ type: 'needJoin' })
      }
    }

    // SignalR group membership is per connection, so a reconnect must
    // re-subscribe and re-hydrate whatever was missed while offline.
    conn.onreconnected(() => {
      hydrate().catch((e) => dispatch({ type: 'fatal', error: friendlyError(e) }))
    })

    conn
      .start()
      .then(hydrate)
      .catch((e) => {
        if (!disposed) dispatch({ type: 'fatal', error: friendlyError(e) })
      })

    return () => {
      disposed = true
      connRef.current = null
      conn.stop().catch(() => {})
    }
  }, [code, mode])

  const invoke = useCallback(
    async (method: string, ...args: unknown[]): Promise<unknown> => {
      const conn = connRef.current
      if (!conn) throw new Error('Not connected.')
      return conn.invoke(method, ...args)
    },
    [],
  )

  const join = useCallback(
    async (name: string) => {
      try {
        const result = (await invoke('JoinGame', code, name)) as JoinResult
        tokenRef.current = result.playerToken
        saveSeat(code, { playerId: result.playerId, token: result.playerToken, name })
        dispatch({ type: 'joined', playerId: result.playerId, lobby: result.lobby })
      } catch (e) {
        dispatch({ type: 'actionError', error: friendlyError(e) })
      }
    },
    [code, invoke],
  )

  const start = useCallback(async () => {
    try {
      await invoke('StartGame', code)
    } catch (e) {
      dispatch({ type: 'actionError', error: friendlyError(e) })
    }
  }, [code, invoke])

  const submitAction = useCallback(
    async (action: ActionDto) => {
      const token = tokenRef.current
      if (!token) return
      try {
        await invoke('SubmitAction', code, token, action)
        dispatch({ type: 'submitted' })
      } catch (e) {
        dispatch({ type: 'actionError', error: friendlyError(e) })
      }
    },
    [code, invoke],
  )

  const submitSequence = useCallback(
    async (sequence: ActionDto[]) => {
      const token = tokenRef.current
      if (!token) return
      try {
        await invoke('SubmitDuelSequence', code, token, sequence)
        dispatch({ type: 'submitted' })
      } catch (e) {
        dispatch({ type: 'actionError', error: friendlyError(e) })
      }
    },
    [code, invoke],
  )

  const rematch = useCallback(async () => {
    try {
      await invoke('Rematch', code)
    } catch (e) {
      dispatch({ type: 'actionError', error: friendlyError(e) })
    }
  }, [code, invoke])

  const finishReveal = useCallback(() => {
    const current = state.reveal
    if (!current) return
    const nextJob = queueRef.current.shift() ?? null
    playingRef.current = nextJob !== null
    dispatch({ type: 'revealDone', view: current.view, nextJob })
  }, [state.reveal])

  return {
    ...state,
    join,
    start,
    submitAction,
    submitSequence,
    rematch,
    finishReveal,
  }
}
