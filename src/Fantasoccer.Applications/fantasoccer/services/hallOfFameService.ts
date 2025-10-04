// Hall of Fame service using Repository Framework
import { RepositoryServices } from 'rystem.repository.client'
import { 
  HallOfFame,
  HallOfFameRaw,
  GeneralLeagueKey,
  GeneralLeagueKeyRaw,
  RankedTeam,
  WinningTeam,
  WinningPlayer,
  RecordGame,
  RecordPlayer,
  PlayerWithMostPointsInYear,
  mapRawHallOfFame,
  mapGeneralLeagueKeyToRaw,
  HallOfFameHelper
} from 'types/hallOfFame'

export class HallOfFameService {

  // ============================================
  // MAIN HALL OF FAME OPERATIONS
  // ============================================

  /**
   * Get complete Hall of Fame data for a league
   * @param groupId - The group ID
   * @param leagueId - The league ID
   * @returns HallOfFame data or null if not found
   */
  async getHallOfFame(
    groupId: string, 
    leagueId: string
  ): Promise<HallOfFame | null> {
    try {
      // Create the key for the repository query
      const key: GeneralLeagueKeyRaw = {
        g: groupId,
        l: leagueId
      }

      // Get the repository instance
      const repository = RepositoryServices
        .Repository<HallOfFameRaw, GeneralLeagueKeyRaw>("HallOfFame")
      
      // Fetch the hall of fame data
      const result = await repository.get(key)
      
      if (result) {
        // Map raw data to clean interface
        return mapRawHallOfFame(result)
      }
      
      return null
    } catch (error) {
      console.error('Error fetching Hall of Fame:', error)
      return null
    }
  }

  // ============================================
  // SPECIFIC DATA EXTRACTORS
  // ============================================

  /**
   * Get all-time rankings for a league
   */
  async getAllTimeRankings(
    groupId: string, 
    leagueId: string
  ): Promise<RankedTeam[]> {
    try {
      const hallOfFame = await this.getHallOfFame(groupId, leagueId)
      return hallOfFame?.allTimeRankings ?? []
    } catch (error) {
      console.error('Error fetching all-time rankings:', error)
      return []
    }
  }

  /**
   * Get all-time rankings sorted by points
   */
  async getAllTimeRankingsSortedByPoints(
    groupId: string, 
    leagueId: string
  ): Promise<RankedTeam[]> {
    try {
      const rankings = await this.getAllTimeRankings(groupId, leagueId)
      return HallOfFameHelper.sortByPoints(rankings)
    } catch (error) {
      console.error('Error fetching sorted rankings:', error)
      return []
    }
  }

  /**
   * Get all-time rankings sorted by value assets
   */
  async getAllTimeRankingsSortedByValue(
    groupId: string, 
    leagueId: string
  ): Promise<RankedTeam[]> {
    try {
      const rankings = await this.getAllTimeRankings(groupId, leagueId)
      return HallOfFameHelper.sortByValueAssets(rankings)
    } catch (error) {
      console.error('Error fetching sorted rankings by value:', error)
      return []
    }
  }

  /**
   * Get winning teams history
   */
  async getWinningTeams(
    groupId: string, 
    leagueId: string
  ): Promise<WinningTeam[]> {
    try {
      const hallOfFame = await this.getHallOfFame(groupId, leagueId)
      return hallOfFame?.winningTeams ?? []
    } catch (error) {
      console.error('Error fetching winning teams:', error)
      return []
    }
  }

  /**
   * Get winning teams sorted by total wins
   */
  async getWinningTeamsSorted(
    groupId: string, 
    leagueId: string
  ): Promise<WinningTeam[]> {
    try {
      const teams = await this.getWinningTeams(groupId, leagueId)
      return HallOfFameHelper.sortWinningTeamsByWins(teams)
    } catch (error) {
      console.error('Error fetching sorted winning teams:', error)
      return []
    }
  }

  /**
   * Get winning players history
   */
  async getWinningPlayers(
    groupId: string, 
    leagueId: string
  ): Promise<WinningPlayer[]> {
    try {
      const hallOfFame = await this.getHallOfFame(groupId, leagueId)
      return hallOfFame?.winningPlayers ?? []
    } catch (error) {
      console.error('Error fetching winning players:', error)
      return []
    }
  }

  /**
   * Get winning players sorted by total wins
   */
  async getWinningPlayersSorted(
    groupId: string, 
    leagueId: string
  ): Promise<WinningPlayer[]> {
    try {
      const players = await this.getWinningPlayers(groupId, leagueId)
      return HallOfFameHelper.sortWinningPlayersByWins(players)
    } catch (error) {
      console.error('Error fetching sorted winning players:', error)
      return []
    }
  }

  /**
   * Get record game (highest scoring game)
   */
  async getRecordGame(
    groupId: string, 
    leagueId: string
  ): Promise<RecordGame | null> {
    try {
      const hallOfFame = await this.getHallOfFame(groupId, leagueId)
      return hallOfFame?.recordGame ?? null
    } catch (error) {
      console.error('Error fetching record game:', error)
      return null
    }
  }

  /**
   * Get record player (player with most points overall)
   */
  async getRecordPlayer(
    groupId: string, 
    leagueId: string
  ): Promise<RecordPlayer | null> {
    try {
      const hallOfFame = await this.getHallOfFame(groupId, leagueId)
      return hallOfFame?.recordPlayer ?? null
    } catch (error) {
      console.error('Error fetching record player:', error)
      return null
    }
  }

  /**
   * Get player with most points in a specific year
   */
  async getPlayerWithMostPointsInYear(
    groupId: string, 
    leagueId: string
  ): Promise<PlayerWithMostPointsInYear | null> {
    try {
      const hallOfFame = await this.getHallOfFame(groupId, leagueId)
      return hallOfFame?.playerWithMostPointsInYear ?? null
    } catch (error) {
      console.error('Error fetching player with most points in year:', error)
      return null
    }
  }

  // ============================================
  // HELPER QUERIES
  // ============================================

  /**
   * Get a specific team's all-time stats
   */
  async getTeamAllTimeStats(
    groupId: string, 
    leagueId: string,
    teamOwner: string
  ): Promise<RankedTeam | null> {
    try {
      const rankings = await this.getAllTimeRankings(groupId, leagueId)
      return rankings.find(t => t.owner.toLowerCase() === teamOwner.toLowerCase()) ?? null
    } catch (error) {
      console.error('Error fetching team all-time stats:', error)
      return null
    }
  }

  /**
   * Get a specific team's championship wins
   */
  async getTeamWins(
    groupId: string, 
    leagueId: string,
    teamOwner: string
  ): Promise<WinningTeam | null> {
    try {
      const winningTeams = await this.getWinningTeams(groupId, leagueId)
      return winningTeams.find(t => t.owner.toLowerCase() === teamOwner.toLowerCase()) ?? null
    } catch (error) {
      console.error('Error fetching team wins:', error)
      return null
    }
  }

  /**
   * Get top N teams by points
   */
  async getTopTeamsByPoints(
    groupId: string, 
    leagueId: string,
    limit: number = 10
  ): Promise<RankedTeam[]> {
    try {
      const rankings = await this.getAllTimeRankingsSortedByPoints(groupId, leagueId)
      return rankings.slice(0, limit)
    } catch (error) {
      console.error('Error fetching top teams:', error)
      return []
    }
  }

  /**
   * Get top N teams by value assets
   */
  async getTopTeamsByValue(
    groupId: string, 
    leagueId: string,
    limit: number = 10
  ): Promise<RankedTeam[]> {
    try {
      const rankings = await this.getAllTimeRankingsSortedByValue(groupId, leagueId)
      return rankings.slice(0, limit)
    } catch (error) {
      console.error('Error fetching top teams by value:', error)
      return []
    }
  }

  /**
   * Get teams with most victories
   */
  async getTeamsWithMostVictories(
    groupId: string, 
    leagueId: string,
    limit: number = 10
  ): Promise<RankedTeam[]> {
    try {
      const rankings = await this.getAllTimeRankings(groupId, leagueId)
      return [...rankings]
        .sort((a, b) => b.victories - a.victories)
        .slice(0, limit)
    } catch (error) {
      console.error('Error fetching teams with most victories:', error)
      return []
    }
  }

  /**
   * Get teams with best goal difference
   */
  async getTeamsWithBestGoalDifference(
    groupId: string, 
    leagueId: string,
    limit: number = 10
  ): Promise<RankedTeam[]> {
    try {
      const rankings = await this.getAllTimeRankings(groupId, leagueId)
      return [...rankings]
        .sort((a, b) => {
          const diffA = HallOfFameHelper.getGoalDifference(a)
          const diffB = HallOfFameHelper.getGoalDifference(b)
          return diffB - diffA
        })
        .slice(0, limit)
    } catch (error) {
      console.error('Error fetching teams with best goal difference:', error)
      return []
    }
  }

  /**
   * Get teams with highest win percentage
   */
  async getTeamsWithHighestWinPercentage(
    groupId: string, 
    leagueId: string,
    limit: number = 10,
    minGames: number = 10
  ): Promise<Array<RankedTeam & { winPercentage: number }>> {
    try {
      const rankings = await this.getAllTimeRankings(groupId, leagueId)
      return rankings
        .filter(t => HallOfFameHelper.getTotalGamesPlayed(t) >= minGames)
        .map(t => ({
          ...t,
          winPercentage: HallOfFameHelper.getWinPercentage(t)
        }))
        .sort((a, b) => b.winPercentage - a.winPercentage)
        .slice(0, limit)
    } catch (error) {
      console.error('Error fetching teams with highest win percentage:', error)
      return []
    }
  }

  /**
   * Get championship winners by year
   */
  async getChampionsByYear(
    groupId: string, 
    leagueId: string
  ): Promise<Record<number, string>> {
    try {
      const winningTeams = await this.getWinningTeams(groupId, leagueId)
      const championsByYear: Record<number, string> = {}

      winningTeams.forEach(team => {
        const years = HallOfFameHelper.getAllYears(team.wins)
        years.forEach(year => {
          championsByYear[year] = team.teamName
        })
      })

      return championsByYear
    } catch (error) {
      console.error('Error fetching champions by year:', error)
      return {}
    }
  }
}

// Export singleton instance
export const hallOfFameService = new HallOfFameService()
