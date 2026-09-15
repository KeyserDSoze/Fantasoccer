import { useState, useEffect, useCallback, useRef } from 'react'
import { calendarService } from '../services/calendarService'
import {
  Calendar,
  EnhancedCalendar,
  CalendarDay,
  CalendarGame,
  CalendarHelper,
  enhanceCalendar
} from '../types/calendar'
import { LeagueKey } from '../types/rank'

export interface UseCalendarOptions {
  /** Auto-refresh interval in milliseconds (default: 0 = disabled) */
  refreshInterval?: number
  /** Skip fetching automatically when true */
  skip?: boolean
}

export interface UseCalendarResult {
  calendar: Calendar | null
  enhancedCalendar: EnhancedCalendar | null
  roundKeys: string[]
  days: CalendarDay[]
  games: CalendarGame[]
  pendingGames: CalendarGame[]
  loading: boolean
  error: string | null
  refetch: () => Promise<void>
  getRound: (roundKey: string) => CalendarDay[]
  getDay: (dayNumber: number) => CalendarDay | null
  getGamesForTeam: (teamName: string) => CalendarGame[]
}

/**
 * Hook to load the fantasy calendar for a specific league key
 */
export function useCalendar(
  leagueKey: LeagueKey | null,
  options: UseCalendarOptions = {}
): UseCalendarResult {
  const {
    refreshInterval = 0,
    skip = false
  } = options

  const [calendar, setCalendar] = useState<Calendar | null>(null)
  const [enhancedCalendar, setEnhancedCalendar] = useState<EnhancedCalendar | null>(null)
  const [roundKeys, setRoundKeys] = useState<string[]>([])
  const [days, setDays] = useState<CalendarDay[]>([])
  const [games, setGames] = useState<CalendarGame[]>([])
  const [pendingGames, setPendingGames] = useState<CalendarGame[]>([])
  const [loading, setLoading] = useState<boolean>(false)
  const [error, setError] = useState<string | null>(null)
  const requestId = useRef(0)

  const resetState = useCallback(() => {
    setCalendar(null)
    setEnhancedCalendar(null)
    setRoundKeys([])
    setDays([])
    setGames([])
    setPendingGames([])
    setError(null)
  }, [])

  const fetchCalendar = useCallback(async (silent = false) => {
    const currentRequestId = ++requestId.current
    if (!leagueKey || skip) {
      resetState()
      return
    }

    if (!silent) {
      setLoading(true)
    }
    setError(null)

    try {
      const [calendarData, enhancedCalendarData] = await Promise.all([
        calendarService.getCalendar(leagueKey),
        calendarService.getEnhancedCalendar(leagueKey)
      ])

      if (currentRequestId !== requestId.current) return

      if (!calendarData) {
        resetState()
        setError('Calendar not available')
        return
      }

      const enhanced = enhancedCalendarData ?? enhanceCalendar(calendarData)

      setCalendar(calendarData)
      setEnhancedCalendar(enhanced)
      setRoundKeys(enhanced?.roundKeys ?? CalendarHelper.getAllRoundKeys(calendarData))
      setDays(enhanced?.allDays ?? CalendarHelper.getAllDays(calendarData))
      setGames(enhanced?.allGames ?? CalendarHelper.getAllGames(calendarData))
      setPendingGames(enhanced?.pendingGames ?? CalendarHelper.getPendingGames(calendarData))
    } catch (err) {
      if (currentRequestId !== requestId.current) return
      console.error('Error fetching calendar:', err)
      setError(err instanceof Error ? err.message : 'Failed to fetch calendar')
    } finally {
      if (!silent) {
        setLoading(false)
      }
    }
  }, [leagueKey, skip, resetState])

  useEffect(() => {
    fetchCalendar(false)
  }, [fetchCalendar])

  useEffect(() => {
    if (refreshInterval <= 0) {
      return
    }

    const intervalId = setInterval(() => {
      fetchCalendar(true)
    }, refreshInterval)

    return () => clearInterval(intervalId)
  }, [fetchCalendar, refreshInterval])

  const getRound = useCallback((roundKey: string): CalendarDay[] => {
    if (enhancedCalendar) {
      return CalendarHelper.getRound(enhancedCalendar, roundKey)
    }
    if (calendar) {
      return CalendarHelper.getRound(calendar, roundKey)
    }
    return []
  }, [calendar, enhancedCalendar])

  const getDay = useCallback((dayNumber: number): CalendarDay | null => {
    if (enhancedCalendar) {
      return CalendarHelper.getDayByNumber(enhancedCalendar, dayNumber)
    }
    if (calendar) {
      return CalendarHelper.getDayByNumber(calendar, dayNumber)
    }
    return null
  }, [calendar, enhancedCalendar])

  const getGamesForTeam = useCallback((teamName: string): CalendarGame[] => {
    if (enhancedCalendar) {
      return CalendarHelper.getGamesForTeam(enhancedCalendar, teamName)
    }
    if (calendar) {
      return CalendarHelper.getGamesForTeam(calendar, teamName)
    }
    return []
  }, [calendar, enhancedCalendar])

  return {
    calendar,
    enhancedCalendar,
    roundKeys,
    days,
    games,
    pendingGames,
    loading,
    error,
    refetch: () => fetchCalendar(false),
    getRound,
    getDay,
    getGamesForTeam
  }
}

export interface UseCalendarDayOptions extends UseCalendarOptions {
  dayNumber: number
}

export interface UseCalendarDayResult {
  day: CalendarDay | null
  games: CalendarGame[]
  loading: boolean
  error: string | null
  refetch: () => Promise<void>
}

/**
 * Hook to get a specific calendar day with its games.
 * Wraps useCalendar internally for convenience when only one day is needed.
 */
export function useCalendarDay(
  leagueKey: LeagueKey | null,
  options: UseCalendarDayOptions
): UseCalendarDayResult {
  const { dayNumber, ...calendarOptions } = options
  const {
    days,
    loading,
    error,
    refetch
  } = useCalendar(leagueKey, calendarOptions)

  const day = days.find(d => d.number === dayNumber) || null

  return {
    day,
    games: day?.games ?? [],
    loading,
    error,
    refetch
  }
}
