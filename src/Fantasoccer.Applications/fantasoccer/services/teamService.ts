// Team service using Repository Framework
import { RepositoryServices } from 'rystem.repository.client'
import {
  TeamRaw,
  Team,
  TeamKey,
  TeamKeyRaw,
  TeamDayKey,
  TeamDayKeyRaw,
  mapRawTeamToTeam,
  mapTeamKeyToRawTeamKey,
  mapTeamDayKeyToRawTeamDayKey,
  TeamKeyHelper,
  enhanceTeam,
  EnhancedTeam,
  TeamHelper,
  Player
} from 'types/team'
import { Role } from 'types/group'

export class TeamService {
  // ============================================
  // INTERNAL REPOSITORY ACCESSORS
  // ============================================

  private getSeasonRepository() {
    return RepositoryServices.Repository<TeamRaw, TeamKeyRaw>('Team')
  }

  private getDayRepository() {
    return RepositoryServices.Repository<TeamRaw, TeamDayKeyRaw>('TeamDay')
  }

  private async fetchSeasonTeamRaw(key: TeamKeyRaw): Promise<TeamRaw | null> {
    try {
      const repository = this.getSeasonRepository()
      return await repository.get(key)
    } catch (error) {
      console.error('Error fetching season team:', error)
      return null
    }
  }

  private async fetchDayTeamRaw(key: TeamDayKeyRaw): Promise<TeamRaw | null> {
    try {
      const repository = this.getDayRepository()
      return await repository.get(key)
    } catch (error) {
      console.error('Error fetching day team:', error)
      return null
    }
  }

  // ============================================
  // SEASON TEAM OPERATIONS
  // ============================================

  async getTeam(key: TeamKey): Promise<Team | null> {
    const rawKey = mapTeamKeyToRawTeamKey(key)
    const rawTeam = await this.fetchSeasonTeamRaw(rawKey)
    return rawTeam ? mapRawTeamToTeam(rawTeam) : null
  }

  async getTeamByComponents(group: string, year: number, basketId: string, email: string): Promise<Team | null> {
    const key = TeamKeyHelper.create(group, year, basketId, email)
    return this.getTeam(key)
  }

  async getEnhancedTeam(key: TeamKey): Promise<EnhancedTeam | null> {
    const team = await this.getTeam(key)
    return team ? enhanceTeam(team) : null
  }

  async getEnhancedTeamByComponents(group: string, year: number, basketId: string, email: string): Promise<EnhancedTeam | null> {
    const key = TeamKeyHelper.create(group, year, basketId, email)
    return this.getEnhancedTeam(key)
  }

  async getTeamPlayers(key: TeamKey): Promise<Player[]> {
    const team = await this.getTeam(key)
    return team ? team.players : []
  }

  async getActiveTeamPlayers(key: TeamKey): Promise<Player[]> {
    const team = await this.getTeam(key)
    return team ? TeamHelper.getActivePlayers(team) : []
  }

  async getTeamPlayersByRole(key: TeamKey, role: Role): Promise<Player[]> {
    const team = await this.getTeam(key)
    return team ? TeamHelper.getPlayersByRole(team, role) : []
  }

  // ============================================
  // DAY TEAM OPERATIONS (LINEUPS)
  // ============================================

  async getTeamDay(key: TeamDayKey): Promise<Team | null> {
    const rawKey = mapTeamDayKeyToRawTeamDayKey(key)
    const rawTeam = await this.fetchDayTeamRaw(rawKey)
    return rawTeam ? mapRawTeamToTeam(rawTeam) : null
  }

  async getTeamDayByComponents(group: string, year: number, basketId: string, day: number, email: string): Promise<Team | null> {
    const key = TeamKeyHelper.createDayKey(group, year, basketId, day, email)
    return this.getTeamDay(key)
  }

  async getEnhancedTeamDay(key: TeamDayKey): Promise<EnhancedTeam | null> {
    const team = await this.getTeamDay(key)
    return team ? enhanceTeam(team) : null
  }

  async getEnhancedTeamDayByComponents(group: string, year: number, basketId: string, day: number, email: string): Promise<EnhancedTeam | null> {
    const key = TeamKeyHelper.createDayKey(group, year, basketId, day, email)
    return this.getEnhancedTeamDay(key)
  }

  async getTeamDayPlayers(key: TeamDayKey): Promise<Player[]> {
    const team = await this.getTeamDay(key)
    return team ? team.players : []
  }

  async getActiveTeamDayPlayers(key: TeamDayKey): Promise<Player[]> {
    const team = await this.getTeamDay(key)
    return team ? TeamHelper.getActivePlayers(team) : []
  }

  async getTeamDayPlayersByRole(key: TeamDayKey, role: Role): Promise<Player[]> {
    const team = await this.getTeamDay(key)
    return team ? TeamHelper.getPlayersByRole(team, role) : []
  }

  // ============================================
  // UTILITIES
  // ============================================

  async getTeamsByBasket(groupId: string, year: number, basketId: string, owners: string[]): Promise<Team[]> {
    try {
      const teams: Team[] = []
      
      // Per ogni owner (email), recupera il team
      for (const owner of owners) {
        const key = TeamKeyHelper.create(groupId, year, basketId, owner)
        const team = await this.getTeam(key)
        if (team) {
          teams.push(team)
        }
      }
      
      return teams
    } catch (error) {
      console.error('Error fetching teams by basket:', error)
      return []
    }
  }

  async getTeamNetCost(key: TeamKey): Promise<number> {
    const team = await this.getTeam(key)
    return team ? TeamHelper.getNetCost(team) : 0
  }

  async getTeamDayNetCost(key: TeamDayKey): Promise<number> {
    const team = await this.getTeamDay(key)
    return team ? TeamHelper.getNetCost(team) : 0
  }
}

export const teamService = new TeamService()
