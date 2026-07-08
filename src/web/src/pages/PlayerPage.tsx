import { useState } from 'react'
import { useGame } from '../useGame'
import { ActionPicker } from '../components/ActionPicker'
import { DuelPlanner } from '../components/DuelPlanner'
import { Countdown } from '../components/Countdown'
import { PlayerBoard } from '../components/PlayerBoard'
import { RevealStage } from '../components/RevealStage'
import { navigate } from '../router'

const NAME_KEY = 'standoff-name'

function JoinForm({ code, error, onJoin }: { code: string; error: string | null; onJoin: (name: string) => void }) {
  const [name, setName] = useState(() => localStorage.getItem(NAME_KEY) ?? '')
  const [joining, setJoining] = useState(false)

  const submit = (e: React.FormEvent) => {
    e.preventDefault()
    const trimmed = name.trim()
    if (!trimmed || joining) return
    localStorage.setItem(NAME_KEY, trimmed)
    setJoining(true)
    onJoin(trimmed)
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
      {error && <div className="error">{error}</div>}
      <button className="primary" type="submit" disabled={!name.trim() || joining}>
        {joining ? 'Joining…' : 'Join the standoff'}
      </button>
    </form>
  )
}

export function PlayerPage({ code }: { code: string }) {
  const game = useGame(code, 'player')
  const { snapshot, playerId } = game
  const me = snapshot?.players.find((p) => p.id === playerId) ?? null
  const isHost = game.lobby !== null && game.lobby.players[0]?.id === playerId

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
          <JoinForm code={code} error={game.actionError} onJoin={game.join} />
        </div>
      )

    case 'lobby':
      return (
        <div className="page">
          <h2>
            Game <span className="code">{code}</span>
          </h2>
          <p className="hint">Waiting for gunslingers… ({game.lobby?.players.length ?? 0}/8)</p>
          <ul className="lobby-list">
            {game.lobby?.players.map((p) => (
              <li key={p.id}>
                🤠 {p.name}
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
              <p className="hint">
                {game.locked
                  ? `${game.locked.lockedCount}/${game.locked.totalExpected} locked in…`
                  : 'Waiting for the round…'}
              </p>
            </>
          ) : game.hasSubmitted ? (
            <>
              <div className="waiting-banner">🔒 Locked in.</div>
              <p className="hint">
                {game.locked
                  ? `${game.locked.lockedCount}/${game.locked.totalExpected} players ready…`
                  : 'Waiting for the others…'}
              </p>
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
          />
        </div>
      )

    case 'gameover': {
      const winners =
        game.winnerIds
          ?.map((id) => snapshot?.players.find((p) => p.id === id)?.name ?? id)
          .join(' & ') ?? '—'
      const iWon = playerId !== null && (game.winnerIds?.includes(playerId) ?? false)
      return (
        <div className="page center">
          <div className="winner-banner">{iWon ? '🏆 You win!' : `🏆 ${winners} wins!`}</div>
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
