import { useState, useEffect, useCallback } from 'react'
import { groupService } from '../services/groupService'
import {
  Group,
  EnhancedGroup,
  League,
  AnnualLeague,
  Basket,
  UserOfAGroup,
  LeagueType
} from '../types/group'

// ============================================
// SINGLE GROUP HOOK
// ============================================

export interface UseGroupOptions {
  /** Auto-refresh interval in milliseconds (default: 0 = no auto-refresh) */
  refreshInterval?: number
}

export interface UseGroupResult {
  group: Group | null
  enhancedGroup: EnhancedGroup | null
  loading: boolean
  error: string | null
  refetch: () => Promise<void>
}

/**
 * Hook to manage a single group's data
 */
export function useGroup(groupId: string | null, options: UseGroupOptions = {}): UseGroupResult {
  const [group, setGroup] = useState<Group | null>(null)
  const [enhancedGroup, setEnhancedGroup] = useState<EnhancedGroup | null>(null)
  const [loading, setLoading] = useState<boolean>(false)
  const [error, setError] = useState<string | null>(null)

  const { refreshInterval = 0 } = options

  const fetchGroup = useCallback(async () => {
    if (!groupId) {
      setGroup(null)
      setEnhancedGroup(null)
      setError(null)
      return
    }

    setLoading(true)
    setError(null)

    try {
      // Fetch both regular and enhanced group data
      const [groupData, enhancedGroupData] = await Promise.all([
        groupService.getGroup(groupId),
        groupService.getEnhancedGroup(groupId)
      ])
      
      setGroup(groupData)
      setEnhancedGroup(enhancedGroupData)
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to fetch group'
      setError(errorMessage)
      console.error('Error fetching group:', err)
    } finally {
      setLoading(false)
    }
  }, [groupId])

  // Initial fetch
  useEffect(() => {
    fetchGroup()
  }, [fetchGroup])

  // Auto-refresh setup
  useEffect(() => {
    if (refreshInterval > 0) {
      const interval = setInterval(fetchGroup, refreshInterval)
      return () => clearInterval(interval)
    }
  }, [fetchGroup, refreshInterval])

  return {
    group,
    enhancedGroup,
    loading,
    error,
    refetch: fetchGroup
  }
}

// ============================================
// USER GROUPS HOOK
// ============================================

export interface UseUserGroupsResult {
  groups: Group[]
  loading: boolean
  error: string | null
  refetch: () => Promise<void>
}

/**
 * Hook to manage all user's groups
 */
export function useUserGroups(): UseUserGroupsResult {
  const [groups, setGroups] = useState<Group[]>([])
  const [loading, setLoading] = useState<boolean>(false)
  const [error, setError] = useState<string | null>(null)

  const fetchUserGroups = useCallback(async () => {
    setLoading(true)
    setError(null)

    try {
      const groupsData = await groupService.getUserGroups()
      setGroups(groupsData)
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to fetch user groups'
      setError(errorMessage)
      console.error('Error fetching user groups:', err)
    } finally {
      setLoading(false)
    }
  }, [])

  // Write operations would be implemented when needed
  // Currently focusing on read operations following serieAService pattern

  // Initial fetch
  useEffect(() => {
    fetchUserGroups()
  }, [fetchUserGroups])

  return {
    groups,
    loading,
    error,
    refetch: fetchUserGroups
  }
}

// ============================================
// GROUP LEAGUES HOOK
// ============================================

export interface UseGroupLeaguesResult {
  leagues: League[]
  annualLeagues: AnnualLeague[]
  loading: boolean
  error: string | null
  refetch: () => Promise<void>
  getAnnualLeague: (leagueId: string, year: number) => AnnualLeague | null
}

/**
 * Hook to manage leagues within a group
 */
export function useGroupLeagues(groupId: string | null, year?: number): UseGroupLeaguesResult {
  const [leagues, setLeagues] = useState<League[]>([])
  const [annualLeagues, setAnnualLeagues] = useState<AnnualLeague[]>([])
  const [loading, setLoading] = useState<boolean>(false)
  const [error, setError] = useState<string | null>(null)

  const fetchLeagues = useCallback(async () => {
    if (!groupId) {
      setLeagues([])
      setAnnualLeagues([])
      setError(null)
      return
    }

    setLoading(true)
    setError(null)

    try {
      const leaguesData = await groupService.getGroupLeagues(groupId)
      setLeagues(leaguesData)

      // If year is specified, also fetch annual leagues for that year
      if (year) {
        const annualLeaguesData = await groupService.getAnnualLeagues(groupId, year)
        setAnnualLeagues(annualLeaguesData)
      } else {
        setAnnualLeagues([])
      }
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to fetch group leagues'
      setError(errorMessage)
      console.error('Error fetching group leagues:', err)
    } finally {
      setLoading(false)
    }
  }, [groupId, year])

  const getAnnualLeague = useCallback((leagueId: string, targetYear: number): AnnualLeague | null => {
    const league = leagues.find(l => l.id === leagueId)
    return league?.years?.find(y => y.year === targetYear) || null
  }, [leagues])

  // Initial fetch
  useEffect(() => {
    fetchLeagues()
  }, [fetchLeagues])

  return {
    leagues,
    annualLeagues,
    loading,
    error,
    refetch: fetchLeagues,
    getAnnualLeague
  }
}

// ============================================
// GROUP BASKETS HOOK
// ============================================

export interface UseGroupBasketsResult {
  baskets: Basket[]
  loading: boolean
  error: string | null
  refetch: () => Promise<void>
  getBasketForOwner: (owner: string, year?: number) => string | null
}

/**
 * Hook to manage baskets within a group
 */
export function useGroupBaskets(groupId: string | null): UseGroupBasketsResult {
  const [baskets, setBaskets] = useState<Basket[]>([])
  const [loading, setLoading] = useState<boolean>(false)
  const [error, setError] = useState<string | null>(null)

  const fetchBaskets = useCallback(async () => {
    if (!groupId) {
      setBaskets([])
      setError(null)
      return
    }

    setLoading(true)
    setError(null)

    try {
      const basketsData = await groupService.getGroupBaskets(groupId)
      setBaskets(basketsData)
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to fetch group baskets'
      setError(errorMessage)
      console.error('Error fetching group baskets:', err)
    } finally {
      setLoading(false)
    }
  }, [groupId])

  const getBasketForOwner = useCallback((owner: string, year?: number): string | null => {
    if (!groupId) return null
    
    for (const basket of baskets) {
      if (basket.years != null) {
        for (const yearly of basket.years) {
          if (year == null || yearly.year === year) {
            if (yearly.teams.some(x => x.owner === owner)) {
              return basket.id
            }
          }
        }
      }
    }
    return null
  }, [baskets, groupId])

  // Write operations would be implemented when needed
  // Currently focusing on read operations following serieAService pattern

  // Initial fetch
  useEffect(() => {
    fetchBaskets()
  }, [fetchBaskets])

  return {
    baskets,
    loading,
    error,
    refetch: fetchBaskets,
    getBasketForOwner
  }
}

// ============================================
// GROUP USERS HOOK
// ============================================

export interface UseGroupUsersResult {
  users: UserOfAGroup[]
  activeUsers: UserOfAGroup[]
  adminUsers: UserOfAGroup[]
  loading: boolean
  error: string | null
  refetch: () => Promise<void>
}

/**
 * Hook to manage users within a group
 */
export function useGroupUsers(groupId: string | null): UseGroupUsersResult {
  const [users, setUsers] = useState<UserOfAGroup[]>([])
  const [activeUsers, setActiveUsers] = useState<UserOfAGroup[]>([])
  const [adminUsers, setAdminUsers] = useState<UserOfAGroup[]>([])
  const [loading, setLoading] = useState<boolean>(false)
  const [error, setError] = useState<string | null>(null)

  const fetchUsers = useCallback(async () => {
    if (!groupId) {
      setUsers([])
      setActiveUsers([])
      setAdminUsers([])
      setError(null)
      return
    }

    setLoading(true)
    setError(null)

    try {
      const [usersData, activeUsersData, adminUsersData] = await Promise.all([
        groupService.getGroupUsers(groupId),
        groupService.getActiveUsers(groupId),
        groupService.getAdminUsers(groupId)
      ])
      
      setUsers(usersData)
      setActiveUsers(activeUsersData)
      setAdminUsers(adminUsersData)
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to fetch group users'
      setError(errorMessage)
      console.error('Error fetching group users:', err)
    } finally {
      setLoading(false)
    }
  }, [groupId])

  // Write operations would be implemented when needed
  // Currently focusing on read operations following serieAService pattern

  // Initial fetch
  useEffect(() => {
    fetchUsers()
  }, [fetchUsers])

  return {
    users,
    activeUsers,
    adminUsers,
    loading,
    error,
    refetch: fetchUsers
  }
}