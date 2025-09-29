import { useCallback, useEffect, useRef, useState } from 'react'
import { liveGroupService } from '../services/liveGroupService'
import {
  LiveGroup,
  EnhancedLiveGroup,
  enhanceLiveGroup,
  LiveGroupHelper,
  LiveLeague,
  EnhancedLiveLeague,
  enhanceLiveLeague,
  LiveLeagueHelper
} from '../types/liveGroup'
import { CalendarDay, CalendarGame } from '../types/calendar'

// ============================================
// LIVE GROUP HOOK
// ============================================

export interface UseLiveGroupOptions {
  refreshInterval?: number
}

export interface UseLiveGroupResult {
  liveGroup: LiveGroup | null
  enhancedLiveGroup: EnhancedLiveGroup | null
  loading: boolean
  refreshing: boolean
  error: string | null
  refetch: () => Promise<void>
  getLeagueById: (leagueId: string) => LiveLeague | null
}

export function useLiveGroup(groupId: string | null, options: UseLiveGroupOptions = {}): UseLiveGroupResult {
  const [liveGroup, setLiveGroup] = useState<LiveGroup | null>(null)
  const [enhancedLiveGroup, setEnhancedLiveGroup] = useState<EnhancedLiveGroup | null>(null)
  const [loading, setLoading] = useState<boolean>(false)
  const [refreshing, setRefreshing] = useState<boolean>(false)
  const [error, setError] = useState<string | null>(null)
  const hasDataRef = useRef<boolean>(false)

  const { refreshInterval = 0 } = options

  const fetchLiveGroup = useCallback(async (forceRefresh = false) => {
    if (!groupId) {
      setLiveGroup(null)
      setEnhancedLiveGroup(null)
      setError(null)
      setLoading(false)
      setRefreshing(false)
      hasDataRef.current = false
      return
    }

    const showInitialLoader = !hasDataRef.current
    if (showInitialLoader) {
      setLoading(true)
    } else if (forceRefresh) {
      setRefreshing(true)
    }
    setError(null)

    try {
      const groupData = await liveGroupService.getLiveGroup(groupId, { forceRefresh })
      const enhancedGroupData = groupData ? enhanceLiveGroup(groupData) : null

      setLiveGroup(groupData)
      setEnhancedLiveGroup(enhancedGroupData)
      hasDataRef.current = Boolean(groupData || enhancedGroupData)
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to fetch live group'
      setError(message)
      console.error('Error fetching live group:', err)
    } finally {
      if (showInitialLoader) {
        setLoading(false)
      }
      if (forceRefresh) {
        setRefreshing(false)
      }
    }
  }, [groupId])

  useEffect(() => {
    void fetchLiveGroup(true)
  }, [fetchLiveGroup])

  useEffect(() => {
    if (refreshInterval > 0) {
      const interval = setInterval(() => {
        void fetchLiveGroup(true)
      }, refreshInterval)
      return () => clearInterval(interval)
    }
  }, [fetchLiveGroup, refreshInterval])

  const getLeagueById = useCallback((leagueId: string): LiveLeague | null => {
    if (!liveGroup) {
      return null
    }

    return LiveGroupHelper.getLeagueById(liveGroup, leagueId)
  }, [liveGroup])

  return {
    liveGroup,
    enhancedLiveGroup,
    loading,
    refreshing,
    error,
    refetch: () => fetchLiveGroup(true),
    getLeagueById
  }
}

// ============================================
// LIVE LEAGUE HOOK
// ============================================

export interface UseLiveLeagueOptions {
  refreshInterval?: number
}

export interface UseLiveLeagueResult {
  league: LiveLeague | null
  enhancedLeague: EnhancedLiveLeague | null
  loading: boolean
  error: string | null
  refetch: () => Promise<void>
  roundKeys: string[]
  pendingGames: CalendarGame[]
  latestRoundKey: string | null
}

export function useLiveLeague(
  groupId: string | null,
  leagueId: string | null,
  options: UseLiveLeagueOptions = {}
): UseLiveLeagueResult {
  const [league, setLeague] = useState<LiveLeague | null>(null)
  const [enhancedLeague, setEnhancedLeague] = useState<EnhancedLiveLeague | null>(null)
  const [loading, setLoading] = useState<boolean>(false)
  const [error, setError] = useState<string | null>(null)

  const { refreshInterval = 0 } = options

  const fetchLeague = useCallback(async () => {
    if (!groupId || !leagueId) {
      setLeague(null)
      setEnhancedLeague(null)
      setError(null)
      return
    }

    setLoading(true)
    setError(null)

    try {
      const [leagueData, enhancedLeagueData] = await Promise.all([
        liveGroupService.getLiveLeague(groupId, leagueId),
        liveGroupService.getEnhancedLiveLeague(groupId, leagueId)
      ])

      setLeague(leagueData)
      setEnhancedLeague(enhancedLeagueData ?? (leagueData ? enhanceLiveLeague(leagueData) : null))
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to fetch live league'
      setError(message)
      console.error('Error fetching live league:', err)
    } finally {
      setLoading(false)
    }
  }, [groupId, leagueId])

  useEffect(() => {
    fetchLeague()
  }, [fetchLeague])

  useEffect(() => {
    if (refreshInterval > 0) {
      const interval = setInterval(fetchLeague, refreshInterval)
      return () => clearInterval(interval)
    }
  }, [fetchLeague, refreshInterval])

  const roundKeys = enhancedLeague?.roundKeys ?? (league ? LiveLeagueHelper.getRoundKeys(league) : [])
  const pendingGames = enhancedLeague?.pendingGames ?? (league ? LiveLeagueHelper.getPendingGames(league) : [])
  const latestRoundKey = enhancedLeague?.latestRoundKey ?? (league ? LiveLeagueHelper.getLatestRoundKey(league) : null)

  return {
    league,
    enhancedLeague,
    loading,
    error,
    refetch: fetchLeague,
    roundKeys,
    pendingGames,
    latestRoundKey
  }
}

// ============================================
// LIVE ROUND HOOK
// ============================================

export interface UseLiveRoundOptions {
  refreshInterval?: number
}

export interface UseLiveRoundResult {
  round: CalendarDay | null
  games: CalendarGame[]
  loading: boolean
  error: string | null
  refetch: () => Promise<void>
}

export function useLiveRound(
  groupId: string | null,
  leagueId: string | null,
  roundKey: string | null,
  options: UseLiveRoundOptions = {}
): UseLiveRoundResult {
  const [round, setRound] = useState<CalendarDay | null>(null)
  const [games, setGames] = useState<CalendarGame[]>([])
  const [loading, setLoading] = useState<boolean>(false)
  const [error, setError] = useState<string | null>(null)

  const { refreshInterval = 0 } = options

  const fetchRound = useCallback(async () => {
    if (!groupId || !leagueId || !roundKey) {
      setRound(null)
      setGames([])
      setError(null)
      return
    }

    setLoading(true)
    setError(null)

    try {
      const roundData = await liveGroupService.getLiveRound(groupId, leagueId, roundKey)
      setRound(roundData)
      setGames(roundData?.games ?? [])
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to fetch live round'
      setError(message)
      console.error('Error fetching live round:', err)
    } finally {
      setLoading(false)
    }
  }, [groupId, leagueId, roundKey])

  useEffect(() => {
    fetchRound()
  }, [fetchRound])

  useEffect(() => {
    if (refreshInterval > 0) {
      const interval = setInterval(fetchRound, refreshInterval)
      return () => clearInterval(interval)
    }
  }, [fetchRound, refreshInterval])

  return {
    round,
    games,
    loading,
    error,
    refetch: fetchRound
  }
}
