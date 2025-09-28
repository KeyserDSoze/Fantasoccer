import { RepositoryServices } from 'rystem.repository.client'
import { authService } from './authService'
import { tokenStorageService } from './tokenStorageService'
import {
  StatPlayersWrapperRaw,
  StatPlayersWrapper,
  mapRawStatPlayersWrapperToWrapper,
  StatPlayer,
  enhanceStatPlayersWrapper,
  EnhancedStatPlayersWrapper,
  StatPlayerHelper,
} from 'types/stat'
import { Role } from 'types/group'

export interface StatPlayersLookupOptions {
  /** Optional override for the season year (defaults to current user year) */
  year?: number
  /** Number of players to include when retrieving top lists (defaults to 5) */
  topLimit?: number
}

export class StatPlayerService {
  // ============================================
  // INTERNAL HELPERS
  // ============================================

  private getRepository() {
    return RepositoryServices.Repository<StatPlayersWrapperRaw, number>('StatPlayersWrapper')
  }

  private async getCurrentUserYear(): Promise<number | null> {
    try {
      const accessToken = await tokenStorageService.getTokenString()
      if (!accessToken) {
        console.warn('No valid access token available')
        return null
      }

      const user = await authService.getUserInfo(accessToken)
      const year = user?.state?.year

      if (!year) {
        console.warn('User year not available')
        return null
      }

      return year
    } catch (error) {
      console.error('Error retrieving user year for stat players:', error)
      return null
    }
  }

  private async resolveYear(year?: number): Promise<number | null> {
    if (year != null) {
      return year
    }

    return this.getCurrentUserYear()
  }

  private map(raw: StatPlayersWrapperRaw | null | undefined): StatPlayersWrapper | null {
    if (!raw) {
      return null
    }

    return mapRawStatPlayersWrapperToWrapper(raw)
  }

  // ============================================
  // PUBLIC API
  // ============================================

  async getWrapperByYear(year: number): Promise<StatPlayersWrapper | null> {
    try {
      const repository = this.getRepository()
      const raw = await repository.get(year)
      return this.map(raw)
    } catch (error) {
      console.error('Error fetching stat players for year:', year, error)
      return null
    }
  }

  async getEnhancedWrapperByYear(year: number, topLimit?: number): Promise<EnhancedStatPlayersWrapper | null> {
    const wrapper = await this.getWrapperByYear(year)
    return wrapper ? enhanceStatPlayersWrapper(wrapper, topLimit) : null
  }

  async getPlayersByYear(year: number): Promise<StatPlayer[]> {
    const wrapper = await this.getWrapperByYear(year)
    return wrapper?.players ?? []
  }

  async getCurrentUserWrapper(options: StatPlayersLookupOptions = {}): Promise<StatPlayersWrapper | null> {
    const year = await this.resolveYear(options.year)
    if (!year) {
      return null
    }

    return this.getWrapperByYear(year)
  }

  async getCurrentUserEnhancedWrapper(options: StatPlayersLookupOptions = {}): Promise<EnhancedStatPlayersWrapper | null> {
    const year = await this.resolveYear(options.year)
    if (!year) {
      return null
    }

    return this.getEnhancedWrapperByYear(year, options.topLimit)
  }

  async getCurrentUserPlayers(options: StatPlayersLookupOptions = {}): Promise<StatPlayer[]> {
    const wrapper = await this.getCurrentUserWrapper(options)
    return wrapper?.players ?? []
  }

  async searchPlayers(term: string, options: StatPlayersLookupOptions = {}): Promise<StatPlayer[]> {
    const players = await this.getCurrentUserPlayers(options)
    return StatPlayerHelper.search(players, term)
  }

  async getPlayersByRole(role: Role, options: StatPlayersLookupOptions = {}): Promise<StatPlayer[]> {
    const players = await this.getCurrentUserPlayers(options)
    return (StatPlayerHelper.groupByRole(players)[role] ?? []).slice()
  }

  async getTopPlayersByAverage(options: StatPlayersLookupOptions = {}): Promise<StatPlayer[]> {
    const players = await this.getCurrentUserPlayers(options)
    return StatPlayerHelper.getTopByAverage(players, options.topLimit)
  }

  async getTopPlayersByFantaAverage(options: StatPlayersLookupOptions = {}): Promise<StatPlayer[]> {
    const players = await this.getCurrentUserPlayers(options)
    return StatPlayerHelper.getTopByFantaAverage(players, options.topLimit)
  }
}

export const statPlayerService = new StatPlayerService()
