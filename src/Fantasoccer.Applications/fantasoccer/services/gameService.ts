import type { GameWrapper, LiveTeam } from '../types/game'
import type { Team } from '../types/team'
import { apiRequest } from './httpClient'
import { formationRequest } from './gameFormation'

export interface OptimalFormationPlayer { playerId: string; playerKey: string; playerName: string; role: number; position: number; projection: number; confidence: number }
export interface OptimalFormationResult { players: OptimalFormationPlayer[]; module: string; score: number; confidence: number; warnings: string[] }
export type GameEditMode = 'formation' | 'live' | 'view'
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
  getOptimalFormation(params: { groupId: string; leagueId: string; year: number; day: number; owner: string; asAdmin?: boolean }) {
    return apiRequest<OptimalFormationResult>(`${competitionPath(params.groupId, params.leagueId, params.year)}/optimal-formation?${new URLSearchParams({ day: String(params.day), owner: params.owner, asAdmin: String(params.asAdmin ?? false) })}`)
  },
}
export default gameService
