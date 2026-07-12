import { useState } from 'react'
import { createConnection, friendlyError } from '../gameClient'
import { navigate } from '../router'
import { saveMonitorToken } from '../session'
import { Logo } from '../components/Logo'
import type { CreateGameResult, CreateGameSettings } from '../types'

/** Selection timer choices; 0 means no timer (rounds wait for everyone). */
const TIMER_CHOICES = [
  { seconds: 15, label: '15 seconds' },
  { seconds: 30, label: '30 seconds' },
  { seconds: 60, label: '1 minute' },
  { seconds: 120, label: '2 minutes' },
  { seconds: 0, label: 'No timer' },
]

export function HomePage() {
  const [code, setCode] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [creating, setCreating] = useState(false)
  const [timerSeconds, setTimerSeconds] = useState(30)

  const joinGame = (e: React.FormEvent) => {
    e.preventDefault()
    const trimmed = code.trim().toUpperCase()
    if (trimmed.length >= 4) navigate(`/game/${trimmed}`)
  }

  const hostGame = async () => {
    setCreating(true)
    setError(null)
    const conn = createConnection()
    try {
      await conn.start()
      const settings: CreateGameSettings = { selectionTimerSeconds: timerSeconds }
      const game = await conn.invoke<CreateGameResult>('CreateGame', settings)
      // This screen is now the monitor: the token it just got is what lets it
      // start, stop and kick — the monitor page picks it back up from storage.
      saveMonitorToken(game.code, game.monitorToken)
      navigate(`/monitor/${game.code}`)
    } catch (e) {
      setError(friendlyError(e))
      setCreating(false)
    } finally {
      // The monitor page opens its own connection and calls WatchGame.
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
        <button className="primary" type="submit" disabled={code.trim().length < 4}>
          Join
        </button>
      </form>

      <div className="or">— or —</div>

      <button className="secondary" onClick={hostGame} disabled={creating}>
        {creating ? 'Creating…' : '📺 Host a game on this screen'}
      </button>

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
    </div>
  )
}
