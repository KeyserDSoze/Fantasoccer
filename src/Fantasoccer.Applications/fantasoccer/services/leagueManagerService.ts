// League Manager service - handles league administration operations
import { buildApiUrl, getCommonHeaders, ApiError } from '../configurations/apiConfig'
import { tokenStorageService } from './tokenStorageService'

export class LeagueManagerService {
  /**
   * Create calendar and rank for a league (randomized)
   */
  async createCalendarAndRank(
    groupId: string,
    leagueId: string,
    year: number
  ): Promise<boolean> {
    try {
      const token = await tokenStorageService.ensureValidToken()
      if (!token) {
        throw new ApiError('No authentication token available', 401)
      }

      const url = buildApiUrl('/LeagueManager/CreateCalendarAndRankAsync', {
        groupId,
        leagueId,
        year: year.toString()
      })

      const response = await fetch(url, {
        method: 'GET',
        headers: {
          ...getCommonHeaders(),
          'Authorization': `Bearer ${token}`
        },
      })

      if (!response.ok) {
        const errorText = await response.text()
        throw new ApiError(
          `Create calendar failed: ${response.status} ${response.statusText}`,
          response.status,
          errorText
        )
      }

      const result = await response.json()
      return result
    } catch (error) {
      if (error instanceof ApiError) throw error
      throw new ApiError(
        `Failed to create calendar and rank: ${error instanceof Error ? error.message : 'Unknown error'}`
      )
    }
  }

  /**
   * Randomize players for auctions
   */
  async randomizePlayersForAuctions(
    groupId: string,
    leagueId: string,
    year: number
  ): Promise<boolean> {
    try {
      const token = await tokenStorageService.ensureValidToken()
      if (!token) {
        throw new ApiError('No authentication token available', 401)
      }

      const url = buildApiUrl('/LeagueManager/RandomizePlayerForAuctionsAsync', {
        groupId,
        leagueId,
        year: year.toString()
      })

      const response = await fetch(url, {
        method: 'GET',
        headers: {
          ...getCommonHeaders(),
          'Authorization': `Bearer ${token}`
        },
      })

      if (!response.ok) {
        const errorText = await response.text()
        throw new ApiError(
          `Randomize players failed: ${response.status} ${response.statusText}`,
          response.status,
          errorText
        )
      }

      const result = await response.json()
      return result
    } catch (error) {
      if (error instanceof ApiError) throw error
      throw new ApiError(
        `Failed to randomize players: ${error instanceof Error ? error.message : 'Unknown error'}`
      )
    }
  }

  /**
   * Recalculate league rankings
   */
  async recalculate(
    groupId: string,
    leagueId: string,
    year: number,
    dayToRecalculate: number,
    onlyLastDay: boolean = false,
    preciseDay: boolean = false
  ): Promise<boolean> {
    try {
      const token = await tokenStorageService.ensureValidToken()
      if (!token) {
        throw new ApiError('No authentication token available', 401)
      }

      const url = buildApiUrl('/LeagueManager/Recalculate', {
        groupId,
        leagueId,
        year: year.toString(),
        dayToRecalculate: dayToRecalculate.toString(),
        onlyLastDay: onlyLastDay.toString(),
        preciseDay: preciseDay.toString()
      })

      const response = await fetch(url, {
        method: 'GET',
        headers: {
          ...getCommonHeaders(),
          'Authorization': `Bearer ${token}`
        },
      })

      if (!response.ok) {
        const errorText = await response.text()
        throw new ApiError(
          `Recalculation failed: ${response.status} ${response.statusText}`,
          response.status,
          errorText
        )
      }

      const result = await response.json()
      return result
    } catch (error) {
      if (error instanceof ApiError) throw error
      throw new ApiError(
        `Failed to recalculate: ${error instanceof Error ? error.message : 'Unknown error'}`
      )
    }
  }
}

// Export singleton instance
export const leagueManagerService = new LeagueManagerService()
