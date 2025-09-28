import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { realVoteService, VoteLookupOptions } from '../services/realVoteService'
import {
  EnhancedVotedRealPlayerWrapper,
  VotedRealPlayer,
  VotedRealPlayerRoleBuckets,
  YearDayKey,
} from '../types/serieA'
import { Role } from '../types/group'

export type VoteType = 'live' | 'official'

interface BaseState {
  wrapper: EnhancedVotedRealPlayerWrapper | null
  isLoading: boolean
  error: string | null
  lastUpdated: Date | null
  key: YearDayKey | null
}

const createInitialState = (enabled: boolean): BaseState => ({
  wrapper: null,
  isLoading: enabled,
  error: null,
  lastUpdated: null,
  key: null,
})

export interface UseRealVotesOptions {
  year?: number
  day: number | null
  autoRefresh?: boolean
  refreshInterval?: number
  enabled?: boolean
}

export interface UseRealVotesResult {
  players: VotedRealPlayer[]
  enhancedWrapper: EnhancedVotedRealPlayerWrapper | null
  buckets: VotedRealPlayerRoleBuckets
  playersWithVote: VotedRealPlayer[]
  playersWithoutVote: VotedRealPlayer[]
  totalPlayers: number
  votedPlayersCount: number
  isLoading: boolean
  error: string | null
  lastUpdated: Date | null
  refetch: () => Promise<void>
  search: (term: string) => Promise<VotedRealPlayer[]>
  getPlayersByRole: (role: Role) => VotedRealPlayer[]
  getYearDayKey: () => YearDayKey | null
}

async function fetchEnhancedWrapper(
  type: VoteType,
  options: VoteLookupOptions
): Promise<EnhancedVotedRealPlayerWrapper | null> {
  if (type === 'live') {
    return realVoteService.getEnhancedLiveWrapper(options)
  }
  return realVoteService.getEnhancedOfficialWrapper(options)
}

async function executeSearch(
  type: VoteType,
  term: string,
  options: VoteLookupOptions
): Promise<VotedRealPlayer[]> {
  if (type === 'live') {
    return realVoteService.searchLivePlayers(term, options)
  }
  return realVoteService.searchOfficialPlayers(term, options)
}

export function useRealVotes(type: VoteType, options: UseRealVotesOptions): UseRealVotesResult {
  const {
    year,
    day,
    autoRefresh = false,
    refreshInterval = 60000,
    enabled = true,
  } = options

  const [state, setState] = useState<BaseState>(() => createInitialState(enabled))
  const mountedRef = useRef(true)
  const intervalRef = useRef<NodeJS.Timeout | null>(null)

  const fetchVotes = useCallback(async (isRefresh = false) => {
    const canRun = enabled && day != null
    if (!canRun) {
      setState(prev => ({ ...prev, isLoading: false }))
      return
    }

    if (!isRefresh) {
      setState(prev => ({ ...prev, isLoading: true, error: null }))
    }

    try {
      const resolvedYear = await realVoteService.resolveYear(year)
      if (resolvedYear == null) {
        setState(prev => ({
          ...prev,
          wrapper: null,
          isLoading: false,
          error: 'Anno utente non disponibile',
          lastUpdated: new Date(),
          key: null,
        }))
        return
      }

      const enhancedWrapper = await fetchEnhancedWrapper(type, { year: resolvedYear, day })

      if (!mountedRef.current) {
        return
      }

      if (enhancedWrapper) {
        setState({
          wrapper: enhancedWrapper,
          isLoading: false,
          error: null,
          lastUpdated: new Date(),
          key: { year: resolvedYear, day },
        })
      } else {
        setState(prev => ({
          ...prev,
          wrapper: null,
          isLoading: false,
          error: 'Non sono disponibili voti per la giornata selezionata',
          lastUpdated: new Date(),
          key: { year: resolvedYear, day },
        }))
      }
    } catch (error) {
      console.error('Errore nel caricamento dei voti reali:', error)
      if (!mountedRef.current) {
        return
      }

      setState(prev => ({
        ...prev,
        wrapper: null,
        isLoading: false,
        error: 'Errore nel caricamento dei voti reali',
        lastUpdated: new Date(),
        key: null,
      }))
    }
  }, [enabled, day, type, year])

  useEffect(() => {
    mountedRef.current = true
    if (enabled && day != null) {
      fetchVotes()
    }

    return () => {
      mountedRef.current = false
      if (intervalRef.current) {
        clearInterval(intervalRef.current)
      }
    }
  }, [enabled, day, fetchVotes])

  useEffect(() => {
    if (!autoRefresh || !enabled || day == null) {
      return undefined
    }

    if (intervalRef.current) {
      clearInterval(intervalRef.current)
    }

    intervalRef.current = setInterval(() => {
      if (!mountedRef.current) {
        return
      }
      fetchVotes(true)
    }, refreshInterval)

    return () => {
      if (intervalRef.current) {
        clearInterval(intervalRef.current)
      }
    }
  }, [autoRefresh, enabled, refreshInterval, fetchVotes, day])

  const players = useMemo(() => state.wrapper?.players ?? [], [state.wrapper])
  const buckets = useMemo(() => state.wrapper?.playersByRole ?? {}, [state.wrapper])
  const playersWithVote = useMemo(() => state.wrapper?.playersWithVote ?? [], [state.wrapper])
  const playersWithoutVote = useMemo(() => state.wrapper?.playersWithoutVote ?? [], [state.wrapper])
  const totalPlayers = state.wrapper?.totalPlayers ?? 0
  const votedPlayersCount = state.wrapper?.votedPlayersCount ?? 0

  const refetch = useCallback(() => fetchVotes(false), [fetchVotes])

  const search = useCallback(async (term: string) => {
    if (day == null) {
      return []
    }

    const effectiveYear = year ?? state.key?.year
    if (effectiveYear == null) {
      return []
    }

    return executeSearch(type, term, { year: effectiveYear, day })
  }, [day, type, year, state.key])

  const getPlayersByRole = useCallback((role: Role) => {
    if (!state.wrapper) {
      return []
    }

    return state.wrapper.playersByRole[role] ?? []
  }, [state.wrapper])

  const getYearDayKey = useCallback((): YearDayKey | null => state.key, [state.key])

  return {
    players,
    enhancedWrapper: state.wrapper,
    buckets,
    playersWithVote,
    playersWithoutVote,
    totalPlayers,
    votedPlayersCount,
    isLoading: state.isLoading,
    error: state.error,
    lastUpdated: state.lastUpdated,
    refetch,
    search,
    getPlayersByRole,
    getYearDayKey,
  }
}

export function useLiveRealVotes(options: UseRealVotesOptions): UseRealVotesResult {
  return useRealVotes('live', options)
}

export function useOfficialRealVotes(options: UseRealVotesOptions): UseRealVotesResult {
  return useRealVotes('official', options)
}
