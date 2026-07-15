import type { ReactNode } from 'react'
import { navigate } from '../router'
import { Logo } from '../components/Logo'
import {
  AttackIcon,
  BulletIcon,
  ChestIcon,
  DodgeIcon,
  DuelIcon,
  FlagIcon,
  GoldBarIcon,
  HitIcon,
  HpIcon,
  LoadIcon,
  SkullIcon,
  SpeakerOffIcon,
  SpeakerOnIcon,
  StarIcon,
  TargetIcon,
} from '../components/icons'

/**
 * Unlisted showcase (/_icons): the whole hand-drawn set on the real saloon
 * theme, each glyph in the colour it carries in-app. Not linked from anywhere
 * — a place to eyeball the icons and their sizes without driving a game.
 */

interface Swatch {
  name: string
  node: ReactNode
  /** Where it shows up. */
  note: string
}

const ACTIONS: Swatch[] = [
  { name: 'DodgeIcon', node: <DodgeIcon />, note: 'Dodge card, dodge/miss beats' },
  { name: 'AttackIcon', node: <AttackIcon />, note: 'Attack card' },
  { name: 'LoadIcon', node: <LoadIcon />, note: 'Load card' },
  { name: 'ChestIcon', node: <ChestIcon />, note: 'Chest card' },
  { name: 'BulletIcon', node: <BulletIcon className="pip-bullet" />, note: 'Ammo pips' },
]

const STATE: Swatch[] = [
  { name: 'HpIcon', node: <HpIcon className="pip-hp" />, note: 'Health pips' },
  { name: 'GoldBarIcon', node: <GoldBarIcon className="pip-gold" />, note: 'Gold pips, chest grab' },
  { name: 'SkullIcon', node: <SkullIcon />, note: 'Elimination, mutual destruction' },
  { name: 'DuelIcon', node: <DuelIcon className="fx-danger" />, note: 'Standoff stage beat' },
  { name: 'FlagIcon', node: <FlagIcon />, note: 'Resign' },
  { name: 'StarIcon', node: <StarIcon className="fx-gold" />, note: 'Winner, host action' },
  { name: 'TargetIcon', node: <TargetIcon />, note: 'Pick who to shoot' },
  { name: 'HitIcon', node: <HitIcon className="impact-hit" />, note: 'A shot that lands' },
]

const CONTROLS: Swatch[] = [
  { name: 'SpeakerOnIcon', node: <SpeakerOnIcon />, note: 'Sound on' },
  { name: 'SpeakerOffIcon', node: <SpeakerOffIcon />, note: 'Sound off' },
]

function Grid({ items }: { items: Swatch[] }) {
  return (
    <div className="ig-grid">
      {items.map((s) => (
        <div key={s.name} className="ig-card">
          <span className="ig-glyph">{s.node}</span>
          <span className="ig-name">{s.name}</span>
          <span className="ig-note">{s.note}</span>
        </div>
      ))}
    </div>
  )
}

export function IconGalleryPage() {
  return (
    <div className="page howto icongallery">
      <Logo />
      <p className="tagline">Icon set</p>

      <section>
        <h2>Actions</h2>
        <Grid items={ACTIONS} />
      </section>

      <section>
        <h2>Game state</h2>
        <Grid items={STATE} />
      </section>

      <section>
        <h2>Controls</h2>
        <Grid items={CONTROLS} />
      </section>

      <section>
        <h2>In context</h2>

        <div className="ig-context">
          <span className="ig-context-label">Health (2 of 3)</span>
          <span className="pips">
            <span className="pip"><HpIcon className="pip-hp" /></span>
            <span className="pip"><HpIcon className="pip-hp" /></span>
            <span className="pip pip-empty"><HpIcon className="pip-hp" /></span>
          </span>
        </div>

        <div className="ig-context">
          <span className="ig-context-label">Gold (4)</span>
          <span className="pips pips-overlap">
            {Array.from({ length: 4 }, (_, i) => (
              <span key={i} className="pip"><GoldBarIcon className="pip-gold" /></span>
            ))}
          </span>
        </div>

        <div className="ig-context">
          <span className="ig-context-label">Ammo (1 of 2)</span>
          <span className="pips">
            <span className="pip"><BulletIcon className="pip-bullet" /></span>
            <span className="pip pip-empty"><BulletIcon className="pip-bullet" /></span>
          </span>
        </div>

        <div className="cards howto-cards">
          <div className="card">
            <span className="card-icon"><DodgeIcon /></span>
            <span className="card-label">Dodge</span>
          </div>
          <div className="card">
            <span className="card-icon"><AttackIcon /></span>
            <span className="card-label">Attack</span>
          </div>
          <div className="card">
            <span className="card-icon"><LoadIcon /></span>
            <span className="card-label">Load</span>
          </div>
          <div className="card">
            <span className="card-icon"><ChestIcon /></span>
            <span className="card-label">Chest</span>
          </div>
        </div>
      </section>

      <section>
        <h2>Dramatic beats (stage size)</h2>
        <div className="ig-beats">
          <GoldBarIcon className="fx-gold" />
          <DuelIcon className="fx-danger" />
          <SkullIcon />
          <StarIcon className="fx-gold" />
          <HitIcon className="impact-hit" />
          <DodgeIcon className="impact-miss" />
        </div>
      </section>

      <button className="primary" onClick={() => navigate('/')}>
        Back to start
      </button>
    </div>
  )
}
