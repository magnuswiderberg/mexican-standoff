import QRCode from 'react-qr-code'
import { useGame } from '../useGame'
import { useLobbyChime, useSound } from '../useSound'
import { useWakeLock } from '../useWakeLock'
import { Countdown } from '../components/Countdown'
import { PlayerBoard, Avatar } from '../components/PlayerBoard'
import { RevealStage } from '../components/RevealStage'
import { SoundToggle } from '../components/SoundToggle'
import { Confetti } from '../components/Confetti'
import { ConfirmButton } from '../components/ConfirmButton'
import { Logo } from '../components/Logo'
import { accentOf } from '../avatars'
import { navigate } from '../router'

/**
 * The optional big screen: same game, same reveal script, bigger renderer.
 * Watches without a seat via WatchGame. Sound defaults ON here — the monitor
 * carries the room while phones stay silent.
 */
export function MonitorPage({ code }: { code: string }) {
  const game = useGame(code, 'monitor')
  const sound = useSound('monitor')
  useLobbyChime(game.phase, game.lobby, sound.play)
  // The big screen is usually a laptop, and a laptop dims mid-standoff too.
  useWakeLock()
  const { snapshot } = game

  const joinUrl = `${location.origin}/game/${code}`

  const content = () => {
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

      // Phone → TV handoff: this screen has no monitor token (the game was started
      // on a phone, or this browser lost its storage), so it asks the host for one
      // and shows a pair code they can match against the screen in front of them.
      case 'pairing':
        return (
          <div className="page tv center">
            <Logo />
            {game.pairingError ? (
              <>
                {/* The host said no, or a game is running — either way, retriable. */}
                <div className="waiting-banner">🚫 {game.pairingError}</div>
                <button className="primary" onClick={game.requestMonitor}>
                  Ask again
                </button>
              </>
            ) : (
              <>
                <h2>Show the board on this screen</h2>
                <p className="hint">
                  On the host's phone, allow the screen showing this code — game{' '}
                  <span className="code">{code}</span>
                </p>
                <div className="code code-big pair-code">{game.myRequest?.pairCode}</div>
                <p className="hint">
                  Waiting for the host <span className="spinner" />
                </p>
              </>
            )}
            <button className="secondary" onClick={() => navigate('/')}>
              Back to start
            </button>
          </div>
        )

      case 'joining': // monitor never joins; unreachable
      case 'lobby':
        return (
          <div className="page tv">
            <Logo />
            <div className="monitor-lobby">
              <div className="qr-panel">
                {/* A new tab, always: clicking the QR on the hosting screen used to
                    navigate this tab into the game, silently taking the monitor
                    down with it. */}
                <a
                  className="qr-link"
                  href={joinUrl}
                  target="_blank"
                  rel="noopener"
                  aria-label="Open the join page in a new tab"
                >
                  <QRCode value={joinUrl} size={280} bgColor="#f5ead6" fgColor="#171310" />
                </a>
                <div className="code code-big">{code}</div>
              </div>
              <div className="lobby-panel">
                <h2>Gunslingers ({game.lobby?.players.length ?? 0}/8)</h2>
                <ul className="lobby-list">
                  {game.lobby?.players.map((p) => (
                    <li key={p.id} className="lobby-player">
                      <Avatar avatar={p.avatar} name={p.name} />
                      <span>{p.name}</span>
                      {p.isBot && <span className="you-badge bot-badge">bot</span>}
                      <button
                        type="button"
                        className="kick-btn"
                        aria-label={`Remove ${p.name}`}
                        onClick={() => game.kick(p.id)}
                      >
                        ✕
                      </button>
                    </li>
                  ))}
                  {(game.lobby?.players.length ?? 0) === 0 && (
                    <li className="hint">
                      Scan to join<span className="ellipsis" />
                    </li>
                  )}
                </ul>
                {game.actionError && <div className="error">{game.actionError}</div>}
                <button className="primary" disabled={!game.lobby?.canStart} onClick={game.start}>
                  {game.lobby?.canStart ? 'Start the standoff' : 'Need at least 2 players'}
                </button>
                {game.lobby?.botsEnabled && (game.lobby?.players.length ?? 0) < 8 && (
                  <button className="secondary" onClick={game.addBot}>
                    🤖 Add a bot
                  </button>
                )}
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
                {/* Both counters track completed rounds/volleys; we're selecting the next one. */}
                {snapshot.isDuel
                  ? `⚔️ ${snapshot.players.length === 2 ? 'Duel' : 'Final Duel'} — Volley ${snapshot.duelVolley + 1}`
                  : `Round ${snapshot.roundNumber + 1}`}
                {snapshot.suddenDeath && ' ☠️'}
              </span>
              <span className="code">{code}</span>
              <Countdown deadline={game.deadline} onUrgentTick={() => sound.play('tick')} />
            </div>
            <PlayerBoard
              players={snapshot.players}
              maxHp={snapshot.startingHp}
              maxBullets={snapshot.maxBullets}
              goldToWin={snapshot.goldToWin}
              lockedIds={game.locked?.lockedPlayerIds ?? []}
              resignedIds={game.locked?.resignedPlayerIds ?? []}
              onKick={game.kick}
            />
            <p className="hint locked-status">
              {game.locked ? (
                `${game.locked.lockedCount}/${game.locked.totalExpected} locked in…`
              ) : (
                <>
                  Players are choosing <span className="spinner" />
                </>
              )}
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
              playSound={sound.play}
            />
          </div>
        )

      case 'stopped':
        return (
          <div className="page tv center">
            <Logo />
            <div className="waiting-banner">🛑 The game was stopped.</div>
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
        return (
          <div className="page tv center">
            {!nobodyWon && <Confetti pieces={140} />}
            <div className="winner-portraits">
              {winners?.map((p) => <Avatar key={p.id} avatar={p.avatar} name={p.name} size="hero" />)}
            </div>
            <div className="winner-banner winner-banner-tv">
              {nobodyWon ? (
                '💀 Mutual destruction — nobody wins!'
              ) : (
                <>
                  🏆{' '}
                  {winners?.map((p, i) => (
                    <span key={p.id}>
                      {i > 0 && ' & '}
                      <span style={{ color: accentOf(p.avatar) }}>{p.name}</span>
                    </span>
                  ))}{' '}
                  {(winners?.length ?? 0) > 1 ? 'win!' : 'wins!'}
                </>
              )}
            </div>
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
              🔁 Back to lobby
            </button>
          </div>
        )
      }
    }
  }

  // The kill switch lives on the monitor (like Start): visible whenever a
  // session is alive — including reveal playback, where a runaway game
  // (e.g. a duel against a permanent dodger) spends nearly all its time.
  // A screen still waiting to be made the board has no control token — and no
  // business ending the room's game.
  const canStop =
    game.phase !== 'connecting' &&
    game.phase !== 'fatal' &&
    game.phase !== 'stopped' &&
    game.phase !== 'pairing'

  return (
    <>
      <SoundToggle sound={sound} />
      {content()}
      {/* Seat recovery: a phone that died or closed its tab rejoins its seat by
          rescanning — the seat token's localStorage backup rebinds the new tab
          (session.ts probes that the old tab is really gone first). */}
      {game.phase === 'selecting' && (
        <a
          className="monitor-rejoin"
          href={joinUrl}
          target="_blank"
          rel="noopener"
          aria-label="Open the rejoin page in a new tab"
        >
          <span className="rejoin-qr">
            <QRCode value={joinUrl} size={84} bgColor="#f5ead6" fgColor="#171310" />
          </span>
          <span>Dropped out? Scan to rejoin</span>
        </a>
      )}
      {canStop && (
        <div className="monitor-stop">
          <ConfirmButton label="🛑 Stop game" confirmLabel="🛑 Stop for everyone?" onConfirm={game.stop} />
        </div>
      )}
    </>
  )
}
