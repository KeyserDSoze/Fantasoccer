import { enhanceGroup, GroupHelper, type Group, type League } from '../types/group'
import { apiRequest } from './httpClient'

export class GroupService {
  getGroup(id: string): Promise<Group> { return apiRequest(`/api/groups/${encodeURIComponent(id)}`) }
  getUserGroups(): Promise<Group[]> { return apiRequest('/api/groups') }
  updateGroup(id: string, group: Group): Promise<Group> { return apiRequest(`/api/groups/${encodeURIComponent(id)}`, 'PUT', group) }
  async getEnhancedGroup(id: string) { return enhanceGroup(await this.getGroup(id)) }
  async getGroupLeagues(id: string): Promise<League[]> { return (await this.getGroup(id)).leagues }
  async getLeague(groupId: string, leagueId: string) { return (await this.getGroup(groupId)).leagues.find(league => league.id === leagueId) ?? null }
  async getAnnualLeague(groupId: string, id: string, year: number) { return GroupHelper.getAnnualLeague(await this.getGroup(groupId), id, year) }
  async getAnnualLeagues(groupId: string, year: number) { return GroupHelper.getAnnualLeagues(await this.getGroup(groupId), year) }
  async getGroupBaskets(id: string) { return (await this.getGroup(id)).baskets }
  async getBasket(groupId: string, id: string) { return (await this.getGroup(groupId)).baskets.find(basket => basket.id === id) ?? null }
  async getBasketIdForOwner(groupId: string, owner: string, year?: number) { return GroupHelper.getBasketId(await this.getGroup(groupId), owner, year) }
  async getGroupUsers(id: string) { return (await this.getGroup(id)).users }
  async getActiveUsers(id: string) { return GroupHelper.getActiveUsers(await this.getGroup(id)) }
  async getAdminUsers(id: string) { return GroupHelper.getAdminUsers(await this.getGroup(id)) }
  async getUserTeam(groupId: string, basketId: string, year: number, email: string) { return (await this.getBasket(groupId, basketId))?.years.find(value => value.year === year)?.teams.find(team => GroupHelper.isOwner(team, email)) ?? null }
  async isTeamOwner(groupId: string, basketId: string, year: number, email: string) { return (await this.getUserTeam(groupId, basketId, year, email)) !== null }
  async isGroupAvailable(id: string) { await this.getGroup(id); return true }
  async getGroupAvailableYears(id: string) { const group = await this.getGroup(id); return [...new Set([...group.leagues.flatMap(league => league.years.map(year => year.year)), ...group.baskets.flatMap(basket => basket.years.map(year => year.year))])].sort((a, b) => b - a) }
}
export const groupService = new GroupService()
