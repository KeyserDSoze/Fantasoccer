import type { LeagueParticipants, QualificationPreview, QualifiedTeam } from '../types/participation'
import { apiRequest } from './httpClient'
import { competitionPath } from './gameService'

// Explicit participants of a league season. Derived leagues admit teams only by
// confirmed qualification; once confirmed they do not follow later standings.
export const participationService = {
  getParticipants: (group: string, league: string, year: number) =>
    apiRequest<LeagueParticipants>(`${competitionPath(group, league, year)}/participants`),
  admit: (group: string, league: string, year: number, teamId: string) =>
    apiRequest<void>(`${competitionPath(group, league, year)}/participants`, 'POST', { teamId }),
  withdraw: (group: string, league: string, year: number, teamId: string) =>
    apiRequest<void>(`${competitionPath(group, league, year)}/participants/${encodeURIComponent(teamId)}`, 'DELETE'),
  getQualification: (group: string, league: string, year: number) =>
    apiRequest<QualificationPreview>(`${competitionPath(group, league, year)}/qualification`),
  confirmQualification: (group: string, league: string, year: number, teams: QualifiedTeam[]) =>
    apiRequest<LeagueParticipants>(`${competitionPath(group, league, year)}/qualification/confirm`, 'POST', { teams }),
}

export default participationService
