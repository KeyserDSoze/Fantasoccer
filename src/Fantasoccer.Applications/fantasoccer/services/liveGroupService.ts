import { apiRequest } from './httpClient'
import { authService } from './authService'
import { tokenStorageService } from './tokenStorageService'
import { getCurrentSeasonYear } from '../utils/season'
import { enhanceLiveGroup, enhanceLiveLeague, LiveGroupHelper, LiveLeagueHelper, type LiveGroup } from '../types/liveGroup'

export class LiveGroupService {
  async getLiveGroup(groupId: string, _options: { forceRefresh?: boolean } = {}): Promise<LiveGroup | null> {
    if (!groupId) return null
    const token = await tokenStorageService.ensureValidToken()
    if (!token) throw new Error('Accesso richiesto.')
    const year = (await authService.getUserInfo(token)).state.year ?? getCurrentSeasonYear()
    return apiRequest(`/api/groups/${encodeURIComponent(groupId)}/live?year=${year}`)
  }
  async getEnhancedLiveGroup(groupId: string, options: { forceRefresh?: boolean } = {}) { const group = await this.getLiveGroup(groupId, options); return group ? enhanceLiveGroup(group) : null }
  async getLiveLeagues(groupId: string, options: { forceRefresh?: boolean } = {}) { return (await this.getLiveGroup(groupId, options))?.leagues ?? [] }
  async getEnhancedLiveLeagues(groupId: string, options: { forceRefresh?: boolean } = {}) { return (await this.getEnhancedLiveGroup(groupId, options))?.leaguesWithRounds ?? [] }
  async getLiveLeague(groupId: string, leagueId: string, options: { forceRefresh?: boolean } = {}) { const group = await this.getLiveGroup(groupId, options); return group ? LiveGroupHelper.getLeagueById(group, leagueId) : null }
  async getEnhancedLiveLeague(groupId: string, leagueId: string, options: { forceRefresh?: boolean } = {}) { const league = await this.getLiveLeague(groupId, leagueId, options); return league ? enhanceLiveLeague(league) : null }
  async getLiveRound(groupId: string, leagueId: string, key: string) { const league = await this.getLiveLeague(groupId, leagueId); return league ? LiveLeagueHelper.getRound(league, key) : null }
  async getPendingGames(groupId: string) { return (await this.getLiveLeagues(groupId)).flatMap(league => LiveLeagueHelper.getPendingGames(league)) }
}
export const liveGroupService = new LiveGroupService()
