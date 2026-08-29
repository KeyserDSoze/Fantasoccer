import { useState, useEffect, useCallback, useRef } from 'react'
import { realVoteService } from 'services/realVoteService'
import { type EnhancedVotedRealPlayerWrapper, type RealDay } from 'types/serieA'
import { RealCalendarHelper } from 'types/serieA'

export interface UseSerieALiveVotesOptions {
  liveDay: RealDay | null
  isLive: boolean
  enablePolling?: boolean
}

export interface UseSerieALiveVotesResult {
  votes: EnhancedVotedRealPlayerWrapper | null
  isLoading: boolean
  error: string | null
  refetch: () => Promise<void>
  lastUpdated: Date | null
}

const LIVE_VOTES_POLL_INTERVAL = 10000 // 10 secondi

export function useSerieALiveVotes({
  liveDay,
  isLive,
  enablePolling = true
}: UseSerieALiveVotesOptions): UseSerieALiveVotesResult {
  const [votes, setVotes] = useState<EnhancedVotedRealPlayerWrapper | null>(null)
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [lastUpdated, setLastUpdated] = useState<Date | null>(null)
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null)

  // Determina se c'è almeno una partita live
  const hasLiveGames = useCallback(() => {
    if (!liveDay) return false
    return liveDay.games.some(game => {
      const isPlayed = RealCalendarHelper.isGamePlayed(game)
      if (isPlayed || game.delayed) return false
      
      const now = new Date()
      const gameDate = game.date
      const twoHoursAfter = new Date(gameDate.getTime() + 2 * 60 * 60 * 1000)
      
      return now >= gameDate && now <= twoHoursAfter
    })
  }, [liveDay])

  // Fetch dei voti live
  const fetchVotes = useCallback(async () => {
    if (!liveDay) {
      setVotes(null)
      setError(null)
      return
    }

    try {
      setIsLoading(true)
      setError(null)

      const result = await realVoteService.getEnhancedLiveWrapper({
        year: liveDay.year,
        day: liveDay.serieADay
      })

      setVotes(result)
      setLastUpdated(new Date())
    } catch (err) {
      console.error('Error fetching live votes:', err)
      setError(err instanceof Error ? err.message : 'Errore nel caricamento dei voti live')
    } finally {
      setIsLoading(false)
    }
  }, [liveDay])

  // Effetto per fetch iniziale - sempre quando c'è un liveDay
  useEffect(() => {
    if (liveDay) {
      void fetchVotes()
    } else {
      setVotes(null)
      setError(null)
    }
  }, [liveDay, fetchVotes])

  // Effetto per polling intelligente - solo durante partite live
  useEffect(() => {
    if (!enablePolling || !isLive || !liveDay) {
      if (intervalRef.current) {
        clearInterval(intervalRef.current)
        intervalRef.current = null
      }
      return
    }

    // Polling solo se ci sono partite live
    intervalRef.current = setInterval(() => {
      if (hasLiveGames()) {
        void fetchVotes()
      }
    }, LIVE_VOTES_POLL_INTERVAL)

    return () => {
      if (intervalRef.current) {
        clearInterval(intervalRef.current)
        intervalRef.current = null
      }
    }
  }, [enablePolling, isLive, liveDay, hasLiveGames, fetchVotes])

  return {
    votes,
    isLoading,
    error,
    refetch: fetchVotes,
    lastUpdated
  }
}
