import { apiRequest } from './httpClient'
import { competitionPath } from './gameService'

export class LeagueManagerService {
  async createCalendarAndRank(groupId: string, leagueId: string, year: number, expectedVersion?: number, replaceExisting = false) {
    await apiRequest(`${competitionPath(groupId, leagueId, year)}/calendar/generate`, 'POST', { expectedVersion, replaceExisting }); return true
  }
  async randomizePlayersForAuctions(groupId: string, leagueId: string, year: number, expectedVersion?: number) {
    const path = `${competitionPath(groupId, leagueId, year)}/auction-order`
    const version = expectedVersion ?? (await apiRequest<{ version: number } | undefined>(path))?.version ?? 0
    await apiRequest(`${path}/randomize`, 'POST', { expectedVersion: version }); return true
  }
  async recalculate(groupId: string, leagueId: string, year: number, day: number, onlyLastDay = false, preciseDay = false) {
    await apiRequest(`${competitionPath(groupId, leagueId, year)}/recalculate`, 'POST', { day, onlyLastDay, preciseDay }); return true
  }
}
export const leagueManagerService = new LeagueManagerService()
