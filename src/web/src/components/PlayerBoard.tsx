import type { ActionDto } from '../types'
import type { DisplayPlayer } from '../reveal'

export interface BoardPlayer {
  id: string
  name: string
  hp: number
  bullets: number
  gold: number
  isAlive: boolean
  revealedAction?: ActionDto | null
  flags?: DisplayPlayer['flags']
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

/**
 * The shared board: one tile per player with HP / bullets / gold. Used by the
 * lobby-to-round views and by the reveal playback (where `flags` drive the
 * step animations). `meId` puts a "you" marker on the local player.
 */
export function PlayerBoard({
  players,
  maxHp,
  maxBullets,
  goldToWin,
  meId,
  targetedIds,
}: {
  players: BoardPlayer[]
  maxHp: number
  maxBullets: number
  goldToWin: number
  meId?: string | null
  targetedIds?: string[]
}) {
  return (
    <div className="board">
      {players.map((p) => {
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
          <div key={p.id} className={classes}>
            <div className="tile-name">
              {p.name}
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
          </div>
        )
      })}
    </div>
  )
}
