import { apiRequest } from './httpClient'
import { competitionPath } from './gameService'
import { marketRequest } from './marketRequest'
import { MarketHelper, type Market, type MarketDraft } from '../types/market'

const path = (group: string, year: number, league: string) => `${competitionPath(group, league, year)}/market`
export class MarketService {
  getMarketsByLeague(group: string, year: number, league: string): Promise<Market[]> { return apiRequest(path(group, year, league)) }
  async getMarket(group: string, year: number, league: string, id: string) { return (await this.getMarketsByLeague(group, year, league)).find(trade => trade.id === id) ?? null }
  async createMarket(group: string, year: number, league: string, draft: MarketDraft) {
    const result = await apiRequest<{ isOk: boolean; message: string }>(path(group, year, league), 'POST', marketRequest(draft))
    if (!result.isOk) throw new Error(result.message)
    return true
  }
  private async command(group: string, year: number, league: string, id: string, action: string, expectedVersion?: number, actorTeamId?: string) {
    const current = expectedVersion ?? (await this.getMarket(group, year, league, id))?.version
    if (current == null) throw new Error('Proposta non trovata.')
    const result = await apiRequest<{ isOk: boolean; message: string }>(`${path(group, year, league)}/${encodeURIComponent(id)}/${action}`, 'POST', { expectedVersion: current, actorTeamId })
    if (!result.isOk) throw new Error(result.message)
    return true
  }
  acceptMarket(group: string, year: number, league: string, id: string, version?: number, actorTeamId?: string) { return this.command(group, year, league, id, 'accept', version, actorTeamId) }
  rejectMarket(group: string, year: number, league: string, id: string, version?: number, actorTeamId?: string) { return this.command(group, year, league, id, 'reject', version, actorTeamId) }
  approveMarket(group: string, year: number, league: string, id: string, version?: number, actorTeamId?: string) { return this.command(group, year, league, id, 'approve', version, actorTeamId) }
  denyMarket(group: string, year: number, league: string, id: string, version?: number, actorTeamId?: string) { return this.command(group, year, league, id, 'deny', version, actorTeamId) }
  cancelMarket(group: string, year: number, league: string, id: string, version?: number, actorTeamId?: string) { return this.command(group, year, league, id, 'cancel', version, actorTeamId) }
  deleteMarket(group: string, year: number, league: string, id: string) { return this.cancelMarket(group, year, league, id) }
  async getMarketsForUser(group: string, year: number, league: string, email: string) { return MarketHelper.getMarketsForUser(await this.getMarketsByLeague(group, year, league), email) }
  async getPendingMarketsForApproval(group: string, year: number, league: string, email: string) { return MarketHelper.getPendingMarketsForApproval(await this.getMarketsByLeague(group, year, league), email) }
  async getActiveMarkets(group: string, year: number, league: string) { return MarketHelper.getActiveMarkets(await this.getMarketsByLeague(group, year, league)) }
  async getCompletedMarkets(group: string, year: number, league: string) { return MarketHelper.getCompletedMarkets(await this.getMarketsByLeague(group, year, league)) }
}
export const marketService = new MarketService()
