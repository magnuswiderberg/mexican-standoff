import { useState } from 'react'
import { createConnection, friendlyError } from '../gameClient'
import { navigate } from '../router'

export function HomePage() {
  const [code, setCode] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [creating, setCreating] = useState(false)

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
      const newCode = await conn.invoke<string>('CreateGame')
      navigate(`/monitor/${newCode}`)
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
      <h1 className="logo">
        🤠 Mexican Standoff
      </h1>
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

      {error && <div className="error">{error}</div>}
    </div>
  )
}
