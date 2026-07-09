import { useEffect, useRef, useState } from 'react'
import QRCode from 'react-qr-code'
import { useGame } from '../useGame'
import { useSound } from '../useSound'
import { Countdown } from '../components/Countdown'
import { PlayerBoard, Avatar } from '../components/PlayerBoard'
import { RevealStage } from '../components/RevealStage'
import { SoundToggle } from '../components/SoundToggle'
import { Confetti } from '../components/Confetti'
import { accentOf } from '../avatars'
import { navigate } from '../router'

const REMATCH_SECONDS = 30

/**
 * Winner-ceremony countdown: auto-starts the rematch when it reaches zero so
 * the party keeps rolling. Pauses while the tab is hidden (an abandoned
 * monitor must not keep restarting games) and can be cancelled.
 */
function RematchCountdown({ onRematch }: { onRematch: () => void }) {
  const [left, setLeft] = useState(REMATCH_SECONDS)
  const [stopped, setStopped] = useState(false)
  const firedRef = useRef(false)
  const onRematchRef = useRef(onRematch)
  onRematchRef.current = onRematch

  useEffect(() => {
    if (stopped) return
    const timer = setInterval(() => {
      if (!document.hidden) setLeft((l) => Math.max(0, l - 1))
    }, 1000)
    return () => clearInterval(timer)
  }, [stopped])

  useEffect(() => {
    if (left === 0 && !stopped && !firedRef.current) {
      firedRef.current = true
      onRematchRef.current()
    }
  }, [left, stopped])

  return (
    <div className="rematch-countdown">
      {stopped ? (
        <button className="primary" onClick={onRematchRef.current}>
          🔁 Rematch
        </button>
      ) : (
        <>
          <button className="primary" onClick={onRematchRef.current}>
            🔁 Rematch now — auto in {left}s
          </button>
          <button className="secondary" onClick={() => setStopped(true)}>
            Stop countdown
          </button>
        </>
      )}
    </div>
  )
}

/**
 * The optional big screen: same game, same reveal script, bigger renderer.
 * Watches without a seat via WatchGame. Sound defaults ON here — the monitor
 * carries the room while phones stay silent.
 */
export function MonitorPage({ code }: { code: string }) {
  const game = useGame(code, 'monitor')
  const sound = useSound('monitor')
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
                  {game.lobby?.players.map((p) => (
                    <li key={p.id} className="lobby-player">
                      <Avatar avatar={p.avatar} name={p.name} />
                      <span>{p.name}</span>
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
              <Countdown deadline={game.deadline} onUrgentTick={() => sound.play('tick')} />
            </div>
            <PlayerBoard
              players={snapshot.players}
              maxHp={snapshot.startingHp}
              maxBullets={snapshot.maxBullets}
              goldToWin={snapshot.goldToWin}
            />
            <p className="hint locked-status">
              {game.locked ? (
                `${game.locked.lockedCount}/${game.locked.totalExpected} locked in…`
              ) : (
                <>
                  Players are choosing<span className="ellipsis" />
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

      case 'gameover': {
        const winners = game.winnerIds
          ?.map((id) => snapshot?.players.find((p) => p.id === id))
          .filter((p) => p !== undefined)
        return (
          <div className="page tv center">
            <Confetti pieces={140} />
            <div className="winner-portraits">
              {winners?.map((p) => <Avatar key={p.id} avatar={p.avatar} name={p.name} size="hero" />)}
            </div>
            <div className="winner-banner winner-banner-tv">
              🏆{' '}
              {winners?.map((p, i) => (
                <span key={p.id}>
                  {i > 0 && ' & '}
                  <span style={{ color: accentOf(p.avatar) }}>{p.name}</span>
                </span>
              ))}{' '}
              {(winners?.length ?? 0) > 1 ? 'win!' : 'wins!'}
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
            <RematchCountdown key={game.snapshot?.roundNumber} onRematch={game.rematch} />
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
