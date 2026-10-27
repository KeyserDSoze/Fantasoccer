// Rank service using Repository Framework
import { authService } from './authService'
import { tokenStorageService } from './tokenStorageService'
import {
  Rank,
  LeagueKey,
  YearDayLeague,
  RankedTeam,
  EnhancedRank,
  EnhancedRankedTeam,
  enhanceRank,
  enhanceRankedTeam,
  enhanceRankWithTeamPositions,
  RankHelper,
  DefaultRank
} from 'types/rank'
import { apiRequest } from './httpClient'
import { competitionPath } from './gameService'

export class RankService {
  // ============================================
  // INTERNAL REPOSITORY ACCESSORS
  // ============================================

  // ============================================
  // MAIN RANK OPERATIONS (using Repository Framework)
  // ============================================

  /**
   * Get rank data for a specific league key (group + league + year)
   */
  async getRank(leagueKey: LeagueKey): Promise<Rank | null> {
    return (await apiRequest<Rank | undefined>(`${competitionPath(leagueKey.group, leagueKey.league, leagueKey.year)}/rank`)) ?? null
  }

  /**
   * Get rank data by individual components
   */
  async getRankByComponents(group: string, league: string, year: number): Promise<Rank | null> {
    const leagueKey = RankHelper.createLeagueKey(group, league, year)
    return this.getRank(leagueKey)
  }

  /**
   * Get enhanced rank with computed properties
   */
  async getEnhancedRank(leagueKey: LeagueKey): Promise<EnhancedRank | null> {
    const rank = await this.getRank(leagueKey)
    
    if (!rank) {
      return null
    }

    return enhanceRank(rank)
  }

  /**
   * Get enhanced rank by individual components
   */
  async getEnhancedRankByComponents(group: string, league: string, year: number): Promise<EnhancedRank | null> {
    const leagueKey = RankHelper.createLeagueKey(group, league, year)
    return this.getEnhancedRank(leagueKey)
  }

  /**
   * Save/Update rank data
   */
  /**
   * Update rank data
   */
  /**
   * Delete rank data
   */
  // ============================================
  // DAILY RANK OPERATIONS
  // ============================================

  /**
   * Reconstruct standings through this Serie A day from saved results/rules.
   * No daily snapshot is stored; money/valueAssets are null when historical
   * balances are unavailable. plusMoney is a theoretical entitlement only.
   */
  async getDailyRank(key: YearDayLeague): Promise<Rank | null> {
    return (await apiRequest<Rank | undefined>(`${competitionPath(key.group, key.league, key.year)}/rank?day=${key.day}`)) ?? null
  }

  /**
   * Get daily rank by individual components
   */
  async getDailyRankByComponents(group: string, league: string, year: number, day: number): Promise<Rank | null> {
    const key = RankHelper.createYearDayLeague(group, league, year, day)
    return this.getDailyRank(key)
  }

  /**
   * Get enhanced daily rank with computed properties
   */
  async getEnhancedDailyRank(key: YearDayLeague): Promise<EnhancedRank | null> {
    const rank = await this.getDailyRank(key)
    return rank ? enhanceRank(rank) : null
  }

  /**
   * Get enhanced daily rank by individual components
   */
  async getEnhancedDailyRankByComponents(group: string, league: string, year: number, day: number): Promise<EnhancedRank | null> {
    const key = RankHelper.createYearDayLeague(group, league, year, day)
    return this.getEnhancedDailyRank(key)
  }

  // ============================================
  // TEAM-SPECIFIC OPERATIONS
  // ============================================

  /**
   * Get a specific team's ranking data
   */
  async getTeamRanking(leagueKey: LeagueKey, roundId: string, owner: string): Promise<RankedTeam | null> {
    const rank = await this.getRank(leagueKey)
    
    if (!rank) {
      return null
    }

    return RankHelper.getRankedTeamByOwner(rank, roundId, owner)
  }

  /**
   * Get enhanced team ranking with position and computed properties
   */
  async getEnhancedTeamRanking(leagueKey: LeagueKey, roundId: string, owner: string): Promise<EnhancedRankedTeam | null> {
    const rank = await this.getRank(leagueKey)
    
    if (!rank) {
      return null
    }

    const team = RankHelper.getRankedTeamByOwner(rank, roundId, owner)
    if (!team) {
      return null
    }

    const position = RankHelper.getTeamPosition(rank, roundId, owner)
    return enhanceRankedTeam(team, position > 0 ? position : undefined)
  }

  /**
   * Get team position in league ranking
   */
  async getTeamPosition(leagueKey: LeagueKey, roundId: string, owner: string): Promise<number> {
    const rank = await this.getRank(leagueKey)
    
    if (!rank) {
      return -1
    }

    return RankHelper.getTeamPosition(rank, roundId, owner)
  }

  // ============================================
  // ROUND-SPECIFIC OPERATIONS
  // ============================================

  /**
   * Get all teams for a specific round, sorted by points
   */
  async getRoundRanking(leagueKey: LeagueKey, roundId: string): Promise<RankedTeam[]> {
    const rank = await this.getRank(leagueKey)
    
    if (!rank) {
      return []
    }

    return RankHelper.getTeamsSortedByPoints(rank, roundId)
  }

  /**
   * Get enhanced round ranking with positions and computed properties
   */
  async getEnhancedRoundRanking(leagueKey: LeagueKey, roundId: string): Promise<EnhancedRankedTeam[]> {
    const rank = await this.getRank(leagueKey)
    
    if (!rank) {
      return []
    }

    const enhancedRankings = enhanceRankWithTeamPositions(rank)
    return enhancedRankings[roundId] || []
  }

  /**
   * Get teams sorted by value assets
   */
  async getRoundRankingByValueAssets(leagueKey: LeagueKey, roundId: string): Promise<RankedTeam[]> {
    const rank = await this.getRank(leagueKey)
    
    if (!rank) {
      return []
    }

    return RankHelper.getTeamsSortedByValueAssets(rank, roundId)
  }

  /**
   * Get available rounds for a rank
   */
  async getAvailableRounds(leagueKey: LeagueKey): Promise<string[]> {
    const rank = await this.getRank(leagueKey)
    
    if (!rank) {
      return []
    }

    return RankHelper.getAvailableRounds(rank)
  }

  /**
   * Get team count for a specific round
   */
  async getRoundTeamCount(leagueKey: LeagueKey, roundId: string): Promise<number> {
    const rank = await this.getRank(leagueKey)
    
    if (!rank) {
      return 0
    }

    return RankHelper.getTeamCount(rank, roundId)
  }

  // ============================================
  // MULTI-GROUP OPERATIONS (for user's groups)
  // ============================================

  /**
   * Get rankings for multiple groups/leagues for a specific year
   */
  async getMultipleRankings(requests: { group: string, league: string, year: number }[]): Promise<Record<string, Rank>> {
    const results: Record<string, Rank> = {}
    
    // Fetch all rankings in parallel
    const rankPromises = requests.map(async (req) => {
      const key = RankHelper.createLeagueKey(req.group, req.league, req.year)
      const rank = await this.getRank(key)
      const keyString = RankHelper.leagueKeyToString(key)
      
      return { keyString, rank }
    })

    const rankings = await Promise.all(rankPromises)
    
    // Filter out null results and build the response
    rankings.forEach(({ keyString, rank }) => {
      if (rank) {
        results[keyString] = rank
      }
    })

    return results
  }

  /**
   * Get user's team rankings across multiple groups/leagues
   */
  async getUserTeamRankings(
    requests: { group: string, league: string, roundId: string, year: number }[], 
    owner: string
  ): Promise<Record<string, { team: RankedTeam, position: number }>> {
    const results: Record<string, { team: RankedTeam, position: number }> = {}
    
    // Fetch all rankings in parallel
    const rankPromises = requests.map(async (req) => {
      const key = RankHelper.createLeagueKey(req.group, req.league, req.year)
      const rank = await this.getRank(key)
      const keyString = RankHelper.leagueKeyToString(key)
      
      if (rank) {
        const team = RankHelper.getRankedTeamByOwner(rank, req.roundId, owner)
        const position = RankHelper.getTeamPosition(rank, req.roundId, owner)
        
        if (team) {
          return { keyString, team, position }
        }
      }
      
      return null
    })

    const rankings = await Promise.all(rankPromises)
    
    // Filter out null results and build the response
    rankings.forEach((result) => {
      if (result) {
        results[result.keyString] = { 
          team: result.team, 
          position: result.position 
        }
      }
    })

    return results
  }

  // ============================================
  // UTILITY METHODS
  // ============================================

  /**
   * Check if rank data exists for a league key
   */
  async isRankAvailable(leagueKey: LeagueKey): Promise<boolean> {
    const rank = await this.getRank(leagueKey)
    return rank !== null
  }

  /**
   * Get current Serie A day from rank data
   */
  async getCurrentSerieADay(leagueKey: LeagueKey): Promise<number> {
    const rank = await this.getRank(leagueKey)
    return rank?.serieADay || 0
  }

  /**
   * Create empty rank structure for initialization
   */
  createEmptyRank(serieADay: number): Rank {
    return {
      serieADay,
      rounds: {}
    }
  }

  // ============================================
  // HELPER METHODS (matching groupService pattern)
  // ============================================

  /**
   * Helper method to create league key from string format
   */
  parseLeagueKeyFromString(keyString: string): LeagueKey | null {
    try {
      const parts: unknown = JSON.parse(keyString)
      return Array.isArray(parts) && parts.length === 3 && typeof parts[0] === 'string' && typeof parts[1] === 'string' &&
        typeof parts[2] === 'number' && Number.isInteger(parts[2]) ? { group: parts[0], league: parts[1], year: parts[2] } : null
    } catch { return null }
  }

  /**
   * Helper method to validate league key
   */
  isValidLeagueKey(leagueKey: LeagueKey): boolean {
    return leagueKey.group.length > 0 && 
           leagueKey.league.length > 0 && 
           leagueKey.year > 0
  }

  /**
   * Get all available years from current user's groups
   * This would be called in combination with groupService
   */
  async getAvailableYearsForUser(): Promise<number[]> {
    try {
      // Get current user's access token from storage
      const accessToken = await tokenStorageService.getTokenString()
      if (!accessToken) {
        console.warn('No valid access token available')
        return []
      }

      // Get current user info to get user identity
      const user = await authService.getUserInfo(accessToken)
      
      if (!user) {
        console.warn('User info not available')
        return []
      }

      // This could be enhanced to query for available years
      // For now, returning common years (could be enhanced based on business logic)
      const currentYear = new Date().getFullYear()
      return [currentYear, currentYear - 1, currentYear - 2]
      
    } catch (error) {
      console.error('Error fetching available years:', error)
      return []
    }
  }
}

// Export a default instance
export const rankService = new RankService()
