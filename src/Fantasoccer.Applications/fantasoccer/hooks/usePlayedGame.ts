import { useState, useEffect, useCallback } from 'react'
import { playedGameService } from '../services/playedGameService'
import {
  GameKey,
  EnhancedPlayedGame,
  PlayedGame,
  PlayedGameScore,
  GameKeyHelper,
  PlayedGameHelper
} from '../types/game'
import { Player } from '../types/team'
import { Role } from '../types/group'

export interface UsePlayedGameOptions {
  refreshInterval?: number
  skip?: boolean
}

export interface UsePlayedGameResult {
  game: PlayedGame | null
  enhancedGame: EnhancedPlayedGame | null
  loading: boolean
  error: string | null
  refetch: () => Promise<void>
  getPlayersByRole: (role: Role, team?: 'home' | 'away' | 'both') => Player[]
  getScore: () => PlayedGameScore | null
  getMVP: () => Player | null
}

export function usePlayedGame(key: GameKey | null, options: UsePlayedGameOptions = {}): UsePlayedGameResult {
  const { refreshInterval = 0, skip = false } = options

  const [game, setGame] = useState<PlayedGame | null>(null)
  const [enhancedGame, setEnhancedGame] = useState<EnhancedPlayedGame | null>(null)
  const [loading, setLoading] = useState<boolean>(false)
  const [error, setError] = useState<string | null>(null)

  const fetchGame = useCallback(async (silent = false) => {
    if (!key || skip) {
      setGame(null)
      setEnhancedGame(null)
      setError(null)
      return
    }

    if (!silent) {
      setLoading(true)
    }
    setError(null)

    try {
      const [gameData, enhancedData] = await Promise.all([
        playedGameService.getPlayedGame(key),
        playedGameService.getEnhancedPlayedGame(key)
      ])

      if (!gameData) {
        setGame(null)
        setEnhancedGame(null)
        setError('Played game not available')
        return
      }

      setGame(gameData)
      setEnhancedGame(enhancedData)
    } catch (err) {
      console.error('Error fetching played game:', err)
      setError(err instanceof Error ? err.message : 'Failed to fetch played game')
    } finally {
      if (!silent) {
        setLoading(false)
      }
    }
  }, [key, skip])

  useEffect(() => {
    fetchGame(false)
  }, [fetchGame])

  useEffect(() => {
    if (refreshInterval <= 0) {
      return
    }

    const intervalId = setInterval(() => {
      fetchGame(true)
    }, refreshInterval)

    return () => clearInterval(intervalId)
  }, [fetchGame, refreshInterval])

  const getPlayersByRole = useCallback((role: Role, team: 'home' | 'away' | 'both' = 'both'): Player[] => {
    if (!key) return []
    const targetGame = enhancedGame ?? game
    if (!targetGame) return []

    if (team === 'home' || team === 'away') {
      return PlayedGameHelper.getPlayersByRole(targetGame, role, team)
    }

    const home = PlayedGameHelper.getPlayersByRole(targetGame, role, 'home')
    const away = PlayedGameHelper.getPlayersByRole(targetGame, role, 'away')
    return [...home, ...away]
  }, [game, enhancedGame, key])

  const getScore = useCallback((): PlayedGameScore | null => {
    if (!key) return null
    const targetGame = enhancedGame ?? game
    return targetGame ? PlayedGameHelper.getScore(targetGame) : null
  }, [game, enhancedGame, key])

  const getMVP = useCallback((): Player | null => {
    const targetGame = enhancedGame ?? game
    return targetGame ? PlayedGameHelper.getMVP(targetGame) : null
  }, [game, enhancedGame])

  const refetch = useCallback(() => fetchGame(false), [fetchGame])

  return {
    game,
    enhancedGame,
    loading,
    error,
    refetch,
    getPlayersByRole,
    getScore,
    getMVP
  }
}

export interface UsePlayedGameByComponentsOptions extends UsePlayedGameOptions {
  group: string | null
  league: string | null
  year: number | null
  day: number | null
  gameId: string | null
}

export function usePlayedGameByComponents(options: UsePlayedGameByComponentsOptions): UsePlayedGameResult {
  const { group, league, year, day, gameId, ...hookOptions } = options

  const key = group && league && year != null && day != null && gameId
    ? GameKeyHelper.create(group, league, year, day, gameId)
    : null

  return usePlayedGame(key, hookOptions)
}
