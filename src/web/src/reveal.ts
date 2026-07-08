import type { ActionDto, GameSnapshot, RevealStepDto } from './types'

/**
 * The reveal script arrives as an ordered list of RevealStepDto. Every device
 * plays it back in lockstep: fold steps one at a time over the pre-round
 * snapshot, pausing per step, so HP/bullets/gold tick down exactly when the
 * narration says they do.
 */

export interface DisplayPlayer {
  id: string
  name: string
  hp: number
  bullets: number
  gold: number
  isAlive: boolean
  /** Card shown face-up for the current volley (null before the flip). */
  revealedAction: ActionDto | null
  /** Transient flags driving CSS animation on the currently played step. */
  flags: {
    dodging?: boolean
    shooting?: boolean
    hit?: boolean
    cancelled?: boolean
    loaded?: boolean
    gotGold?: boolean
    eliminated?: boolean
    winner?: boolean
  }
}

export interface DisplayState {
  players: DisplayPlayer[]
  caption: string
  /** Set once a gameEnded step has played. */
  winnerIds: string[] | null
}

export function initialDisplayState(prev: GameSnapshot): DisplayState {
  return {
    players: prev.players.map((p) => ({
      id: p.id,
      name: p.name,
      hp: p.hp,
      bullets: p.bullets,
      gold: p.gold,
      isAlive: p.isAlive,
      revealedAction: null,
      flags: {},
    })),
    caption: '',
    winnerIds: null,
  }
}

/** Milliseconds each step stays on screen. */
export function stepDuration(step: RevealStepDto): number {
  switch (step.type) {
    case 'actionsRevealed':
      return 2400
    case 'shotFired':
      return 2000
    case 'actionCancelled':
      return 1600
    case 'gunLoaded':
      return 1200
    case 'suddenDeathBullet':
      return 1600
    case 'chestResolved':
      return 2000
    case 'playerEliminated':
      return 2400
    case 'actionFizzled':
      return 1600
    case 'gameEnded':
      return 2800
    default:
      return 1500
  }
}

function describeAction(action: ActionDto, name: (id: string) => string): string {
  switch (action.type) {
    case 'dodge':
      return 'Dodge'
    case 'load':
      return 'Load'
    case 'attack':
      return `Attack ${name(action.targetId ?? '?')}`
    case 'chest':
      return `Chest ${(action.chestIndex ?? 0) + 1}`
  }
}

/** Returns the next display state after `step`, with a narration caption. */
export function applyStep(state: DisplayState, step: RevealStepDto): DisplayState {
  const players = state.players.map((p) => ({ ...p, flags: {} as DisplayPlayer['flags'] }))
  const byId = new Map(players.map((p) => [p.id, p]))
  const name = (id: string) => byId.get(id)?.name ?? '?'
  let caption = ''
  let winnerIds = state.winnerIds

  switch (step.type) {
    case 'actionsRevealed': {
      for (const p of players) p.revealedAction = null
      for (const a of step.actions ?? []) {
        const p = byId.get(a.playerId)
        if (!p) continue
        p.revealedAction = a.action
        if (a.action.type === 'dodge') p.flags.dodging = true
      }
      caption = 'Cards on the table!'
      break
    }
    case 'shotFired': {
      const shooter = byId.get(step.shooterId ?? '')
      const target = byId.get(step.targetId ?? '')
      if (shooter) {
        shooter.bullets = Math.max(0, shooter.bullets - 1)
        shooter.flags.shooting = true
      }
      if (step.hit && target) {
        target.hp = Math.max(0, target.hp - 1)
        target.flags.hit = true
        caption = `${name(step.shooterId!)} shoots ${name(step.targetId!)} — hit!`
      } else {
        if (target) target.flags.dodging = true
        caption = `${name(step.shooterId!)} shoots ${name(step.targetId!)} — dodged!`
      }
      break
    }
    case 'actionCancelled': {
      const p = byId.get(step.playerId ?? '')
      if (p) p.flags.cancelled = true
      caption = `${name(step.playerId!)} was hit — ${describeAction(step.action!, name)} is cancelled!`
      break
    }
    case 'gunLoaded': {
      const p = byId.get(step.playerId ?? '')
      if (p) {
        p.bullets = step.bulletsNow ?? p.bullets + 1
        p.flags.loaded = true
      }
      caption = `${name(step.playerId!)} loads a bullet.`
      break
    }
    case 'suddenDeathBullet': {
      const p = byId.get(step.playerId ?? '')
      if (p) {
        p.bullets = step.bulletsNow ?? p.bullets + 1
        p.flags.loaded = true
      }
      caption = `Sudden death — ${name(step.playerId!)} gets a free bullet!`
      break
    }
    case 'chestResolved': {
      const chest = `Chest ${(step.chestIndex ?? 0) + 1}`
      const contenders = step.contenderIds ?? []
      if (step.chestWinnerId) {
        const p = byId.get(step.chestWinnerId)
        if (p) {
          p.gold += 1
          p.flags.gotGold = true
        }
        caption = `${name(step.chestWinnerId)} grabs a gold bar from ${chest}!`
      } else if (contenders.length > 1) {
        caption = `${chest}: standoff between ${contenders.map(name).join(' & ')} — nobody gets gold!`
      } else {
        caption = `${chest} stays shut.`
      }
      break
    }
    case 'playerEliminated': {
      const p = byId.get(step.playerId ?? '')
      const looters = step.looterIds ?? []
      const share = step.goldPerLooter ?? 0
      if (p) {
        p.isAlive = false
        p.flags.eliminated = true
        p.gold = 0 // split among looters, remainder lost
      }
      for (const id of looters) {
        const looter = byId.get(id)
        if (looter && share > 0) {
          looter.gold += share
          looter.flags.gotGold = true
        }
      }
      caption =
        share > 0
          ? `${name(step.playerId!)} is down! ${looters.map(name).join(' & ')} loot ${share} gold each.`
          : `${name(step.playerId!)} is down!`
      break
    }
    case 'actionFizzled': {
      const p = byId.get(step.playerId ?? '')
      if (p) p.flags.cancelled = true
      caption = `${name(step.playerId!)}'s ${describeAction(step.action!, name)} fizzles into a Dodge.`
      break
    }
    case 'gameEnded': {
      winnerIds = step.winnerIds ?? []
      for (const id of winnerIds) {
        const p = byId.get(id)
        if (p) p.flags.winner = true
      }
      const names = winnerIds.map(name).join(' & ')
      caption =
        step.winReason === 'LastStanding'
          ? `${names} is the last one standing!`
          : step.winReason === 'MutualDestruction'
            ? `Mutual destruction — ${names} share the victory!`
            : `${names} wins with the gold!`
      break
    }
  }

  return { players, caption, winnerIds }
}
