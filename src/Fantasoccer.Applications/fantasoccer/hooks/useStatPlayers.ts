import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { statPlayerService, StatPlayersLookupOptions } from '../services/statPlayerService'
import {
  EnhancedStatPlayersWrapper,
  StatPlayer,
  StatPlayerRoleBuckets,
} from '../types/stat'
import { Role } from '../types/group'

interface UseStatPlayersState {
  wrapper: EnhancedStatPlayersWrapper | null
  isLoading: boolean
  error: string | null
  lastUpdated: Date | null
}

const createInitialState = (enabled: boolean): UseStatPlayersState => ({
  wrapper: null,
  isLoading: enabled,
  error: null,
  lastUpdated: null,
})

export interface UseStatPlayersOptions extends StatPlayersLookupOptions {
  autoRefresh?: boolean
  refreshInterval?: number
  enabled?: boolean
}

export interface UseStatPlayersResult {
  players: StatPlayer[]
  enhancedWrapper: EnhancedStatPlayersWrapper | null
  buckets: StatPlayerRoleBuckets
  topByAverage: StatPlayer[]
  topByFantaAverage: StatPlayer[]
  isLoading: boolean
  error: string | null
  lastUpdated: Date | null
  refetch: () => Promise<void>
  search: (term: string) => Promise<StatPlayer[]>
  getPlayersByRole: (role: Role) => StatPlayer[]
  getTopByAverage: (limit?: number) => Promise<StatPlayer[]>
  getTopByFantaAverage: (limit?: number) => Promise<StatPlayer[]>
}

export function useStatPlayers(options: UseStatPlayersOptions = {}): UseStatPlayersResult {
  const {
    year,
    topLimit,
    autoRefresh = false,
    refreshInterval = 120000,
    enabled = true,
  } = options

  const [state, setState] = useState<UseStatPlayersState>(() => createInitialState(enabled))
  const mountedRef = useRef(true)
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null)

  const fetchWrapper = useCallback(async (isRefresh = false) => {
    if (!enabled) {
      setState(prev => ({ ...prev, isLoading: false }))
      return
    }

    if (!isRefresh) {
      setState(prev => ({ ...prev, isLoading: true, error: null }))
    }

    try {
      const wrapper = await statPlayerService.getCurrentUserEnhancedWrapper({ year, topLimit })

      if (!mountedRef.current) {
        return
      }

      if (wrapper) {
        setState({
          wrapper,
          isLoading: false,
          error: null,
          lastUpdated: new Date(),
        })
      } else {
        setState(prev => ({
          ...prev,
          wrapper: null,
          isLoading: false,
          error: 'Statistiche non disponibili',
          lastUpdated: new Date(),
        }))
      }
    } catch (error) {
      console.error('Errore nel caricamento delle statistiche giocatori:', error)
      if (!mountedRef.current) {
        return
      }

      setState(prev => ({
        ...prev,
        wrapper: null,
        isLoading: false,
        error: 'Errore nel caricamento delle statistiche giocatori',
        lastUpdated: new Date(),
      }))
    }
  }, [enabled, year, topLimit])

  useEffect(() => {
    mountedRef.current = true

    if (enabled) {
      fetchWrapper()
    }

    return () => {
      mountedRef.current = false
      if (intervalRef.current) {
        clearInterval(intervalRef.current)
      }
    }
  }, [enabled, fetchWrapper])

  useEffect(() => {
    if (!autoRefresh || !enabled) {
      return undefined
    }

    if (intervalRef.current) {
      clearInterval(intervalRef.current)
    }

    intervalRef.current = setInterval(() => {
      if (!mountedRef.current) {
        return
      }
      fetchWrapper(true)
    }, refreshInterval)

    return () => {
      if (intervalRef.current) {
        clearInterval(intervalRef.current)
      }
    }
  }, [autoRefresh, enabled, refreshInterval, fetchWrapper])

  const players = useMemo(() => state.wrapper?.players ?? [], [state.wrapper])
  const buckets = useMemo(() => state.wrapper?.playersByRole ?? {}, [state.wrapper])
  const topByAverage = useMemo(() => state.wrapper?.topByAverage ?? [], [state.wrapper])
  const topByFantaAverage = useMemo(() => state.wrapper?.topByFantaAverage ?? [], [state.wrapper])

  const refetch = useCallback(() => fetchWrapper(false), [fetchWrapper])

  const search = useCallback((term: string) => {
    return statPlayerService.searchPlayers(term, { year, topLimit })
  }, [year, topLimit])

  const getPlayersByRole = useCallback((role: Role) => {
    if (!state.wrapper) {
      return []
    }

    return state.wrapper.playersByRole[role] ?? []
  }, [state.wrapper])

  const getTopAverageAsync = useCallback((limit?: number) => {
    return statPlayerService.getTopPlayersByAverage({ year, topLimit: limit ?? topLimit })
  }, [year, topLimit])

  const getTopFantaAverageAsync = useCallback((limit?: number) => {
    return statPlayerService.getTopPlayersByFantaAverage({ year, topLimit: limit ?? topLimit })
  }, [year, topLimit])

  return {
    players,
    enhancedWrapper: state.wrapper,
    buckets,
    topByAverage,
    topByFantaAverage,
    isLoading: state.isLoading,
    error: state.error,
    lastUpdated: state.lastUpdated,
    refetch,
    search,
    getPlayersByRole,
    getTopByAverage: getTopAverageAsync,
    getTopByFantaAverage: getTopFantaAverageAsync,
  }
}
