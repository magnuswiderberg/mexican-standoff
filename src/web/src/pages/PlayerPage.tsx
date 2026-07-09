import { useState } from 'react'
import { useGame } from '../useGame'
import { useSound } from '../useSound'
import { ActionPicker } from '../components/ActionPicker'
import { DuelPlanner } from '../components/DuelPlanner'
import { Countdown } from '../components/Countdown'
import { PlayerBoard, Avatar } from '../components/PlayerBoard'
import { RevealStage } from '../components/RevealStage'
import { SoundToggle } from '../components/SoundToggle'
import { Confetti } from '../components/Confetti'
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
            style={selected === a.key ? { borderColor: a.accent } : undefined}
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
              Waiting for gunslingers<span className="ellipsis" /> ({game.lobby?.players.length ?? 0}/8)
            </p>
            <ul className="lobby-list">
              {game.lobby?.players.map((p) => (
                <li key={p.id} className="lobby-player">
                  <Avatar avatar={p.avatar} name={p.name} />
                  <span>{p.name}</span>
                  {p.id === playerId && <span className="you-badge">you</span>}
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
          </div>
        )

      case 'selecting': {
        if (!snapshot || !me) return <div className="page center">Loading…</div>
        const alive = me.isAlive
        return (
          <div className="page">
            <div className="round-header">
              <span>
                {/* RoundNumber counts completed rounds; we're selecting the next one. */}
                Round {snapshot.roundNumber + 1}
                {snapshot.isDuel && ' ⚔️'}
              </span>
              <Countdown deadline={game.deadline} />
            </div>

            {!alive ? (
              <>
                <div className="spectator-banner">☠️ You're out — spectating.</div>
                <PlayerBoard
                  players={snapshot.players}
                  maxHp={snapshot.startingHp}
                  maxBullets={snapshot.maxBullets}
                  goldToWin={snapshot.goldToWin}
                  meId={playerId}
                />
                {game.locked ? (
                  <LockProgress locked={game.locked.lockedCount} total={game.locked.totalExpected} />
                ) : (
                  <p className="hint center-text">
                    Waiting for the round<span className="ellipsis" />
                  </p>
                )}
              </>
            ) : game.hasSubmitted ? (
              <>
                <div className="waiting-banner">🔒 Locked in.</div>
                {game.locked ? (
                  <LockProgress locked={game.locked.lockedCount} total={game.locked.totalExpected} />
                ) : (
                  <p className="hint center-text">
                    Waiting for the others<span className="ellipsis" />
                  </p>
                )}
                <PlayerBoard
                  players={snapshot.players}
                  maxHp={snapshot.startingHp}
                  maxBullets={snapshot.maxBullets}
                  goldToWin={snapshot.goldToWin}
                  meId={playerId}
                />
              </>
            ) : snapshot.isDuel ? (
              <DuelPlanner
                snapshot={snapshot}
                playerId={playerId!}
                error={game.actionError}
                onSubmit={game.submitSequence}
              />
            ) : (
              <>
                <PlayerBoard
                  players={snapshot.players}
                  maxHp={snapshot.startingHp}
                  maxBullets={snapshot.maxBullets}
                  goldToWin={snapshot.goldToWin}
                  meId={playerId}
                />
                <ActionPicker
                  snapshot={snapshot}
                  playerId={playerId!}
                  error={game.actionError}
                  onSubmit={game.submitAction}
                />
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
              startingHp={game.reveal.prev.startingHp}
              onDone={game.finishReveal}
              playSound={sound.play}
            />
          </div>
        )

      case 'gameover': {
        const winners = game.winnerIds
          ?.map((id) => snapshot?.players.find((p) => p.id === id))
          .filter((p) => p !== undefined)
        const iWon = playerId !== null && (game.winnerIds?.includes(playerId) ?? false)
        return (
          <div className="page center">
            {iWon && <Confetti />}
            <div className="winner-banner">
              🏆{' '}
              {iWon
                ? 'You win!'
                : winners?.map((p, i) => (
                    <span key={p.id}>
                      {i > 0 && ' & '}
                      <span style={{ color: accentOf(p.avatar) }}>{p.name}</span>
                    </span>
                  ))}
              {!iWon && ' wins!'}
            </div>
            {snapshot && (
              <PlayerBoard
                players={snapshot.players}
                maxHp={snapshot.startingHp}
                maxBullets={snapshot.maxBullets}
                goldToWin={snapshot.goldToWin}
                meId={playerId}
              />
            )}
            {game.actionError && <div className="error">{game.actionError}</div>}
            <button className="primary" onClick={game.rematch}>
              🔁 Play again
            </button>
          </div>
        )
      }
    }
  }

  return (
    <>
      <SoundToggle sound={sound} />
      {content()}
    </>
  )
}
