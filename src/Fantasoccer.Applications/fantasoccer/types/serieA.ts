// Types matching the C# RealCalendar structure
import { convertUTCToItalianTimezone } from '../utils/timezoneHelpers';
import { Role, RoleExtensions } from './group';

// ============================================
// RAW INTERFACES (matching JSON property names from C#)
// ============================================

export interface RealTeamRaw {
  n: string           // Name (matches [JsonPropertyName("n")])
  a: string           // Abbreviation (matches [JsonPropertyName("a")])
}

export interface RealGameRaw {
  h: RealTeamRaw      // Home team (matches [JsonPropertyName("h")])
  a: RealTeamRaw      // Away team (matches [JsonPropertyName("a")])
  d: string | null    // Date (matches [JsonPropertyName("d")]) - ISO string from backend
  g: number | null    // Home goals (matches [JsonPropertyName("g")])
  y: number | null    // Away goals (matches [JsonPropertyName("y")])
  e: boolean          // Delayed (matches [JsonPropertyName("e")])
}

export interface RealDayRaw {
  y: number           // Year (matches [JsonPropertyName("y")])
  a: number           // Serie A Day number (matches [JsonPropertyName("a")])
  g: RealGameRaw[]    // Games (matches [JsonPropertyName("g")])
}

export interface RealCalendarRaw {
  y: number           // Year (matches [JsonPropertyName("y")])
  d: RealDayRaw[]     // Days (matches [JsonPropertyName("d")])
}

export interface RealRankedTeamRaw {
  n: string          // Team name (matches [JsonPropertyName("n")])
  p: number          // Points (matches [JsonPropertyName("p")])
  v: number          // Victories (matches [JsonPropertyName("v")])
  d: number          // Draws (matches [JsonPropertyName("d")])
  e: number          // Defeats (matches [JsonPropertyName("e")])
  g: number          // Goals scored (matches [JsonPropertyName("g")])
  s: number          // Goals conceded (matches [JsonPropertyName("s")])
  k: number          // Matches played (matches [JsonPropertyName("k")])
}

export interface RealRankRaw {
  r: RealRankedTeamRaw[] // Ranked teams (matches [JsonPropertyName("r")])
}

export interface RealPlayerRaw {
  n: string          // Player name (matches [JsonPropertyName("n")])
  t: RealTeamRaw     // Real team (matches [JsonPropertyName("t")])
  r: Role            // Role enum value (matches [JsonPropertyName("r")])
  a: boolean         // Active flag (matches [JsonPropertyName("a")])
  vh: boolean        // Visible flag (matches [JsonPropertyName("vh")])
}

export interface RealPlayersWrapperRaw {
  p: RealPlayerRaw[] // Players collection (matches [JsonPropertyName("p")])
}

// ============================================
// CLEAN INTERFACES (with readable property names)
// ============================================

export interface RealTeam {
  name: string
  abbreviation: string
}

export interface RealGame {
  home: RealTeam
  away: RealTeam
  date: string | null
  homeGoals: number | null
  awayGoals: number | null
  delayed: boolean
}

export interface RealDay {
  year: number
  serieADay: number
  games: RealGame[]
}

export interface RealCalendar {
  year: number
  days: RealDay[]
}

export interface RealRankedTeam {
  name: string
  points: number
  victories: number
  draws: number
  defeats: number
  goalsFor: number
  goalsAgainst: number
  goalDifference: number
  played: number
}

export interface RealRank {
  teams: RealRankedTeam[]
}

export interface RealPlayer {
  name: string
  key: string
  realTeam: RealTeam
  role: Role
  roleLabel: string
  isActive: boolean
  isVisible: boolean
}

export interface RealPlayersWrapper {
  players: RealPlayer[]
}

export type RealPlayerRoleBuckets = Partial<Record<Role, RealPlayer[]>>

export interface EnhancedRealPlayersWrapper extends RealPlayersWrapper {
  activePlayers: RealPlayer[]
  inactivePlayers: RealPlayer[]
  playersByRole: RealPlayerRoleBuckets
  hasVisiblePlayers: boolean
}



// ============================================
// MAPPING FUNCTIONS
// ============================================

export function mapRawTeamToTeam(raw: RealTeamRaw): RealTeam {
  return {
    name: raw.n,
    abbreviation: raw.a
  }
}

export function mapRawGameToGame(raw: RealGameRaw): RealGame {
  return {
    home: mapRawTeamToTeam(raw.h),
    away: mapRawTeamToTeam(raw.a),
    date: convertUTCToItalianTimezone(raw.d),
    homeGoals: raw.g,
    awayGoals: raw.y,
    delayed: raw.e
  }
}

export function mapRawDayToDay(raw: RealDayRaw): RealDay {
  return {
    year: raw.y,
    serieADay: raw.a,
    games: raw.g.map(mapRawGameToGame)
  }
}

export function mapRawCalendarToCalendar(raw: RealCalendarRaw): RealCalendar {
  return {
    year: raw.y,
    days: raw.d.map(mapRawDayToDay)
  }
}

export function mapRawRankedTeamToRankedTeam(raw: RealRankedTeamRaw): RealRankedTeam {
  const goalsFor = raw.g ?? 0
  const goalsAgainst = raw.s ?? 0

  return {
    name: raw.n,
    points: raw.p ?? 0,
    victories: raw.v ?? 0,
    draws: raw.d ?? 0,
    defeats: raw.e ?? 0,
    goalsFor,
    goalsAgainst,
    goalDifference: goalsFor - goalsAgainst,
    played: raw.k ?? 0
  }
}

export function mapRawRankToRank(raw: RealRankRaw): RealRank {
  return {
    teams: raw?.r?.map(mapRawRankedTeamToRankedTeam) ?? []
  }
}

export function mapRawRealPlayerToRealPlayer(raw: RealPlayerRaw): RealPlayer {
  const role = raw?.r ?? Role.Undefined

  return {
    name: raw.n,
    key: RealPlayerHelper.buildKey(raw.n),
    realTeam: mapRawTeamToTeam(raw.t),
    role,
    roleLabel: RoleExtensions.asString(role),
    isActive: raw?.a ?? false,
    isVisible: raw?.vh ?? true
  }
}

export function mapRawPlayersWrapperToPlayersWrapper(raw: RealPlayersWrapperRaw | null | undefined): RealPlayersWrapper {
  const players = raw?.p?.map(mapRawRealPlayerToRealPlayer) ?? []

  return {
    players: RealPlayerHelper.sortByRoleAndName(players)
  }
}

// ============================================
// REAL PLAYER HELPERS AND ENHANCERS
// ============================================

const REAL_PLAYER_ROLE_SORT_ORDER: Role[] = [
  Role.GoalKeeper,
  Role.Defensor,
  Role.Midfielder,
  Role.Forward,
  Role.Undefined
]

export class RealPlayerHelper {
  static buildKey(name: string): string {
    if (!name) {
      return ''
    }

    return name
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '')
      .toLowerCase()
      .replace(/[^a-z]/g, '')
  }

  static sortByRoleAndName(players: RealPlayer[]): RealPlayer[] {
    return [...players].sort((a, b) => {
      const roleComparison = REAL_PLAYER_ROLE_SORT_ORDER.indexOf(a.role) - REAL_PLAYER_ROLE_SORT_ORDER.indexOf(b.role)

      if (roleComparison !== 0) {
        return roleComparison
      }

      return a.name.localeCompare(b.name, 'it-IT', { sensitivity: 'base' })
    })
  }

  static groupByRole(players: RealPlayer[]): RealPlayerRoleBuckets {
    return players.reduce<RealPlayerRoleBuckets>((acc, player) => {
      const role = player.role ?? Role.Undefined
      acc[role] = acc[role] ? [...acc[role]!, player] : [player]
      return acc
    }, {})
  }

  static filterByRole(players: RealPlayer[], role: Role): RealPlayer[] {
    return players.filter(player => player.role === role)
  }

  static filterActive(players: RealPlayer[]): RealPlayer[] {
    return players.filter(player => player.isActive)
  }

  static filterVisible(players: RealPlayer[]): RealPlayer[] {
    return players.filter(player => player.isVisible)
  }

  static search(players: RealPlayer[], term: string): RealPlayer[] {
    if (!term) {
      return players
    }

    const normalizedTerm = RealPlayerHelper.buildKey(term)

    return players.filter(player =>
      player.name.toLowerCase().includes(term.toLowerCase()) ||
      player.key.includes(normalizedTerm)
    )
  }
}

export const enhanceRealPlayersWrapper = (wrapper: RealPlayersWrapper): EnhancedRealPlayersWrapper => {
  const activePlayers = RealPlayerHelper.filterActive(wrapper.players)
  const inactivePlayers = wrapper.players.filter(player => !player.isActive)
  const groupedPlayers = RealPlayerHelper.groupByRole(wrapper.players)

  const sortedGroupedPlayers = Object.entries(groupedPlayers).reduce<RealPlayerRoleBuckets>((acc, [roleKey, players]) => {
    const role = Number(roleKey) as Role
    acc[role] = RealPlayerHelper.sortByRoleAndName(players)
    return acc
  }, {})

  return {
    ...wrapper,
    activePlayers: RealPlayerHelper.sortByRoleAndName(activePlayers),
    inactivePlayers: RealPlayerHelper.sortByRoleAndName(inactivePlayers),
    playersByRole: sortedGroupedPlayers,
    hasVisiblePlayers: activePlayers.some(player => player.isVisible)
  }
}

// Helper functions to match C# computed properties
export class RealCalendarHelper {
  private static readonly TWO_HOURS_MS = (2 * 60 + 15) * 60 * 1000 // 2h 15min
  private static readonly EIGHT_HOURS_MS = (8 * 60) * 60 * 1000 + RealCalendarHelper.TWO_HOURS_MS // 8h + 2h 15min

  static getCurrentItalyTime(): Date {
    // Simple implementation - in production you might want a more robust timezone handling
    return new Date()
  }

  static isGamePlayed(game: RealGame): boolean {
    if (!game.date || game.delayed) return false
    const gameDate = new Date(game.date)
    return RealCalendarHelper.getCurrentItalyTime() > gameDate
  }

  static getLiveDay(calendar: RealCalendar): RealDay | null {
    const now = RealCalendarHelper.getCurrentItalyTime()
    
    return calendar.days.find(day => {
      const activeGames = day.games.filter(x => !x.delayed)
      if (activeGames.length === 0) return false
      
      const firstGame = activeGames.sort((a, b) => 
        new Date(a.date || 0).getTime() - new Date(b.date || 0).getTime()
      )[0]
      
      const lastGame = activeGames.sort((a, b) => 
        new Date(b.date || 0).getTime() - new Date(a.date || 0).getTime()
      )[0]
      
      if (!firstGame.date || !lastGame.date) return false
      
      const firstGameDate = new Date(firstGame.date)
      const lastGameDate = new Date(lastGame.date)
      const lastGameEndTime = new Date(lastGameDate.getTime() + RealCalendarHelper.EIGHT_HOURS_MS)
      
      return now >= firstGameDate && now <= lastGameEndTime
    }) || null
  }

  static getLastDay(calendar: RealCalendar): RealDay | null {
    const now = RealCalendarHelper.getCurrentItalyTime()
    
    const sortedDays = calendar.days
      .sort((a, b) => b.serieADay - a.serieADay) // Sort by Serie A day descending
      
    return sortedDays.find(day => {
      const activeGames = day.games.filter(x => !x.delayed)
      if (activeGames.length === 0) return false
      
      const lastGame = activeGames.sort((a, b) => 
        new Date(b.date || 0).getTime() - new Date(a.date || 0).getTime()
      )[0]
      
      if (!lastGame.date) return false
      
      const lastGameDate = new Date(lastGame.date)
      const lastGameEndTime = new Date(lastGameDate.getTime() + RealCalendarHelper.TWO_HOURS_MS)
      
      return now >= lastGameEndTime
    }) || null
  }

  static getNextDay(calendar: RealCalendar): RealDay | null {
    const liveDay = RealCalendarHelper.getLiveDay(calendar)
    const lastDay = RealCalendarHelper.getLastDay(calendar)
    
    const currentDay = (liveDay?.serieADay ?? lastDay?.serieADay ?? 0) + 1
    
    return calendar.days.find(x => x.serieADay === currentDay) || null
  }

  static getLiveGames(calendar: RealCalendar): RealGame[] {
    const liveDay = RealCalendarHelper.getLiveDay(calendar)
    if (!liveDay) return []
    
    const now = RealCalendarHelper.getCurrentItalyTime()
    
    return liveDay.games.filter(game => {
      if (game.delayed || !game.date) return false
      
      const gameDate = new Date(game.date)
      const gameEndTime = new Date(gameDate.getTime() + RealCalendarHelper.TWO_HOURS_MS)
      
      return now >= gameDate && now <= gameEndTime
    })
  }

  static isLive(calendar: RealCalendar): boolean {
    return RealCalendarHelper.getLiveGames(calendar).length > 0
  }

  static getLiveSerieADay(calendar: RealCalendar): number {
    return RealCalendarHelper.getLiveDay(calendar)?.serieADay ?? 0
  }

  static isDuringSerieADay(calendar: RealCalendar): boolean {
    return RealCalendarHelper.getLiveSerieADay(calendar) > 0
  }

  /**
   * Utility method per debug - mostra informazioni sulle date del calendario
   */
  static debugCalendarDates(calendar: RealCalendar): void {
    console.log('🗓️ Debug Calendario Serie A - Date convertite:')
    console.log('=' .repeat(50))
    
    calendar.days.forEach(day => {
      console.log(`📅 Giornata ${day.serieADay} (Anno ${day.year}):`)
      
      day.games.forEach((game, index) => {
        if (game.date) {
          const gameDate = new Date(game.date)
          console.log(`  ${index + 1}. ${game.home.name} vs ${game.away.name}`)
          console.log(`     📍 Data originale: ${game.date}`)
          console.log(`     🇮🇹 Data Italia: ${gameDate.toLocaleString('it-IT', { 
            timeZone: 'Europe/Rome',
            weekday: 'short',
            year: 'numeric',
            month: '2-digit', 
            day: '2-digit',
            hour: '2-digit',
            minute: '2-digit'
          })}`)
          console.log(`     ⏰ Timezone: ${gameDate.toString().match(/GMT[+-]\d{4}/)?.[0] || 'Unknown'}`)
        }
      })
      console.log('---')
    })
  }
}

// Enhanced RealCalendar interface with computed properties
export interface EnhancedRealCalendar extends RealCalendar {
  liveDay?: RealDay | null
  lastDay?: RealDay | null
  nextDay?: RealDay | null
  liveGames?: RealGame[]
  isLive?: boolean
  liveSerieADay?: number
  isDuringSerieADay?: boolean
}

// Function to enhance a RealCalendar with computed properties
export const enhanceRealCalendar = (calendar: RealCalendar): EnhancedRealCalendar => {
  return {
    ...calendar,
    liveDay: RealCalendarHelper.getLiveDay(calendar),
    lastDay: RealCalendarHelper.getLastDay(calendar),
    nextDay: RealCalendarHelper.getNextDay(calendar),
    liveGames: RealCalendarHelper.getLiveGames(calendar),
    isLive: RealCalendarHelper.isLive(calendar),
    liveSerieADay: RealCalendarHelper.getLiveSerieADay(calendar),
    isDuringSerieADay: RealCalendarHelper.isDuringSerieADay(calendar),
  }
}

// ============================================
// TIMEZONE UTILITIES (EXPORTED)
// ============================================

/**
 * Utility function per convertire manualmente una data UTC+0 a timezone italiano
 * Utile per test e debug
 */
export const convertUTCToItalianTimeUtil = convertUTCToItalianTimezone;