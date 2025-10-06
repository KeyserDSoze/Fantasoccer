// Auction types for real-time auction management with raw interfaces
// Follows the pattern from rank.ts with JsonPropertyName mapping
import type { Role } from './group'
import type { AnnualTeamRaw, AnnualTeam } from './group'
import { mapRawAnnualTeamToAnnualTeam } from './group'
import type { LeagueKeyRaw, LeagueKey } from './rank'
import { mapRawLeagueKeyToLeagueKey } from './rank'
import type { StatPlayerRaw, StatPlayer } from './stat'
import { mapRawStatPlayerToStatPlayer } from './stat'

// Re-export for convenience
export type { StatPlayerRaw, StatPlayer }

// ============================================
// ENUMS
// ============================================

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

// ============================================
// RAW INTERFACES (matching C# JsonPropertyName)
// ============================================

/**
 * Raw AuctionWin interface matching C# JsonPropertyName
 */
export interface AuctionWinRaw {
  k: string         // PlayerKey
  o: string | null  // Owner
  p: number         // Price
}

/**
 * Raw ActualAuction interface matching C# JsonPropertyName
 */
export interface ActualAuctionRaw {
  pl: StatPlayerRaw  // Player (uses StatPlayerRaw from stat.ts)
  s: string          // Started (DateTime as ISO string)
  o: string | null   // Owner
  p: number          // Price
}

/**
 * Raw AuctionWrapper interface matching C# JsonPropertyName
 */
export interface AuctionWrapperRaw {
  i: string                              // Id
  l: LeagueKeyRaw                        // LeagueKey
  c: string                              // Creator
  t: string                              // Created (DateTime as ISO string)
  tp: number                             // Type (AuctionType enum)
  a: ActualAuctionRaw | null            // Actual
  w: AuctionWinRaw[]                    // Winnings
  s: number                              // Status (AuctionStatus enum)
  p: Record<string, string[]>           // ListOfPlayers (Role as string key)
  pr: AnnualTeamRaw[]                   // Participants
  ls: Record<string, string | null>     // LastShow (Role as string key)
}

/**
 * Raw Team interface (simplified, only what we need for auction display)
 * Note: Different from team.ts TeamRaw which has full player roster
 */
export interface AuctionTeamRaw {
  n: string      // Name
  o: string      // Owner
  c: number      // Cost
  ap: PlayerRaw[] // ActivePlayers
}

/**
 * Raw Player interface (in team)
 * Note: Simpler than team.ts PlayerRaw (no status, position, etc.)
 */
export interface PlayerRaw {
  n: string    // Name
  r: number    // Role
  t: string    // RealTeam
  p: number    // Price
}

/**
 * Raw AuctionHubWrapper interface (what comes in AuctionState event)
 * Matching C# class with JsonPropertyName
 */
export interface AuctionHubWrapperRaw {
  w: AuctionWrapperRaw                          // Wrapper
  u: string                                      // LastUpdate (DateTime)
  g: any | null                                  // Group (not used in frontend)
  l: any | null                                  // League (not used in frontend)
  ln: string | null                              // LeagueName
  t: Record<string, AuctionTeamRaw>             // Teams (TeamKey serialized as string -> Team)
  r: StatPlayerRaw[] | null                     // RealPlayers
}

// ============================================
// CLEAN INTERFACES
// ============================================

/**
 * Clean AuctionWin interface
 */
export interface AuctionWin {
  playerKey: string
  owner: string | null
  price: number
}

/**
 * Clean ActualAuction interface
 */
export interface ActualAuction {
  player: StatPlayer
  started: Date
  owner: string | null
  price: number
}

/**
 * Clean AuctionWrapper interface
 */
export interface AuctionWrapper {
  id: string
  leagueKey: LeagueKey
  creator: string
  created: Date
  type: AuctionType
  actual: ActualAuction | null
  winnings: AuctionWin[]
  status: AuctionStatus
  listOfPlayers: Record<Role, string[]>
  participants: AnnualTeam[]
  lastShow: Record<Role, string | null>
}

/**
 * Clean Team interface for auction display
 */
export interface AuctionTeam {
  name: string
  owner: string
  cost: number
  playersCount: number
  remainingBudget: number
  remainingSlots: number
  players: AuctionPlayer[]
}

/**
 * Clean Player interface (in team roster)
 */
export interface AuctionPlayer {
  name: string
  role: Role
  realTeam: string
  price: number
}

/**
 * Clean AuctionHubWrapper interface (AuctionState)
 */
export interface AuctionState {
  wrapper: AuctionWrapper
  lastUpdate: Date
  leagueName: string | null
  teams: AuctionTeam[]
  realPlayers: StatPlayer[]
  // Computed on frontend
  isAuctioneer: boolean
  userEmail: string | null
}

/**
 * PlayerWithStats interface (for CurrentPlayer event)
 * Note: This seems to not exist in backend, using StatPlayer instead
 */
export interface PlayerWithStats {
  name: string
  role: string
  realTeam: string
  stats?: {
    goal: number
    assist: number
    yellowCard: number
    redCard: number
    average: number
    fantaAverage: number
  }
}

// ============================================
// VOLATILE INTERFACES (custom events)
// ============================================

/**
 * PlayerAssigned event data (volatile, not stored)
 */
export interface PlayerAssignedData {
  Owner: string
  TeamName: string | null
  Price: number
  PlayerName: string
  TeamCost: number | null
  TeamPlayersCount: number | null
}

// ============================================
// MAPPING FUNCTIONS
// ============================================

/**
 * Map raw AuctionWin to clean interface
 */
export function mapRawAuctionWin(raw: AuctionWinRaw): AuctionWin {
  return {
    playerKey: raw.k,
    owner: raw.o,
    price: raw.p
  }
}

/**
 * Map raw ActualAuction to clean interface
 */
export function mapRawActualAuction(raw: ActualAuctionRaw): ActualAuction {
  return {
    player: mapRawStatPlayerToStatPlayer(raw.pl),
    started: new Date(raw.s),
    owner: raw.o,
    price: raw.p
  }
}

/**
 * Map raw AuctionWrapper to clean interface
 */
export function mapRawAuctionWrapper(raw: AuctionWrapperRaw): AuctionWrapper {
  // Convert string keys back to Role enum for listOfPlayers and lastShow
  const listOfPlayers: Record<Role, string[]> = {} as Record<Role, string[]>
  for (const [key, value] of Object.entries(raw.p)) {
    listOfPlayers[parseInt(key) as Role] = value
  }

  const lastShow: Record<Role, string | null> = {} as Record<Role, string | null>
  for (const [key, value] of Object.entries(raw.ls)) {
    lastShow[parseInt(key) as Role] = value
  }

  return {
    id: raw.i,
    leagueKey: mapRawLeagueKeyToLeagueKey(raw.l),
    creator: raw.c,
    created: new Date(raw.t),
    type: raw.tp as AuctionType,
    actual: raw.a ? mapRawActualAuction(raw.a) : null,
    winnings: raw.w.map(mapRawAuctionWin),
    status: raw.s as AuctionStatus,
    listOfPlayers,
    participants: raw.pr.map(mapRawAnnualTeamToAnnualTeam),
    lastShow
  }
}

/**
 * Map raw Player to clean interface
 */
export function mapRawPlayer(raw: PlayerRaw): AuctionPlayer {
  return {
    name: raw.n,
    role: raw.r as Role,
    realTeam: raw.t,
    price: raw.p
  }
}

/**
 * Map raw Team to clean interface
 * Computes playersCount, remainingBudget, remainingSlots from backend Team structure
 */
export function mapRawTeam(raw: AuctionTeamRaw): AuctionTeam {
  // Backend Team has ActivePlayers array
  const players: AuctionPlayer[] = (raw.ap || []).map((p: PlayerRaw) => mapRawPlayer(p))
  const playersCount = players.length
  const cost = raw.c || 0
  const remainingBudget = 1000 - cost - (25 - playersCount) // Must keep 1€ per remaining slot
  const remainingSlots = 25 - playersCount

  return {
    name: raw.n,
    owner: raw.o,
    cost,
    playersCount,
    remainingBudget,
    remainingSlots,
    players
  }
}

/**
 * Map raw AuctionHubWrapper to clean AuctionState
 */
export function mapRawAuctionHubWrapper(
  raw: AuctionHubWrapperRaw,
  userEmail: string | null,
  isAuctioneer: boolean
): AuctionState {
  const teams: AuctionTeam[] = Object.values(raw.t).map(mapRawTeam)
  const realPlayers: StatPlayer[] = (raw.r || []).map(mapRawStatPlayerToStatPlayer)

  return {
    wrapper: mapRawAuctionWrapper(raw.w),
    lastUpdate: new Date(raw.u),
    leagueName: raw.ln,
    teams,
    realPlayers,
    isAuctioneer,
    userEmail
  }
}

/**
 * Convert StatPlayer to PlayerWithStats (for compatibility)
 */
export function statPlayerToPlayerWithStats(player: StatPlayer): PlayerWithStats {
  return {
    name: player.name,
    role: player.role.toString(),
    realTeam: player.realTeam.name  // StatPlayer extends RealPlayer which has realTeam: RealTeam
    // stats are not available in StatPlayer, would need separate API call
  }
}

// ============================================
// ============================================
// LABELS AND DESCRIPTIONS
// ============================================

/**
 * Auction type labels
 */
export const AuctionTypeLabels: Record<AuctionType, string> = {
  [AuctionType.Normal]: 'Normale (Alfabetico)',
  [AuctionType.RandomByLetter]: 'Random per Lettera',
  [AuctionType.RandomList]: 'Random Casuale'
}

/**
 * Auction type descriptions
 */
export const AuctionTypeDescriptions: Record<AuctionType, string> = {
  [AuctionType.Normal]: 'Giocatori in ordine alfabetico per ruolo',
  [AuctionType.RandomByLetter]: 'Ordine casuale partendo da una lettera random per ogni ruolo',
  [AuctionType.RandomList]: 'Lista completamente randomizzata per ogni ruolo'
}
