import { useEffect, useState } from 'react'
import { arrivedInApp, navigate } from '../router'
import { Logo } from '../components/Logo'
import { AttackIcon, BulletIcon, ChestIcon, DodgeIcon, LoadIcon } from '../components/icons'
import type { RulesView } from '../types'

/** Rendered until /api/rules answers (and standalone if it never does). */
const FALLBACK_RULES: RulesView = {
  startingHp: 2,
  maxBullets: 2,
  goldToWin: 6,
  goldPerChest: 2,
  duelSequenceLength: 3,
}

/** Dim text link to the rules page, shared by the home page and the lobby. */
export function HowToPlayLink() {
  return (
    <a
      className="howto-link"
      href="/how-to-play"
      onClick={(e) => {
        e.preventDefault()
        navigate('/how-to-play')
      }}
    >
      📖 How to play
    </a>
  )
}

/** Static rules explainer; the numbers come from the engine via /api/rules. */
export function HowToPlayPage() {
  const [rules, setRules] = useState(FALLBACK_RULES)

  useEffect(() => {
    let cancelled = false
    fetch('/api/rules')
      .then((r) => (r.ok ? (r.json() as Promise<RulesView>) : FALLBACK_RULES))
      .then((r) => {
        if (!cancelled) setRules(r)
      })
      .catch(() => {})
    return () => {
      cancelled = true
    }
  }, [])

  // Came from the home page or a lobby: back returns there (a lobby seat
  // rebinds via the stored token). Loaded directly: home is the only exit.
  const dismiss = () => (arrivedInApp() ? history.back() : navigate('/'))

  return (
    <div className="page howto">
      <Logo />
      <p className="tagline">How to play</p>

      <section>
        <h2>The goal</h2>
        <p>
          Be the first to collect <strong>{rules.goldToWin} gold bars 🪙</strong> — or be the{' '}
          <strong>last one standing</strong>. You have <strong>{rules.startingHp} ❤️</strong> and a
          gun that holds <strong>{rules.maxBullets} bullets</strong>. Everyone starts with an{' '}
          <em>empty</em> gun.
        </p>
      </section>

      <section>
        <h2>Each round</h2>
        <p>
          Everyone secretly picks <strong>one card</strong>. When all are locked in, the cards flip
          at the same time and the round plays out.
        </p>
        <div className="cards howto-cards">
          <div className="card">
            <span className="card-icon">
              <DodgeIcon />
            </span>
            <span className="card-label">Dodge</span>
          </div>
          <div className="card">
            <span className="card-icon">
              <AttackIcon />
            </span>
            <span className="card-label">Attack</span>
          </div>
          <div className="card">
            <span className="card-icon">
              <LoadIcon />
            </span>
            <span className="card-label">Load</span>
          </div>
          <div className="card">
            <span className="card-icon">
              <ChestIcon />
            </span>
            <span className="card-label">Chest</span>
          </div>
        </div>
        <ul className="howto-actions">
          <li>
            <span className="howto-action-name">
              <DodgeIcon /> Dodge
            </span>{' '}
            — duck the volley. Nothing can hit you this round.
          </li>
          <li>
            <span className="howto-action-name">
              <AttackIcon /> Attack
            </span>{' '}
            — fire one bullet at a player for 1 damage. The bullet is spent even if they dodge.
          </li>
          <li>
            <span className="howto-action-name">
              <LoadIcon /> Load
            </span>{' '}
            — put a bullet <BulletIcon /> in your gun (it holds {rules.maxBullets}).
          </li>
          <li>
            <span className="howto-action-name">
              <ChestIcon /> Chest
            </span>{' '}
            — grab <strong>{rules.goldPerChest} gold bars</strong>… if you're the <em>only</em> one
            at that chest. Two or more grabbers stare each other down and <strong>nobody</strong>{' '}
            gets gold.
          </li>
        </ul>
      </section>

      <section>
        <h2>How the volley resolves</h2>
        <ol className="howto-order">
          <li>Dodgers duck — they can't be hit.</li>
          <li>
            <strong>All shots fire at once.</strong> Shooting someone doesn't stop their shot: if
            you shoot each other, you both get hit.
          </li>
          <li>
            <strong>Getting hit cancels your Load or Chest.</strong> You lose the ❤️ <em>and</em>{' '}
            come away empty-handed. Dodge is your only defense.
          </li>
          <li>Un-hit loaders get their bullet; lone un-hit grabbers get their gold.</li>
          <li>
            Anyone at 0 ❤️ is out. Their gold is <strong>looted</strong> — split between everyone
            who shot them this round.
          </li>
        </ol>
        <p>
          Out of the game? You spectate — and get a one-tap seat in the rematch. Dead players never
          win, even if loot pushes them past {rules.goldToWin} gold.
        </p>
      </section>

      <section>
        <h2>The Final Duel</h2>
        <p>
          When only <strong>2 players</strong> remain (or the game starts with 2), the endgame
          changes: both secretly program a{' '}
          <strong>sequence of {rules.duelSequenceLength} actions</strong>, then watch them play out
          step by step. If a planned action becomes impossible mid-sequence (your Load was
          cancelled, so that Attack has no bullet), it fizzles into a Dodge. Duel too long without
          blood or gold? Sudden death: the chest is taken away and both guns get a free bullet.
        </p>
      </section>

      <section>
        <h2>Gunslinger wisdom</h2>
        <ul className="howto-tips">
          <li>Round one, every gun is empty — nobody can shoot. Load, or race for the chest?</li>
          <li>An obvious chest run invites a bullet; an obvious dodge wastes everyone's time.</li>
          <li>A rich opponent is a walking treasure chest — downing them splits their gold.</li>
          <li>
            Dodge costs nothing, but it wins nothing. You can't duck your way to {rules.goldToWin}{' '}
            gold.
          </li>
        </ul>
      </section>

      <button className="primary" onClick={dismiss}>
        Got it — let's play
      </button>
    </div>
  )
}
