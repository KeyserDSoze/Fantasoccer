import { useCallback, useEffect, useRef, useState } from 'react'
import { serieAService } from '../services/serieAService'
import type { RealCalendar, RealRank } from '../types/serieA'
import { seasonRefreshInterval } from '../utils/serieAMatch'

// Calendar and standings of any Serie A season, kept up to date without a refresh button.
export function useSerieASeason(year: number | null) {
  const [calendar, setCalendar] = useState<RealCalendar | null>(null)
  const [rank, setRank] = useState<RealRank | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const requestId = useRef(0)
  const calendarRef = useRef<RealCalendar | null>(null)

  const load = useCallback(async (background: boolean) => {
    if (year === null) return
    const id = ++requestId.current
    if (!background) { setIsLoading(true); setError(null) }
    try {
      const [nextCalendar, nextRank] = await Promise.all([serieAService.getCalendarByYear(year), serieAService.getRankByYear(year)])
      if (id !== requestId.current) return
      calendarRef.current = nextCalendar
      setCalendar(nextCalendar); setRank(nextRank)
      setError(nextCalendar?.days.length ? null : 'Calendario non disponibile per questa stagione.')
    } catch (reason: any) {
      if (id !== requestId.current) return
      if (!background || !calendarRef.current) setError(reason?.message ?? 'Serie A non disponibile.')
    } finally {
      if (id === requestId.current) setIsLoading(false)
    }
  }, [year])

  useEffect(() => {
    calendarRef.current = null
    setCalendar(null); setRank(null)
    void load(false)
    return () => { requestId.current++ }
  }, [load])

  const loaded = calendar !== null
  useEffect(() => {
    if (!loaded) return
    let timer: ReturnType<typeof setTimeout> | null = null
    const schedule = () => {
      const interval = seasonRefreshInterval(calendarRef.current)
      if (interval === null) return
      timer = setTimeout(async () => { await load(true); schedule() }, interval)
    }
    schedule()
    return () => { if (timer) clearTimeout(timer) }
  }, [loaded, load])

  return { calendar, rank, isLoading, error }
}
