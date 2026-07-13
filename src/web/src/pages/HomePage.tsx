import { useState } from 'react'
import { createConnection, friendlyError } from '../gameClient'
import { navigate } from '../router'
import { saveMonitorToken } from '../session'
import { Logo } from '../components/Logo'
import { HowToPlayLink } from './HowToPlayPage'
import type { CreateGameResult, CreateGameSettings } from '../types'

/** Selection timer choices; 0 means no timer (rounds wait for everyone). */
const TIMER_CHOICES = [
  { seconds: 0, label: 'No timer' },
  { seconds: 15, label: '15 seconds' },
  { seconds: 30, label: '30 seconds' },
  { seconds: 60, label: '1 minute' },
  { seconds: 120, label: '2 minutes' },
]

/** Where the hosting screen goes after CreateGame: into the game, or up on the wall. */
type HostAs = 'player' | 'monitor'

export function HomePage() {
  const [code, setCode] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [creating, setCreating] = useState<HostAs | null>(null)
  const [timerSeconds, setTimerSeconds] = useState(0)

  const hasCode = code.trim().length >= 4

  const joinGame = (e: React.FormEvent) => {
    e.preventDefault()
    const trimmed = code.trim().toUpperCase()
    if (trimmed.length >= 4) navigate(`/game/${trimmed}`)
  }

  const hostGame = async (as: HostAs) => {
    setCreating(as)
    setError(null)
    const conn = createConnection()
    try {
      await conn.start()
      const settings: CreateGameSettings = { selectionTimerSeconds: timerSeconds }
      const game = await conn.invoke<CreateGameResult>('CreateGame', settings)
      // Keep the monitor token either way: it is what lets this screen start,
      // stop and kick. Hosting as a player we don't need it (the host's own seat
      // token is a control token too), but storing it means this device — and
      // only this device — can still put the game up on a monitor later.
      saveMonitorToken(game.code, game.monitorToken)
      // As a player: no monitor page ever connects, so the game is monitor-less
      // and the first seat to join — this one — hosts it from the lobby.
      navigate(as === 'monitor' ? `/monitor/${game.code}` : `/game/${game.code}`)
    } catch (e) {
      setError(friendlyError(e))
      setCreating(null)
    } finally {
      // The page we land on opens its own connection and hydrates from the hub.
      conn.stop().catch(() => {})
    }
  }

  return (
    <div className="page home">
      <div className="splash">
        <img src="/splash.jpg" alt="Three gunslingers facing off over a chest of gold" />
      </div>
      <Logo />
      <p className="tagline">A quick mind game — unloaded guns, gold, and nerve.</p>

      {/* Hosting leads. Players scan the host's QR straight into /game/CODE and
          never see this page at all — whoever is here is almost always the one
          starting the game. */}
      <div className="host-choices">
        <button className="primary" onClick={() => hostGame('player')} disabled={creating !== null}>
          {creating === 'player' ? 'Creating…' : '🤠 Host & play'}
        </button>
        <p className="hint host-hint">Start a game on this phone — the others scan you to join.</p>

        <button className="secondary" onClick={() => hostGame('monitor')} disabled={creating !== null}>
          {creating === 'monitor' ? 'Creating…' : '📺 Host on a big screen'}
        </button>
        <p className="hint host-hint">Put the board on a TV or laptop; everyone plays on their phone.</p>
      </div>

      <details className="host-settings">
        <summary>⚙️ Game settings</summary>
        <label className="setting-row">
          <span>Selection timer</span>
          <select value={timerSeconds} onChange={(e) => setTimerSeconds(Number(e.target.value))}>
            {TIMER_CHOICES.map((c) => (
              <option key={c.seconds} value={c.seconds}>
                {c.label}
              </option>
            ))}
          </select>
        </label>
      </details>

      {error && <div className="error">{error}</div>}

      <div className="or">— or —</div>

      {/* The fallback for anyone who can't scan: a code, and the two things a
          device can do with one — take a seat, or become the game's board. Both
          buttons stay on screen (greyed until there is a code), because a TV that
          has never seen this app has to be able to find the board route. */}
      <form className="join-form" onSubmit={joinGame}>
        <input
          value={code}
          onChange={(e) => setCode(e.target.value.toUpperCase())}
          placeholder="GAME CODE"
          maxLength={5}
          autoCapitalize="characters"
          autoCorrect="off"
          spellCheck={false}
        />
        <div className="code-actions">
          <button className="secondary" type="submit" disabled={!hasCode}>
            🤠 Join as player
          </button>
          {/* The monitor page does the asking: this screen has no token, so it
              shows a pair code and waits for the host to allow it. */}
          <button
            type="button"
            className="secondary"
            disabled={!hasCode}
            onClick={() => navigate(`/monitor/${code.trim().toUpperCase()}`)}
          >
            📺 Show the board
          </button>
        </div>
      </form>

      <HowToPlayLink />
    </div>
  )
}
