// Live group service using Repository Framework
import { RepositoryServices } from 'rystem.repository.client'
import {
  LiveGroupRaw,
  mapRawLiveGroupToLiveGroup,
  LiveGroup,
  enhanceLiveGroup,
  EnhancedLiveGroup,
  LiveLeague,
  EnhancedLiveLeague,
  LiveGroupHelper,
  LiveLeagueHelper,
  enhanceLiveLeague
} from 'types/liveGroup'
import { CalendarDay, CalendarGame } from 'types/calendar'

export class LiveGroupService {
  private getRepository() {
    return RepositoryServices.Repository<LiveGroupRaw, string>('LiveGroup')
  }

  private async fetchRaw(groupId: string): Promise<LiveGroupRaw | null> {
    if (!groupId) {
      return null
    }

    try {
      const repository = this.getRepository()
      return await repository.get(groupId)
    } catch (error) {
      console.error('Error fetching live group:', error)
      return null
    }
  }

  async getLiveGroup(groupId: string): Promise<LiveGroup | null> {
    const raw = await this.fetchRaw(groupId)
    return raw ? mapRawLiveGroupToLiveGroup(raw) : null
  }

  async getEnhancedLiveGroup(groupId: string): Promise<EnhancedLiveGroup | null> {
    const group = await this.getLiveGroup(groupId)
    return group ? enhanceLiveGroup(group) : null
  }

  async getLiveLeagues(groupId: string): Promise<LiveLeague[]> {
    const group = await this.getLiveGroup(groupId)
    return group?.leagues ?? []
  }

  async getEnhancedLiveLeagues(groupId: string): Promise<EnhancedLiveLeague[]> {
    const leagues = await this.getLiveLeagues(groupId)
    return leagues.map(enhanceLiveLeague)
  }

  async getLiveLeague(groupId: string, leagueId: string): Promise<LiveLeague | null> {
    const group = await this.getLiveGroup(groupId)
    if (!group) {
      return null
    }

  return LiveGroupHelper.getLeagueById(group, leagueId)
  }

  async getEnhancedLiveLeague(groupId: string, leagueId: string): Promise<EnhancedLiveLeague | null> {
    const league = await this.getLiveLeague(groupId, leagueId)
    return league ? enhanceLiveLeague(league) : null
  }

  async getLiveRound(groupId: string, leagueId: string, roundKey: string): Promise<CalendarDay | null> {
    const league = await this.getLiveLeague(groupId, leagueId)
    if (!league) {
      return null
    }

    return LiveLeagueHelper.getRound(league, roundKey)
  }

  async getPendingGames(groupId: string): Promise<CalendarGame[]> {
    const leagues = await this.getLiveLeagues(groupId)
    return leagues.flatMap(league => LiveLeagueHelper.getPendingGames(league))
  }
}

export const liveGroupService = new LiveGroupService()
