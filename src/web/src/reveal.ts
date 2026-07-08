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
  /** Avatar palette key (see colors.ts). */
  color: string
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
    /** Gold gained on this step (drives the floating "+n" indicator). */
    goldGained?: number
    eliminated?: boolean
    winner?: boolean
  }
}

/** One run of caption text; parts with a playerId render in that player's color. */
export interface CaptionPart {
  text: string
  playerId?: string
}

export interface DisplayState {
  players: DisplayPlayer[]
  caption: CaptionPart[]
  /** Set once a gameEnded step has played. */
  winnerIds: string[] | null
}

export function initialDisplayState(prev: GameSnapshot): DisplayState {
  return {
    players: prev.players.map((p) => ({
      id: p.id,
      name: p.name,
      color: p.color,
      hp: p.hp,
      bullets: p.bullets,
      gold: p.gold,
      isAlive: p.isAlive,
      revealedAction: null,
      flags: {},
    })),
    caption: [],
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

function describeAction(action: ActionDto): string {
  switch (action.type) {
    case 'dodge':
      return 'Dodge'
    case 'load':
      return 'Load'
    case 'attack':
      return 'Attack'
    case 'chest':
      return `Chest ${(action.chestIndex ?? 0) + 1}`
  }
}

/** Returns the next display state after `step`, with a narration caption. */
export function applyStep(state: DisplayState, step: RevealStepDto): DisplayState {
  const players = state.players.map((p) => ({ ...p, flags: {} as DisplayPlayer['flags'] }))
  const byId = new Map(players.map((p) => [p.id, p]))
  const t = (text: string): CaptionPart => ({ text })
  const who = (id: string | null | undefined): CaptionPart => ({
    text: byId.get(id ?? '')?.name ?? '?',
    playerId: id ?? undefined,
  })
  /** Interleaves player-name parts with " & ". */
  const all = (ids: string[]): CaptionPart[] =>
    ids.flatMap((id, i) => (i === 0 ? [who(id)] : [t(' & '), who(id)]))
  let caption: CaptionPart[] = []
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
      caption = [t('Cards on the table!')]
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
        caption = [who(step.shooterId), t(' shoots '), who(step.targetId), t(' — hit!')]
      } else {
        if (target) target.flags.dodging = true
        caption = [who(step.shooterId), t(' shoots '), who(step.targetId), t(' — dodged!')]
      }
      break
    }
    case 'actionCancelled': {
      const p = byId.get(step.playerId ?? '')
      if (p) p.flags.cancelled = true
      caption = [who(step.playerId), t(` was hit — ${describeAction(step.action!)} is cancelled!`)]
      break
    }
    case 'gunLoaded': {
      const p = byId.get(step.playerId ?? '')
      if (p) {
        p.bullets = step.bulletsNow ?? p.bullets + 1
        p.flags.loaded = true
      }
      caption = [who(step.playerId), t(' loads a bullet.')]
      break
    }
    case 'suddenDeathBullet': {
      const p = byId.get(step.playerId ?? '')
      if (p) {
        p.bullets = step.bulletsNow ?? p.bullets + 1
        p.flags.loaded = true
      }
      caption = [t('Sudden death — '), who(step.playerId), t(' gets a free bullet!')]
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
          p.flags.goldGained = 1
        }
        caption = [who(step.chestWinnerId), t(` grabs a gold bar from ${chest}!`)]
      } else if (contenders.length > 1) {
        caption = [
          t(`${chest}: standoff between `),
          ...all(contenders),
          t(' — nobody gets gold!'),
        ]
      } else {
        caption = [t(`${chest} stays shut.`)]
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
          looter.flags.goldGained = share
        }
      }
      caption =
        share > 0
          ? [who(step.playerId), t(' is down! '), ...all(looters), t(` loot ${share} gold each.`)]
          : [who(step.playerId), t(' is down!')]
      break
    }
    case 'actionFizzled': {
      const p = byId.get(step.playerId ?? '')
      if (p) p.flags.cancelled = true
      caption = [who(step.playerId), t(`'s ${describeAction(step.action!)} fizzles into a Dodge.`)]
      break
    }
    case 'gameEnded': {
      winnerIds = step.winnerIds ?? []
      for (const id of winnerIds) {
        const p = byId.get(id)
        if (p) p.flags.winner = true
      }
      const names = all(winnerIds)
      caption =
        step.winReason === 'LastStanding'
          ? [...names, t(winnerIds.length > 1 ? ' are the last standing!' : ' is the last one standing!')]
          : step.winReason === 'MutualDestruction'
            ? [t('Mutual destruction — '), ...names, t(' share the victory!')]
            : [...names, t(winnerIds.length > 1 ? ' win with the gold!' : ' wins with the gold!')]
      break
    }
  }

  return { players, caption, winnerIds }
}
