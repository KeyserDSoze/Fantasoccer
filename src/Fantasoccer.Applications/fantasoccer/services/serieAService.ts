import { authService } from './authService'
import { tokenStorageService } from './tokenStorageService'
import { getFootball } from './footballApi'
import { apiRequest } from './httpClient'
import { enhanceRealCalendar, type RealCalendar, type RealRank } from '../types/serieA'
import { getCurrentSeasonYear } from '../utils/season'

export class SerieAService {
  private async getCurrentUserYear() {
    const token = await tokenStorageService.ensureValidToken()
    if (!token) throw new Error('Accesso richiesto.')
    return (await authService.getUserInfo(token)).state.year ?? getCurrentSeasonYear()
  }
  getCalendarByYear(year: number) { return getFootball<RealCalendar>(year, 'calendar') }
  async getCurrentUserCalendar() { return this.getCalendarByYear(await this.getCurrentUserYear()) }
  async getEnhancedCurrentUserCalendar() { const value = await this.getCurrentUserCalendar(); return value ? enhanceRealCalendar(value) : null }
  async getEnhancedCalendarByYear(year: number) { const value = await this.getCalendarByYear(year); return value ? enhanceRealCalendar(value) : null }
  async isCalendarAvailable(year: number) { return !!(await this.getCalendarByYear(year))?.days.length }
  getRankByYear(year: number) { return getFootball<RealRank>(year, 'rank') }
  async getCurrentUserRank() { return this.getRankByYear(await this.getCurrentUserYear()) }
  getAvailableYears(): Promise<number[]> { return apiRequest('/api/football/years') }
  async updateGameDelayed(year: number, day: number, gameIndex: number, delayed: boolean) {
    const calendar = await this.getCalendarByYear(year)
    const game = calendar?.days.find(value => value.serieADay === day)?.games[gameIndex]
    if (!game) throw new Error('Partita non trovata.')
    await apiRequest(`/api/football/${year}/days/${day}/games/${game.ordinal ?? gameIndex}/delay`, 'PATCH', { delayed, expectedDelayed: game.delayed })
    return this.getCalendarByYear(year)
  }
}
export const serieAService = new SerieAService()
