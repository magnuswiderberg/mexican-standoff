import { useCallback, useEffect, useReducer, useRef } from 'react'
import type { HubConnection } from '@microsoft/signalr'
import { createConnection, friendlyError } from './gameClient'
import {
  clearSeat,
  loadMonitorToken,
  loadSeat,
  releaseSeatHold,
  saveMonitorToken,
  saveSeat,
} from './session'
import type {
  ActionDto,
  GameSnapshot,
  GameView,
  JoinResult,
  LobbyView,
  MonitorDecisionView,
  MonitorRequestView,
  PlayerLockedView,
  RoundResolvedView,
  RoundStartedView,
} from './types'

export type UiPhase =
  | 'connecting'
  | 'joining' // player page only: no seat yet, show the join form
  | 'pairing' // monitor page only: no monitor token, waiting for the host to hand one over
  | 'lobby'
  | 'selecting'
  | 'revealing'
  | 'gameover'
  | 'stopped' // the monitor stopped the game; terminal info screen
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
  /** Local echo of what we locked in (action, or duel sequence), for the waiting banner. */
  submittedAction: ActionDto | null
  submittedSequence: ActionDto[] | null
  winnerIds: string[] | null
  winReason: string | null
  playerId: string | null
  reveal: RevealJob | null
  /** Local echo of our own resign, so the UI flips before the next snapshot arrives. */
  resigned: boolean
  /** A monitor page is watching — rematches start from the monitor, not the phones. */
  hasMonitor: boolean
  /** Some screen is asking to become the board; the host's device renders the prompt. */
  pendingMonitor: MonitorRequestView | null
  /** This screen's own request while it waits to be made the board (monitor mode). */
  myRequest: MonitorRequestView | null
  /** Why this screen is not the board: the host said no, a game is running, nobody answered. */
  pairingError: string | null
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
  submittedAction: null,
  submittedSequence: null,
  winnerIds: null,
  winReason: null,
  playerId: null,
  reveal: null,
  resigned: false,
  hasMonitor: false,
  pendingMonitor: null,
  myRequest: null,
  pairingError: null,
}

type Msg =
  | { type: 'fatal'; error: string }
  | { type: 'actionError'; error: string | null }
  | { type: 'needJoin'; lobby: LobbyView | null }
  | { type: 'joined'; playerId: string; lobby: LobbyView }
  | { type: 'hydrate'; view: GameView }
  | { type: 'lobbyUpdated'; lobby: LobbyView }
  | { type: 'roundStarted'; view: RoundStartedView }
  | { type: 'playerLocked'; view: PlayerLockedView }
  | { type: 'submitted'; action?: ActionDto; sequence?: ActionDto[] }
  | { type: 'resigned' }
  | { type: 'returnedToLobby'; lobby: LobbyView }
  | { type: 'stopped' }
  | { type: 'monitorPresence'; hasMonitor: boolean }
  | { type: 'monitorRequested'; request: MonitorRequestView | null }
  | { type: 'pairing'; request: MonitorRequestView }
  | { type: 'pairingRefused'; error: string }
  | { type: 'startReveal'; job: RevealJob }
  | { type: 'revealDone'; view: RoundResolvedView; nextJob: RevealJob | null }

function reduce(state: State, msg: Msg): State {
  switch (msg.type) {
    case 'fatal':
      return { ...state, phase: 'fatal', fatalError: msg.error }
    case 'actionError':
      return { ...state, actionError: msg.error }
    case 'needJoin':
      return { ...state, phase: 'joining', lobby: msg.lobby ?? state.lobby }
    case 'joined':
      return { ...state, phase: 'lobby', playerId: msg.playerId, lobby: msg.lobby }
    case 'hydrate': {
      const v = msg.view
      const phase: UiPhase =
        v.phase === 'Lobby'
          ? 'lobby'
          : v.phase === 'Selecting'
            ? 'selecting'
            : v.phase === 'Stopped'
              ? 'stopped'
              : 'gameover'
      const playerId = v.playerId ?? state.playerId
      return {
        ...state,
        phase,
        lobby: v.lobby,
        snapshot: v.snapshot,
        deadline: v.deadline,
        playerId,
        hasSubmitted: v.hasSubmitted,
        // A rehydrate (refresh/reconnect) can't recover what we picked — the
        // banner falls back to a plain "Locked in."
        submittedAction: null,
        submittedSequence: null,
        winnerIds: v.winnerIds,
        winReason: v.winReason,
        locked: null,
        reveal: null,
        actionError: null,
        resigned: v.snapshot?.players.find((p) => p.id === playerId)?.isResigned ?? false,
        hasMonitor: v.hasMonitor,
        pendingMonitor: v.pendingMonitor,
      }
    }
    case 'lobbyUpdated':
      return { ...state, lobby: msg.lobby }
    case 'roundStarted':
      // Fires when a game starts from the lobby (resign flags reset server-side).
      return {
        ...state,
        phase: 'selecting',
        snapshot: msg.view.snapshot,
        deadline: msg.view.deadline,
        locked: null,
        hasSubmitted: false,
        submittedAction: null,
        submittedSequence: null,
        winnerIds: null,
        winReason: null,
        reveal: null,
        actionError: null,
        resigned: false,
      }
    case 'playerLocked':
      return { ...state, locked: msg.view }
    case 'submitted':
      return {
        ...state,
        hasSubmitted: true,
        submittedAction: msg.action ?? null,
        submittedSequence: msg.sequence ?? null,
        actionError: null,
      }
    case 'resigned':
      return { ...state, resigned: true, actionError: null }
    case 'returnedToLobby':
      return {
        ...state,
        phase: 'lobby',
        lobby: msg.lobby,
        snapshot: null,
        deadline: null,
        locked: null,
        hasSubmitted: false,
        submittedAction: null,
        submittedSequence: null,
        winnerIds: null,
        winReason: null,
        reveal: null,
        actionError: null,
        resigned: false,
      }
    case 'stopped':
      return { ...state, phase: 'stopped', deadline: null, locked: null, reveal: null }
    case 'monitorPresence':
      return { ...state, hasMonitor: msg.hasMonitor }
    case 'monitorRequested':
      return { ...state, pendingMonitor: msg.request }
    case 'pairing':
      return { ...state, phase: 'pairing', myRequest: msg.request, pairingError: null }
    case 'pairingRefused':
      return { ...state, phase: 'pairing', myRequest: null, pairingError: msg.error }
    case 'startReveal':
      return { ...state, phase: 'revealing', reveal: msg.job, locked: null, actionError: null }
    case 'revealDone': {
      const v = msg.view
      if (msg.nextJob) return { ...state, snapshot: v.snapshot, reveal: msg.nextJob }
      // Non-null (even empty: mutual destruction, no winner) means game over.
      const over = v.winnerIds !== null
      return {
        ...state,
        phase: over ? 'gameover' : 'selecting',
        snapshot: v.snapshot,
        deadline: v.nextDeadline,
        winnerIds: v.winnerIds,
        winReason: v.winReason,
        hasSubmitted: false,
        submittedAction: null,
        submittedSequence: null,
        locked: null,
        reveal: null,
      }
    }
  }
}

export interface GameApi extends State {
  join(name: string, color: string | null): Promise<void>
  leave(): Promise<void>
  kick(targetPlayerId: string): Promise<void>
  addBot(): Promise<void>
  start(): Promise<void>
  submitAction(action: ActionDto): Promise<void>
  submitSequence(sequence: ActionDto[]): Promise<void>
  resign(): Promise<void>
  rematch(): Promise<void>
  stop(): Promise<void>
  /** Host: answer the screen asking to become the board. */
  decideMonitor(allow: boolean): Promise<void>
  /** This screen: ask (again) to be made the board. */
  requestMonitor(): Promise<void>
  finishReveal(): void
}

/**
 * One SignalR connection per mounted page. `player` mode reconnects a stored
 * seat (or asks to join); `monitor` mode watches without a seat, proving the
 * monitor token this screen got when it hosted the game. Reveal scripts are
 * queued so a server-side timeout resolving the next round mid-playback can
 * never skip a reveal.
 */
export function useGame(code: string, mode: 'player' | 'monitor'): GameApi {
  const [state, dispatch] = useReducer(reduce, initial)
  const connRef = useRef<HubConnection | null>(null)
  // The seat token lives in memory once known; storage (see session.ts) is
  // only the cross-refresh backup — tabs are isolated players, so a second tab
  // never steals a live tab's seat.
  const tokenRef = useRef<string | null>(null)
  // The monitor's counterpart to the seat token: this screen's proof that it
  // hosts the game. The server takes either as the control token.
  const monitorTokenRef = useRef<string | null>(null)
  // Mirrors state.playerId for the connection handlers (kick detection).
  const playerIdRef = useRef<string | null>(null)
  // Set while our own LeaveGame call is in flight, so the lobby echo without
  // our seat isn't mistaken for a kick.
  const leavingRef = useRef(false)
  // Latest snapshot the server told us about (applied or not yet played).
  const snapshotRef = useRef<GameSnapshot | null>(null)
  const queueRef = useRef<RevealJob[]>([])
  const playingRef = useRef(false)

  useEffect(() => {
    let disposed = false
    const conn = createConnection()
    connRef.current = conn
    monitorTokenRef.current = mode === 'monitor' ? loadMonitorToken(code) : null
    snapshotRef.current = null
    playerIdRef.current = null
    leavingRef.current = false
    queueRef.current = []
    playingRef.current = false

    conn.on('LobbyUpdated', (lobby: LobbyView) => {
      if (disposed) return
      const myId = playerIdRef.current
      if (mode === 'player' && tokenRef.current && myId && !lobby.players.some((p) => p.id === myId)) {
        // Our seat is gone: the host kicked us (or our own leave echoed back).
        const kicked = !leavingRef.current
        tokenRef.current = null
        playerIdRef.current = null
        clearSeat(code)
        dispatch({ type: 'needJoin', lobby })
        if (kicked) dispatch({ type: 'actionError', error: 'The host removed you from the game.' })
        return
      }
      dispatch({ type: 'lobbyUpdated', lobby })
    })

    // A device without a role is only watching: a player without a seat (the join
    // form's live lobby), or a screen still waiting to be made the board. Game
    // events must not yank either screen away from what it is doing.
    const seated = () =>
      mode === 'monitor' ? monitorTokenRef.current !== null : tokenRef.current !== null

    conn.on('RoundStarted', (view: RoundStartedView) => {
      if (disposed || !seated()) return
      snapshotRef.current = view.snapshot
      queueRef.current = []
      playingRef.current = false
      dispatch({ type: 'roundStarted', view })
    })

    conn.on('PlayerLocked', (view: PlayerLockedView) => {
      if (!disposed) dispatch({ type: 'playerLocked', view })
    })

    conn.on('ReturnedToLobby', (lobby: LobbyView) => {
      if (disposed) return
      snapshotRef.current = null
      queueRef.current = []
      playingRef.current = false
      if (seated()) {
        dispatch({ type: 'returnedToLobby', lobby })
      } else {
        // Unseated join form: just refresh its lobby list.
        dispatch({ type: 'lobbyUpdated', lobby })
      }
    })

    conn.on('GameStopped', () => {
      if (disposed) return
      queueRef.current = []
      playingRef.current = false
      dispatch({ type: 'stopped' })
    })

    conn.on('MonitorPresence', (hasMonitor: boolean) => {
      if (!disposed) dispatch({ type: 'monitorPresence', hasMonitor })
    })

    // Group-wide, and harmless: it carries no authority. Only the host's device
    // renders the prompt, and only the host's control token can answer it.
    conn.on('MonitorRequested', (request: MonitorRequestView | null) => {
      if (!disposed) dispatch({ type: 'monitorRequested', request })
    })

    // The host answered *this* screen's request (monitor mode only — the server
    // sends it to the asking connection alone).
    conn.on('MonitorDecision', (decision: MonitorDecisionView) => {
      if (disposed) return
      if (!decision.granted || !decision.monitorToken) {
        dispatch({
          type: 'pairingRefused',
          error: decision.message ?? "The host didn't add this screen.",
        })
        return
      }
      // We are the board now: keep the token like a screen that hosted the game,
      // so a reload comes straight back as the monitor.
      monitorTokenRef.current = decision.monitorToken
      saveMonitorToken(code, decision.monitorToken)
      hydrate().catch((e) => dispatch({ type: 'fatal', error: friendlyError(e) }))
    })

    conn.on('RoundResolved', (view: RoundResolvedView) => {
      if (disposed || !seated()) return
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
        const monitorToken = monitorTokenRef.current
        if (!monitorToken) {
          // A screen that never hosted this game: the phone → TV handoff. Ask the
          // host to make us the board and show the pair code while we wait. A
          // refusal (a game is running) is retriable, not fatal.
          try {
            const request = await conn.invoke<MonitorRequestView>('RequestMonitor', code)
            if (!disposed) dispatch({ type: 'pairing', request })
          } catch (e) {
            if (!disposed) dispatch({ type: 'pairingRefused', error: friendlyError(e) })
          }
          return
        }
        const view = await conn.invoke<GameView>('WatchAsMonitor', code, monitorToken)
        if (disposed) return
        snapshotRef.current = view.snapshot
        dispatch({ type: 'hydrate', view })
        return
      }

      // No seat yet: watch the game so the join form sees the live lobby
      // (names + taken avatar colors) while the player picks.
      async function watchForJoin() {
        try {
          const view = await conn.invoke<GameView>('WatchGame', code)
          if (!disposed) dispatch({ type: 'needJoin', lobby: view.lobby })
        } catch (e) {
          if (!disposed) dispatch({ type: 'fatal', error: friendlyError(e) })
        }
      }

      const token = tokenRef.current ?? (await loadSeat(code))?.token
      if (!token) {
        await watchForJoin()
        return
      }
      try {
        const view = await conn.invoke<GameView>('Reconnect', code, token)
        if (disposed) return
        tokenRef.current = token
        playerIdRef.current = view.playerId ?? null
        snapshotRef.current = view.snapshot
        dispatch({ type: 'hydrate', view })
      } catch {
        // Stale seat (server restarted, game gone) — join fresh.
        if (disposed) return
        tokenRef.current = null
        clearSeat(code)
        await watchForJoin()
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
      releaseSeatHold()
      conn.stop().catch(() => {})
    }
  }, [code, mode])

  // A monitor request dies on the server's clock whether or not anyone acts on it.
  // Run the same clock down on both sides, or a host who never noticed the prompt
  // leaves the waiting screen on "waiting for the host" forever — and leaves himself
  // an Allow button that can only fail. The server sends seconds left, not an
  // instant, so a device with a skewed clock still expires it at the right moment.

  // The screen that asked: stop waiting, and offer to ask again.
  const myRequest = state.myRequest
  useEffect(() => {
    if (!myRequest) return
    const timer = setTimeout(
      () => dispatch({ type: 'pairingRefused', error: 'No answer from the host — ask again.' }),
      myRequest.expiresInSeconds * 1000,
    )
    return () => clearTimeout(timer)
  }, [myRequest])

  // The host: take the dead prompt off the screen.
  const pendingMonitor = state.pendingMonitor
  useEffect(() => {
    if (!pendingMonitor) return
    const timer = setTimeout(
      () => dispatch({ type: 'monitorRequested', request: null }),
      pendingMonitor.expiresInSeconds * 1000,
    )
    return () => clearTimeout(timer)
  }, [pendingMonitor])

  const invoke = useCallback(
    async (method: string, ...args: unknown[]): Promise<unknown> => {
      const conn = connRef.current
      if (!conn) throw new Error('Not connected.')
      return conn.invoke(method, ...args)
    },
    [],
  )

  // What the server takes as proof for the game controls (start, stop, kick,
  // rematch, add bot): the monitor's token on the big screen, the seat token on
  // a phone — where only the host's is accepted.
  const controlToken = useCallback(
    () => (mode === 'monitor' ? monitorTokenRef.current : tokenRef.current),
    [mode],
  )

  const join = useCallback(
    async (name: string, color: string | null) => {
      try {
        const result = (await invoke('JoinGame', code, name, color)) as JoinResult
        tokenRef.current = result.playerToken
        playerIdRef.current = result.playerId
        saveSeat(code, { playerId: result.playerId, token: result.playerToken, name })
        dispatch({ type: 'joined', playerId: result.playerId, lobby: result.lobby })
      } catch (e) {
        dispatch({ type: 'actionError', error: friendlyError(e) })
      }
    },
    [code, invoke],
  )

  const leave = useCallback(async () => {
    const token = tokenRef.current
    if (!token) return
    leavingRef.current = true
    try {
      await invoke('LeaveGame', code, token)
      // The lobby echo may have reset us already; if not, do it here.
      if (tokenRef.current === token) {
        tokenRef.current = null
        playerIdRef.current = null
        clearSeat(code)
        dispatch({ type: 'needJoin', lobby: null })
      }
    } catch (e) {
      dispatch({ type: 'actionError', error: friendlyError(e) })
    } finally {
      leavingRef.current = false
    }
  }, [code, invoke])

  const kick = useCallback(
    async (targetPlayerId: string) => {
      try {
        await invoke('KickPlayer', code, controlToken(), targetPlayerId)
      } catch (e) {
        dispatch({ type: 'actionError', error: friendlyError(e) })
      }
    },
    [code, invoke, controlToken],
  )

  const addBot = useCallback(async () => {
    try {
      await invoke('AddBot', code, controlToken())
    } catch (e) {
      dispatch({ type: 'actionError', error: friendlyError(e) })
    }
  }, [code, invoke, controlToken])

  const start = useCallback(async () => {
    try {
      await invoke('StartGame', code, controlToken())
    } catch (e) {
      dispatch({ type: 'actionError', error: friendlyError(e) })
    }
  }, [code, invoke, controlToken])

  const submitAction = useCallback(
    async (action: ActionDto) => {
      const token = tokenRef.current
      if (!token) return
      try {
        await invoke('SubmitAction', code, token, action)
        dispatch({ type: 'submitted', action })
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
        dispatch({ type: 'submitted', sequence })
      } catch (e) {
        dispatch({ type: 'actionError', error: friendlyError(e) })
      }
    },
    [code, invoke],
  )

  const resign = useCallback(async () => {
    const token = tokenRef.current
    if (!token) return
    try {
      await invoke('Resign', code, token)
      dispatch({ type: 'resigned' })
    } catch (e) {
      dispatch({ type: 'actionError', error: friendlyError(e) })
    }
  }, [code, invoke])

  const rematch = useCallback(async () => {
    try {
      await invoke('Rematch', code, controlToken())
    } catch (e) {
      dispatch({ type: 'actionError', error: friendlyError(e) })
    }
  }, [code, invoke, controlToken])

  const stop = useCallback(async () => {
    try {
      await invoke('StopGame', code, controlToken())
    } catch (e) {
      dispatch({ type: 'actionError', error: friendlyError(e) })
    }
  }, [code, invoke, controlToken])

  const decideMonitor = useCallback(
    async (allow: boolean) => {
      try {
        await invoke('DecideMonitor', code, controlToken(), allow)
      } catch (e) {
        dispatch({ type: 'actionError', error: friendlyError(e) })
      }
      // Either way the request is spent — drop the prompt even if the call raced
      // another host device answering first.
      dispatch({ type: 'monitorRequested', request: null })
    },
    [code, invoke, controlToken],
  )

  const requestMonitor = useCallback(async () => {
    try {
      const request = (await invoke('RequestMonitor', code)) as MonitorRequestView
      dispatch({ type: 'pairing', request })
    } catch (e) {
      dispatch({ type: 'pairingRefused', error: friendlyError(e) })
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
    leave,
    kick,
    addBot,
    start,
    submitAction,
    submitSequence,
    resign,
    rematch,
    stop,
    decideMonitor,
    requestMonitor,
    finishReveal,
  }
}
