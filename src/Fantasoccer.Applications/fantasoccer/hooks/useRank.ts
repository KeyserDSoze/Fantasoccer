import { useCallback, useEffect, useMemo, useState } from 'react'
import { rankService } from '../services/rankService'
import {
  LeagueKey,
  YearDayLeague,
  Rank,
  EnhancedRank,
  RankedTeam,
  EnhancedRankedTeam,
  RankHelper,
  enhanceRank,
  enhanceRankWithTeamPositions
} from '../types/rank'

export interface UseRankOptions {
  refreshInterval?: number
  skip?: boolean
}

export interface UseRankResult {
  rank: Rank | null
  enhancedRank: EnhancedRank | null
  loading: boolean
  error: string | null
  refetch: () => Promise<void>
  availableRounds: string[]
  getRoundRanking: (roundId: string) => RankedTeam[]
  getEnhancedRoundRanking: (roundId: string) => EnhancedRankedTeam[]
  getTeamByOwner: (roundId: string, owner: string) => RankedTeam | null
  getTeamPosition: (roundId: string, owner: string) => number
}

const useRankBase = (
  fetchRank: () => Promise<{ rank: Rank | null; enhancedRank: EnhancedRank | null }>,
  options: UseRankOptions = {}
): UseRankResult => {
  const { refreshInterval = 0, skip = false } = options

  const [rank, setRank] = useState<Rank | null>(null)
  const [enhancedRank, setEnhancedRank] = useState<EnhancedRank | null>(null)
  const [loading, setLoading] = useState<boolean>(false)
  const [error, setError] = useState<string | null>(null)

  const executeFetch = useCallback(
    async (silent = false) => {
      if (skip) {
        setRank(null)
        setEnhancedRank(null)
        setError(null)
        return
      }

      if (!silent) {
        setLoading(true)
      }
      setError(null)

      try {
  const { rank: fetchedRank, enhancedRank: fetchedEnhanced } = await fetchRank()
  setRank(fetchedRank)
  setEnhancedRank(fetchedEnhanced ?? (fetchedRank ? enhanceRank(fetchedRank) : null))
      } catch (err) {
        const message = err instanceof Error ? err.message : 'Failed to fetch rank'
        setError(message)
        console.error('Error fetching rank:', err)
      } finally {
        if (!silent) {
          setLoading(false)
        }
      }
    },
    [fetchRank, skip]
  )

  useEffect(() => {
    executeFetch(false)
  }, [executeFetch])

  useEffect(() => {
    if (refreshInterval > 0) {
      const interval = setInterval(() => executeFetch(true), refreshInterval)
      return () => clearInterval(interval)
    }
  }, [executeFetch, refreshInterval])

  const enhancedRoundMap = useMemo(() => {
    return rank ? enhanceRankWithTeamPositions(rank) : {}
  }, [rank])

  const availableRounds = useMemo(() => {
    if (enhancedRank?.availableRounds) {
      return enhancedRank.availableRounds
    }
    return rank ? RankHelper.getAvailableRounds(rank) : []
  }, [enhancedRank, rank])

  const getRoundRanking = useCallback(
    (roundId: string): RankedTeam[] => {
      if (!rank) {
        return []
      }
      return RankHelper.getTeamsSortedByPoints(rank, roundId)
    },
    [rank]
  )

  const getEnhancedRoundRanking = useCallback(
    (roundId: string): EnhancedRankedTeam[] => {
      return enhancedRoundMap[roundId] ?? []
    },
    [enhancedRoundMap]
  )

  const getTeamByOwner = useCallback(
    (roundId: string, owner: string): RankedTeam | null => {
      if (!rank) {
        return null
      }
      return RankHelper.getRankedTeamByOwner(rank, roundId, owner)
    },
    [rank]
  )

  const getTeamPosition = useCallback(
    (roundId: string, owner: string): number => {
      if (!rank) {
        return -1
      }
      return RankHelper.getTeamPosition(rank, roundId, owner)
    },
    [rank]
  )

  const refetch = useCallback(() => executeFetch(false), [executeFetch])

  return {
    rank,
    enhancedRank,
    loading,
    error,
    refetch,
    availableRounds,
    getRoundRanking,
    getEnhancedRoundRanking,
    getTeamByOwner,
    getTeamPosition
  }
}

export const useRank = (leagueKey: LeagueKey | null, options: UseRankOptions = {}): UseRankResult => {
  const fetcher = useCallback(async () => {
    if (!leagueKey) {
      return { rank: null, enhancedRank: null }
    }

    const [rank, enhancedRank] = await Promise.all([
      rankService.getRank(leagueKey),
      rankService.getEnhancedRank(leagueKey)
    ])

    return { rank, enhancedRank }
  }, [leagueKey])

  return useRankBase(fetcher, options)
}

export const useRankByComponents = (
  group: string | null,
  league: string | null,
  year: number | null,
  options: UseRankOptions = {}
): UseRankResult => {
  const hasValidKey = Boolean(group && league && year != null)

  const leagueKey: LeagueKey | null = useMemo(() => {
    if (!hasValidKey || group == null || league == null || year == null) {
      return null
    }

    return {
      group,
      league,
      year
    }
  }, [group, league, year, hasValidKey])

  return useRank(leagueKey, { ...options, skip: options.skip || !hasValidKey })
}

export const useDailyRank = (
  key: YearDayLeague | null,
  options: UseRankOptions = {}
): UseRankResult => {
  const fetcher = useCallback(async () => {
    if (!key) {
      return { rank: null, enhancedRank: null }
    }

    const [rank, enhancedRank] = await Promise.all([
      rankService.getDailyRank(key),
      rankService.getEnhancedDailyRank(key)
    ])

    return { rank, enhancedRank }
  }, [key])

  return useRankBase(fetcher, options)
}

export const useDailyRankByComponents = (
  group: string | null,
  league: string | null,
  year: number | null,
  day: number | null,
  options: UseRankOptions = {}
): UseRankResult => {
  const hasValidKey = Boolean(group && league && year != null && day != null)

  const key: YearDayLeague | null = useMemo(() => {
    if (!hasValidKey || group == null || league == null || year == null || day == null) {
      return null
    }

    return {
      group,
      league,
      year,
      day
    }
  }, [group, league, year, day, hasValidKey])

  return useDailyRank(key, { ...options, skip: options.skip || !hasValidKey })
}
