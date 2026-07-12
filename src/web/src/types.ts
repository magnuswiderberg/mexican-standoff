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
  /** Avatar key (see avatars.ts). */
  avatar: string
  hp: number
  bullets: number
  gold: number
  isAlive: boolean
  /** Resigned players stay in the game but auto-Dodge every round. */
  isResigned: boolean
}

export interface GameSnapshot {
  code: string
  phase: string
  roundNumber: number
  isDuel: boolean
  /** Completed duel volleys (programmed sequences) — display volley is this + 1. */
  duelVolley: number
  suddenDeath: boolean
  chestCount: number
  goldToWin: number
  maxBullets: number
  startingHp: number
  duelSequenceLength: number
  players: PlayerSnapshot[]
}

/** GET /api/rules — the engine's default rule numbers, for the "How to play" page. */
export interface RulesView {
  startingHp: number
  maxBullets: number
  goldToWin: number
  goldPerChest: number
  duelSequenceLength: number
}

/** Per-game settings sent with CreateGame; omitted fields fall back to server config. */
export interface CreateGameSettings {
  /** Seconds to pick an action; 0 disables the timer. */
  selectionTimerSeconds?: number | null
}

export interface CreateGameResult {
  code: string
  /** Secret that authorizes running this game (start/stop/kick/rematch) from the monitor. */
  monitorToken: string
}

export interface LobbyPlayer {
  id: string
  name: string
  /** Avatar key (see avatars.ts). */
  avatar: string
  /** Dev bot seat — the server plays it. */
  isBot: boolean
}

export interface LobbyView {
  code: string
  players: LobbyPlayer[]
  canStart: boolean
  /** Dev-only: the server allows the host to add bot seats. */
  botsEnabled: boolean
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
  /** Everyone who has locked in so far — drives the per-tile ✓ badges. */
  lockedPlayerIds: string[]
  /** Resigned (or mid-game-kicked) players — shows the 🏳️ before the snapshot catches up. */
  resignedPlayerIds: string[]
}

export type RevealStepType =
  | 'actionsRevealed'
  | 'shotFired'
  | 'actionCancelled'
  | 'gunLoaded'
  | 'suddenDeathBullet'
  | 'chestResolved'
  | 'playerEliminated'
  | 'playerResigned'
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
  goldGained?: number | null
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

export type ServerPhase = 'Lobby' | 'Selecting' | 'GameOver' | 'Stopped'

export interface GameView {
  phase: ServerPhase
  lobby: LobbyView
  snapshot: GameSnapshot | null
  deadline: string | null
  playerId: string | null
  hasSubmitted: boolean
  winnerIds: string[] | null
  winReason: string | null
  /** A monitor page is watching — rematches start from the monitor, not the phones. */
  hasMonitor: boolean
}
