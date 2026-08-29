import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { realPlayerService, SearchPlayersOptions } from '../services/realPlayerService'
import {
  EnhancedRealPlayersWrapper,
  RealPlayer,
  RealPlayerRoleBuckets,
} from '../types/serieA'
import { LeagueKey } from '../types/rank'
import { Role } from '../types/group'

interface BaseState {
  wrapper: EnhancedRealPlayersWrapper | null
  isLoading: boolean
  error: string | null
  lastUpdated: Date | null
}

const createInitialState = (enabled: boolean): BaseState => ({
  wrapper: null,
  isLoading: enabled,
  error: null,
  lastUpdated: null
})

export interface UseRealPlayersOptions {
  /** When provided the hook will fetch the players for the specified year instead of the current user */
  year?: number
  /** Automatically refresh the list on an interval (disabled by default) */
  autoRefresh?: boolean
  /** Refresh interval in milliseconds when autoRefresh is enabled (default: 60000) */
  refreshInterval?: number
  /** Allow the hook to fetch data (default: true) */
  enabled?: boolean
}

export interface UseRealPlayersResult {
  players: RealPlayer[]
  enhancedWrapper: EnhancedRealPlayersWrapper | null
  buckets: RealPlayerRoleBuckets
  activePlayers: RealPlayer[]
  inactivePlayers: RealPlayer[]
  hasVisiblePlayers: boolean
  isLoading: boolean
  error: string | null
  lastUpdated: Date | null
  refetch: () => Promise<void>
  search: (term: string, options?: Omit<SearchPlayersOptions, 'year'>) => Promise<RealPlayer[]>
  getPlayersByRole: (role: Role) => RealPlayer[]
}

export function useRealPlayers(options: UseRealPlayersOptions = {}): UseRealPlayersResult {
  const {
    year,
    autoRefresh = false,
    refreshInterval = 60000,
    enabled = true,
  } = options

  const [state, setState] = useState<BaseState>(() => createInitialState(enabled))
  const mountedRef = useRef(true)
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null)

  const fetchPlayers = useCallback(async (isRefresh = false) => {
    const canRun = enabled
    if (!canRun) {
      setState(prev => ({ ...prev, isLoading: false }))
      return
    }

    if (!isRefresh) {
      setState(prev => ({ ...prev, isLoading: true, error: null }))
    }

    try {
      const enhancedWrapper = year != null
        ? await realPlayerService.getEnhancedPlayersWrapperByYear(year)
        : await realPlayerService.getCurrentUserEnhancedWrapper()

      if (!mountedRef.current) {
        return
      }

      if (enhancedWrapper) {
        setState({
          wrapper: enhancedWrapper,
          isLoading: false,
          error: null,
          lastUpdated: new Date()
        })
      } else {
        setState(prev => ({
          ...prev,
          wrapper: null,
          isLoading: false,
          error: 'Lista giocatori non disponibile',
          lastUpdated: new Date()
        }))
      }
    } catch (error) {
      console.error('Errore nel caricamento dei RealPlayers:', error)
      if (!mountedRef.current) {
        return
      }

      setState(prev => ({
        ...prev,
        wrapper: null,
        isLoading: false,
        error: 'Errore nel caricamento dei giocatori reali',
        lastUpdated: new Date()
      }))
    }
  }, [enabled, year])

  useEffect(() => {
    mountedRef.current = true
    if (enabled) {
      fetchPlayers()
    }

    return () => {
      mountedRef.current = false
      if (intervalRef.current) {
        clearInterval(intervalRef.current)
      }
    }
  }, [enabled, fetchPlayers])

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
      fetchPlayers(true)
    }, refreshInterval)

    return () => {
      if (intervalRef.current) {
        clearInterval(intervalRef.current)
      }
    }
  }, [autoRefresh, enabled, refreshInterval, fetchPlayers])

  const players = useMemo(() => state.wrapper?.players ?? [], [state.wrapper])
  const buckets = useMemo(() => state.wrapper?.playersByRole ?? {}, [state.wrapper])
  const activePlayers = useMemo(() => state.wrapper?.activePlayers ?? [], [state.wrapper])
  const inactivePlayers = useMemo(() => state.wrapper?.inactivePlayers ?? [], [state.wrapper])
  const hasVisiblePlayers = state.wrapper?.hasVisiblePlayers ?? false

  const refetch = useCallback(() => fetchPlayers(false), [fetchPlayers])

  const search = useCallback((term: string, options?: Omit<SearchPlayersOptions, 'year'>) => {
    return realPlayerService.searchPlayers(term, {
      ...options,
      year: year ?? undefined,
    })
  }, [year])

  const getPlayersByRole = useCallback((role: Role) => {
    if (!state.wrapper) {
      return []
    }

    return state.wrapper.playersByRole[role] ?? []
  }, [state.wrapper])

  return {
    players,
    enhancedWrapper: state.wrapper,
    buckets,
    activePlayers,
    inactivePlayers,
    hasVisiblePlayers,
    isLoading: state.isLoading,
    error: state.error,
    lastUpdated: state.lastUpdated,
    refetch,
    search,
    getPlayersByRole,
  }
}

export interface UseRandomizedRealPlayersOptions {
  autoRefresh?: boolean
  refreshInterval?: number
  enabled?: boolean
}

export interface UseRandomizedRealPlayersResult {
  players: RealPlayer[]
  enhancedWrapper: EnhancedRealPlayersWrapper | null
  buckets: RealPlayerRoleBuckets
  activePlayers: RealPlayer[]
  inactivePlayers: RealPlayer[]
  hasVisiblePlayers: boolean
  isLoading: boolean
  error: string | null
  lastUpdated: Date | null
  refetch: () => Promise<void>
  search: (term: string, options?: { onlyActive?: boolean; onlyVisible?: boolean }) => Promise<RealPlayer[]>
  getPlayersByRole: (role: Role) => RealPlayer[]
}

export function useRandomizedRealPlayers(
  leagueKey: LeagueKey | null,
  options: UseRandomizedRealPlayersOptions = {}
): UseRandomizedRealPlayersResult {
  const {
    autoRefresh = false,
    refreshInterval = 60000,
    enabled = true,
  } = options

  const effectiveEnabled = enabled && !!leagueKey
  const [state, setState] = useState<BaseState>(() => createInitialState(effectiveEnabled))
  const mountedRef = useRef(true)
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null)

  const keySignature = leagueKey ? `${leagueKey.group}-${leagueKey.league}-${leagueKey.year}` : 'none'

  const fetchPlayers = useCallback(async (isRefresh = false) => {
    if (!leagueKey || !effectiveEnabled) {
      setState(prev => ({ ...prev, isLoading: false }))
      return
    }

    if (!isRefresh) {
      setState(prev => ({ ...prev, isLoading: true, error: null }))
    }

    try {
      const enhancedWrapper = await realPlayerService.getRandomizedEnhancedWrapper(leagueKey)

      if (!mountedRef.current) {
        return
      }

      if (enhancedWrapper) {
        setState({
          wrapper: enhancedWrapper,
          isLoading: false,
          error: null,
          lastUpdated: new Date()
        })
      } else {
        setState(prev => ({
          ...prev,
          wrapper: null,
          isLoading: false,
          error: 'Lista randomizzata non disponibile',
          lastUpdated: new Date()
        }))
      }
    } catch (error) {
      console.error('Errore nel caricamento della lista randomizzata di RealPlayers:', error)
      if (!mountedRef.current) {
        return
      }

      setState(prev => ({
        ...prev,
        wrapper: null,
        isLoading: false,
        error: 'Errore nel caricamento della lista randomizzata',
        lastUpdated: new Date()
      }))
    }
  }, [leagueKey, effectiveEnabled])

  useEffect(() => {
    mountedRef.current = true
    if (effectiveEnabled) {
      fetchPlayers()
    }

    return () => {
      mountedRef.current = false
      if (intervalRef.current) {
        clearInterval(intervalRef.current)
      }
    }
  }, [effectiveEnabled, fetchPlayers, keySignature])

  useEffect(() => {
    if (!autoRefresh || !effectiveEnabled) {
      return undefined
    }

    if (intervalRef.current) {
      clearInterval(intervalRef.current)
    }

    intervalRef.current = setInterval(() => {
      if (!mountedRef.current) {
        return
      }
      fetchPlayers(true)
    }, refreshInterval)

    return () => {
      if (intervalRef.current) {
        clearInterval(intervalRef.current)
      }
    }
  }, [autoRefresh, effectiveEnabled, refreshInterval, fetchPlayers])

  const players = useMemo(() => state.wrapper?.players ?? [], [state.wrapper])
  const buckets = useMemo(() => state.wrapper?.playersByRole ?? {}, [state.wrapper])
  const activePlayers = useMemo(() => state.wrapper?.activePlayers ?? [], [state.wrapper])
  const inactivePlayers = useMemo(() => state.wrapper?.inactivePlayers ?? [], [state.wrapper])
  const hasVisiblePlayers = state.wrapper?.hasVisiblePlayers ?? false

  const refetch = useCallback(() => fetchPlayers(false), [fetchPlayers])

  const search = useCallback((term: string, searchOptions?: { onlyActive?: boolean; onlyVisible?: boolean }) => {
    if (!leagueKey) {
      return Promise.resolve([])
    }

    return realPlayerService.searchRandomizedPlayers(term, leagueKey, searchOptions)
  }, [leagueKey])

  const getPlayersByRole = useCallback((role: Role) => {
    if (!state.wrapper) {
      return []
    }

    return state.wrapper.playersByRole[role] ?? []
  }, [state.wrapper])

  return {
    players,
    enhancedWrapper: state.wrapper,
    buckets,
    activePlayers,
    inactivePlayers,
    hasVisiblePlayers,
    isLoading: state.isLoading,
    error: state.error,
    lastUpdated: state.lastUpdated,
    refetch,
    search,
    getPlayersByRole,
  }
}
