import { buildApiUrl, getCommonHeaders } from 'configurations/apiConfig'
import { tokenStorageService } from './tokenStorageService'
import { mapOpeningCompetitionStandings, OpeningCompetitionStandings, OpeningCompetitionStandingsRaw } from 'types/openingCompetition'
import { mapRawTeamToTeam, mapTeamToRawTeam, Team, TeamRaw } from 'types/team'

export const openingCompetitionService = {
  async prepare(groupId: string, leagueId: string, year: number): Promise<OpeningCompetitionStandings> {
    const token = await tokenStorageService.ensureValidToken()
    const response = await fetch(buildApiUrl('/OpeningCompetition/Prepare', { groupId, leagueId, year }), {
      headers: getCommonHeaders(true, token ?? undefined)
    })
    if (!response.ok) throw new Error(response.status === 404 ? 'La competizione di apertura non è disponibile.' : `Errore ${response.status} nel caricamento della competizione.`)
    return mapOpeningCompetitionStandings(await response.json() as OpeningCompetitionStandingsRaw)
  },
  async team(groupId: string, leagueId: string, year: number, day = 1): Promise<Team> {
    const token = await tokenStorageService.ensureValidToken()
    const response = await fetch(buildApiUrl('/OpeningCompetition/Team', { groupId, leagueId, year, day }), { headers: getCommonHeaders(true, token ?? undefined) })
    if (!response.ok) throw new Error(response.status === 404 ? 'Squadra di apertura non disponibile.' : `Errore ${response.status} nel caricamento della squadra.`)
    return mapRawTeamToTeam(await response.json() as TeamRaw)
  },
  async saveTeam(groupId: string, leagueId: string, year: number, team: Team, day = 1): Promise<void> {
    const token = await tokenStorageService.ensureValidToken()
    const response = await fetch(buildApiUrl('/OpeningCompetition/SaveTeam', { groupId, leagueId, year, day }), {
      method: 'POST',
      headers: { ...getCommonHeaders(true, token ?? undefined), 'Content-Type': 'application/json' },
      body: JSON.stringify(mapTeamToRawTeam(team))
    })
    if (!response.ok) throw new Error(`Errore ${response.status} nel salvataggio della formazione iniziale.`)
    const result = await response.json() as { i?: boolean; m?: string }
    if (result.i === false) throw new Error(result.m || 'Formazione non valida.')
  }
}
