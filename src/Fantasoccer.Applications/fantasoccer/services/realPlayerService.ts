// RealPlayer service leveraging Repository Framework
import { RepositoryServices } from 'rystem.repository.client'
import { authService } from './authService'
import { tokenStorageService } from './tokenStorageService'
import {
  RealPlayer,
  RealPlayerHelper,
  RealPlayerRoleBuckets,
  RealPlayersWrapper,
  RealPlayersWrapperRaw,
  mapRawPlayersWrapperToPlayersWrapper,
  enhanceRealPlayersWrapper,
  EnhancedRealPlayersWrapper,
} from 'types/serieA'
import { LeagueKey, LeagueKeyRaw, mapLeagueKeyToRawLeagueKey } from 'types/rank'
import { Role } from 'types/group'

export interface SearchPlayersOptions {
  /** If provided the year repository will be queried instead of using the current user year */
  year?: number
  /** When true only active players are returned */
  onlyActive?: boolean
  /** Include only visible players (defaults to false) */
  onlyVisible?: boolean
}

export class RealPlayerService {
  // ============================================
  // INTERNAL HELPERS
  // ============================================

  private getYearRepository() {
    return RepositoryServices.Repository<RealPlayersWrapperRaw, number>('RealPlayers')
  }

  private getRandomRepository() {
    return RepositoryServices.Repository<RealPlayersWrapperRaw, LeagueKeyRaw>('RandomRealPlayers')
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
      console.error('Error retrieving user year:', error)
      return null
    }
  }

  private mapWrapper(raw: RealPlayersWrapperRaw | null | undefined): RealPlayersWrapper | null {
    if (!raw) {
      return null
    }

    return mapRawPlayersWrapperToPlayersWrapper(raw)
  }

  // ============================================
  // YEAR-BASED REPOSITORY METHODS
  // ============================================

  async getPlayersWrapperByYear(year: number): Promise<RealPlayersWrapper | null> {
    try {
      const repository = this.getYearRepository()
      const rawWrapper = await repository.get(year)
      return this.mapWrapper(rawWrapper)
    } catch (error) {
      console.error('Error fetching RealPlayers for year:', year, error)
      return null
    }
  }

  async getEnhancedPlayersWrapperByYear(year: number): Promise<EnhancedRealPlayersWrapper | null> {
    const wrapper = await this.getPlayersWrapperByYear(year)
    return wrapper ? enhanceRealPlayersWrapper(wrapper) : null
  }

  async getPlayersByYear(year: number): Promise<RealPlayer[]> {
    const wrapper = await this.getPlayersWrapperByYear(year)
    return wrapper?.players ?? []
  }

  async getCurrentUserPlayersWrapper(): Promise<RealPlayersWrapper | null> {
    const year = await this.getCurrentUserYear()
    if (!year) {
      return null
    }

    return this.getPlayersWrapperByYear(year)
  }

  async getCurrentUserPlayers(): Promise<RealPlayer[]> {
    const wrapper = await this.getCurrentUserPlayersWrapper()
    return wrapper?.players ?? []
  }

  async getCurrentUserEnhancedWrapper(): Promise<EnhancedRealPlayersWrapper | null> {
    const wrapper = await this.getCurrentUserPlayersWrapper()
    return wrapper ? enhanceRealPlayersWrapper(wrapper) : null
  }

  async getPlayersBucketsByYear(year: number): Promise<RealPlayerRoleBuckets> {
    const wrapper = await this.getPlayersWrapperByYear(year)
    return wrapper ? RealPlayerHelper.groupByRole(wrapper.players) : {}
  }

  // ============================================
  // RANDOMIZED (LEAGUE SCOPED) METHODS
  // ============================================

  async getRandomizedPlayersWrapper(leagueKey: LeagueKey): Promise<RealPlayersWrapper | null> {
    try {
      const repository = this.getRandomRepository()
      const rawKey = mapLeagueKeyToRawLeagueKey(leagueKey)
      const rawWrapper = await repository.get(rawKey)
      return this.mapWrapper(rawWrapper)
    } catch (error) {
      console.error('Error fetching randomized RealPlayers for league:', leagueKey, error)
      return null
    }
  }

  async getRandomizedPlayers(leagueKey: LeagueKey): Promise<RealPlayer[]> {
    const wrapper = await this.getRandomizedPlayersWrapper(leagueKey)
    return wrapper?.players ?? []
  }

  async getRandomizedEnhancedWrapper(leagueKey: LeagueKey): Promise<EnhancedRealPlayersWrapper | null> {
    const wrapper = await this.getRandomizedPlayersWrapper(leagueKey)
    return wrapper ? enhanceRealPlayersWrapper(wrapper) : null
  }

  async getRandomizedPlayersBuckets(leagueKey: LeagueKey): Promise<RealPlayerRoleBuckets> {
    const wrapper = await this.getRandomizedPlayersWrapper(leagueKey)
    return wrapper ? RealPlayerHelper.groupByRole(wrapper.players) : {}
  }

  // ============================================
  // SEARCH & FILTER UTILITIES
  // ============================================

  async searchPlayers(term: string, options: SearchPlayersOptions = {}): Promise<RealPlayer[]> {
    const { year, onlyActive = false, onlyVisible = false } = options

    const players = year
      ? await this.getPlayersByYear(year)
      : await this.getCurrentUserPlayers()

    let filteredPlayers = players
    if (onlyActive) {
      filteredPlayers = RealPlayerHelper.filterActive(filteredPlayers)
    }
    if (onlyVisible) {
      filteredPlayers = RealPlayerHelper.filterVisible(filteredPlayers)
    }

    return RealPlayerHelper.search(filteredPlayers, term)
  }

  async getPlayersByRole(role: Role, year?: number): Promise<RealPlayer[]> {
    const players = year
      ? await this.getPlayersByYear(year)
      : await this.getCurrentUserPlayers()

    return RealPlayerHelper.filterByRole(players, role)
  }

  async searchRandomizedPlayers(
    term: string,
    leagueKey: LeagueKey,
    options: { onlyActive?: boolean; onlyVisible?: boolean } = {}
  ): Promise<RealPlayer[]> {
    const { onlyActive = false, onlyVisible = false } = options
    const players = await this.getRandomizedPlayers(leagueKey)

    let filteredPlayers = players
    if (onlyActive) {
      filteredPlayers = RealPlayerHelper.filterActive(filteredPlayers)
    }
    if (onlyVisible) {
      filteredPlayers = RealPlayerHelper.filterVisible(filteredPlayers)
    }

    return RealPlayerHelper.search(filteredPlayers, term)
  }
}

export const realPlayerService = new RealPlayerService()
