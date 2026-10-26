// Calendar service using Repository Framework
import { apiRequest } from './httpClient'
import { competitionPath } from './gameService'
import {
  Calendar,
  CalendarDay,
  CalendarGame,
  EnhancedCalendar,
  enhanceCalendar,
  CalendarHelper
} from 'types/calendar'
import {
  LeagueKey,
  RankHelper
} from 'types/rank'

export class CalendarService {

  // ============================================
  // INTERNAL HELPERS
  // ============================================

  // ============================================
  // MAIN CALENDAR OPERATIONS
  // ============================================

  /**
   * Retrieve the fantasy calendar for a specific league key
   */
  async getCalendar(leagueKey: LeagueKey): Promise<Calendar | null> {
    return (await apiRequest<Calendar | undefined>(`${competitionPath(leagueKey.group, leagueKey.league, leagueKey.year)}/calendar`)) ?? null
  }

  /**
   * Retrieve the fantasy calendar by individual key components
   */
  async getCalendarByComponents(group: string, league: string, year: number): Promise<Calendar | null> {
    const leagueKey = RankHelper.createLeagueKey(group, league, year)
    return this.getCalendar(leagueKey)
  }

  /**
   * Retrieve an enhanced fantasy calendar with computed helpers
   */
  async getEnhancedCalendar(leagueKey: LeagueKey): Promise<EnhancedCalendar | null> {
    const calendar = await this.getCalendar(leagueKey)
    return calendar ? enhanceCalendar(calendar) : null
  }

  /**
   * Retrieve enhanced calendar by individual key components
   */
  async getEnhancedCalendarByComponents(group: string, league: string, year: number): Promise<EnhancedCalendar | null> {
    const leagueKey = RankHelper.createLeagueKey(group, league, year)
    return this.getEnhancedCalendar(leagueKey)
  }

  // ============================================
  // ROUND & DAY UTILITIES
  // ============================================

  /**
   * Get all available round keys for a league
   */
  async getRoundKeys(leagueKey: LeagueKey): Promise<string[]> {
    const calendar = await this.getCalendar(leagueKey)
    return calendar ? CalendarHelper.getAllRoundKeys(calendar) : []
  }

  /**
   * Get the list of calendar days for a specific round key
   */
  async getRound(leagueKey: LeagueKey, roundKey: string): Promise<CalendarDay[]> {
    const calendar = await this.getCalendar(leagueKey)
    return calendar ? CalendarHelper.getRound(calendar, roundKey) : []
  }

  /**
   * Get a specific calendar day by its sequential number
   */
  async getDay(leagueKey: LeagueKey, dayNumber: number): Promise<CalendarDay | null> {
    const calendar = await this.getCalendar(leagueKey)
    return calendar ? CalendarHelper.getDayByNumber(calendar, dayNumber) : null
  }

  /**
   * Get all calendar days regardless of round structure
   */
  async getAllDays(leagueKey: LeagueKey): Promise<CalendarDay[]> {
    const calendar = await this.getCalendar(leagueKey)
    return calendar ? CalendarHelper.getAllDays(calendar) : []
  }

  // ============================================
  // GAME UTILITIES
  // ============================================

  /**
   * Get all games scheduled for a league
   */
  async getAllGames(leagueKey: LeagueKey): Promise<CalendarGame[]> {
    const calendar = await this.getCalendar(leagueKey)
    return calendar ? CalendarHelper.getAllGames(calendar) : []
  }

  /**
   * Get all games for a specific team (home or away)
   */
  async getGamesForTeam(leagueKey: LeagueKey, teamName: string): Promise<CalendarGame[]> {
    const calendar = await this.getCalendar(leagueKey)
    return calendar ? CalendarHelper.getGamesForTeam(calendar, teamName) : []
  }

  /**
   * Get all games that do not yet have a recorded result
   */
  async getPendingGames(leagueKey: LeagueKey): Promise<CalendarGame[]> {
    const calendar = await this.getCalendar(leagueKey)
    return calendar ? CalendarHelper.getPendingGames(calendar) : []
  }
}

// Export singleton instance
export const calendarService = new CalendarService()
