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
  private cache = new Map<string, { liveGroup: LiveGroup | null; enhancedGroup: EnhancedLiveGroup | null }>()

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

  async getLiveGroup(groupId: string, options: { forceRefresh?: boolean } = {}): Promise<LiveGroup | null> {
    if (!groupId) {
      return null
    }

    const { forceRefresh = false } = options
    const cachedEntry = this.cache.get(groupId)

    if (!forceRefresh && cachedEntry?.liveGroup) {
      return cachedEntry.liveGroup
    }

    const raw = await this.fetchRaw(groupId)

    if (!raw) {
      if (cachedEntry) {
        return cachedEntry.liveGroup
      }
      this.cache.set(groupId, { liveGroup: null, enhancedGroup: null })
      return null
    }

    const liveGroup = mapRawLiveGroupToLiveGroup(raw)
    const enhancedGroup = enhanceLiveGroup(liveGroup)
    this.cache.set(groupId, { liveGroup, enhancedGroup })
    return liveGroup
  }

  async getEnhancedLiveGroup(groupId: string, options: { forceRefresh?: boolean } = {}): Promise<EnhancedLiveGroup | null> {
    if (!groupId) {
      return null
    }

    const { forceRefresh = false } = options
    if (!forceRefresh) {
      const cachedEntry = this.cache.get(groupId)
      if (cachedEntry?.enhancedGroup) {
        return cachedEntry.enhancedGroup
      }
    }

    const group = await this.getLiveGroup(groupId, options)
    if (!group) {
      return null
    }

    const enhancedGroup = enhanceLiveGroup(group)
    this.cache.set(groupId, { liveGroup: group, enhancedGroup })
    return enhancedGroup
  }

  async getLiveLeagues(groupId: string, options: { forceRefresh?: boolean } = {}): Promise<LiveLeague[]> {
    const group = await this.getLiveGroup(groupId, options)
    return group?.leagues ?? []
  }

  async getEnhancedLiveLeagues(groupId: string, options: { forceRefresh?: boolean } = {}): Promise<EnhancedLiveLeague[]> {
    const enhancedGroup = await this.getEnhancedLiveGroup(groupId, options)
    return enhancedGroup?.leaguesWithRounds ?? []
  }

  async getLiveLeague(groupId: string, leagueId: string, options: { forceRefresh?: boolean } = {}): Promise<LiveLeague | null> {
    const group = await this.getLiveGroup(groupId, options)
    if (!group) {
      return null
    }

    return LiveGroupHelper.getLeagueById(group, leagueId)
  }

  async getEnhancedLiveLeague(groupId: string, leagueId: string, options: { forceRefresh?: boolean } = {}): Promise<EnhancedLiveLeague | null> {
    const { forceRefresh = false } = options
    const league = await this.getLiveLeague(groupId, leagueId, options)
    if (!league) {
      return null
    }

    const cachedEntry = this.cache.get(groupId)
    if (cachedEntry?.enhancedGroup) {
      const enhancedLeague = cachedEntry.enhancedGroup.leaguesWithRounds.find((item) => item.id === leagueId)
      if (enhancedLeague && !forceRefresh) {
        return enhancedLeague
      }
    }

    return enhanceLiveLeague(league)
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
