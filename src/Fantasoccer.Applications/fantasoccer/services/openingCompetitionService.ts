import type { OpeningCompetitionStandings } from '../types/openingCompetition'
import type { Team } from '../types/team'
import { apiRequest } from './httpClient'
import { competitionPath } from './gameService'
import { teamService } from './teamService'

export const openingCompetitionService = {
  async get(groupId: string, leagueId: string, year: number) {
    return (await apiRequest<OpeningCompetitionStandings | undefined>(`${competitionPath(groupId, leagueId, year)}/opening/standings`)) ?? null
  },
  prepare(groupId: string, leagueId: string, year: number) {
    return apiRequest<OpeningCompetitionStandings>(`${competitionPath(groupId, leagueId, year)}/opening/prepare`, 'POST')
  },
  team(groupId: string, leagueId: string, year: number, day = 1, teamId?: string) {
    return apiRequest<Team>(`${competitionPath(groupId, leagueId, year)}/opening/team?${new URLSearchParams({ day: String(day), ...(teamId ? { teamId } : {}) })}`)
  },
  async saveTeam(groupId: string, leagueId: string, year: number, team: Team, day = 1) {
    const result = await teamService.saveFormation({ ...team, groupId, seasonIndex: year, leagueId, day, kind: 3 }, leagueId)
    if (!result.isOk) throw new Error(result.message)
  },
}
