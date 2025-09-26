// Hook per gestire il calendario Serie A
import { useState, useEffect, useCallback, useRef } from 'react'
import { serieAService } from '../services/serieAService'
import { EnhancedRealCalendar, RealDay, RealGame } from '../types/serieA'

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

// Funzione helper per formattare la data
export const formatGameDate = (dateString: string | null): string => {
  if (!dateString) return 'Data non disponibile'
  
  try {
    const date = new Date(dateString)
    return date.toLocaleDateString('it-IT', {
      weekday: 'short',
      day: 'numeric',
      month: 'short',
      hour: '2-digit',
      minute: '2-digit'
    })
  } catch {
    return 'Data non valida'
  }
}

// Funzione helper per ottenere lo status della giornata
export const getDayStatusText = (
  isLive: boolean, 
  liveDay: RealDay | null, 
  nextDay: RealDay | null
): string => {
  if (isLive && liveDay) {
    return `Giornata ${liveDay.serieADay} - IN CORSO`
  } else if (nextDay) {
    return `Prossima Giornata (${nextDay.serieADay})`
  } else {
    return 'Nessuna giornata disponibile'
  }
}