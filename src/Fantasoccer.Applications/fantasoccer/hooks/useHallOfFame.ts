// Hooks for Hall of Fame data
import { useState, useEffect, useCallback } from 'react'
import { hallOfFameService } from 'services/hallOfFameService'
import {
  HallOfFame,
  RankedTeam,
  WinningTeam,
  WinningPlayer,
  RecordGame,
  RecordPlayer,
  PlayerWithMostPointsInYear,
} from 'types/hallOfFame'

// ============================================
// RESULT TYPES
// ============================================

export interface UseHallOfFameResult {
  hallOfFame: HallOfFame | null
  loading: boolean
  error: string | null
  refetch: () => Promise<void>
}

export interface UseAllTimeRankingsResult {
  rankings: RankedTeam[]
  loading: boolean
  error: string | null
  refetch: () => Promise<void>
}

export interface UseWinningTeamsResult {
  winningTeams: WinningTeam[]
  loading: boolean
  error: string | null
  refetch: () => Promise<void>
}

export interface UseWinningPlayersResult {
  winningPlayers: WinningPlayer[]
  loading: boolean
  error: string | null
  refetch: () => Promise<void>
}

export interface UseRecordsResult {
  recordGame: RecordGame | null
  recordPlayer: RecordPlayer | null
  playerWithMostPointsInYear: PlayerWithMostPointsInYear | null
  loading: boolean
  error: string | null
  refetch: () => Promise<void>
}

// ============================================
// MAIN HOOK - Complete Hall of Fame
// ============================================

/**
 * Hook to fetch complete Hall of Fame data for a league
 * @param groupId - The group ID
 * @param leagueId - The league ID
 * @param autoRefresh - Optional auto-refresh interval in milliseconds
 */
export function useHallOfFame(
  groupId: string | null,
  leagueId: string | null,
  autoRefresh?: number
): UseHallOfFameResult {
  const [hallOfFame, setHallOfFame] = useState<HallOfFame | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const fetchHallOfFame = useCallback(async () => {
    if (!groupId || !leagueId) {
      setHallOfFame(null)
      setError(null)
      return
    }

    setLoading(true)
    setError(null)

    try {
      const result = await hallOfFameService.getHallOfFame(groupId, leagueId)
      setHallOfFame(result)
      
      if (!result) {
        setError('Hall of Fame data not found')
      }
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to fetch Hall of Fame'
      console.error('Error in useHallOfFame:', errorMessage)
      setError(errorMessage)
      setHallOfFame(null)
    } finally {
      setLoading(false)
    }
  }, [groupId, leagueId])

  // Initial fetch
  useEffect(() => {
    fetchHallOfFame()
  }, [fetchHallOfFame])

  // Auto-refresh
  useEffect(() => {
    if (!autoRefresh || !groupId || !leagueId) return

    const intervalId = setInterval(fetchHallOfFame, autoRefresh)
    return () => clearInterval(intervalId)
  }, [autoRefresh, groupId, leagueId, fetchHallOfFame])

  return {
    hallOfFame,
    loading,
    error,
    refetch: fetchHallOfFame,
  }
}

// ============================================
// SPECIFIC DATA HOOKS
// ============================================

/**
 * Hook to fetch all-time rankings
 */
export function useAllTimeRankings(
  groupId: string | null,
  leagueId: string | null,
  sortBy: 'points' | 'value' = 'points'
): UseAllTimeRankingsResult {
  const [rankings, setRankings] = useState<RankedTeam[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const fetchRankings = useCallback(async () => {
    if (!groupId || !leagueId) {
      setRankings([])
      setError(null)
      return
    }

    setLoading(true)
    setError(null)

    try {
      const result = sortBy === 'points'
        ? await hallOfFameService.getAllTimeRankingsSortedByPoints(groupId, leagueId)
        : await hallOfFameService.getAllTimeRankingsSortedByValue(groupId, leagueId)
      
      setRankings(result)
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to fetch rankings'
      console.error('Error in useAllTimeRankings:', errorMessage)
      setError(errorMessage)
      setRankings([])
    } finally {
      setLoading(false)
    }
  }, [groupId, leagueId, sortBy])

  useEffect(() => {
    fetchRankings()
  }, [fetchRankings])

  return {
    rankings,
    loading,
    error,
    refetch: fetchRankings,
  }
}

/**
 * Hook to fetch winning teams
 */
export function useWinningTeams(
  groupId: string | null,
  leagueId: string | null
): UseWinningTeamsResult {
  const [winningTeams, setWinningTeams] = useState<WinningTeam[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const fetchWinningTeams = useCallback(async () => {
    if (!groupId || !leagueId) {
      setWinningTeams([])
      setError(null)
      return
    }

    setLoading(true)
    setError(null)

    try {
      const result = await hallOfFameService.getWinningTeamsSorted(groupId, leagueId)
      setWinningTeams(result)
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to fetch winning teams'
      console.error('Error in useWinningTeams:', errorMessage)
      setError(errorMessage)
      setWinningTeams([])
    } finally {
      setLoading(false)
    }
  }, [groupId, leagueId])

  useEffect(() => {
    fetchWinningTeams()
  }, [fetchWinningTeams])

  return {
    winningTeams,
    loading,
    error,
    refetch: fetchWinningTeams,
  }
}

/**
 * Hook to fetch winning players
 */
export function useWinningPlayers(
  groupId: string | null,
  leagueId: string | null
): UseWinningPlayersResult {
  const [winningPlayers, setWinningPlayers] = useState<WinningPlayer[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const fetchWinningPlayers = useCallback(async () => {
    if (!groupId || !leagueId) {
      setWinningPlayers([])
      setError(null)
      return
    }

    setLoading(true)
    setError(null)

    try {
      const result = await hallOfFameService.getWinningPlayersSorted(groupId, leagueId)
      setWinningPlayers(result)
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to fetch winning players'
      console.error('Error in useWinningPlayers:', errorMessage)
      setError(errorMessage)
      setWinningPlayers([])
    } finally {
      setLoading(false)
    }
  }, [groupId, leagueId])

  useEffect(() => {
    fetchWinningPlayers()
  }, [fetchWinningPlayers])

  return {
    winningPlayers,
    loading,
    error,
    refetch: fetchWinningPlayers,
  }
}

/**
 * Hook to fetch all records (game, player, yearly player)
 */
export function useRecords(
  groupId: string | null,
  leagueId: string | null
): UseRecordsResult {
  const [recordGame, setRecordGame] = useState<RecordGame | null>(null)
  const [recordPlayer, setRecordPlayer] = useState<RecordPlayer | null>(null)
  const [playerWithMostPointsInYear, setPlayerWithMostPointsInYear] = useState<PlayerWithMostPointsInYear | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const fetchRecords = useCallback(async () => {
    if (!groupId || !leagueId) {
      setRecordGame(null)
      setRecordPlayer(null)
      setPlayerWithMostPointsInYear(null)
      setError(null)
      return
    }

    setLoading(true)
    setError(null)

    try {
      const [game, player, yearPlayer] = await Promise.all([
        hallOfFameService.getRecordGame(groupId, leagueId),
        hallOfFameService.getRecordPlayer(groupId, leagueId),
        hallOfFameService.getPlayerWithMostPointsInYear(groupId, leagueId),
      ])

      setRecordGame(game)
      setRecordPlayer(player)
      setPlayerWithMostPointsInYear(yearPlayer)
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to fetch records'
      console.error('Error in useRecords:', errorMessage)
      setError(errorMessage)
      setRecordGame(null)
      setRecordPlayer(null)
      setPlayerWithMostPointsInYear(null)
    } finally {
      setLoading(false)
    }
  }, [groupId, leagueId])

  useEffect(() => {
    fetchRecords()
  }, [fetchRecords])

  return {
    recordGame,
    recordPlayer,
    playerWithMostPointsInYear,
    loading,
    error,
    refetch: fetchRecords,
  }
}

// ============================================
// SPECIALIZED HOOKS
// ============================================

/**
 * Hook to fetch top N teams by points
 */
export function useTopTeamsByPoints(
  groupId: string | null,
  leagueId: string | null,
  limit: number = 10
): UseAllTimeRankingsResult {
  const [rankings, setRankings] = useState<RankedTeam[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const fetchTopTeams = useCallback(async () => {
    if (!groupId || !leagueId) {
      setRankings([])
      setError(null)
      return
    }

    setLoading(true)
    setError(null)

    try {
      const result = await hallOfFameService.getTopTeamsByPoints(groupId, leagueId, limit)
      setRankings(result)
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to fetch top teams'
      console.error('Error in useTopTeamsByPoints:', errorMessage)
      setError(errorMessage)
      setRankings([])
    } finally {
      setLoading(false)
    }
  }, [groupId, leagueId, limit])

  useEffect(() => {
    fetchTopTeams()
  }, [fetchTopTeams])

  return {
    rankings,
    loading,
    error,
    refetch: fetchTopTeams,
  }
}

/**
 * Hook to fetch a specific team's all-time stats
 */
export function useTeamAllTimeStats(
  groupId: string | null,
  leagueId: string | null,
  teamOwner: string | null
): {
  teamStats: RankedTeam | null
  loading: boolean
  error: string | null
  refetch: () => Promise<void>
} {
  const [teamStats, setTeamStats] = useState<RankedTeam | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const fetchTeamStats = useCallback(async () => {
    if (!groupId || !leagueId || !teamOwner) {
      setTeamStats(null)
      setError(null)
      return
    }

    setLoading(true)
    setError(null)

    try {
      const result = await hallOfFameService.getTeamAllTimeStats(groupId, leagueId, teamOwner)
      setTeamStats(result)
      
      if (!result) {
        setError('Team stats not found')
      }
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to fetch team stats'
      console.error('Error in useTeamAllTimeStats:', errorMessage)
      setError(errorMessage)
      setTeamStats(null)
    } finally {
      setLoading(false)
    }
  }, [groupId, leagueId, teamOwner])

  useEffect(() => {
    fetchTeamStats()
  }, [fetchTeamStats])

  return {
    teamStats,
    loading,
    error,
    refetch: fetchTeamStats,
  }
}

/**
 * Hook to fetch champions by year
 */
export function useChampionsByYear(
  groupId: string | null,
  leagueId: string | null
): {
  champions: Record<number, string>
  loading: boolean
  error: string | null
  refetch: () => Promise<void>
} {
  const [champions, setChampions] = useState<Record<number, string>>({})
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const fetchChampions = useCallback(async () => {
    if (!groupId || !leagueId) {
      setChampions({})
      setError(null)
      return
    }

    setLoading(true)
    setError(null)

    try {
      const result = await hallOfFameService.getChampionsByYear(groupId, leagueId)
      setChampions(result)
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to fetch champions'
      console.error('Error in useChampionsByYear:', errorMessage)
      setError(errorMessage)
      setChampions({})
    } finally {
      setLoading(false)
    }
  }, [groupId, leagueId])

  useEffect(() => {
    fetchChampions()
  }, [fetchChampions])

  return {
    champions,
    loading,
    error,
    refetch: fetchChampions,
  }
}
