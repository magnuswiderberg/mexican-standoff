// TypeScript mirror of src/MexicanStandoff.Server/Contracts (SignalR sends camelCase).

export type ActionType = 'dodge' | 'load' | 'attack' | 'chest'

export interface ActionDto {
  type: ActionType
  targetId?: string | null
  chestIndex?: number | null
}

export interface PlayerSnapshot {
  id: string
  name: string
  hp: number
  bullets: number
  gold: number
  isAlive: boolean
}

export interface GameSnapshot {
  code: string
  phase: string
  roundNumber: number
  isDuel: boolean
  suddenDeath: boolean
  chestCount: number
  goldToWin: number
  maxBullets: number
  startingHp: number
  duelSequenceLength: number
  players: PlayerSnapshot[]
}

export interface LobbyPlayer {
  id: string
  name: string
}

export interface LobbyView {
  code: string
  players: LobbyPlayer[]
  canStart: boolean
}

export interface JoinResult {
  playerId: string
  playerToken: string
  lobby: LobbyView
}

export interface RoundStartedView {
  snapshot: GameSnapshot
  deadline: string | null
}

export interface PlayerLockedView {
  playerId: string
  lockedCount: number
  totalExpected: number
}

export type RevealStepType =
  | 'actionsRevealed'
  | 'shotFired'
  | 'actionCancelled'
  | 'gunLoaded'
  | 'suddenDeathBullet'
  | 'chestResolved'
  | 'playerEliminated'
  | 'actionFizzled'
  | 'gameEnded'

export interface RevealActionDto {
  playerId: string
  action: ActionDto
}

export interface RevealStepDto {
  type: RevealStepType
  actions?: RevealActionDto[] | null
  playerId?: string | null
  shooterId?: string | null
  targetId?: string | null
  hit?: boolean | null
  action?: ActionDto | null
  bulletsNow?: number | null
  chestIndex?: number | null
  contenderIds?: string[] | null
  chestWinnerId?: string | null
  looterIds?: string[] | null
  goldPerLooter?: number | null
  goldLost?: number | null
  winnerIds?: string[] | null
  winReason?: string | null
}

export interface RoundResolvedView {
  reveal: RevealStepDto[]
  snapshot: GameSnapshot
  nextDeadline: string | null
  winnerIds: string[] | null
  winReason: string | null
}

export type ServerPhase = 'Lobby' | 'Selecting' | 'GameOver'

export interface GameView {
  phase: ServerPhase
  lobby: LobbyView
  snapshot: GameSnapshot | null
  deadline: string | null
  playerId: string | null
  hasSubmitted: boolean
  winnerIds: string[] | null
  winReason: string | null
}
