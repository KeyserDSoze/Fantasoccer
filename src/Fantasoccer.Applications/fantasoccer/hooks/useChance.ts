import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { chanceService, ChanceLookupOptions, ChanceSearchFilters } from '../services/chanceService'
import {
  ChancedRealPlayer,
  EnhancedChancedRealPlayerWrapper,
  ChancedRealPlayerRoleBuckets,
} from '../types/chance'

interface BaseState {
  wrapper: EnhancedChancedRealPlayerWrapper | null
  isLoading: boolean
  error: string | null
  lastUpdated: Date | null
}

const createInitialState = (loading: boolean): BaseState => ({
  wrapper: null,
  isLoading: loading,
  error: null,
  lastUpdated: null,
})

export interface UseChancedRealPlayersOptions {
  year?: number
  day: number | null
  autoRefresh?: boolean
  refreshInterval?: number
  enabled?: boolean
  filters?: ChanceSearchFilters
  search?: string
}

export interface UseChancedRealPlayersResult {
  players: ChancedRealPlayer[]
  playersByRole: ChancedRealPlayerRoleBuckets
  availablePlayers: ChancedRealPlayer[]
  injuredPlayers: ChancedRealPlayer[]
  doubtfulPlayers: ChancedRealPlayer[]
  warnedPlayers: ChancedRealPlayer[]
  disqualifiedPlayers: ChancedRealPlayer[]
  filteredPlayers: ChancedRealPlayer[]
  enhancedWrapper: EnhancedChancedRealPlayerWrapper | null
  isLoading: boolean
  error: string | null
  lastUpdated: Date | null
  searchTerm: string
  setSearchTerm: (value: string) => void
  filters: ChanceSearchFilters
  setFilters: (filters: ChanceSearchFilters) => void
  refetch: () => Promise<void>
}

const applyFilters = (players: ChancedRealPlayer[], filters: ChanceSearchFilters): ChancedRealPlayer[] => {
  let filtered = [...players]

  if (filters.onlyAvailable) {
    filtered = filtered.filter(player => player.chance.isAvailable)
  }

  if (filters.includeInjured === false) {
    filtered = filtered.filter(player => !player.chance.isInjured)
  }

  if (filters.includeDisqualified === false) {
    filtered = filtered.filter(player => !player.chance.isDisqualified)
  }

  if (filters.includeWarned === false) {
    filtered = filtered.filter(player => !player.chance.isWarned)
  }

  if (filters.includeDoubtful === false) {
    filtered = filtered.filter(player => !player.chance.isDoubtful)
  }

  return filtered
}

const DEFAULT_FILTERS: ChanceSearchFilters = {
  includeInjured: true,
  includeDisqualified: true,
  includeWarned: true,
  includeDoubtful: true,
  onlyAvailable: false,
}

export function useChancedRealPlayers(options: UseChancedRealPlayersOptions): UseChancedRealPlayersResult {
  const {
    year,
    day,
    autoRefresh = false,
    refreshInterval = 60000,
    enabled = true,
    filters: initialFilters,
    search: initialSearch = '',
  } = options

  const effectiveEnabled = enabled && day != null

  const [state, setState] = useState<BaseState>(() => createInitialState(effectiveEnabled))
  const [searchTerm, setSearchTerm] = useState<string>(initialSearch)
  const [filters, setFilters] = useState<ChanceSearchFilters>({
    ...DEFAULT_FILTERS,
    ...(initialFilters ?? {}),
  })

  const mountedRef = useRef(true)
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null)

  const fetchPlayers = useCallback(async (isRefresh = false) => {
    if (!effectiveEnabled || day == null) {
      setState(prev => ({ ...prev, isLoading: false }))
      return
    }

    if (!isRefresh) {
      setState(prev => ({ ...prev, isLoading: true, error: null }))
    }

    try {
      const resolvedYear = await chanceService.resolveYear(year)
      if (!resolvedYear) {
        if (!mountedRef.current) {
          return
        }
        setState(prev => ({
          ...prev,
          wrapper: null,
          isLoading: false,
          error: 'Anno non disponibile',
          lastUpdated: new Date(),
        }))
        return
      }

      const lookup: ChanceLookupOptions = { year: resolvedYear, day }
      const enhancedWrapper = await chanceService.getEnhancedWrapper(lookup)

      if (!mountedRef.current) {
        return
      }

      if (enhancedWrapper) {
        setState({
          wrapper: enhancedWrapper,
          isLoading: false,
          error: null,
          lastUpdated: new Date(),
        })
      } else {
        setState(prev => ({
          ...prev,
          wrapper: null,
          isLoading: false,
          error: 'Probabili formazioni non disponibili',
          lastUpdated: new Date(),
        }))
      }
    } catch (error) {
      console.error('Errore nel caricamento delle probabili formazioni:', error)
      if (!mountedRef.current) {
        return
      }

      setState(prev => ({
        ...prev,
        wrapper: null,
        isLoading: false,
        error: 'Errore nel caricamento delle probabili formazioni',
        lastUpdated: new Date(),
      }))
    }
  }, [effectiveEnabled, day, year])

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
  }, [effectiveEnabled, fetchPlayers])

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
  const playersByRole = useMemo(() => state.wrapper?.playersByRole ?? {}, [state.wrapper])
  const availablePlayers = useMemo(() => state.wrapper?.availablePlayers ?? [], [state.wrapper])
  const injuredPlayers = useMemo(() => state.wrapper?.injuredPlayers ?? [], [state.wrapper])
  const doubtfulPlayers = useMemo(() => state.wrapper?.doubtfulPlayers ?? [], [state.wrapper])
  const warnedPlayers = useMemo(() => state.wrapper?.warnedPlayers ?? [], [state.wrapper])
  const disqualifiedPlayers = useMemo(() => state.wrapper?.disqualifiedPlayers ?? [], [state.wrapper])

  const filteredPlayers = useMemo(() => {
    const basePlayers = applyFilters(players, filters)
    if (!searchTerm) {
      return basePlayers
    }

    const normalized = searchTerm.toLowerCase()
    return basePlayers.filter(player =>
      player.name.toLowerCase().includes(normalized) ||
      player.chance.availabilityLabel.toLowerCase().includes(normalized) ||
      player.realTeam.name.toLowerCase().includes(normalized)
    )
  }, [players, filters, searchTerm])

  const refetch = useCallback(() => fetchPlayers(false), [fetchPlayers])

  return {
    players,
    playersByRole,
    availablePlayers,
    injuredPlayers,
    doubtfulPlayers,
    warnedPlayers,
    disqualifiedPlayers,
    filteredPlayers,
    enhancedWrapper: state.wrapper,
    isLoading: state.isLoading,
    error: state.error,
    lastUpdated: state.lastUpdated,
    searchTerm,
    setSearchTerm,
    filters,
    setFilters,
    refetch,
  }
}

export interface UseCurrentYearChancedPlayersOptions extends Omit<UseChancedRealPlayersOptions, 'year'> {
  day: number
}

export function useCurrentYearChancedRealPlayers(options: UseCurrentYearChancedPlayersOptions): UseChancedRealPlayersResult {
  return useChancedRealPlayers({
    ...options,
    year: undefined,
  })
}
