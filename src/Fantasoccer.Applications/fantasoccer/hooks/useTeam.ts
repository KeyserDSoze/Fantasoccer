import { useState, useEffect, useCallback } from 'react'
import { teamService } from '../services/teamService'
import {
  Team,
  EnhancedTeam,
  TeamKey,
  TeamDayKey,
  TeamKeyHelper,
  TeamHelper,
  Player
} from '../types/team'
import { Role } from '../types/group'

export interface UseTeamOptions {
  refreshInterval?: number
  skip?: boolean
}

export interface UseTeamResult {
  team: Team | null
  enhancedTeam: EnhancedTeam | null
  loading: boolean
  error: string | null
  refetch: () => Promise<void>
  getActivePlayers: () => Player[]
  getPlayersByRole: (role: Role) => Player[]
  getNetCost: () => number
}

export function useTeam(key: TeamKey | null, options: UseTeamOptions = {}): UseTeamResult {
  const { refreshInterval = 0, skip = false } = options

  const [team, setTeam] = useState<Team | null>(null)
  const [enhancedTeam, setEnhancedTeam] = useState<EnhancedTeam | null>(null)
  const [loading, setLoading] = useState<boolean>(false)
  const [error, setError] = useState<string | null>(null)

  const fetchTeam = useCallback(async (silent = false) => {
    if (!key || skip) {
      setTeam(null)
      setEnhancedTeam(null)
      setError(null)
      return
    }

    if (!silent) {
      setLoading(true)
    }
    setError(null)

    try {
      const [teamData, enhancedData] = await Promise.all([
        teamService.getTeam(key),
        teamService.getEnhancedTeam(key)
      ])

      if (!teamData) {
        setTeam(null)
        setEnhancedTeam(null)
        setError('Team not available')
        return
      }

      setTeam(teamData)
      setEnhancedTeam(enhancedData)
    } catch (err) {
      console.error('Error fetching team:', err)
      setError(err instanceof Error ? err.message : 'Failed to fetch team')
    } finally {
      if (!silent) {
        setLoading(false)
      }
    }
  }, [key, skip])

  useEffect(() => {
    fetchTeam(false)
  }, [fetchTeam])

  useEffect(() => {
    if (refreshInterval <= 0) {
      return
    }

    const intervalId = setInterval(() => {
      fetchTeam(true)
    }, refreshInterval)

    return () => clearInterval(intervalId)
  }, [fetchTeam, refreshInterval])

  const getActivePlayers = useCallback((): Player[] => {
    const target = enhancedTeam ?? team
    return target ? TeamHelper.getActivePlayers(target) : []
  }, [team, enhancedTeam])

  const getPlayersByRole = useCallback((role: Role): Player[] => {
    const target = enhancedTeam ?? team
    return target ? TeamHelper.getPlayersByRole(target, role) : []
  }, [team, enhancedTeam])

  const getNetCost = useCallback((): number => {
    const target = enhancedTeam ?? team
    return target ? TeamHelper.getNetCost(target) : 0
  }, [team, enhancedTeam])

  const refetch = useCallback(() => fetchTeam(false), [fetchTeam])

  return {
    team,
    enhancedTeam,
    loading,
    error,
    refetch,
    getActivePlayers,
    getPlayersByRole,
    getNetCost
  }
}

export interface UseTeamByComponentsOptions extends UseTeamOptions {
  group: string | null
  year: number | null
  basketId: string | null
  email: string | null
}

export function useTeamByComponents(options: UseTeamByComponentsOptions): UseTeamResult {
  const { group, year, basketId, email, ...hookOptions } = options

  const key: TeamKey | null = group && basketId && email && year != null
    ? TeamKeyHelper.create(group, year, basketId, email)
    : null

  return useTeam(key, hookOptions)
}

export interface UseTeamDayOptions extends UseTeamOptions {}

export interface UseTeamDayResult {
  team: Team | null
  enhancedTeam: EnhancedTeam | null
  loading: boolean
  error: string | null
  refetch: () => Promise<void>
  getActivePlayers: () => Player[]
  getPlayersByRole: (role: Role) => Player[]
  getNetCost: () => number
}

export function useTeamDay(key: TeamDayKey | null, options: UseTeamDayOptions = {}): UseTeamDayResult {
  const { refreshInterval = 0, skip = false } = options

  const [team, setTeam] = useState<Team | null>(null)
  const [enhancedTeam, setEnhancedTeam] = useState<EnhancedTeam | null>(null)
  const [loading, setLoading] = useState<boolean>(false)
  const [error, setError] = useState<string | null>(null)

  const fetchTeam = useCallback(async (silent = false) => {
    if (!key || skip) {
      setTeam(null)
      setEnhancedTeam(null)
      setError(null)
      return
    }

    if (!silent) {
      setLoading(true)
    }
    setError(null)

    try {
      const [teamData, enhancedData] = await Promise.all([
        teamService.getTeamDay(key),
        teamService.getEnhancedTeamDay(key)
      ])

      if (!teamData) {
        setTeam(null)
        setEnhancedTeam(null)
        setError('Team day not available')
        return
      }

      setTeam(teamData)
      setEnhancedTeam(enhancedData)
    } catch (err) {
      console.error('Error fetching team day:', err)
      setError(err instanceof Error ? err.message : 'Failed to fetch team day')
    } finally {
      if (!silent) {
        setLoading(false)
      }
    }
  }, [key, skip])

  useEffect(() => {
    fetchTeam(false)
  }, [fetchTeam])

  useEffect(() => {
    if (refreshInterval <= 0) {
      return
    }

    const intervalId = setInterval(() => {
      fetchTeam(true)
    }, refreshInterval)

    return () => clearInterval(intervalId)
  }, [fetchTeam, refreshInterval])

  const getActivePlayers = useCallback((): Player[] => {
    const target = enhancedTeam ?? team
    return target ? TeamHelper.getActivePlayers(target) : []
  }, [team, enhancedTeam])

  const getPlayersByRole = useCallback((role: Role): Player[] => {
    const target = enhancedTeam ?? team
    return target ? TeamHelper.getPlayersByRole(target, role) : []
  }, [team, enhancedTeam])

  const getNetCost = useCallback((): number => {
    const target = enhancedTeam ?? team
    return target ? TeamHelper.getNetCost(target) : 0
  }, [team, enhancedTeam])

  const refetch = useCallback(() => fetchTeam(false), [fetchTeam])

  return {
    team,
    enhancedTeam,
    loading,
    error,
    refetch,
    getActivePlayers,
    getPlayersByRole,
    getNetCost
  }
}

export interface UseTeamDayByComponentsOptions extends UseTeamDayOptions {
  group: string | null
  year: number | null
  basketId: string | null
  day: number | null
  email: string | null
}

export function useTeamDayByComponents(options: UseTeamDayByComponentsOptions): UseTeamDayResult {
  const { group, year, basketId, day, email, ...hookOptions } = options

  const key: TeamDayKey | null = group && basketId && email && year != null && day != null
    ? TeamKeyHelper.createDayKey(group, year, basketId, day, email)
    : null

  return useTeamDay(key, hookOptions)
}
