import type { Role, AnnualLeague, AnnualTeam } from './group'
import type { LeagueKey } from './rank'
import type { StatPlayer } from './stat'
import type { Entity } from './default'
import type { Team, TeamKey } from './team'
export type { StatPlayer } from './stat'

export enum AuctionType { Normal = 0, RandomByLetter = 1, RandomList = 2 }
export enum AuctionKind { Starting = 0, Repairing = 1 }
export enum AuctionStatus { NotStarted = 0, Paused = 1, InProgress = 2, Finished = 3 }
export interface AuctionWin { playerId: string; playerKey: string; teamId: string | null; owner: string | null; price: number }
export interface ActualAuction {
  player: StatPlayer
  started: string | Date
  timerStartedAtUtc: string | null
  winningTeamId: string | null
  owner: string | null
  substitutedPlayerId: string | null
  substituted: string | null
  price: number
}
export interface AuctionWrapper {
  id: string
  leagueKey: LeagueKey
  creator: string
  created: string | Date
  type: AuctionType
  kind: AuctionKind
  actual: ActualAuction | null
  winnings: AuctionWin[]
  status: AuctionStatus
  playerCounts: Partial<Record<Role, { total: number; shown: number }>>
  participants: AnnualTeam[]
  lastShow: Partial<Record<Role, string | null>>
  secondsPerAuction: number
  currentRole: Role
  version: number
}
export interface AuctionState {
  wrapper: AuctionWrapper
  lastUpdate: string | Date
  league: AnnualLeague
  leagueName: string
  teams: Entity<Team, TeamKey>[]
  realPlayers: StatPlayer[]
  isAuctioneer: boolean
  userEmail: string
}
export interface PlayerWithStats { name: string; role: string; realTeam: string; stats?: { goal: number; assist: number; yellowCard: number; redCard: number; average: number; fantaAverage: number } }
export interface BidPlaced { amount: number; bidderEmail: string; bidderName: string; playerName: string; substituted: string | null }
export interface PlayerAssignedInAuction { owner: string; teamName: string; price: number; playerName: string; teamCost: number; teamPlayersCount: number }
export interface PlayerAssignedMessage { playerAssignedInAuction: PlayerAssignedInAuction | null; team: Entity<Team, TeamKey> | null }
export interface AuctionCommand { expectedVersion?: number; role?: number; playerId?: string; teamId?: string; amount?: number; substitutedPlayerId?: string; timer?: number; newAuctioneerUserId?: string; playerStatus?: number; revenue?: number; rosterOrdinal?: number; expectedRosterVersion?: number }
export interface AuctionUpdate { auctionId: string; version: number; operation: string }
export const statPlayerToPlayerWithStats = (player: StatPlayer): PlayerWithStats => ({ name: player.name, role: String(player.role), realTeam: player.realTeam.name })
export const AuctionTypeLabels = { [AuctionType.Normal]: 'Normale (Alfabetico)', [AuctionType.RandomByLetter]: 'Random per Lettera', [AuctionType.RandomList]: 'Random Casuale' }
export const AuctionTypeDescriptions = { [AuctionType.Normal]: 'Giocatori in ordine alfabetico per ruolo', [AuctionType.RandomByLetter]: 'Lettera iniziale casuale per ruolo', [AuctionType.RandomList]: 'Lista casuale per ruolo' }
export const AuctionKindLabels = { [AuctionKind.Starting]: 'Asta Iniziale', [AuctionKind.Repairing]: 'Asta di Riparazione' }
export const AuctionKindDescriptions = { [AuctionKind.Starting]: 'Formazione della rosa completa', [AuctionKind.Repairing]: 'Sostituzione di giocatori infortunati o venduti' }
