// Types matching the C# Rank structure
// Repository Framework integration for Rank with LeagueKey

// ============================================
// RAW INTERFACES (matching JSON property names from C#)
// ============================================

/**
 * Raw RankedTeam interface matching C# JsonPropertyName attributes
 * Represents a team with ranking statistics
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
 * Raw Rank interface matching C# JsonPropertyName attributes
 * Contains daily rank data for multiple leagues
 */
export interface RankRaw {
  d: number                                    // SerieADay
  r: Record<string, RankedTeamRaw[]>          // RankedTeams (league -> teams)
}

/**
 * Raw LeagueKey interface matching C# JsonPropertyName attributes
 * Used as the repository key for Rank data
 */
export interface LeagueKeyRaw {
  g: string   // Group
  l: string   // League
  y: number   // Year
}

/**
 * Raw YearDayLeague interface matching C# JsonPropertyName attributes
 * Used as the repository key for daily rank data
 */
export interface YearDayLeagueRaw {
  y: number   // Year
  d: number   // Day
  g: string   // Group
  l: string   // League
}

// ============================================
// CLEAN INTERFACES (with readable property names)
// ============================================

/**
 * Clean RankedTeam interface with readable property names
 */
export interface RankedTeam {
  name: string
  owner: string
  point: number
  victories: number
  draws: number
  defeats: number
  goal: number
  sufferedGoal: number
  valuePoint: number
  sufferedValuePoint: number
  plusMoney: number
  money: number
  // Computed property (matching C# ValueAssets)
  valueAssets: number
}

/**
 * Clean Rank interface with readable property names
 */
export interface Rank {
  serieADay: number
  rounds: Record<string, RankedTeam[]>  // roundKey -> teams ("@" = default, others = gironi)
}

/**
 * Clean LeagueKey interface with readable property names
 * Used as the repository key for Rank data
 */
export interface LeagueKey {
  group: string
  league: string
  year: number
}

/**
 * Extended interface with additional computed properties
 */
export interface YearDayLeague extends LeagueKey {
  day: number
}

// ============================================
// MAPPING FUNCTIONS
// ============================================

/**
 * Maps raw RankedTeam data to clean interface
 */
export function mapRawRankedTeamToRankedTeam(raw: RankedTeamRaw): RankedTeam {
  return {
    name: raw.n,
    owner: raw.o,
    point: raw.p,
    victories: raw.v,
    draws: raw.d,
    defeats: raw.e,
    goal: raw.g,
    sufferedGoal: raw.s,
    valuePoint: raw.x,
    sufferedValuePoint: raw.w,
    plusMoney: raw.z,
    money: raw.m,
    // Computed property: ValueAssets = Money + PlusMoney
    valueAssets: raw.m + raw.z
  }
}

/**
 * Maps clean RankedTeam to raw interface
 */
export function mapRankedTeamToRawRankedTeam(team: RankedTeam): RankedTeamRaw {
  return {
    n: team.name,
    o: team.owner,
    p: team.point,
    v: team.victories,
    d: team.draws,
    e: team.defeats,
    g: team.goal,
    s: team.sufferedGoal,
    x: team.valuePoint,
    w: team.sufferedValuePoint,
    z: team.plusMoney,
    m: team.money
  }
}

/**
 * Maps raw Rank data to clean interface
 */
export function mapRawRankToRank(raw: RankRaw): Rank {
  const rounds: Record<string, RankedTeam[]> = {}

  // Map each round's teams ("@" = default, others = gironi specifici)
  for (const [roundKey, teams] of Object.entries(raw.r)) {
    rounds[roundKey] = teams.map(mapRawRankedTeamToRankedTeam)
  }

  return {
    serieADay: raw.d,
    rounds
  }
}

/**
 * Maps clean Rank to raw interface
 */
export function mapRankToRawRank(rank: Rank): RankRaw {
  const rounds: Record<string, RankedTeamRaw[]> = {}

  // Map each round's teams back to raw
  for (const [roundKey, teams] of Object.entries(rank.rounds)) {
    rounds[roundKey] = teams.map(mapRankedTeamToRawRankedTeam)
  }

  return {
    d: rank.serieADay,
    r: rounds
  }
}

/**
 * Maps raw LeagueKey data to clean interface
 */
export function mapRawLeagueKeyToLeagueKey(raw: LeagueKeyRaw): LeagueKey {
  return {
    group: raw.g,
    league: raw.l,
    year: raw.y
  }
}

/**
 * Maps clean LeagueKey to raw interface
 */
export function mapLeagueKeyToRawLeagueKey(key: LeagueKey): LeagueKeyRaw {
  return {
    g: key.group,
    l: key.league,
    y: key.year
  }
}

/**
 * Maps raw YearDayLeague data to clean interface
 */
export function mapRawYearDayLeagueToYearDayLeague(raw: YearDayLeagueRaw): YearDayLeague {
  return {
    group: raw.g,
    league: raw.l,
    year: raw.y,
    day: raw.d
  }
}

/**
 * Maps clean YearDayLeague to raw interface
 */
export function mapYearDayLeagueToRawYearDayLeague(key: YearDayLeague): YearDayLeagueRaw {
  return {
    g: key.group,
    l: key.league,
    y: key.year,
    d: key.day
  }
}

// ============================================
// HELPER FUNCTIONS AND UTILITIES
// ============================================

export class RankHelper {
  /**
   * Get ranked team by owner from a specific league
   */
  static getRankedTeamByOwner(rank: Rank, roundId: string, owner: string): RankedTeam | null {
    const roundTeams = rank.rounds[roundId]
    if (!roundTeams) return null

    return roundTeams.find(team => team.owner === owner) || null
  }

  /**
   * Get all rounds available in a rank
   */
  static getAvailableRounds(rank: Rank): string[] {
    return Object.keys(rank.rounds)
  }

  /**
   * Get team count for a specific round
   */
  static getTeamCount(rank: Rank, roundId: string): number {
    return rank.rounds[roundId]?.length || 0
  }

  /**
   * Get teams sorted by points for a specific round
   */
  static getTeamsSortedByPoints(rank: Rank, roundId: string): RankedTeam[] {
    const teams = rank.rounds[roundId]
    if (!teams) return []

    return [...teams].sort((a, b) => b.point - a.point)
  }

  /**
   * Get teams sorted by value assets for a specific round
   */
  static getTeamsSortedByValueAssets(rank: Rank, roundId: string): RankedTeam[] {
    const teams = rank.rounds[roundId]
    if (!teams) return []

    return [...teams].sort((a, b) => b.valueAssets - a.valueAssets)
  }

  /**
   * Get team position in ranking (1-based)
   */
  static getTeamPosition(rank: Rank, roundId: string, owner: string): number {
    const sortedTeams = RankHelper.getTeamsSortedByPoints(rank, roundId)
    const position = sortedTeams.findIndex(team => team.owner === owner)
    return position >= 0 ? position + 1 : -1 // 1-based position, -1 if not found
  }

  /**
   * Calculate goal difference for a team
   */
  static getGoalDifference(team: RankedTeam): number {
    return team.goal - team.sufferedGoal
  }

  /**
   * Get total games played by a team
   */
  static getTotalGamesPlayed(team: RankedTeam): number {
    return team.victories + team.draws + team.defeats
  }

  /**
   * Calculate points per game for a team
   */
  static getPointsPerGame(team: RankedTeam): number {
    const totalGames = RankHelper.getTotalGamesPlayed(team)
    return totalGames > 0 ? team.point / totalGames : 0
  }

  /**
   * Add two RankedTeam objects (matching C# operator +)
   */
  static addRankedTeams(a: RankedTeam, b: RankedTeam): RankedTeam {
    return {
      name: a.name,
      owner: a.owner,
      point: a.point + b.point,
      defeats: a.defeats + b.defeats,
      goal: a.goal + b.goal,
      draws: a.draws + b.draws,
      money: a.money + b.money,
      plusMoney: a.plusMoney + b.plusMoney,
      sufferedGoal: a.sufferedGoal + b.sufferedGoal,
      sufferedValuePoint: a.sufferedValuePoint + b.sufferedValuePoint,
      valuePoint: a.valuePoint + b.valuePoint,
      victories: a.victories + b.victories,
      valueAssets: (a.money + a.plusMoney) + (b.money + b.plusMoney)
    }
  }

  /**
   * Create a LeagueKey from components
   */
  static createLeagueKey(group: string, league: string, year: number): LeagueKey {
    return { group, league, year }
  }

  /**
   * Create a YearDayLeague key from components
   */
  static createYearDayLeague(group: string, league: string, year: number, day: number): YearDayLeague {
    return { group, league, year, day }
  }

  /**
   * Convert LeagueKey to string representation
   */
  static leagueKeyToString(key: LeagueKey): string {
    return `${key.group}-${key.league}-${key.year}`
  }

  /**
   * Convert YearDayLeague to string representation
   */
  static yearDayLeagueToString(key: YearDayLeague): string {
    return `${key.group}-${key.league}-${key.year}-${key.day}`
  }
}

// ============================================
// ENHANCED INTERFACES
// ============================================

/**
 * Enhanced Rank interface with computed properties
 */
export interface EnhancedRank extends Rank {
  availableRounds: string[]
  totalTeamsCount: number
  roundTeamCounts: Record<string, number>
}

/**
 * Enhanced RankedTeam interface with computed properties
 */
export interface EnhancedRankedTeam extends RankedTeam {
  goalDifference: number
  totalGamesPlayed: number
  pointsPerGame: number
  position?: number  // Will be set by ranking context
}

// ============================================
// ENHANCEMENT FUNCTIONS
// ============================================

/**
 * Enhance a Rank with computed properties
 */
export const enhanceRank = (rank: Rank): EnhancedRank => {
  const availableRounds = RankHelper.getAvailableRounds(rank)
  const roundTeamCounts: Record<string, number> = {}
  let totalTeamsCount = 0

  for (const roundId of availableRounds) {
    const count = RankHelper.getTeamCount(rank, roundId)
    roundTeamCounts[roundId] = count
    totalTeamsCount += count
  }

  return {
    ...rank,
    availableRounds,
    totalTeamsCount,
    roundTeamCounts
  }
}

/**
 * Enhance a RankedTeam with computed properties
 */
export const enhanceRankedTeam = (team: RankedTeam, position?: number): EnhancedRankedTeam => ({
  ...team,
  goalDifference: RankHelper.getGoalDifference(team),
  totalGamesPlayed: RankHelper.getTotalGamesPlayed(team),
  pointsPerGame: RankHelper.getPointsPerGame(team),
  position
})

/**
 * Enhance all teams in a rank with computed properties and positions
 */
export const enhanceRankWithTeamPositions = (rank: Rank): Record<string, EnhancedRankedTeam[]> => {
  const enhancedRounds: Record<string, EnhancedRankedTeam[]> = {}

  for (const [roundId, teams] of Object.entries(rank.rounds)) {
    const sortedTeams = RankHelper.getTeamsSortedByPoints(rank, roundId)
    enhancedRounds[roundId] = sortedTeams.map((team, index) => 
      enhanceRankedTeam(team, index + 1)
    )
  }

  return enhancedRounds
}

// ============================================
// DEFAULT VALUES AND CONSTANTS
// ============================================

/**
 * Default empty RankedTeam
 */
export const DefaultRankedTeam: RankedTeam = {
  name: '',
  owner: '',
  point: 0,
  victories: 0,
  draws: 0,
  defeats: 0,
  goal: 0,
  sufferedGoal: 0,
  valuePoint: 0,
  sufferedValuePoint: 0,
  plusMoney: 0,
  money: 0,
  valueAssets: 0
}

/**
 * Default empty Rank
 */
export const DefaultRank: Rank = {
  serieADay: 0,
  rounds: {}
}

// ============================================
// LUCK PARAMETER TYPES
// ============================================

/**
 * Types of luck events that can occur in a game
 */
export type LuckEventType =
  | 'win-vs-worst'              // Vittoria contro il peggior punteggio del turno (+5)
  | 'win-vs-second-worst'       // Vittoria contro il secondo peggior punteggio (+6)
  | 'win-narrow'                // Vittoria di misura con <6 punti di scarto (+0.5 per punto)
  | 'draw-wide-gap'             // Pareggio con ampio scarto di punti (scala 0-3)
  | 'loss-narrow'               // Sconfitta di misura (-0.5 per punto)
  | 'loss-as-best'              // Sconfitta pur essendo il miglior punteggio (-8)
  | 'loss-as-top3'              // Sconfitta pur essendo nel top 3 (-4)
  | 'win-as-worst'              // Vittoria pur essendo il peggior punteggio (+7)

/**
 * Details of a single luck event in a game
 */
export interface LuckEvent {
  type: LuckEventType
  points: number                // Punti fortuna guadagnati/persi
  gameId: string                // ID della partita
  gameDay: number               // Giornata della partita
  opponent: string              // Nome avversario
  myScore: number               // Punteggio della squadra
  opponentScore: number         // Punteggio avversario
  result: 'win' | 'draw' | 'loss' // Risultato della partita
  detail: string                // Descrizione dettagliata dell'evento
}

/**
 * Luck calculation for a single team
 */
export interface TeamLuck {
  owner: string
  name: string
  totalLuck: number             // Somma di tutti i punti fortuna
  gamesPlayed: number           // Partite giocate (escluse annullate)
  avgLuck: number               // Fortuna media per partita (totalLuck / gamesPlayed)
  events: LuckEvent[]           // Lista dettagliata di tutti gli eventi
}

/**
 * Enhanced RankedTeam with luck parameter
 */
export interface EnhancedRankedTeamWithLuck extends EnhancedRankedTeam {
  luck?: TeamLuck                // Parametro fortuna (opzionale, calcolato se disponibile calendario)
}