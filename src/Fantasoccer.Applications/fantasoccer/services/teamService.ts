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
import { Role, LeagueSetting } from 'types/group'
import { rankService } from './rankService'

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

  /**
   * Calculate moneyFromRank based on rank data and league settings
   * Matches C# logic: team.MoneyFromRank = teamInRank.Goal * league.Settings.MoneyForGoal + teamInRank.SufferedGoal * league.Settings.MoneyForSufferedGoal
   */
  private async calculateMoneyFromRank(
    team: Team,
    groupId: string,
    leagueId: string,
    year: number,
    leagueSettings?: LeagueSetting
  ): Promise<number> {
    // Se non ci sono settings o non ci sono soldi da assegnare, ritorna 0
    if (!leagueSettings || (leagueSettings.moneyForGoal === 0 && leagueSettings.moneyForSufferedGoal === 0)) {
      return 0
    }

    try {
      // Recupera la classifica
      const rank = await rankService.getRankByComponents(groupId, leagueId, year)
      if (!rank) {
        return 0
      }

      // Cerca il team nella classifica (cerca in tutti i gironi)
      let teamInRank = null
      for (const roundTeams of Object.values(rank.rounds)) {
        teamInRank = roundTeams.find(x => x.owner === team.owner)
        if (teamInRank) break
      }

      if (!teamInRank) {
        return 0
      }

      // Calcola il moneyFromRank come in C#
      const moneyFromRank = 
        teamInRank.goal * leagueSettings.moneyForGoal + 
        teamInRank.sufferedGoal * leagueSettings.moneyForSufferedGoal

      return moneyFromRank
    } catch (error) {
      console.error('Error calculating moneyFromRank:', error)
      return 0
    }
  }

  async getEnhancedTeam(
    key: TeamKey, 
    leagueId?: string, 
    leagueSettings?: LeagueSetting
  ): Promise<EnhancedTeam | null> {
    const team = await this.getTeam(key)
    if (!team) return null

    // Se moneyFromRank non è valorizzato (è 0 o undefined) e abbiamo i parametri necessari, calcolalo
    if ((!team.moneyFromRank || team.moneyFromRank === 0) && leagueId && leagueSettings) {
      team.moneyFromRank = await this.calculateMoneyFromRank(team, key.group, leagueId, key.year, leagueSettings)
    }

    return enhanceTeam(team)
  }

  async getEnhancedTeamByComponents(
    group: string, 
    year: number, 
    basketId: string, 
    email: string,
    leagueId?: string,
    leagueSettings?: LeagueSetting
  ): Promise<EnhancedTeam | null> {
    const key = TeamKeyHelper.create(group, year, basketId, email)
    return this.getEnhancedTeam(key, leagueId, leagueSettings)
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

  async getEnhancedTeamDay(
    key: TeamDayKey,
    leagueId?: string,
    leagueSettings?: LeagueSetting
  ): Promise<EnhancedTeam | null> {
    const team = await this.getTeamDay(key)
    if (!team) return null

    // Se moneyFromRank non è valorizzato (è 0 o undefined) e abbiamo i parametri necessari, calcolalo
    if ((!team.moneyFromRank || team.moneyFromRank === 0) && leagueId && leagueSettings) {
      team.moneyFromRank = await this.calculateMoneyFromRank(team, key.group, leagueId, key.year, leagueSettings)
    }

    return enhanceTeam(team)
  }

  async getEnhancedTeamDayByComponents(
    group: string, 
    year: number, 
    basketId: string, 
    day: number, 
    email: string,
    leagueId?: string,
    leagueSettings?: LeagueSetting
  ): Promise<EnhancedTeam | null> {
    const key = TeamKeyHelper.createDayKey(group, year, basketId, day, email)
    return this.getEnhancedTeamDay(key, leagueId, leagueSettings)
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

  async getEnhancedTeamsByBasket(
    groupId: string, 
    year: number, 
    basketId: string, 
    owners: string[],
    leagueId?: string,
    leagueSettings?: LeagueSetting
  ): Promise<EnhancedTeam[]> {
    try {
      const teams: EnhancedTeam[] = []
      
      // Per ogni owner (email), recupera il team enhanced
      for (const owner of owners) {
        const key = TeamKeyHelper.create(groupId, year, basketId, owner)
        const enhancedTeam = await this.getEnhancedTeam(key, leagueId, leagueSettings)
        if (enhancedTeam) {
          teams.push(enhancedTeam)
        }
      }
      
      return teams
    } catch (error) {
      console.error('Error fetching enhanced teams by basket:', error)
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
