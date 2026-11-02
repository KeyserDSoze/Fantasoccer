import { useCallback, useEffect, useRef, useState } from 'react'
import footballMatchService from '../services/footballMatchService'
import type { FootballMatch } from '../types/footballMatch'
import { matchRefreshInterval } from '../utils/serieAMatch'

export interface UseSerieAMatchOptions { year: number | null; day: number | null; ordinal: number | null; groupId?: string | null; leagueId?: string | null }

// A match page keeps itself up to date: no refresh button. The interval follows how close the match is.
export function useSerieAMatch({ year, day, ordinal, groupId, leagueId }: UseSerieAMatchOptions) {
  const [match, setMatch] = useState<FootballMatch | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [lastUpdated, setLastUpdated] = useState<Date | null>(null)
  const requestId = useRef(0)
  const matchRef = useRef<FootballMatch | null>(null)
  const ready = year !== null && day !== null && ordinal !== null

  const load = useCallback(async (background: boolean) => {
    if (!ready) return
    const id = ++requestId.current
    if (!background) { setIsLoading(true); setError(null) }
    try {
      const value = await footballMatchService.getMatch({ year, day, ordinal, groupId, leagueId })
      if (id !== requestId.current) return
      matchRef.current = value ?? null
      setMatch(value ?? null); setError(value ? null : 'Partita non trovata.'); setLastUpdated(new Date())
    } catch (reason: any) {
      if (id !== requestId.current) return
      // A failed background refresh keeps the last lineup on screen.
      if (!background || !matchRef.current) setError(reason?.message ?? 'Partita non disponibile.')
    } finally {
      if (id === requestId.current) setIsLoading(false)
    }
  }, [ready, year, day, ordinal, groupId, leagueId])

  useEffect(() => {
    matchRef.current = null
    setMatch(null)
    void load(false)
    return () => { requestId.current++ }
  }, [load])

  useEffect(() => {
    if (!match) return
    let timer: ReturnType<typeof setTimeout> | null = null
    const schedule = () => {
      const interval = matchRefreshInterval(matchRef.current)
      if (interval === null) return
      timer = setTimeout(async () => { await load(true); schedule() }, interval)
    }
    schedule()
    return () => { if (timer) clearTimeout(timer) }
  }, [match === null, load])

  return { match, isLoading, error, lastUpdated }
}
