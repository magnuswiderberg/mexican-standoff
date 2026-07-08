import { useLayoutEffect, useRef, useState } from 'react'
import type { CSSProperties } from 'react'
import type { ActionDto } from '../types'
import type { DisplayPlayer } from '../reveal'
import { colorOf } from '../colors'

export interface BoardPlayer {
  id: string
  name: string
  color: string
  hp: number
  bullets: number
  gold: number
  isAlive: boolean
  revealedAction?: ActionDto | null
  flags?: DisplayPlayer['flags']
}

/** A shot to animate as a tracer across the board; `key` remounts the effect per step. */
export interface ShotFx {
  shooterId: string
  targetId: string
  hit: boolean
  key: number
}

function actionLabel(action: ActionDto): string {
  switch (action.type) {
    case 'dodge':
      return '💨 Dodge'
    case 'load':
      return '🔺 Load'
    case 'attack':
      return '🔫 Attack'
    case 'chest':
      return `💰 Chest ${(action.chestIndex ?? 0) + 1}`
  }
}

export function Avatar({ color, name, size }: { color: string; name: string; size?: 'big' }) {
  return (
    <span
      className={`avatar ${size === 'big' ? 'avatar-big' : ''}`}
      style={{ background: colorOf(color) }}
    >
      {(name.trim()[0] ?? '?').toUpperCase()}
    </span>
  )
}

function Pips({ count, max, symbol, empty }: { count: number; max: number; symbol: string; empty: string }) {
  return (
    <span className="pips">
      {Array.from({ length: max }, (_, i) => (
        <span key={i} className={i < count ? 'pip' : 'pip pip-empty'}>
          {i < count ? symbol : empty}
        </span>
      ))}
    </span>
  )
}

interface TracerLine {
  x1: number
  y1: number
  x2: number
  y2: number
}

/**
 * The shared board: one tile per player with HP / bullets / gold. Used by the
 * lobby-to-round views and by the reveal playback (where `flags` drive the
 * step animations). `meId` puts a "you" marker on the local player; `shot`
 * draws a bullet tracer from shooter to target; `animKey` remounts transient
 * indicators (floating −1 HP / +gold) once per reveal step.
 */
export function PlayerBoard({
  players,
  maxHp,
  maxBullets,
  goldToWin,
  meId,
  targetedIds,
  shot,
  animKey = 0,
}: {
  players: BoardPlayer[]
  maxHp: number
  maxBullets: number
  goldToWin: number
  meId?: string | null
  targetedIds?: string[]
  shot?: ShotFx | null
  animKey?: number
}) {
  const boardRef = useRef<HTMLDivElement>(null)
  const tileRefs = useRef(new Map<string, HTMLDivElement>())
  const [line, setLine] = useState<TracerLine | null>(null)

  useLayoutEffect(() => {
    const board = boardRef.current
    const from = shot && tileRefs.current.get(shot.shooterId)
    const to = shot && tileRefs.current.get(shot.targetId)
    if (!board || !from || !to) {
      setLine(null)
      return
    }
    const b = board.getBoundingClientRect()
    const f = from.getBoundingClientRect()
    const t = to.getBoundingClientRect()
    setLine({
      x1: f.left + f.width / 2 - b.left,
      y1: f.top + f.height / 2 - b.top,
      x2: t.left + t.width / 2 - b.left,
      y2: t.top + t.height / 2 - b.top,
    })
  }, [shot])

  return (
    <div className="board" ref={boardRef}>
      {players.map((p, i) => {
        const f = p.flags ?? {}
        const classes = [
          'tile',
          !p.isAlive && 'tile-dead',
          f.hit && 'tile-hit',
          f.dodging && 'tile-dodge',
          f.shooting && 'tile-shooting',
          f.loaded && 'tile-loaded',
          f.gotGold && 'tile-gold',
          f.cancelled && 'tile-cancelled',
          f.eliminated && 'tile-eliminated',
          f.winner && 'tile-winner',
          targetedIds?.includes(p.id) && 'tile-targeted',
        ]
          .filter(Boolean)
          .join(' ')
        return (
          <div
            key={p.id}
            className={classes}
            style={{ '--player-color': colorOf(p.color), '--i': i } as CSSProperties}
            ref={(el) => {
              if (el) tileRefs.current.set(p.id, el)
              else tileRefs.current.delete(p.id)
            }}
          >
            <div className="tile-name">
              <Avatar color={p.color} name={p.name} />
              <span className="tile-name-text">{p.name}</span>
              {p.id === meId && <span className="you-badge">you</span>}
            </div>
            <div className="tile-stats">
              <Pips count={p.hp} max={maxHp} symbol="❤️" empty="🖤" />
              <Pips count={p.bullets} max={maxBullets} symbol="🔸" empty="▫️" />
              <span className="gold">
                {'🪙'.repeat(p.gold)}
                <span className="gold-target">/{goldToWin}</span>
              </span>
            </div>
            {p.revealedAction && <div className="tile-action">{actionLabel(p.revealedAction)}</div>}
            {!p.isAlive && <div className="tile-out">OUT</div>}
            {f.eliminated && <div className="tile-skull">☠️</div>}
            {f.hit && (
              <span key={`hp${animKey}`} className="float float-hurt">
                −1 ❤️
              </span>
            )}
            {(f.goldGained ?? 0) > 0 && (
              <span key={`au${animKey}`} className="float float-gold">
                +{f.goldGained} 🪙
              </span>
            )}
          </div>
        )
      })}
      {shot && line && (
        <div className="shot-layer" key={shot.key}>
          <div className="muzzle-flash" style={{ left: line.x1, top: line.y1 }} />
          <div
            className="bullet"
            style={
              {
                left: line.x1,
                top: line.y1,
                '--dx': `${line.x2 - line.x1}px`,
                '--dy': `${line.y2 - line.y1}px`,
              } as CSSProperties
            }
          />
          <div
            className={`impact ${shot.hit ? 'impact-hit' : 'impact-miss'}`}
            style={{ left: line.x2, top: line.y2 }}
          >
            {shot.hit ? '💥' : '💨'}
          </div>
        </div>
      )}
    </div>
  )
}
