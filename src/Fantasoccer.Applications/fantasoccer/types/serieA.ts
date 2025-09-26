// Types matching the C# RealCalendar structure
import { convertUTCToItalianTimezone } from '../utils/timezoneHelpers';

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