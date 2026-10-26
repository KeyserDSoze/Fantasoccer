// RealPlayer service leveraging Repository Framework
import { getFootball } from './footballApi'
import { apiRequest } from './httpClient'
import { competitionPath } from './gameService'
import { authService } from './authService'
import { tokenStorageService } from './tokenStorageService'
import {
  RealPlayer,
  RealPlayerHelper,
  RealPlayerRoleBuckets,
  RealPlayersWrapper,
  enhanceRealPlayersWrapper,
  EnhancedRealPlayersWrapper,
} from 'types/serieA'
import { LeagueKey } from 'types/rank'
import { Role } from 'types/group'
import { getCurrentSeasonYear } from 'utils/season'

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

  private async getCurrentUserYear(): Promise<number | null> {
    try {
      const accessToken = await tokenStorageService.getTokenString()
      if (!accessToken) {
        console.warn('No valid access token available')
        return null
      }

      const user = await authService.getUserInfo(accessToken)
       return user?.state?.year ?? getCurrentSeasonYear()
    } catch (error) {
      console.error('Error retrieving user year:', error)
      return null
    }
  }

  // ============================================
  // YEAR-BASED REPOSITORY METHODS
  // ============================================

  async getPlayersWrapperByYear(year: number): Promise<RealPlayersWrapper | null> {
    return getFootball<RealPlayersWrapper>(year, 'players')
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
    return (await apiRequest<RealPlayersWrapper | undefined>(`${competitionPath(leagueKey.group, leagueKey.league, leagueKey.year)}/auction-order`)) ?? null
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
