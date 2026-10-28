import type { GameWrapper, LiveTeam } from '../types/game'
import type { Team } from '../types/team'
import { apiRequest } from './httpClient'
import { formationRequest } from './gameFormation'

export interface OptimalFormationPlayer { playerId: string; playerKey: string; playerName: string; role: number; position: number; projection: number; confidence: number }
export interface OptimalFormationResult { players: OptimalFormationPlayer[]; module: string; score: number; confidence: number; warnings: string[] }
export type GameEditMode = 'formation' | 'live' | 'view'
export interface LineupCopySource { leagueId: string; leagueName: string; teamId: string; teamName: string; hasLineup: boolean; version: number; lastUpdateUtc: string | null }
export interface GameContext { groupId: string; leagueId: string; year: number; gameId: string; editMode: GameEditMode }
export const competitionPath = (group: string, league: string, year: number) => `/api/competitions/${encodeURIComponent(group)}/${encodeURIComponent(league)}/${year}`
const gamePath = (context: Omit<GameContext, 'editMode'>) => `${competitionPath(context.groupId, context.leagueId, context.year)}/games/${encodeURIComponent(context.gameId)}`
export const gameService = {
  getGame(params: Omit<GameContext, 'editMode'> & { editMode?: GameEditMode; asAdmin?: boolean }) {
    return apiRequest<GameWrapper>(`${gamePath(params)}?${new URLSearchParams({ editMode: params.editMode ?? 'view', asAdmin: String(params.asAdmin ?? false) })}`)
  },
  saveTeam(context: GameContext, team: LiveTeam, asAdmin = false) {
    return apiRequest<{ isOk: boolean; message: string; team: Team }>(`${gamePath(context)}/formation`, 'PUT', formationRequest(team, context.editMode === 'live', asAdmin))
  },
  // teamId selects the exact team (several teams per user, SuperAdmin acting for others).
  getOptimalFormation(params: { groupId: string; leagueId: string; year: number; day: number; owner: string; teamId?: string; asAdmin?: boolean }) {
    const query = new URLSearchParams({ day: String(params.day), owner: params.owner, asAdmin: String(params.asAdmin ?? false) })
    if (params.teamId) query.set('teamId', params.teamId)
    return apiRequest<OptimalFormationResult>(`${competitionPath(params.groupId, params.leagueId, params.year)}/optimal-formation?${query}`)
  },
  // Competitions of the same day that share this team's authoritative roster.
  getCopySources(params: { groupId: string; leagueId: string; year: number; day: number; teamId: string }) {
    return apiRequest<LineupCopySource[]>(`${competitionPath(params.groupId, params.leagueId, params.year)}/lineups/copy-sources?${new URLSearchParams({ day: String(params.day), teamId: params.teamId })}`)
  },
  // One-off copy; the target lineup goes through every save validation.
  copyFormation(context: GameContext, target: LiveTeam, source: { leagueId: string; teamId: string }, asAdmin = false) {
    return apiRequest<{ isOk: boolean; message: string; team: Team }>(`${gamePath(context)}/formation/copy`, 'POST', {
      teamId: target.teamId, sourceLeagueId: source.leagueId, sourceTeamId: source.teamId, expectedVersion: target.version,
      sourceVersion: target.sourceVersion, live: context.editMode === 'live', asAdmin })
  },
}
export default gameService
