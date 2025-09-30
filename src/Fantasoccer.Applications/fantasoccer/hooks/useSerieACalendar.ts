// Hook per gestire il calendario Serie A
import { useState, useEffect, useCallback, useRef } from 'react'
import { serieAService } from '../services/serieAService'
import { EnhancedRealCalendar, RealDay, RealGame, RealRank, RealCalendarHelper } from '../types/serieA'

interface SerieACalendarState {
  calendar: EnhancedRealCalendar | null
  liveDay: RealDay | null
  nextDay: RealDay | null
  liveGames: RealGame[]
  isLive: boolean
  liveSerieADay: number
  isLoading: boolean
  error: string | null
  lastUpdated: Date | null
}

interface UseSerieACalendarOptions {
  /** Intervallo di aggiornamento in millisecondi (default: 30000 = 30 secondi) */
  refreshInterval?: number
  /** Se abilitare il polling automatico (default: true) */
  enablePolling?: boolean
  /** Se abilitare il polling solo quando ci sono partite live (default: true) */
  pollOnlyWhenLive?: boolean
}

export function useSerieACalendar(options: UseSerieACalendarOptions = {}) {
  const {
    refreshInterval = 30000, // 30 secondi
    enablePolling = true,
    pollOnlyWhenLive = true
  } = options

  const [state, setState] = useState<SerieACalendarState>({
    calendar: null,
    liveDay: null,
    nextDay: null,
    liveGames: [],
    isLive: false,
    liveSerieADay: 0,
    isLoading: true,
    error: null,
    lastUpdated: null
  })

  const intervalRef = useRef<NodeJS.Timeout | null>(null)
  const mountedRef = useRef(true)

  const fetchCalendar = useCallback(async (isRefresh = false) => {
    try {
      // Solo mostro loading se non è un refresh (primo caricamento)
      if (!isRefresh) {
        setState(prev => ({ ...prev, isLoading: true, error: null }))
      }
      
      // Ottieni il calendario enhanced per l'utente corrente
      const enhancedCalendar = await serieAService.getEnhancedCurrentUserCalendar()
      
      if (!mountedRef.current) return
      
      if (enhancedCalendar) {
        setState({
          calendar: enhancedCalendar,
          liveDay: enhancedCalendar.liveDay || null,
          nextDay: enhancedCalendar.nextDay || null,
          liveGames: enhancedCalendar.liveGames || [],
          isLive: enhancedCalendar.isLive || false,
          liveSerieADay: enhancedCalendar.liveSerieADay || 0,
          isLoading: false,
          error: null,
          lastUpdated: new Date()
        })
      } else {
        setState(prev => ({
          ...prev,
          isLoading: false,
          error: 'Calendario non disponibile',
          lastUpdated: new Date()
        }))
      }
    } catch (error) {
      if (!mountedRef.current) return
      
      console.error('Errore nel caricamento del calendario:', error)
      setState(prev => ({
        ...prev,
        isLoading: false,
        error: 'Errore nel caricamento del calendario',
        lastUpdated: new Date()
      }))
    }
  }, [])

  // Setup del polling automatico
  const setupPolling = useCallback(() => {
    // Pulisci l'intervallo precedente se esiste
    if (intervalRef.current) {
      clearInterval(intervalRef.current)
    }

    if (!enablePolling) return

    // Avvia il polling
    intervalRef.current = setInterval(() => {
      if (!mountedRef.current) return

      // Se pollOnlyWhenLive è true, aggiorna solo quando ci sono partite live
      // oppure sempre se è false
      if (!pollOnlyWhenLive || state.isLive) {
        console.log('🔄 Auto-refresh Serie A calendar')
        fetchCalendar(true) // true = è un refresh, non mostra loading
      }
    }, refreshInterval)
  }, [enablePolling, pollOnlyWhenLive, state.isLive, refreshInterval, fetchCalendar])

  useEffect(() => {
    mountedRef.current = true
    
    // Caricamento iniziale
    fetchCalendar()

    return () => {
      mountedRef.current = false
      if (intervalRef.current) {
        clearInterval(intervalRef.current)
      }
    }
  }, [fetchCalendar])

  // Effetto per gestire il polling quando cambia lo stato live
  useEffect(() => {
    setupPolling()
    
    return () => {
      if (intervalRef.current) {
        clearInterval(intervalRef.current)
      }
    }
  }, [setupPolling])

  const refetch = useCallback(() => {
    return fetchCalendar(false) // false = mostra loading
  }, [fetchCalendar])

  return {
    ...state,
    refetch
  }
}

// Funzione helper per ottenere lo status della giornata
export const getDayStatusText = (
  isLive: boolean,
  liveDay: RealDay | null,
  nextDay: RealDay | null
): string => {
  if (liveDay) {
    const hasRemainingGames = liveDay.games.some(game => !RealCalendarHelper.isGamePlayed(game) && !game.delayed)

    if (isLive || hasRemainingGames) {
      return `Giornata ${liveDay.serieADay} - IN CORSO`
    }

    return `Giornata ${liveDay.serieADay} - Completata`
  }

  if (nextDay) {
    return `Prossima Giornata (${nextDay.serieADay})`
  }

  return 'Nessuna giornata disponibile'
}

interface SerieARankState {
  rank: RealRank | null
  isLoading: boolean
  error: string | null
  lastUpdated: Date | null
}

interface UseSerieARankOptions {
  /** Intervallo di aggiornamento in millisecondi (default: 120000 = 2 minuti) */
  refreshInterval?: number
  /** Se abilitare il polling automatico (default: true) */
  enablePolling?: boolean
}

export function useSerieARank(options: UseSerieARankOptions = {}) {
  const { refreshInterval = 120000, enablePolling = true } = options

  const [state, setState] = useState<SerieARankState>({
    rank: null,
    isLoading: true,
    error: null,
    lastUpdated: null
  })

  const intervalRef = useRef<NodeJS.Timeout | null>(null)
  const mountedRef = useRef(true)

  const fetchRank = useCallback(async (isRefresh = false) => {
    if (!isRefresh) {
      setState(prev => ({ ...prev, isLoading: true, error: null }))
    }

    try {
      const rank = await serieAService.getCurrentUserRank()

      if (!mountedRef.current) {
        return
      }

      if (rank) {
        setState({
          rank,
          isLoading: false,
          error: null,
          lastUpdated: new Date()
        })
      } else {
        setState(prev => ({
          ...prev,
          rank: null,
          isLoading: false,
          error: 'Classifica non disponibile',
          lastUpdated: new Date()
        }))
      }
    } catch (error) {
      console.error('Errore nel caricamento della classifica Serie A:', error)
      if (!mountedRef.current) {
        return
      }

      setState(prev => ({
        ...prev,
        isLoading: false,
        error: 'Errore nel caricamento della classifica',
        lastUpdated: new Date()
      }))
    }
  }, [])

  useEffect(() => {
    mountedRef.current = true
    fetchRank()

    return () => {
      mountedRef.current = false
      if (intervalRef.current) {
        clearInterval(intervalRef.current)
      }
    }
  }, [fetchRank])

  useEffect(() => {
    if (!enablePolling) {
      return undefined
    }

    intervalRef.current = setInterval(() => {
      if (!mountedRef.current) {
        return
      }

      fetchRank(true)
    }, refreshInterval)

    return () => {
      if (intervalRef.current) {
        clearInterval(intervalRef.current)
      }
    }
  }, [enablePolling, refreshInterval, fetchRank])

  const refetch = useCallback(() => fetchRank(false), [fetchRank])

  return {
    ...state,
    refetch
  }
}