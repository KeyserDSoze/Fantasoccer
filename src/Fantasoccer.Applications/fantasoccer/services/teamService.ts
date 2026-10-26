import { TeamKeyHelper, enhanceTeam, TeamHelper, type Team, type TeamKey, type TeamDayKey, type EnhancedTeam } from '../types/team'
import type { Role, LeagueSetting } from '../types/group'
import { apiRequest } from './httpClient'

export class TeamService {
  private async roster(key: TeamKey, day = 0): Promise<Team | null> {
    const query = new URLSearchParams({ groupId: key.group, basketId: key.basketId, year: String(key.year), owner: key.email, kind: day ? '1' : '0', day: String(day) })
    return (await apiRequest<Team | undefined>(`/api/teams/roster?${query}`)) ?? null
  }
  getTeam(key: TeamKey) { return this.roster(key) }
  getTeamByComponents(group: string, year: number, basketId: string, email: string) { return this.getTeam(TeamKeyHelper.create(group, year, basketId, email)) }
  async getEnhancedTeam(key: TeamKey, _leagueId?: string, _settings?: LeagueSetting): Promise<EnhancedTeam | null> {
    const team = await this.getTeam(key); return team ? enhanceTeam(team) : null
  }
  getEnhancedTeamByComponents(group: string, year: number, basketId: string, email: string, leagueId?: string, settings?: LeagueSetting) { return this.getEnhancedTeam(TeamKeyHelper.create(group, year, basketId, email), leagueId, settings) }
  async getTeamPlayers(key: TeamKey) { return (await this.getTeam(key))?.players ?? [] }
  async getActiveTeamPlayers(key: TeamKey) { const team = await this.getTeam(key); return team ? TeamHelper.getActivePlayers(team) : [] }
  async getTeamPlayersByRole(key: TeamKey, role: Role) { const team = await this.getTeam(key); return team ? TeamHelper.getPlayersByRole(team, role) : [] }
  getTeamDay(key: TeamDayKey) { return this.roster(key, key.day) }
  getTeamDayByComponents(group: string, year: number, basketId: string, day: number, email: string) { return this.getTeamDay(TeamKeyHelper.createDayKey(group, year, basketId, day, email)) }
  async getEnhancedTeamDay(key: TeamDayKey, _leagueId?: string, _settings?: LeagueSetting): Promise<EnhancedTeam | null> {
    const team = await this.getTeamDay(key); return team ? enhanceTeam(team) : null
  }
  getEnhancedTeamDayByComponents(group: string, year: number, basketId: string, day: number, email: string, leagueId?: string, settings?: LeagueSetting) { return this.getEnhancedTeamDay(TeamKeyHelper.createDayKey(group, year, basketId, day, email), leagueId, settings) }
  async getTeamDayPlayers(key: TeamDayKey) { return (await this.getTeamDay(key))?.players ?? [] }
  async getActiveTeamDayPlayers(key: TeamDayKey) { const team = await this.getTeamDay(key); return team ? TeamHelper.getActivePlayers(team) : [] }
  async getTeamDayPlayersByRole(key: TeamDayKey, role: Role) { const team = await this.getTeamDay(key); return team ? TeamHelper.getPlayersByRole(team, role) : [] }
  async getTeamsByBasket(group: string, year: number, basketId: string, owners: string[]) { return (await Promise.all(owners.map(email => this.getTeamByComponents(group, year, basketId, email)))).filter((team): team is Team => !!team) }
  async getEnhancedTeamsByBasket(group: string, year: number, basketId: string, owners: string[], _leagueId?: string, _settings?: LeagueSetting) { return (await this.getTeamsByBasket(group, year, basketId, owners)).map(enhanceTeam) }
  async getTeamNetCost(key: TeamKey) { const team = await this.getTeam(key); return team ? TeamHelper.getCost(team) : 0 }
  async getTeamDayNetCost(key: TeamDayKey) { const team = await this.getTeamDay(key); return team ? TeamHelper.getCost(team) : 0 }
  async saveFormation(team: Team, leagueId?: string, live = false, asAdmin = false) {
    if (!team.groupId || !team.basketId || !team.seasonIndex || team.version == null || team.players.some(player => player.status === 0 && !player.playerId))
      throw new Error('Ricaricare la rosa PostgreSQL prima di salvare.')
    return apiRequest<{ isOk: boolean; message: string; team: Team }>('/api/teams/formation', 'PUT', {
      groupId: team.groupId, basketId: team.basketId, year: team.seasonIndex, owner: team.owner, teamId: team.teamId,
      day: team.day ?? 0, kind: team.kind ?? 0, leagueId: team.leagueId || leagueId || null, expectedVersion: team.version, sourceVersion: team.sourceVersion, live, asAdmin,
      positions: team.players.filter(player => player.status === 0).map(player => ({ playerId: player.playerId, position: player.position })),
    })
  }
}
export const teamService = new TeamService()
