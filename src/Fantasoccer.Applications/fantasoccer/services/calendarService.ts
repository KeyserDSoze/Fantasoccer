// Calendar service using Repository Framework
import { RepositoryServices } from 'rystem.repository.client'
import {
  Calendar,
  CalendarRaw,
  CalendarDay,
  CalendarGame,
  EnhancedCalendar,
  mapRawCalendarToCalendar,
  enhanceCalendar,
  CalendarHelper
} from 'types/calendar'
import {
  LeagueKey,
  LeagueKeyRaw,
  mapLeagueKeyToRawLeagueKey,
  RankHelper
} from 'types/rank'

export class CalendarService {

  // ============================================
  // INTERNAL HELPERS
  // ============================================

  private getRepository() {
    return RepositoryServices.Repository<CalendarRaw, LeagueKeyRaw>('Calendar')
  }

  private async fetchCalendarRaw(leagueKey: LeagueKeyRaw): Promise<CalendarRaw | null> {
    try {
      const repository = this.getRepository()
      return await repository.get(leagueKey)
    } catch (error) {
      console.error('Error fetching raw calendar:', error)
      return null
    }
  }

  // ============================================
  // MAIN CALENDAR OPERATIONS
  // ============================================

  /**
   * Retrieve the fantasy calendar for a specific league key
   */
  async getCalendar(leagueKey: LeagueKey): Promise<Calendar | null> {
    const rawKey = mapLeagueKeyToRawLeagueKey(leagueKey)
    const rawCalendar = await this.fetchCalendarRaw(rawKey)

    if (!rawCalendar) {
      return null
    }

    return mapRawCalendarToCalendar(rawCalendar)
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
