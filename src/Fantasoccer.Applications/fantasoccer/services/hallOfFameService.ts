import { apiRequest } from './httpClient'
import { HallOfFameHelper, type HallOfFame } from '../types/hallOfFame'

export class HallOfFameService {
  refresh(group: string, league: string, expectedVersion: number) { return apiRequest(`/api/groups/${encodeURIComponent(group)}/leagues/${encodeURIComponent(league)}/hall-of-fame/refresh`, 'POST', { expectedVersion }) }
  confirmBaseline(group: string, league: string, expectedVersion: number) { return apiRequest(`/api/groups/${encodeURIComponent(group)}/leagues/${encodeURIComponent(league)}/hall-of-fame/confirm-baseline`, 'POST', { expectedVersion }) }
  async getHallOfFame(group: string, league: string): Promise<HallOfFame | null> {
    return (await apiRequest<HallOfFame | undefined>(`/api/groups/${encodeURIComponent(group)}/leagues/${encodeURIComponent(league)}/hall-of-fame`)) ?? null
  }
  async getAllTimeRankings(group: string, league: string) { return (await this.getHallOfFame(group, league))?.allTimeRankings ?? [] }
  async getAllTimeRankingsSortedByPoints(group: string, league: string) { return HallOfFameHelper.sortByPoints(await this.getAllTimeRankings(group, league)) }
  async getAllTimeRankingsSortedByValue(group: string, league: string) { return HallOfFameHelper.sortByValueAssets(await this.getAllTimeRankings(group, league)) }
  async getWinningTeams(group: string, league: string) { return (await this.getHallOfFame(group, league))?.winningTeams ?? [] }
  async getWinningTeamsSorted(group: string, league: string) { return HallOfFameHelper.sortWinningTeamsByWins(await this.getWinningTeams(group, league)) }
  async getWinningPlayers(group: string, league: string) { return (await this.getHallOfFame(group, league))?.winningPlayers ?? [] }
  async getWinningPlayersSorted(group: string, league: string) { return HallOfFameHelper.sortWinningPlayersByWins(await this.getWinningPlayers(group, league)) }
  async getRecordGame(group: string, league: string) { return (await this.getHallOfFame(group, league))?.recordGame ?? null }
  async getRecordPlayer(group: string, league: string) { return (await this.getHallOfFame(group, league))?.recordPlayer ?? null }
  async getPlayerWithMostPointsInYear(group: string, league: string) { return (await this.getHallOfFame(group, league))?.playerWithMostPointsInYear ?? null }
  async getTeamAllTimeStats(group: string, league: string, owner: string) { return (await this.getAllTimeRankings(group, league)).find(team => team.owner.toLowerCase() === owner.toLowerCase()) ?? null }
  async getTeamWins(group: string, league: string, owner: string) { return (await this.getWinningTeams(group, league)).find(team => team.owner.toLowerCase() === owner.toLowerCase()) ?? null }
  async getTopTeamsByPoints(group: string, league: string, limit = 10) { return (await this.getAllTimeRankingsSortedByPoints(group, league)).slice(0, limit) }
  async getTopTeamsByValue(group: string, league: string, limit = 10) { return (await this.getAllTimeRankingsSortedByValue(group, league)).slice(0, limit) }
  async getTeamsWithMostVictories(group: string, league: string, limit = 10) { return (await this.getAllTimeRankings(group, league)).sort((a, b) => b.victories - a.victories).slice(0, limit) }
  async getTeamsWithBestGoalDifference(group: string, league: string, limit = 10) { return (await this.getAllTimeRankings(group, league)).sort((a, b) => HallOfFameHelper.getGoalDifference(b) - HallOfFameHelper.getGoalDifference(a)).slice(0, limit) }
  async getTeamsWithHighestWinPercentage(group: string, league: string, limit = 10, minGames = 10) {
    return (await this.getAllTimeRankings(group, league)).filter(team => HallOfFameHelper.getTotalGamesPlayed(team) >= minGames)
      .map(team => ({ ...team, winPercentage: HallOfFameHelper.getWinPercentage(team) })).sort((a, b) => b.winPercentage - a.winPercentage).slice(0, limit)
  }
  async getChampionsByYear(group: string, league: string) {
    const champions: Record<number, string> = {}
    for (const team of await this.getWinningTeams(group, league))
      for (const year of HallOfFameHelper.getAllYears(team.wins)) champions[year] = team.teamName ?? team.owner
    return champions
  }
}
export const hallOfFameService = new HallOfFameService()
