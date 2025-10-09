// Auction types for real-time auction management with raw interfaces
// Follows the pattern from rank.ts with JsonPropertyName mapping
import { Entity } from 'rystem.repository.client'
import type { Role } from './group'
import type { AnnualTeamRaw, AnnualTeam, AnnualLeagueRaw, AnnualLeague } from './group'
import { mapRawAnnualTeamToAnnualTeam, mapRawAnnualLeagueToAnnualLeague } from './group'
import type { LeagueKeyRaw, LeagueKey } from './rank'
import { mapRawLeagueKeyToLeagueKey } from './rank'
import type { StatPlayerRaw, StatPlayer } from './stat'
import { mapRawStatPlayerToStatPlayer } from './stat'
import { mapRawTeamKeyToTeamKey, mapRawTeamToTeam, Team, TeamKey, type TeamKeyRaw, type TeamRaw } from './team'
import { EntityRaw, mapRawEntityToEntity } from './default'

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

export enum AuctionKind {
  Starting = 0,
  Repairing = 1
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
  sb: string | null  // Substituted (new owner if player was substituted)
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
  knd: number                            // Kind (AuctionKind enum)
  a: ActualAuctionRaw | null            // Actual
  w: AuctionWinRaw[]                    // Winnings
  s: number                              // Status (AuctionStatus enum)
  p: Record<string, string[]>           // ListOfPlayers (Role as string key)
  pr: AnnualTeamRaw[]                   // Participants
  ls: Record<string, string | null>     // LastShow (Role as string key)
  sec: number                            // SecondsPerAuction
}



/**
 * Raw AuctionHubWrapper interface (what comes in AuctionState event)
 * Matching C# class with JsonPropertyName
 * NOTE: Teams changed from Dictionary to List<Entity<Team, TeamKey>>
 */
export interface AuctionHubWrapperRaw {
  w: AuctionWrapperRaw                          // Wrapper
  u: string                                      // LastUpdate (DateTime)
  g: any | null                                  // Group (not used in frontend)
  l: AnnualLeagueRaw | null                      // League
  ln: string | null                              // LeagueName
  t: EntityRaw<TeamRaw, TeamKeyRaw>[]    // Teams (List<Entity<Team, TeamKey>>)
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
  substituted: string | null
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
  king: AuctionKind
  actual: ActualAuction | null
  winnings: AuctionWin[]
  status: AuctionStatus
  listOfPlayers: Record<Role, string[]>
  participants: AnnualTeam[]
  lastShow: Record<Role, string | null>
  secondsPerAuction: number
}


/**
 * Clean AuctionHubWrapper interface (AuctionState)
 */
export interface AuctionState {
  wrapper: AuctionWrapper
  lastUpdate: Date
  league: AnnualLeague | null
  leagueName: string | null
  teams: Entity<Team, TeamKey>[]
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
    price: raw.p,
    substituted: raw.sb
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
    king: raw.knd as AuctionKind,
    actual: raw.a ? mapRawActualAuction(raw.a) : null,
    winnings: raw.w.map(mapRawAuctionWin),
    status: raw.s as AuctionStatus,
    listOfPlayers,
    participants: raw.pr.map(mapRawAnnualTeamToAnnualTeam),
    lastShow,
    secondsPerAuction: raw.sec ?? 10
  }
}


/**
 * Map raw AuctionHubWrapper to clean AuctionState
 * This is the main mapper to be used in the service layer
 */
export function mapRawAuctionHubWrapperToAuctionState(
  raw: AuctionHubWrapperRaw,
  userEmail: string | null
): AuctionState {
  // Map teams from List<Entity<Team, TeamKey>> to AuctionTeam[]
  // Filter out entities without value (HasValue = false)
  const teams: Entity<Team, TeamKey>[] = (raw.t || [])
    .filter(entity => entity.v !== null && entity.v !== undefined)
    .map(entity => mapRawEntityToEntity(entity, mapRawTeamToTeam, mapRawTeamKeyToTeamKey))
  
  const realPlayers: StatPlayer[] = (raw.r || []).map(mapRawStatPlayerToStatPlayer)
  
  // Compute isAuctioneer by comparing creator email with current user email (case-insensitive)
  const isAuctioneer = userEmail 
    ? raw.w.c.toLowerCase() === userEmail.toLowerCase() 
    : false

  return {
    wrapper: mapRawAuctionWrapper(raw.w),
    lastUpdate: new Date(raw.u),
    league: raw.l ? mapRawAnnualLeagueToAnnualLeague(raw.l) : null,
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

/**
 * Auction kind labels
 */
export const AuctionKindLabels: Record<AuctionKind, string> = {
  [AuctionKind.Starting]: 'Asta Iniziale',
  [AuctionKind.Repairing]: 'Asta di Riparazione'
}

/**
 * Auction kind descriptions
 */
export const AuctionKindDescriptions: Record<AuctionKind, string> = {
  [AuctionKind.Starting]: 'Asta iniziale per formare la rosa completa',
  [AuctionKind.Repairing]: 'Asta di riparazione per sostituire giocatori infortunati o venduti'
}
