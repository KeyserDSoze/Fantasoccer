import { apiRequest } from './httpClient'
import { setPlayerImageRevision } from 'utils/playerImageRevision'

export interface ReloadTeamPlayerImagesRequest { groupId: string; year: number; teamId: string }
export interface TeamPlayerImageRefreshFailure { playerId: string; playerKey: string; playerName: string; error: string | null }
export interface TeamPlayerImageRefreshSummary {
  total: number; succeededCount: number; failedCount: number
  succeeded: { playerId: string; playerKey: string; playerName: string; revision: string }[]
  failed: TeamPlayerImageRefreshFailure[]
}
export async function reloadPlayerImage(groupId: string, year: number, playerId: string) {
  if (!playerId) throw new Error('Ricaricare la rosa prima di aggiornare la foto.')
  const result = await apiRequest<{ playerId: string; playerKey: string; revision: string }>('/api/player-images/refresh', 'POST', { groupId, year, playerId })
  setPlayerImageRevision(result.playerKey, result.revision)
}
export async function reloadTeamPlayerImages(request: ReloadTeamPlayerImagesRequest): Promise<TeamPlayerImageRefreshSummary> {
  if (!request.teamId) throw new Error('Ricaricare la squadra prima di aggiornare le foto.')
  const summary = await apiRequest<TeamPlayerImageRefreshSummary>('/api/player-images/refresh-team', 'POST', request)
  summary.succeeded.forEach(player => setPlayerImageRevision(player.playerKey, player.revision))
  return summary
}
