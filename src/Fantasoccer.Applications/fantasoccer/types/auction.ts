// Auction types for real-time auction management
import type { Role } from './group'

// Auction type (modo di ordinamento giocatori)
export enum AuctionType {
  Normal = 0,
  RandomByLetter = 1,
  RandomList = 2
}

// Auction status
export enum AuctionStatus {
  NotStarted = 0,
  Paused = 1,
  InProgress = 2,
  Finished = 3
}

// League key for auction
export interface LeagueKey {
  group: string
  league: string
  year: number
}

// Annual team info
export interface AnnualTeam {
  name: string
  owner: string
  additionalOwners: string[]
}

// Auction winning record
export interface AuctionWin {
  playerName: string
  owner: string | null
  price: number
}

// Main auction wrapper
export interface Auction {
  id: string
  leagueKey: LeagueKey
  creator: string
  created: Date
  type: AuctionType
  actual: AuctionWin | null
  winnings: AuctionWin[]
  status: AuctionStatus
  listOfPlayers: Record<Role, string[]>
  participants: AnnualTeam[]
  lastShow: Record<Role, string | null>
}

// Player being shown in auction
export interface PlayerShown {
  playerName: string
  role: string
  realTeam: string
  isActive: boolean
}

// Player with full stats
export interface PlayerWithStats {
  playerName: string
  role: string
  realTeam: string
  stats: {
    goal: number
    assist: number
    yellowCard: number
    redCard: number
    average: number
    fantaAverage: number
  }
}

// Team data in auction
export interface AuctionTeam {
  owner: string
  name: string
  cost: number
  playersCount: number
  players: AuctionPlayer[]
  remainingBudget: number
  remainingSlots: number
}

// Player in team
export interface AuctionPlayer {
  name: string
  role: string
  price: number
  realTeam: string
}

// Complete auction state (from "AuctionState" event)
export interface AuctionState {
  auctionId: string
  status: string
  type: string
  leagueName: string
  creator: string
  isAuctioneer: boolean
  currentPlayer: AuctionWin | null
  teams: AuctionTeam[]
  participants: AnnualTeam[]
  winnings: AuctionWin[]
}

// Available players by role
export interface AvailablePlayers {
  role: string
  count: number
  players: {
    name: string
    realTeam: string
    role: string
    average: number
    fantaAverage: number
  }[]
}

// Auction type labels
export const AuctionTypeLabels: Record<AuctionType, string> = {
  [AuctionType.Normal]: 'Normale (Alfabetico)',
  [AuctionType.RandomByLetter]: 'Random per Lettera',
  [AuctionType.RandomList]: 'Random Casuale'
}

// Auction type descriptions
export const AuctionTypeDescriptions: Record<AuctionType, string> = {
  [AuctionType.Normal]: 'Giocatori in ordine alfabetico per ruolo',
  [AuctionType.RandomByLetter]: 'Ordine casuale partendo da una lettera random per ogni ruolo',
  [AuctionType.RandomList]: 'Lista completamente randomizzata per ogni ruolo'
}
