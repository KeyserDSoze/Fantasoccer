import { RepositoryServices } from 'rystem.repository.client'
import { authService } from './authService'
import { tokenStorageService } from './tokenStorageService'
import {
  ChancedRealPlayer,
  ChancedRealPlayerRoleBuckets,
  ChancedRealPlayerWrapper,
  ChancedRealPlayerWrapperRaw,
  EnhancedChancedRealPlayerWrapper,
  enhanceChancedRealPlayersWrapper,
  mapRawChancedWrapperToWrapper,
  ChancedRealPlayerHelper,
} from 'types/chance'
import { YearDayKey, YearDayKeyRaw, mapYearDayKeyToRawYearDayKey } from 'types/serieA'
import { getCurrentSeasonYear } from 'utils/season'

export interface ChanceLookupOptions {
  year: number
  day: number
}

export interface ChanceSearchFilters {
  onlyAvailable?: boolean
  includeInjured?: boolean
  includeDisqualified?: boolean
  includeWarned?: boolean
  includeDoubtful?: boolean
}

export class ChanceService {
  // ============================================
  // INTERNAL HELPERS
  // ============================================

  private getRepository() {
    return RepositoryServices.Repository<ChancedRealPlayerWrapperRaw, YearDayKeyRaw>('ChancedRealPlayerWrapper')
  }

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

  async resolveYear(year?: number): Promise<number | null> {
    if (year != null) {
      return year
    }
    return this.getCurrentUserYear()
  }

  private mapWrapper(raw: ChancedRealPlayerWrapperRaw | null | undefined): ChancedRealPlayerWrapper | null {
    if (!raw) {
      return null
    }

    return mapRawChancedWrapperToWrapper(raw)
  }

  private buildKey(options: ChanceLookupOptions): YearDayKey {
    return {
      year: options.year,
      day: options.day,
    }
  }

  private validateOptions(options: ChanceLookupOptions): boolean {
    if (options.year == null || Number.isNaN(options.year)) {
      console.error('Year is required to fetch chance data')
      return false
    }
    if (options.day == null || Number.isNaN(options.day)) {
      console.error('Day is required to fetch chance data')
      return false
    }
    return true
  }

  private applyFilters(players: ChancedRealPlayer[], filters?: ChanceSearchFilters): ChancedRealPlayer[] {
    if (!filters) {
      return players
    }

    let filtered = players

    if (filters.onlyAvailable) {
      filtered = filtered.filter(player => player.chance.isAvailable)
    }

    if (filters.includeInjured === false) {
      filtered = filtered.filter(player => !player.chance.isInjured)
    }

    if (filters.includeDisqualified === false) {
      filtered = filtered.filter(player => !player.chance.isDisqualified)
    }

    if (filters.includeWarned === false) {
      filtered = filtered.filter(player => !player.chance.isWarned)
    }

    if (filters.includeDoubtful === false) {
      filtered = filtered.filter(player => !player.chance.isDoubtful)
    }

    return filtered
  }

  // ============================================
  // MAIN REPOSITORY METHODS
  // ============================================

  async getWrapper(options: ChanceLookupOptions): Promise<ChancedRealPlayerWrapper | null> {
    if (!this.validateOptions(options)) {
      return null
    }

    try {
      const repository = this.getRepository()
      const key = mapYearDayKeyToRawYearDayKey(this.buildKey(options))
      const rawWrapper = await repository.get(key)
      return this.mapWrapper(rawWrapper)
    } catch (error) {
      console.error('Error fetching chanced real players:', options, error)
      return null
    }
  }

  async getEnhancedWrapper(options: ChanceLookupOptions): Promise<EnhancedChancedRealPlayerWrapper | null> {
    const wrapper = await this.getWrapper(options)
    return wrapper ? enhanceChancedRealPlayersWrapper(wrapper) : null
  }

  async getPlayers(options: ChanceLookupOptions): Promise<ChancedRealPlayer[]> {
    const wrapper = await this.getWrapper(options)
    return wrapper?.players ?? []
  }

  async getBuckets(options: ChanceLookupOptions): Promise<ChancedRealPlayerRoleBuckets> {
    const enhanced = await this.getEnhancedWrapper(options)
    return enhanced?.playersByRole ?? {}
  }

  async getInjuredPlayers(options: ChanceLookupOptions): Promise<ChancedRealPlayer[]> {
    const players = await this.getPlayers(options)
    return ChancedRealPlayerHelper.filterInjured(players)
  }

  async getDoubtfulPlayers(options: ChanceLookupOptions): Promise<ChancedRealPlayer[]> {
    const players = await this.getPlayers(options)
    return ChancedRealPlayerHelper.filterDoubtful(players)
  }

  async getWarnedPlayers(options: ChanceLookupOptions): Promise<ChancedRealPlayer[]> {
    const players = await this.getPlayers(options)
    return ChancedRealPlayerHelper.filterWarned(players)
  }

  async getDisqualifiedPlayers(options: ChanceLookupOptions): Promise<ChancedRealPlayer[]> {
    const players = await this.getPlayers(options)
    return ChancedRealPlayerHelper.filterDisqualified(players)
  }

  async getAvailablePlayers(options: ChanceLookupOptions): Promise<ChancedRealPlayer[]> {
    const players = await this.getPlayers(options)
    return ChancedRealPlayerHelper.filterAvailable(players)
  }

  // ============================================
  // CONVENIENCE HELPERS
  // ============================================

  async getCurrentYearWrapper(day: number): Promise<ChancedRealPlayerWrapper | null> {
    const year = await this.getCurrentUserYear()
    if (!year) {
      return null
    }

    return this.getWrapper({ year, day })
  }

  async getCurrentYearEnhancedWrapper(day: number): Promise<EnhancedChancedRealPlayerWrapper | null> {
    const wrapper = await this.getCurrentYearWrapper(day)
    return wrapper ? enhanceChancedRealPlayersWrapper(wrapper) : null
  }

  async getCurrentYearPlayers(day: number): Promise<ChancedRealPlayer[]> {
    const wrapper = await this.getCurrentYearWrapper(day)
    return wrapper?.players ?? []
  }

  async searchPlayers(
    term: string,
    options: ChanceLookupOptions,
    filters?: ChanceSearchFilters
  ): Promise<ChancedRealPlayer[]> {
    const players = await this.getPlayers(options)
    const filtered = this.applyFilters(players, filters)

    if (!term) {
      return filtered
    }

    const normalizedTerm = term.toLowerCase()
    return filtered.filter(player =>
      player.name.toLowerCase().includes(normalizedTerm) ||
      player.chance.availabilityLabel.toLowerCase().includes(normalizedTerm)
    )
  }

  async searchCurrentYearPlayers(
    term: string,
    day: number,
    filters?: ChanceSearchFilters
  ): Promise<ChancedRealPlayer[]> {
    const year = await this.getCurrentUserYear()
    if (!year) {
      return []
    }

    return this.searchPlayers(term, { year, day }, filters)
  }
}

export const chanceService = new ChanceService()
