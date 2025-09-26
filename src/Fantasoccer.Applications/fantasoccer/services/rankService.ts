// Rank service using Repository Framework
import { authService } from './authService'
import { tokenStorageService } from './tokenStorageService'
import { 
  Rank, 
  RankRaw, 
  LeagueKey,
  LeagueKeyRaw,
  YearDayLeague,
  RankedTeam,
  EnhancedRank,
  EnhancedRankedTeam,
  mapRawRankToRank,
  mapRankToRawRank,
  mapRawLeagueKeyToLeagueKey,
  mapLeagueKeyToRawLeagueKey,
  enhanceRank,
  enhanceRankedTeam,
  enhanceRankWithTeamPositions,
  RankHelper,
  DefaultRank
} from 'types/rank'
import { RepositoryServices } from 'rystem.repository.client'

export class RankService {

  // ============================================
  // MAIN RANK OPERATIONS (using Repository Framework)
  // ============================================

  /**
   * Get rank data for a specific league key (group + league + year)
   */
  async getRank(leagueKey: LeagueKey): Promise<Rank | null> {
    try {
      // Get the repository instance
      const repository = RepositoryServices
        .Repository<RankRaw, LeagueKeyRaw>("Rank")
      
      // Convert clean key to raw key for repository call
      const rawKey = mapLeagueKeyToRawLeagueKey(leagueKey)
      
      // Fetch the rank data
      const result = await repository.get(rawKey)
      
      if (result) {
        // Map raw data to clean interface
        return mapRawRankToRank(result)
      }
      
      return null
    } catch (error) {
      console.error('Error fetching rank:', error)
      return null
    }
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
  async saveRank(leagueKey: LeagueKey, rank: Rank): Promise<boolean> {
    try {
      // Get the repository instance
      const repository = RepositoryServices
        .Repository<RankRaw, LeagueKeyRaw>("Rank")
      
      // Convert clean data to raw format
      const rawKey = mapLeagueKeyToRawLeagueKey(leagueKey)
      const rawRank = mapRankToRawRank(rank)
      
      // Save the rank data
      await repository.insert(rawKey, rawRank)
      
      return true
    } catch (error) {
      console.error('Error saving rank:', error)
      return false
    }
  }

  /**
   * Update rank data
   */
  async updateRank(leagueKey: LeagueKey, rank: Rank): Promise<boolean> {
    try {
      // Get the repository instance
      const repository = RepositoryServices
        .Repository<RankRaw, LeagueKeyRaw>("Rank")
      
      // Convert clean data to raw format
      const rawKey = mapLeagueKeyToRawLeagueKey(leagueKey)
      const rawRank = mapRankToRawRank(rank)
      
      // Update the rank data
      await repository.update(rawKey, rawRank)
      
      return true
    } catch (error) {
      console.error('Error updating rank:', error)
      return false
    }
  }

  /**
   * Delete rank data
   */
  async deleteRank(leagueKey: LeagueKey): Promise<boolean> {
    try {
      // Get the repository instance
      const repository = RepositoryServices
        .Repository<RankRaw, LeagueKeyRaw>("Rank")
      
      // Convert clean key to raw key for repository call
      const rawKey = mapLeagueKeyToRawLeagueKey(leagueKey)
      
      // Delete the rank data
      await repository.delete(rawKey)
      
      return true
    } catch (error) {
      console.error('Error deleting rank:', error)
      return false
    }
  }

  // ============================================
  // TEAM-SPECIFIC OPERATIONS
  // ============================================

  /**
   * Get a specific team's ranking data
   */
  async getTeamRanking(leagueKey: LeagueKey, leagueId: string, owner: string): Promise<RankedTeam | null> {
    const rank = await this.getRank(leagueKey)
    
    if (!rank) {
      return null
    }

    return RankHelper.getRankedTeamByOwner(rank, leagueId, owner)
  }

  /**
   * Get enhanced team ranking with position and computed properties
   */
  async getEnhancedTeamRanking(leagueKey: LeagueKey, leagueId: string, owner: string): Promise<EnhancedRankedTeam | null> {
    const rank = await this.getRank(leagueKey)
    
    if (!rank) {
      return null
    }

    const team = RankHelper.getRankedTeamByOwner(rank, leagueId, owner)
    if (!team) {
      return null
    }

    const position = RankHelper.getTeamPosition(rank, leagueId, owner)
    return enhanceRankedTeam(team, position > 0 ? position : undefined)
  }

  /**
   * Get team position in league ranking
   */
  async getTeamPosition(leagueKey: LeagueKey, leagueId: string, owner: string): Promise<number> {
    const rank = await this.getRank(leagueKey)
    
    if (!rank) {
      return -1
    }

    return RankHelper.getTeamPosition(rank, leagueId, owner)
  }

  // ============================================
  // LEAGUE-SPECIFIC OPERATIONS
  // ============================================

  /**
   * Get all teams for a specific league, sorted by points
   */
  async getLeagueRanking(leagueKey: LeagueKey, leagueId: string): Promise<RankedTeam[]> {
    const rank = await this.getRank(leagueKey)
    
    if (!rank) {
      return []
    }

    return RankHelper.getTeamsSortedByPoints(rank, leagueId)
  }

  /**
   * Get enhanced league ranking with positions and computed properties
   */
  async getEnhancedLeagueRanking(leagueKey: LeagueKey, leagueId: string): Promise<EnhancedRankedTeam[]> {
    const rank = await this.getRank(leagueKey)
    
    if (!rank) {
      return []
    }

    const enhancedRankings = enhanceRankWithTeamPositions(rank)
    return enhancedRankings[leagueId] || []
  }

  /**
   * Get teams sorted by value assets
   */
  async getLeagueRankingByValueAssets(leagueKey: LeagueKey, leagueId: string): Promise<RankedTeam[]> {
    const rank = await this.getRank(leagueKey)
    
    if (!rank) {
      return []
    }

    return RankHelper.getTeamsSortedByValueAssets(rank, leagueId)
  }

  /**
   * Get available leagues for a rank
   */
  async getAvailableLeagues(leagueKey: LeagueKey): Promise<string[]> {
    const rank = await this.getRank(leagueKey)
    
    if (!rank) {
      return []
    }

    return RankHelper.getAvailableLeagues(rank)
  }

  /**
   * Get team count for a specific league
   */
  async getLeagueTeamCount(leagueKey: LeagueKey, leagueId: string): Promise<number> {
    const rank = await this.getRank(leagueKey)
    
    if (!rank) {
      return 0
    }

    return RankHelper.getTeamCount(rank, leagueId)
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
    requests: { group: string, league: string, leagueId: string, year: number }[], 
    owner: string
  ): Promise<Record<string, { team: RankedTeam, position: number }>> {
    const results: Record<string, { team: RankedTeam, position: number }> = {}
    
    // Fetch all rankings in parallel
    const rankPromises = requests.map(async (req) => {
      const key = RankHelper.createLeagueKey(req.group, req.league, req.year)
      const rank = await this.getRank(key)
      const keyString = RankHelper.leagueKeyToString(key)
      
      if (rank) {
        const team = RankHelper.getRankedTeamByOwner(rank, req.leagueId, owner)
        const position = RankHelper.getTeamPosition(rank, req.leagueId, owner)
        
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
      rankedTeams: {}
    }
  }

  // ============================================
  // HELPER METHODS (matching groupService pattern)
  // ============================================

  /**
   * Helper method to create league key from string format
   */
  parseLeagueKeyFromString(keyString: string): LeagueKey | null {
    const parts = keyString.split('-')
    if (parts.length !== 3) {
      return null
    }

    return {
      group: parts[0],
      league: parts[1],
      year: parseInt(parts[2], 10)
    }
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