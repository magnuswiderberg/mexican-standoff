import { useState } from 'react'
import { useGame } from '../useGame'
import { useLobbyChime, useSound } from '../useSound'
import { useWakeLock } from '../useWakeLock'
import { ActionPicker } from '../components/ActionPicker'
import { DuelPlanner } from '../components/DuelPlanner'
import { Countdown } from '../components/Countdown'
import { PlayerBoard, Avatar, actionLabel } from '../components/PlayerBoard'
import { RevealStage } from '../components/RevealStage'
import { SoundToggle } from '../components/SoundToggle'
import { Confetti } from '../components/Confetti'
import { ConfirmButton } from '../components/ConfirmButton'
import { InvitePanel } from '../components/InvitePanel'
import { FlagIcon, SkullIcon, StarIcon } from '../components/icons'
import { HowToPlayLink } from './HowToPlayPage'
import { AVATARS, accentOf, avatarOf, avatarUrl } from '../avatars'
import type { LobbyView } from '../types'
import { navigate } from '../router'

const NAME_KEY = 'standoff-name'
const AVATAR_KEY = 'standoff-avatar'

function JoinForm({
  code,
  error,
  lobby,
  onJoin,
}: {
  code: string
  error: string | null
  lobby: LobbyView | null
  onJoin: (name: string, color: string | null) => void
}) {
  const [name, setName] = useState(() => localStorage.getItem(NAME_KEY) ?? '')
  const [picked, setPicked] = useState<string | null>(() => localStorage.getItem(AVATAR_KEY))
  const [joining, setJoining] = useState(false)

  const taken = new Set(lobby?.players.map((p) => p.avatar) ?? [])
  // The stored favorite wins if free; otherwise fall back to the first free portrait.
  const selected =
    picked !== null && !taken.has(picked) && avatarOf(picked)
      ? picked
      : (AVATARS.find((a) => !taken.has(a.key))?.key ?? null)
  const selectedSpec = avatarOf(selected)

  const submit = (e: React.FormEvent) => {
    e.preventDefault()
    const trimmed = name.trim()
    if (!trimmed || joining) return
    localStorage.setItem(NAME_KEY, trimmed)
    if (selected) localStorage.setItem(AVATAR_KEY, selected)
    setJoining(true)
    onJoin(trimmed, selected)
  }

  if (error && joining) setJoining(false)

  return (
    <form className="join-form join-form-page" onSubmit={submit}>
      <h2>
        Join game <span className="code">{code}</span>
      </h2>
      <input
        value={name}
        onChange={(e) => setName(e.target.value)}
        placeholder="Your name"
        maxLength={20}
        autoFocus
      />
      <div className="avatar-grid">
        {AVATARS.map((a) => (
          <button
            key={a.key}
            type="button"
            className={`avatar-pick ${selected === a.key ? 'avatar-pick-selected' : ''} ${taken.has(a.key) ? 'avatar-pick-taken' : ''}`}
            style={
              selected === a.key
                ? ({ borderColor: a.accent, '--pick-glow': a.accent } as React.CSSProperties)
                : undefined
            }
            disabled={taken.has(a.key)}
            aria-label={taken.has(a.key) ? `${a.persona} (taken)` : a.persona}
            onClick={() => setPicked(a.key)}
          >
            <img src={avatarUrl(a.key)} alt={a.persona} />
            {taken.has(a.key) && <span className="avatar-pick-x">✕</span>}
          </button>
        ))}
      </div>
      {selectedSpec && (
        <p className="persona" style={{ color: selectedSpec.accent }}>
          {selectedSpec.name} — {selectedSpec.persona}
        </p>
      )}
      {(lobby?.players.length ?? 0) > 0 && (
        <p className="hint">
          Already in:{' '}
          {lobby!.players.map((p, i) => (
            <span key={p.id}>
              {i > 0 && ', '}
              <span style={{ color: accentOf(p.avatar) }}>{p.name}</span>
            </span>
          ))}
        </p>
      )}
      {error && <div className="error">{error}</div>}
      <button className="primary" type="submit" disabled={!name.trim() || joining}>
        {joining ? 'Joining…' : 'Join the standoff'}
      </button>
    </form>
  )
}

/**
 * A screen is asking to become the board (phone → TV handoff). Only the host sees
 * this, and only their tap hands the monitor token over — the pair code is on the
 * asking screen, so they can check they are approving the TV they are looking at.
 */
function MonitorPrompt({
  pairCode,
  onDecide,
}: {
  pairCode: string
  onDecide: (allow: boolean) => void
}) {
  return (
    <div className="monitor-prompt">
      <p>
        A screen wants to show the board. Does it say <span className="code">{pairCode}</span>?
      </p>
      <div className="monitor-prompt-actions">
        <button className="primary" onClick={() => onDecide(true)}>
          Yes — put it up
        </button>
        <button className="secondary" onClick={() => onDecide(false)}>
          No
        </button>
      </div>
    </div>
  )
}

function LockProgress({ locked, total }: { locked: number; total: number }) {
  return (
    <div className="lock-progress">
      <div className="lock-progress-bar" style={{ width: `${(locked / Math.max(1, total)) * 100}%` }} />
      <span className="lock-progress-label">
        {locked}/{total} locked in
      </span>
    </div>
  )
}

export function PlayerPage({ code }: { code: string }) {
  const game = useGame(code, 'player')
  const sound = useSound('player')
  useLobbyChime(game.phase, game.lobby, sound.play)
  useWakeLock()
  const { snapshot, playerId } = game
  const me = snapshot?.players.find((p) => p.id === playerId) ?? null
  const isHost = game.lobby !== null && game.lobby.players[0]?.id === playerId

  const content = () => {
    switch (game.phase) {
      case 'connecting':
        return <div className="page center">Connecting…</div>

      case 'fatal':
        return (
          <div className="page center">
            <div className="error">{game.fatalError}</div>
            <button className="secondary" onClick={() => navigate('/')}>
              Back to start
            </button>
          </div>
        )

      case 'joining':
        return (
          <div className="page">
            <JoinForm code={code} error={game.actionError} lobby={game.lobby} onJoin={game.join} />
          </div>
        )

      case 'lobby':
        return (
          <div className="page">
            <h2>
              Game <span className="code">{code}</span>
            </h2>
            <p className="hint">
              Waiting for gunslingers <span className="spinner" /> ({game.lobby?.players.length ?? 0}/8)
            </p>
            {/* With no monitor, this phone is the only place the QR can live —
                so open it on the empty lobby the host lands on. A monitor game
                already has the code up on the wall; keep it folded away there. */}
            <InvitePanel
              code={code}
              alone={!game.hasMonitor && (game.lobby?.players.length ?? 0) <= 1}
            />
            <ul className="lobby-list">
              {game.lobby?.players.map((p) => (
                <li key={p.id} className="lobby-player">
                  <Avatar avatar={p.avatar} name={p.name} />
                  <span>{p.name}</span>
                  {p.id === playerId && <span className="you-badge">you</span>}
                  {p.isBot && <span className="you-badge bot-badge">bot</span>}
                  {isHost && p.id !== playerId && (
                    <button
                      type="button"
                      className="kick-btn"
                      aria-label={`Remove ${p.name}`}
                      onClick={() => game.kick(p.id)}
                    >
                      ✕
                    </button>
                  )}
                </li>
              ))}
            </ul>
            {game.actionError && <div className="error">{game.actionError}</div>}
            {isHost ? (
              <button className="primary" disabled={!game.lobby?.canStart} onClick={game.start}>
                {game.lobby?.canStart ? 'Start the standoff' : 'Need at least 2 players'}
              </button>
            ) : (
              <p className="hint">The host starts the game.</p>
            )}
            {isHost && game.lobby?.botsEnabled && (game.lobby?.players.length ?? 0) < 8 && (
              <button className="secondary" onClick={game.addBot}>
                Add a bot
              </button>
            )}
            <button className="secondary" onClick={game.leave}>
              Leave game
            </button>
            {/* Same-tab is fine: coming back remounts the page and the stored
                seat token rejoins this lobby. */}
            <HowToPlayLink />
          </div>
        )

      case 'selecting': {
        if (!snapshot || !me) return <div className="page center">Loading…</div>
        const alive = me.isAlive
        const lockedIds = game.locked?.lockedPlayerIds ?? []
        const resignedIds = game.locked?.resignedPlayerIds ?? []
        // resignedIds covers being kicked mid-round — the snapshot only catches up at resolution.
        const resigned = game.resigned || me.isResigned || resignedIds.includes(me.id)
        // Echo the pick on the locked-in banner so nobody second-guesses mid-wait.
        // A rehydrate loses the local echo — the banner falls back to bare "Locked in."
        const submittedTarget =
          game.submittedAction?.type === 'attack'
            ? (snapshot.players.find((p) => p.id === game.submittedAction?.targetId) ?? null)
            : null
        const resignButton = alive && !resigned && (
          <ConfirmButton
            className="resign-btn"
            label={<><FlagIcon /> Resign</>}
            confirmLabel={<><FlagIcon /> Tap again to resign</>}
            onConfirm={game.resign}
          />
        )
        return (
          <div className="page">
            <div className="round-header">
              <span>
                {/* Both counters track completed rounds/volleys; we're selecting the next one. */}
                {snapshot.isDuel
                  ? `${snapshot.players.length === 2 ? 'Duel' : 'Final Duel'} — Volley ${snapshot.duelVolley + 1}`
                  : `Round ${snapshot.roundNumber + 1}`}
              </span>
              <Countdown deadline={game.deadline} />
            </div>

            {!alive ? (
              <>
                <div className="spectator-banner"><SkullIcon /> You're out — spectating.</div>
                <PlayerBoard
                  players={snapshot.players}
                  maxHp={snapshot.maxHp}
                  maxBullets={snapshot.maxBullets}
                  goldToWin={snapshot.goldToWin}
                  meId={playerId}
                  lockedIds={lockedIds}
                  resignedIds={resignedIds}
                  onKick={isHost ? game.kick : undefined}
                />
                {game.locked ? (
                  <LockProgress locked={game.locked.lockedCount} total={game.locked.totalExpected} />
                ) : (
                  <p className="hint center-text">
                    Waiting for the round<span className="ellipsis" />
                  </p>
                )}
              </>
            ) : resigned ? (
              <>
                <div className="waiting-banner"><FlagIcon /> You've resigned — you're out when this round ends.</div>
                {game.locked && (
                  <LockProgress locked={game.locked.lockedCount} total={game.locked.totalExpected} />
                )}
                <PlayerBoard
                  players={snapshot.players}
                  maxHp={snapshot.maxHp}
                  maxBullets={snapshot.maxBullets}
                  goldToWin={snapshot.goldToWin}
                  meId={playerId}
                  lockedIds={lockedIds}
                  resignedIds={resignedIds}
                  onKick={isHost ? game.kick : undefined}
                />
              </>
            ) : game.hasSubmitted ? (
              <>
                <div className="waiting-banner">
                  {game.submittedAction ? (
                    <>
                      Locked in: {actionLabel(game.submittedAction, snapshot.chestCount)}
                      {submittedTarget && (
                        <>
                          {' → '}
                          <span style={{ color: accentOf(submittedTarget.avatar) }}>
                            {submittedTarget.name}
                          </span>
                        </>
                      )}
                    </>
                  ) : game.submittedSequence ? (
                    <>
                      Locked in:{' '}
                      {game.submittedSequence.map((a, i) => (
                        <span key={i}>
                          {i > 0 && ' → '}
                          {actionLabel(a, snapshot.chestCount)}
                        </span>
                      ))}
                    </>
                  ) : (
                    'Locked in.'
                  )}
                </div>
                {game.locked ? (
                  <LockProgress locked={game.locked.lockedCount} total={game.locked.totalExpected} />
                ) : (
                  <p className="hint center-text">
                    Waiting for the others<span className="ellipsis" />
                  </p>
                )}
                <PlayerBoard
                  players={snapshot.players}
                  maxHp={snapshot.maxHp}
                  maxBullets={snapshot.maxBullets}
                  goldToWin={snapshot.goldToWin}
                  meId={playerId}
                  lockedIds={lockedIds}
                  resignedIds={resignedIds}
                  onKick={isHost ? game.kick : undefined}
                />
                {resignButton}
              </>
            ) : snapshot.isDuel ? (
              <>
                {/* "Own stats always visible" holds in the duel too — both duelists' tiles above the planner. */}
                <PlayerBoard
                  players={snapshot.players.filter((p) => p.isAlive)}
                  maxHp={snapshot.maxHp}
                  maxBullets={snapshot.maxBullets}
                  goldToWin={snapshot.goldToWin}
                  meId={playerId}
                  lockedIds={lockedIds}
                  resignedIds={resignedIds}
                  onKick={isHost ? game.kick : undefined}
                />
                <DuelPlanner
                  snapshot={snapshot}
                  playerId={playerId!}
                  error={game.actionError}
                  onSubmit={game.submitSequence}
                />
                {resignButton}
              </>
            ) : (
              <>
                <PlayerBoard
                  players={snapshot.players}
                  maxHp={snapshot.maxHp}
                  maxBullets={snapshot.maxBullets}
                  goldToWin={snapshot.goldToWin}
                  meId={playerId}
                  lockedIds={lockedIds}
                  resignedIds={resignedIds}
                  onKick={isHost ? game.kick : undefined}
                />
                <ActionPicker
                  snapshot={snapshot}
                  playerId={playerId!}
                  error={game.actionError}
                  onSubmit={game.submitAction}
                />
                {resignButton}
              </>
            )}
          </div>
        )
      }

      case 'revealing':
        if (!game.reveal) return null
        return (
          <div className="page">
            <RevealStage
              job={game.reveal}
              meId={playerId}
              maxHp={game.reveal.prev.maxHp}
              onDone={game.finishReveal}
              playSound={sound.play}
            />
          </div>
        )

      case 'stopped':
        return (
          <div className="page center">
            <div className="waiting-banner">The game was stopped.</div>
            <p className="hint">Thanks for playing — round up the gang for another one!</p>
            <button className="primary" onClick={() => navigate('/')}>
              Back to start
            </button>
          </div>
        )

      case 'gameover': {
        const winners = game.winnerIds
          ?.map((id) => snapshot?.players.find((p) => p.id === id))
          .filter((p) => p !== undefined)
        const nobodyWon = (game.winnerIds?.length ?? 0) === 0
        const iWon = playerId !== null && (game.winnerIds?.includes(playerId) ?? false)
        return (
          <div className="page center">
            {iWon && <Confetti />}
            {/* Same hero portrait the monitor throws up — the phones are the
                monitor, and a win deserves a face on every screen. */}
            {!nobodyWon && (
              <div className="winner-portraits">
                {winners?.map((p) => (
                  <Avatar key={p.id} avatar={p.avatar} name={p.name} size="hero" />
                ))}
              </div>
            )}
            <div className="winner-banner">
              {nobodyWon ? (
                <><SkullIcon /> Mutual destruction — nobody wins!</>
              ) : (
                <>
                  <StarIcon />{' '}
                  {iWon
                    ? 'You win!'
                    : winners?.map((p, i) => (
                        <span key={p.id}>
                          {i > 0 && ' & '}
                          <span style={{ color: accentOf(p.avatar) }}>{p.name}</span>
                        </span>
                      ))}
                  {!iWon && ' wins!'}
                </>
              )}
            </div>
            {snapshot && (
              <PlayerBoard
                players={snapshot.players}
                maxHp={snapshot.maxHp}
                maxBullets={snapshot.maxBullets}
                goldToWin={snapshot.goldToWin}
                meId={playerId}
              />
            )}
            {game.actionError && <div className="error">{game.actionError}</div>}
            {game.hasMonitor ? (
              <p className="hint">The next game starts from the monitor.</p>
            ) : isHost ? (
              <button className="primary" onClick={game.rematch}>
                Play again — back to lobby
              </button>
            ) : (
              <p className="hint">The host starts the next game.</p>
            )}
          </div>
        )
      }
    }
  }

  // Between games only — the server refuses mid-round requests, and a prompt landing
  // on top of the action picker is a mis-tap waiting to happen. A TV that turns up
  // mid-game waits for this one to finish; that is what Stop and the rematch are for.
  const canDecideMonitor = game.phase === 'lobby' || game.phase === 'gameover'

  return (
    <>
      <SoundToggle sound={sound} />
      {isHost && canDecideMonitor && game.pendingMonitor && (
        <MonitorPrompt pairCode={game.pendingMonitor.pairCode} onDecide={game.decideMonitor} />
      )}
      {content()}
    </>
  )
}
