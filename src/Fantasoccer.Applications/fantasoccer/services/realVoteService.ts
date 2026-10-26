import { getFootball } from './footballApi'
import { authService } from './authService'
import { tokenStorageService } from './tokenStorageService'
import { getCurrentSeasonYear } from 'utils/season'
import {
  EnhancedVotedRealPlayerWrapper,
  VotedRealPlayer,
  VotedRealPlayerRoleBuckets,
  VotedRealPlayerWrapper,
  enhanceVotedRealPlayersWrapper,
} from 'types/serieA'

export interface VoteLookupOptions {
  year: number
  day: number
}

export class RealVoteService {
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

  private validateOptions(options: VoteLookupOptions): boolean {
    if (options.year == null || Number.isNaN(options.year)) {
      console.error('Year is required to fetch votes')
      return false
    }
    if (options.day == null || Number.isNaN(options.day)) {
      console.error('Day is required to fetch votes')
      return false
    }
    return true
  }

  // ============================================
  // LIVE VOTES
  // ============================================

  async getLiveWrapper(options: VoteLookupOptions): Promise<VotedRealPlayerWrapper | null> {
    if (!this.validateOptions(options)) {
      return null
    }

    return getFootball<VotedRealPlayerWrapper>(options.year, `days/${options.day}/votes/live`)
  }

  async getEnhancedLiveWrapper(options: VoteLookupOptions): Promise<EnhancedVotedRealPlayerWrapper | null> {
    const wrapper = await this.getLiveWrapper(options)
    return wrapper ? enhanceVotedRealPlayersWrapper(wrapper) : null
  }

  async getLivePlayers(options: VoteLookupOptions): Promise<VotedRealPlayer[]> {
    const wrapper = await this.getLiveWrapper(options)
    return wrapper?.players ?? []
  }

  async getLiveBuckets(options: VoteLookupOptions): Promise<VotedRealPlayerRoleBuckets> {
    const enhanced = await this.getEnhancedLiveWrapper(options)
    return enhanced?.playersByRole ?? {}
  }

  // ============================================
  // OFFICIAL VOTES
  // ============================================

  async getOfficialWrapper(options: VoteLookupOptions): Promise<VotedRealPlayerWrapper | null> {
    if (!this.validateOptions(options)) {
      return null
    }

    return getFootball<VotedRealPlayerWrapper>(options.year, `days/${options.day}/votes/official`)
  }

  async getEnhancedOfficialWrapper(options: VoteLookupOptions): Promise<EnhancedVotedRealPlayerWrapper | null> {
    const wrapper = await this.getOfficialWrapper(options)
    return wrapper ? enhanceVotedRealPlayersWrapper(wrapper) : null
  }

  async getOfficialPlayers(options: VoteLookupOptions): Promise<VotedRealPlayer[]> {
    const wrapper = await this.getOfficialWrapper(options)
    return wrapper?.players ?? []
  }

  async getOfficialBuckets(options: VoteLookupOptions): Promise<VotedRealPlayerRoleBuckets> {
    const enhanced = await this.getEnhancedOfficialWrapper(options)
    return enhanced?.playersByRole ?? {}
  }

  // ============================================
  // CONVENIENCE HELPERS
  // ============================================

  async getCurrentYearLiveWrapper(day: number): Promise<VotedRealPlayerWrapper | null> {
    const year = await this.getCurrentUserYear()
    if (!year) {
      return null
    }
    return this.getLiveWrapper({ year, day })
  }

  async getCurrentYearEnhancedLiveWrapper(day: number): Promise<EnhancedVotedRealPlayerWrapper | null> {
    const wrapper = await this.getCurrentYearLiveWrapper(day)
    return wrapper ? enhanceVotedRealPlayersWrapper(wrapper) : null
  }

  async getCurrentYearOfficialWrapper(day: number): Promise<VotedRealPlayerWrapper | null> {
    const year = await this.getCurrentUserYear()
    if (!year) {
      return null
    }
    return this.getOfficialWrapper({ year, day })
  }

  async getCurrentYearEnhancedOfficialWrapper(day: number): Promise<EnhancedVotedRealPlayerWrapper | null> {
    const wrapper = await this.getCurrentYearOfficialWrapper(day)
    return wrapper ? enhanceVotedRealPlayersWrapper(wrapper) : null
  }

  async searchLivePlayers(term: string, options: VoteLookupOptions): Promise<VotedRealPlayer[]> {
    const players = await this.getLivePlayers(options)
    if (!term) {
      return players
    }
    return players.filter(player => player.name.toLowerCase().includes(term.toLowerCase()))
  }

  async searchOfficialPlayers(term: string, options: VoteLookupOptions): Promise<VotedRealPlayer[]> {
    const players = await this.getOfficialPlayers(options)
    if (!term) {
      return players
    }
    return players.filter(player => player.name.toLowerCase().includes(term.toLowerCase()))
  }

  async searchCurrentYearLivePlayers(term: string, day: number): Promise<VotedRealPlayer[]> {
    const year = await this.getCurrentUserYear()
    if (!year) {
      return []
    }
    return this.searchLivePlayers(term, { year, day })
  }

  async searchCurrentYearOfficialPlayers(term: string, day: number): Promise<VotedRealPlayer[]> {
    const year = await this.getCurrentUserYear()
    if (!year) {
      return []
    }
    return this.searchOfficialPlayers(term, { year, day })
  }
}

export const realVoteService = new RealVoteService()
