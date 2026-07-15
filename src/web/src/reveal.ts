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
  /** Avatar key (see avatars.ts). */
  avatar: string
  hp: number
  bullets: number
  gold: number
  isAlive: boolean
  /** Resigned players stay in the game but auto-Dodge every round. */
  isResigned: boolean
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
  /** Chests in play — with a single chest, captions say "Chest", not "Chest 1". */
  chestCount: number
}

export function initialDisplayState(prev: GameSnapshot): DisplayState {
  return {
    chestCount: prev.chestCount,
    players: prev.players.map((p) => ({
      id: p.id,
      name: p.name,
      avatar: p.avatar,
      hp: p.hp,
      bullets: p.bullets,
      gold: p.gold,
      isAlive: p.isAlive,
      isResigned: p.isResigned,
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
    // Kill/loot and endgame banners hold longer — they're the beats people
    // react to, and the loot gold lands mid-step (sound at +700ms).
    case 'playerEliminated':
      return 3400
    case 'playerResigned':
      return 3000
    case 'actionFizzled':
      return 1600
    case 'gameEnded':
      return 3400
    default:
      return 1500
  }
}

export function chestName(index: number | null | undefined, chestCount: number): string {
  return chestCount > 1 ? `Chest ${(index ?? 0) + 1}` : 'Chest'
}

function describeAction(action: ActionDto, chestCount: number): string {
  switch (action.type) {
    case 'dodge':
      return 'Dodge'
    case 'load':
      return 'Load'
    case 'attack':
      return 'Attack'
    case 'chest':
      return chestName(action.chestIndex, chestCount)
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
      const acts = step.actions ?? []
      for (const a of acts) {
        const p = byId.get(a.playerId)
        if (!p) continue
        p.revealedAction = a.action
        if (a.action.type === 'dodge') p.flags.dodging = true
      }
      // Head-to-head (duel volleys, 2-player games): name the picks outright —
      // with only two cards on the table, the pair IS the story.
      caption =
        acts.length === 2
          ? [
              who(acts[0].playerId),
              t(`: ${describeAction(acts[0].action, state.chestCount)}`),
              t(' vs '),
              who(acts[1].playerId),
              t(`: ${describeAction(acts[1].action, state.chestCount)}`),
            ]
          : [t('Cards on the table!')]
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
      caption = [who(step.playerId), t(` was hit — ${describeAction(step.action!, state.chestCount)} is cancelled!`)]
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
      const chest = chestName(step.chestIndex, state.chestCount)
      const contenders = step.contenderIds ?? []
      if (step.chestWinnerId) {
        const gained = step.goldGained ?? 1
        const p = byId.get(step.chestWinnerId)
        if (p) {
          p.gold += gained
          p.flags.gotGold = true
          p.flags.goldGained = gained
        }
        caption = [
          who(step.chestWinnerId),
          t(` grabs ${gained === 1 ? 'a gold bar' : `${gained} gold bars`} from ${chest}!`),
        ]
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
      const bars = share === 1 ? '1 gold bar' : `${share} gold bars`
      caption =
        share > 0
          ? looters.length === 1
            ? [who(step.playerId), t(' is down! '), ...all(looters), t(` loots ${bars}.`)]
            : [who(step.playerId), t(' is down! '), ...all(looters), t(` loot ${bars} each.`)]
          : [who(step.playerId), t(' is down!')]
      break
    }
    case 'playerResigned': {
      const p = byId.get(step.playerId ?? '')
      const lost = step.goldLost ?? 0
      if (p) {
        p.isAlive = false
        p.hp = 0
        p.gold = 0 // abandoned, nobody loots it
        p.flags.eliminated = true
      }
      caption =
        lost > 0
          ? [
              who(step.playerId),
              t(` resigns — ${lost === 1 ? '1 gold bar' : `${lost} gold bars`} left on the table!`),
            ]
          : [who(step.playerId), t(' resigns and walks away.')]
      break
    }
    case 'actionFizzled': {
      const p = byId.get(step.playerId ?? '')
      if (p) p.flags.cancelled = true
      caption = [who(step.playerId), t(`'s ${describeAction(step.action!, state.chestCount)} fizzles into a Dodge.`)]
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
            ? [t('Mutual destruction — nobody wins!')]
            : [...names, t(winnerIds.length > 1 ? ' win with the gold!' : ' wins with the gold!')]
      break
    }
  }

  return { players, caption, winnerIds, chestCount: state.chestCount }
}
