import QRCode from 'react-qr-code'
import { useGame } from '../useGame'
import { Countdown } from '../components/Countdown'
import { PlayerBoard } from '../components/PlayerBoard'
import { RevealStage } from '../components/RevealStage'
import { navigate } from '../router'

/**
 * The optional big screen: same game, same reveal script, bigger renderer.
 * Watches without a seat via WatchGame.
 */
export function MonitorPage({ code }: { code: string }) {
  const game = useGame(code, 'monitor')
  const { snapshot } = game

  const joinUrl = `${location.origin}/game/${code}`

  switch (game.phase) {
    case 'connecting':
      return <div className="page tv center">Connecting…</div>

    case 'fatal':
      return (
        <div className="page tv center">
          <div className="error">{game.fatalError}</div>
          <button className="secondary" onClick={() => navigate('/')}>
            Back to start
          </button>
        </div>
      )

    case 'joining': // monitor never joins; unreachable
    case 'lobby':
      return (
        <div className="page tv">
          <h1 className="logo">🤠 Mexican Standoff</h1>
          <div className="monitor-lobby">
            <div className="qr-panel">
              <QRCode value={joinUrl} size={220} bgColor="#f5ead6" fgColor="#171310" />
              <div className="join-url">{joinUrl.replace(/^https?:\/\//, '')}</div>
              <div className="code code-big">{code}</div>
            </div>
            <div className="lobby-panel">
              <h2>Gunslingers ({game.lobby?.players.length ?? 0}/8)</h2>
              <ul className="lobby-list">
                {game.lobby?.players.map((p) => <li key={p.id}>🤠 {p.name}</li>)}
                {(game.lobby?.players.length ?? 0) === 0 && <li className="hint">Scan to join…</li>}
              </ul>
              {game.actionError && <div className="error">{game.actionError}</div>}
              <button className="primary" disabled={!game.lobby?.canStart} onClick={game.start}>
                {game.lobby?.canStart ? 'Start the standoff' : 'Need at least 2 players'}
              </button>
            </div>
          </div>
        </div>
      )

    case 'selecting':
      if (!snapshot) return <div className="page tv center">Loading…</div>
      return (
        <div className="page tv">
          <div className="round-header">
            <span>
              {/* RoundNumber counts completed rounds; we're selecting the next one. */}
              Round {snapshot.roundNumber + 1}
              {snapshot.isDuel && ' — ⚔️ Final Duel'}
              {snapshot.suddenDeath && ' ☠️'}
            </span>
            <span className="code">{code}</span>
            <Countdown deadline={game.deadline} />
          </div>
          <PlayerBoard
            players={snapshot.players}
            maxHp={snapshot.startingHp}
            maxBullets={snapshot.maxBullets}
            goldToWin={snapshot.goldToWin}
          />
          <p className="hint locked-status">
            {game.locked
              ? `${game.locked.lockedCount}/${game.locked.totalExpected} locked in…`
              : 'Players are choosing…'}
          </p>
        </div>
      )

    case 'revealing':
      if (!game.reveal) return null
      return (
        <div className="page tv">
          <RevealStage
            job={game.reveal}
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
      return (
        <div className="page tv center">
          <div className="winner-banner">🏆 {winners} wins!</div>
          {snapshot && (
            <PlayerBoard
              players={snapshot.players}
              maxHp={snapshot.startingHp}
              maxBullets={snapshot.maxBullets}
              goldToWin={snapshot.goldToWin}
            />
          )}
          {game.actionError && <div className="error">{game.actionError}</div>}
          <button className="primary" onClick={game.rematch}>
            🔁 Rematch
          </button>
        </div>
      )
    }
  }
}
