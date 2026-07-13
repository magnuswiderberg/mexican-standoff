import { useEffect, useLayoutEffect, useRef, useState } from 'react'
import type { CSSProperties, ReactNode } from 'react'
import type { ActionDto } from '../types'
import { chestName } from '../reveal'
import type { DisplayPlayer } from '../reveal'
import { accentOf, avatarOf, avatarUrl } from '../avatars'
import { AttackIcon, BulletIcon, ChestIcon, DodgeIcon, LoadIcon } from './icons'

export interface BoardPlayer {
  id: string
  name: string
  avatar: string
  hp: number
  bullets: number
  gold: number
  isAlive: boolean
  /** Resigned players stay in the game but auto-Dodge every round. */
  isResigned?: boolean
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

/**
 * Big dramatic-beat emoji for a reveal step. With a `playerId` it pops over
 * that player's tile (the actor), otherwise center stage over the whole board.
 */
export interface StageFx {
  icon: string
  playerId: string | null
  key: number
}

export function actionLabel(action: ActionDto, chestCount: number): ReactNode {
  switch (action.type) {
    case 'dodge':
      return <><DodgeIcon /> Dodge</>
    case 'load':
      return <><LoadIcon /> Load</>
    case 'attack':
      return <><AttackIcon /> Attack</>
    case 'chest':
      return <><ChestIcon /> {chestName(action.chestIndex, chestCount)}</>
  }
}

export function Avatar({ avatar, name, size }: { avatar: string; name: string; size?: 'big' | 'hero' | 'tile' }) {
  const cls = `avatar ${size ? `avatar-${size}` : ''}`
  const spec = avatarOf(avatar)
  if (!spec) {
    // Unknown key (shouldn't happen): fall back to an initial disc.
    return <span className={`${cls} avatar-fallback`}>{(name.trim()[0] ?? '?').toUpperCase()}</span>
  }
  return (
    <img
      className={cls}
      src={avatarUrl(avatar)}
      alt={spec.persona}
      title={`${spec.name} — ${spec.persona}`}
      // The tile portrait sits inside the player-colored tile border, so no ring.
      style={size === 'tile' ? undefined : { boxShadow: `0 0 0 2px ${spec.accent}` }}
    />
  )
}

/** A row of filled/empty pips; `empty` defaults to a dimmed copy of `symbol`. */
function Pips({ count, max, symbol, empty, overlap }: { count: number; max: number; symbol: ReactNode; empty?: ReactNode; overlap?: boolean }) {
  return (
    <span className={overlap ? 'pips pips-overlap' : 'pips'}>
      {Array.from({ length: max }, (_, i) => (
        <span key={i} className={i < count ? 'pip' : 'pip pip-empty'}>
          {i < count ? symbol : (empty ?? symbol)}
        </span>
      ))}
    </span>
  )
}

/** Two-tap kick on a player tile: ✕ arms, "kick?" fires. Disarms by itself. */
function TileKick({ name, onKick }: { name: string; onKick: () => void }) {
  const [armed, setArmed] = useState(false)

  useEffect(() => {
    if (!armed) return
    const timer = setTimeout(() => setArmed(false), 4000)
    return () => clearTimeout(timer)
  }, [armed])

  return (
    <button
      type="button"
      className={`kick-btn tile-kick ${armed ? 'tile-kick-armed' : ''}`}
      aria-label={armed ? `Confirm removing ${name}` : `Remove ${name}`}
      onClick={() => {
        if (armed) onKick()
        else setArmed(true)
      }}
    >
      {armed ? 'kick?' : '✕'}
    </button>
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
  chestCount = 1,
  meId,
  targetedIds,
  lockedIds,
  resignedIds,
  onKick,
  shot,
  stage,
  reserveAction = false,
  animKey = 0,
}: {
  players: BoardPlayer[]
  maxHp: number
  maxBullets: number
  goldToWin: number
  /** Chests in play — with a single chest, action chips say "Chest", not "Chest 1". */
  chestCount?: number
  meId?: string | null
  targetedIds?: string[]
  /** During selection: who has locked in. Non-null shows a ✓/spinner badge per living player. */
  lockedIds?: string[] | null
  /** Resigned/kicked mid-round — snapshots only catch up at resolution, this doesn't wait. */
  resignedIds?: string[]
  /** Host/monitor mid-game kick (forced resign) — a two-tap ✕ on living tiles (never on `meId`). */
  onKick?: (playerId: string) => void
  shot?: ShotFx | null
  /** Reveal-step stage icon — anchored to the actor's tile when it has a playerId. */
  stage?: StageFx | null
  /**
   * Reveal playback: hold the action chip's space on every tile even before the
   * cards flip (and on tiles that never act). Without it the tiles resize the
   * moment chips appear or a new volley starts, and the board flickers.
   */
  reserveAction?: boolean
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
    // Two tiles = head-to-head: the revealed-action chips scale up (see .board-two).
    <div className={players.length === 2 ? 'board board-two' : 'board'} ref={boardRef}>
      {players.map((p, i) => {
        const f = p.flags ?? {}
        const resigned = p.isResigned || (resignedIds?.includes(p.id) ?? false)
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
            style={{ '--player-color': accentOf(p.avatar), '--i': i } as CSSProperties}
            ref={(el) => {
              if (el) tileRefs.current.set(p.id, el)
              else tileRefs.current.delete(p.id)
            }}
          >
            <Avatar avatar={p.avatar} name={p.name} size="tile" />
            <div className="tile-body">
              <div className="tile-name">
                <span className="tile-name-text">{p.name}</span>
                {p.id === meId && <span className="you-badge">you</span>}
                {resigned && p.isAlive && (
                  <span className="you-badge resigned-badge" title="Resigned — only dodging">
                    🏳️
                  </span>
                )}
              </div>
              <div className="tile-stats">
                <Pips count={p.hp} max={maxHp} symbol="❤️" empty="🖤" />
                <Pips count={p.bullets} max={maxBullets} symbol={<BulletIcon className="pip-bullet" />} />
                <Pips count={p.gold} max={Math.max(goldToWin, p.gold)} symbol="🪙" overlap />
              </div>
              {/* Distinct keys: the placeholder and the real chip must be separate
                  elements, or React reuses the node and the card flip never plays. */}
              {p.revealedAction ? (
                <div key="action" className="tile-action">
                  {actionLabel(p.revealedAction, chestCount)}
                </div>
              ) : (
                reserveAction && <div key="action-empty" className="tile-action tile-action-empty" aria-hidden />
              )}
            </div>
            {lockedIds && p.isAlive && (
              <div
                className={`lock-badge ${lockedIds.includes(p.id) ? 'lock-badge-done' : ''}`}
                title={lockedIds.includes(p.id) ? 'Locked in' : 'Still choosing'}
              >
                {lockedIds.includes(p.id) ? '✓' : <span className="spinner" />}
              </div>
            )}
            {onKick && p.isAlive && !resigned && p.id !== meId && (
              <TileKick name={p.name} onKick={() => onKick(p.id)} />
            )}
            {!p.isAlive && <div className="tile-out">OUT</div>}
            {f.eliminated && <div className="tile-skull">☠️</div>}
            {stage && stage.playerId === p.id && (
              <div className="tile-stage" key={`stage${stage.key}`} aria-hidden>
                {stage.icon}
              </div>
            )}
            {f.dodging && (
              <span key={`dg${animKey}`} className="dodge-fx" aria-hidden>
                💨
              </span>
            )}
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
      {stage && !players.some((p) => p.id === stage.playerId) && (
        <div className="stage-icon" key={`stage${stage.key}`} aria-hidden>
          {stage.icon}
        </div>
      )}
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
