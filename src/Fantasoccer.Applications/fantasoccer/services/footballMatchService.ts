import type { FootballMatch } from '../types/footballMatch'
import { apiRequest } from './httpClient'

export interface MatchRequest { year: number; day: number; ordinal: number; groupId?: string | null; leagueId?: string | null }

export const footballMatchService = {
  getMatch({ year, day, ordinal, groupId, leagueId }: MatchRequest) {
    const query = groupId && leagueId ? `?${new URLSearchParams({ groupId, leagueId })}` : ''
    return apiRequest<FootballMatch>(`/api/football/${year}/days/${day}/matches/${ordinal}${query}`)
  },
}
export default footballMatchService
