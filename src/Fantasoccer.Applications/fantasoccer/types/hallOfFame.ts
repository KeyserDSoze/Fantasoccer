// Types matching the C# HallOfFame domain structures
import { Player, PlayerRaw, mapRawPlayerToPlayer } from './team'
import { CalendarGame, CalendarGameRaw, mapRawCalendarGameToCalendarGame } from './calendar'
import { formatSeasonFromYear } from 'utils/season'

// ============================================
// RAW INTERFACES (matching JSON property names from C#)
// ============================================

/**
 * GeneralLeagueKey - Key for league-wide data (no year)
 */
export interface GeneralLeagueKeyRaw {
  g: string  // Group
  l: string  // League
}

/**
 * RankedTeam - Team ranking data
 */
export interface RankedTeamRaw {
  n: string   // Name
  o: string   // Owner
  p: number   // Point
  v: number   // Victories
  d: number   // Draws
  e: number   // Defeats
  g: number   // Goal
  s: number   // SufferedGoal
  x: number   // ValuePoint
  w: number   // SufferedValuePoint
  z: number   // PlusMoney
  m: number   // Money
}

/**
 * WinningTeam - Team wins across years
 */
export interface WinningTeamRaw {
  o: string                          // Owner
  t: string                          // Team
  w: Record<string, number[]>        // Wins: { leagueId: [years...] }
}

/**
 * WinningPlayer - Player wins across years
 */
export interface WinningPlayerRaw {
  p: PlayerRaw                       // Player
  w: Record<string, number[]>        // Wins: { leagueId: [years...] }
}

/**
 * PlayerWithMostPointsInYear - Player record for a specific year
 */
export interface PlayerWithMostPointsInYearRaw {
  p: PlayerRaw    // Player
  y: number       // Year
  pts: number     // Points
}

/**
 * RecordPlayer - Player with highest points (overall)
 */
export interface RecordPlayerRaw {
  p: PlayerRaw    // Player
  pts: number     // Points
}

/**
 * RecordGame - Game with highest points
 */
export interface RecordGameRaw {
  g: CalendarGameRaw  // Game
  y: number           // Year
}

/**
 * HallOfFame - Complete Hall of Fame data
 */
export interface HallOfFameRaw {
  rg: RecordGameRaw                      // RecordGame
  rp: RecordPlayerRaw | null             // RecordPlayer (nullable)
  ppmy: PlayerWithMostPointsInYearRaw | null  // PlayerWithMostPointsInYear (nullable)
  wp: WinningPlayerRaw[]                 // WinningPlayers
  wt: WinningTeamRaw[]                   // WinningTeams
  rts: RankedTeamRaw[]                   // Ranks (all-time rankings)
}

// ============================================
// CLEAN INTERFACES (with readable property names)
// ============================================

export interface GeneralLeagueKey {
  groupId: string
  leagueId: string
}

export interface RankedTeam {
  name: string
  owner: string
  points: number
  victories: number
  draws: number
  defeats: number
  goals: number
  sufferedGoals: number
  valuePoints: number
  sufferedValuePoints: number
  plusMoney: number
  money: number
  // Computed property (like C#)
  valueAssets: number  // money + plusMoney
}

export interface WinningTeam {
  owner: string
  teamName: string
  wins: Record<string, number[]>  // { leagueId: [years...] }
}

export interface WinningPlayer {
  player: Player
  wins: Record<string, number[]>  // { leagueId: [years...] }
}

export interface PlayerWithMostPointsInYear {
  player: Player
  year: number
  points: number
}

export interface RecordPlayer {
  player: Player
  points: number
}

export interface RecordGame {
  game: CalendarGame
  year: number
}

export interface HallOfFame {
  recordGame: RecordGame
  recordPlayer: RecordPlayer | null
  playerWithMostPointsInYear: PlayerWithMostPointsInYear | null
  winningPlayers: WinningPlayer[]
  winningTeams: WinningTeam[]
  allTimeRankings: RankedTeam[]
}

// ============================================
// MAPPING FUNCTIONS (Raw → Clean)
// ============================================

export const mapRawGeneralLeagueKey = (raw: GeneralLeagueKeyRaw): GeneralLeagueKey => ({
  groupId: raw.g,
  leagueId: raw.l
})

export const mapRawRankedTeam = (raw: RankedTeamRaw): RankedTeam => ({
  name: raw.n,
  owner: raw.o,
  points: raw.p,
  victories: raw.v,
  draws: raw.d,
  defeats: raw.e,
  goals: raw.g,
  sufferedGoals: raw.s,
  valuePoints: raw.x,
  sufferedValuePoints: raw.w,
  plusMoney: raw.z,
  money: raw.m,
  valueAssets: raw.m + raw.z  // Computed property
})

export const mapRawWinningTeam = (raw: WinningTeamRaw): WinningTeam => ({
  owner: raw.o,
  teamName: raw.t,
  wins: raw.w
})

export const mapRawWinningPlayer = (raw: WinningPlayerRaw): WinningPlayer => ({
  player: mapRawPlayerToPlayer(raw.p),
  wins: raw.w
})

export const mapRawPlayerWithMostPointsInYear = (raw: PlayerWithMostPointsInYearRaw): PlayerWithMostPointsInYear => ({
  player: mapRawPlayerToPlayer(raw.p),
  year: raw.y,
  points: raw.pts
})

export const mapRawRecordPlayer = (raw: RecordPlayerRaw): RecordPlayer => ({
  player: mapRawPlayerToPlayer(raw.p),
  points: raw.pts
})

export const mapRawRecordGame = (raw: RecordGameRaw): RecordGame => ({
  game: mapRawCalendarGameToCalendarGame(raw.g),
  year: raw.y
})

export const mapRawHallOfFame = (raw: HallOfFameRaw): HallOfFame => ({
  recordGame: mapRawRecordGame(raw.rg),
  recordPlayer: raw.rp ? mapRawRecordPlayer(raw.rp) : null,
  playerWithMostPointsInYear: raw.ppmy ? mapRawPlayerWithMostPointsInYear(raw.ppmy) : null,
  winningPlayers: raw.wp.map(mapRawWinningPlayer),
  winningTeams: raw.wt.map(mapRawWinningTeam),
  allTimeRankings: raw.rts.map(mapRawRankedTeam)
})

// ============================================
// REVERSE MAPPING FUNCTIONS (Clean → Raw)
// ============================================

export const mapGeneralLeagueKeyToRaw = (key: GeneralLeagueKey): GeneralLeagueKeyRaw => ({
  g: key.groupId,
  l: key.leagueId
})

export const mapRankedTeamToRaw = (team: RankedTeam): RankedTeamRaw => ({
  n: team.name,
  o: team.owner,
  p: team.points,
  v: team.victories,
  d: team.draws,
  e: team.defeats,
  g: team.goals,
  s: team.sufferedGoals,
  x: team.valuePoints,
  w: team.sufferedValuePoints,
  z: team.plusMoney,
  m: team.money
})

// ============================================
// HELPER FUNCTIONS
// ============================================

export class HallOfFameHelper {
  /**
   * Get total wins for a team across all leagues
   */
  static getTotalWins(team: WinningTeam): number {
    return Object.values(team.wins).reduce((total, years) => total + years.length, 0)
  }

  /**
   * Get total wins for a player across all leagues
   */
  static getTotalPlayerWins(player: WinningPlayer): number {
    return Object.values(player.wins).reduce((total, years) => total + years.length, 0)
  }

  /**
   * Get wins for a team in a specific league
   */
  static getWinsInLeague(team: WinningTeam, leagueId: string): number[] {
    return team.wins[leagueId] ?? []
  }

  /**
   * Get wins for a player in a specific league
   */
  static getPlayerWinsInLeague(player: WinningPlayer, leagueId: string): number[] {
    return player.wins[leagueId] ?? []
  }

  /**
   * Sort ranked teams by points (descending)
   */
  static sortByPoints(teams: RankedTeam[]): RankedTeam[] {
    return [...teams].sort((a, b) => b.points - a.points)
  }

  /**
   * Sort ranked teams by value assets (descending)
   */
  static sortByValueAssets(teams: RankedTeam[]): RankedTeam[] {
    return [...teams].sort((a, b) => b.valueAssets - a.valueAssets)
  }

  /**
   * Calculate goal difference for a team
   */
  static getGoalDifference(team: RankedTeam): number {
    return team.goals - team.sufferedGoals
  }

  /**
   * Calculate value points difference for a team
   */
  static getValuePointsDifference(team: RankedTeam): number {
    return team.valuePoints - team.sufferedValuePoints
  }

  /**
   * Get total games played by a team
   */
  static getTotalGamesPlayed(team: RankedTeam): number {
    return team.victories + team.draws + team.defeats
  }

  /**
   * Calculate win percentage for a team
   */
  static getWinPercentage(team: RankedTeam): number {
    const totalGames = this.getTotalGamesPlayed(team)
    if (totalGames === 0) return 0
    return (team.victories / totalGames) * 100
  }

  /**
   * Sort winning teams by total wins (descending)
   */
  static sortWinningTeamsByWins(teams: WinningTeam[]): WinningTeam[] {
    return [...teams].sort((a, b) => this.getTotalWins(b) - this.getTotalWins(a))
  }

  /**
   * Sort winning players by total wins (descending)
   */
  static sortWinningPlayersByWins(players: WinningPlayer[]): WinningPlayer[] {
    return [...players].sort((a, b) => this.getTotalPlayerWins(b) - this.getTotalPlayerWins(a))
  }

  /**
   * Get years range from wins
   */
  static getYearsRange(wins: Record<string, number[]>): { min: number; max: number } | null {
    const allYears = Object.values(wins).flat()
    if (allYears.length === 0) return null
    return {
      min: Math.min(...allYears),
      max: Math.max(...allYears)
    }
  }

  /**
   * Format season from year number
   */
  static formatSeason(year: number): string {
    return formatSeasonFromYear(year)
  }

  /**
   * Get all years from wins (sorted)
   */
  static getAllYears(wins: Record<string, number[]>): number[] {
    const allYears = Object.values(wins).flat()
    return [...new Set(allYears)].sort((a, b) => a - b)
  }
}
