import { useState, useEffect, useCallback } from 'react'
import { appIdentityService } from '../services/appIdentityService'
import {
  AppIdentity,
  AppIdentityState,
  GroupForIdentity,
  IdentityRole
} from '../types/identity'

// ============================================
// SINGLE APP IDENTITY HOOK
// ============================================

export interface UseAppIdentityOptions {
  /** Auto-refresh interval in milliseconds (default: 0 = no auto-refresh) */
  refreshInterval?: number
}

export interface UseAppIdentityResult {
  identity: AppIdentity | null
  loading: boolean
  error: string | null
  refetch: () => Promise<void>
}

/**
 * Hook to manage a single user's identity data
 * @param email - User's email address (used as key)
 */
export function useAppIdentity(
  email: string | null, 
  options: UseAppIdentityOptions = {}
): UseAppIdentityResult {
  const [identity, setIdentity] = useState<AppIdentity | null>(null)
  const [loading, setLoading] = useState<boolean>(false)
  const [error, setError] = useState<string | null>(null)

  const { refreshInterval = 0 } = options

  const fetchIdentity = useCallback(async () => {
    if (!email) {
      setIdentity(null)
      setError(null)
      return
    }

    setLoading(true)
    setError(null)

    try {
      const identityData = await appIdentityService.getAppIdentity(email)
      setIdentity(identityData)
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to fetch app identity'
      setError(errorMessage)
      console.error('Error fetching app identity:', err)
    } finally {
      setLoading(false)
    }
  }, [email])

  // Initial fetch
  useEffect(() => {
    fetchIdentity()
  }, [fetchIdentity])

  // Auto-refresh setup
  useEffect(() => {
    if (refreshInterval > 0 && email) {
      const interval = setInterval(fetchIdentity, refreshInterval)
      return () => clearInterval(interval)
    }
  }, [fetchIdentity, refreshInterval, email])

  return {
    identity,
    loading,
    error,
    refetch: fetchIdentity
  }
}

// ============================================
// ALL IDENTITIES HOOK (Super Admin)
// ============================================

export interface UseAllIdentitiesOptions {
  /** Auto-refresh interval in milliseconds (default: 0 = no auto-refresh) */
  refreshInterval?: number
}

export interface UseAllIdentitiesResult {
  identities: AppIdentity[]
  loading: boolean
  error: string | null
  refetch: () => Promise<void>
}

/**
 * Hook to get all user identities (Super Admin only)
 * Fetches complete list of all users in the system
 */
export function useAllIdentities(
  options: UseAllIdentitiesOptions = {}
): UseAllIdentitiesResult {
  const [identities, setIdentities] = useState<AppIdentity[]>([])
  const [loading, setLoading] = useState<boolean>(false)
  const [error, setError] = useState<string | null>(null)

  const { refreshInterval = 0 } = options

  const fetchIdentities = useCallback(async () => {
    setLoading(true)
    setError(null)

    try {
      const allIdentities = await appIdentityService.getAllIdentities()
      setIdentities(allIdentities)
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to fetch all identities'
      setError(errorMessage)
      console.error('Error fetching all identities:', err)
    } finally {
      setLoading(false)
    }
  }, [])

  // Initial fetch
  useEffect(() => {
    fetchIdentities()
  }, [fetchIdentities])

  // Auto-refresh setup
  useEffect(() => {
    if (refreshInterval > 0) {
      const interval = setInterval(fetchIdentities, refreshInterval)
      return () => clearInterval(interval)
    }
  }, [fetchIdentities, refreshInterval])

  return {
    identities,
    loading,
    error,
    refetch: fetchIdentities
  }
}

// ============================================
// USER STATE HOOK
// ============================================

export interface UseUserStateResult {
  state: AppIdentityState | null
  loading: boolean
  error: string | null
  updateState: (stateUpdates: Partial<AppIdentityState>) => Promise<boolean>
  updateGroup: (groupId: string) => Promise<boolean>
  updateLeague: (leagueId: string) => Promise<boolean>
  updateYear: (year: number) => Promise<boolean>
  updateWorkingEmail: (workingEmail: string) => Promise<boolean>
  refetch: () => Promise<void>
}

/**
 * Hook to manage user's state
 */
export function useUserState(email: string | null): UseUserStateResult {
  const [state, setState] = useState<AppIdentityState | null>(null)
  const [loading, setLoading] = useState<boolean>(false)
  const [error, setError] = useState<string | null>(null)

  const fetchState = useCallback(async () => {
    if (!email) {
      setState(null)
      setError(null)
      return
    }

    setLoading(true)
    setError(null)

    try {
      const stateData = await appIdentityService.getUserState(email)
      setState(stateData)
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to fetch user state'
      setError(errorMessage)
      console.error('Error fetching user state:', err)
    } finally {
      setLoading(false)
    }
  }, [email])

  const updateState = useCallback(async (stateUpdates: Partial<AppIdentityState>) => {
    if (!email) return false

    try {
      const success = await appIdentityService.updateUserState(email, stateUpdates)
      if (success) {
        await fetchState()
      }
      return success
    } catch (err) {
      console.error('Error updating user state:', err)
      return false
    }
  }, [email, fetchState])

  const updateGroup = useCallback(async (groupId: string) => {
    if (!email) return false

    try {
      const success = await appIdentityService.updateUserGroup(email, groupId)
      if (success) {
        await fetchState()
      }
      return success
    } catch (err) {
      console.error('Error updating user group:', err)
      return false
    }
  }, [email, fetchState])

  const updateLeague = useCallback(async (leagueId: string) => {
    if (!email) return false

    try {
      const success = await appIdentityService.updateUserLeague(email, leagueId)
      if (success) {
        await fetchState()
      }
      return success
    } catch (err) {
      console.error('Error updating user league:', err)
      return false
    }
  }, [email, fetchState])

  const updateYear = useCallback(async (year: number) => {
    if (!email) return false

    try {
      const success = await appIdentityService.updateUserYear(email, year)
      if (success) {
        await fetchState()
      }
      return success
    } catch (err) {
      console.error('Error updating user year:', err)
      return false
    }
  }, [email, fetchState])

  const updateWorkingEmail = useCallback(async (workingEmail: string) => {
    if (!email) return false

    try {
      const success = await appIdentityService.updateUserWorkingEmail(email, workingEmail)
      if (success) {
        await fetchState()
      }
      return success
    } catch (err) {
      console.error('Error updating user working email:', err)
      return false
    }
  }, [email, fetchState])

  // Initial fetch
  useEffect(() => {
    fetchState()
  }, [fetchState])

  return {
    state,
    loading,
    error,
    updateState,
    updateGroup,
    updateLeague,
    updateYear,
    updateWorkingEmail,
    refetch: fetchState
  }
}

// ============================================
// USER GROUPS HOOK
// ============================================

export interface UseUserGroupsResult {
  groups: GroupForIdentity[]
  loading: boolean
  error: string | null
  addGroup: (newGroup: { id: string; name: string; role: IdentityRole }) => Promise<boolean>
  removeGroup: (groupId: string) => Promise<boolean>
  updateRoleInGroup: (groupId: string, newRole: IdentityRole) => Promise<boolean>
  hasRoleInGroup: (groupId: string, role: IdentityRole) => boolean
  isAdminInGroup: (groupId: string) => boolean
  isParticipantInGroup: (groupId: string) => boolean
  refetch: () => Promise<void>
}

/**
 * Hook to manage user's groups
 */
export function useUserGroups(email: string | null): UseUserGroupsResult {
  const [groups, setGroups] = useState<GroupForIdentity[]>([])
  const [loading, setLoading] = useState<boolean>(false)
  const [error, setError] = useState<string | null>(null)

  const fetchGroups = useCallback(async () => {
    if (!email) {
      setGroups([])
      setError(null)
      return
    }

    setLoading(true)
    setError(null)

    try {
      const groupsData = await appIdentityService.getUserGroups(email)
      setGroups(groupsData)
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to fetch user groups'
      setError(errorMessage)
      console.error('Error fetching user groups:', err)
    } finally {
      setLoading(false)
    }
  }, [email])

  const addGroup = useCallback(async (newGroup: { id: string; name: string; role: IdentityRole }) => {
    if (!email) return false

    try {
      const success = await appIdentityService.addUserGroup(email, newGroup)
      if (success) {
        await fetchGroups()
      }
      return success
    } catch (err) {
      console.error('Error adding user group:', err)
      return false
    }
  }, [email, fetchGroups])

  const removeGroup = useCallback(async (groupId: string) => {
    if (!email) return false

    try {
      const success = await appIdentityService.removeUserGroup(email, groupId)
      if (success) {
        await fetchGroups()
      }
      return success
    } catch (err) {
      console.error('Error removing user group:', err)
      return false
    }
  }, [email, fetchGroups])

  const updateRoleInGroup = useCallback(async (groupId: string, newRole: IdentityRole) => {
    if (!email) return false

    try {
      const success = await appIdentityService.updateUserRoleInGroup(email, groupId, newRole)
      if (success) {
        await fetchGroups()
      }
      return success
    } catch (err) {
      console.error('Error updating role in group:', err)
      return false
    }
  }, [email, fetchGroups])

  const hasRoleInGroup = useCallback((groupId: string, role: IdentityRole) => {
    const group = groups.find(g => g.id === groupId)
    if (!group) return false
    return (group.role & role) === role
  }, [groups])

  const isAdminInGroup = useCallback((groupId: string) => {
    return hasRoleInGroup(groupId, IdentityRole.Admin)
  }, [hasRoleInGroup])

  const isParticipantInGroup = useCallback((groupId: string) => {
    return hasRoleInGroup(groupId, IdentityRole.Participant)
  }, [hasRoleInGroup])

  // Initial fetch
  useEffect(() => {
    fetchGroups()
  }, [fetchGroups])

  return {
    groups,
    loading,
    error,
    addGroup,
    removeGroup,
    updateRoleInGroup,
    hasRoleInGroup,
    isAdminInGroup,
    isParticipantInGroup,
    refetch: fetchGroups
  }
}

// ============================================
// IDENTITY OPERATIONS HOOK
// ============================================

export interface UseAppIdentityOperationsResult {
  createIdentity: (identity: AppIdentity) => Promise<boolean>
  updateIdentity: (email: string, identity: AppIdentity) => Promise<boolean>
  deleteIdentity: (email: string) => Promise<boolean>
}

/**
 * Hook for identity CRUD operations
 */
export function useAppIdentityOperations(): UseAppIdentityOperationsResult {
  const createIdentity = useCallback(async (identity: AppIdentity) => {
    try {
      return await appIdentityService.insertAppIdentity(identity)
    } catch (err) {
      console.error('Error creating identity:', err)
      return false
    }
  }, [])

  const updateIdentity = useCallback(async (email: string, identity: AppIdentity) => {
    try {
      return await appIdentityService.updateAppIdentity(email, identity)
    } catch (err) {
      console.error('Error updating identity:', err)
      return false
    }
  }, [])

  const deleteIdentity = useCallback(async (email: string) => {
    try {
      return await appIdentityService.deleteAppIdentity(email)
    } catch (err) {
      console.error('Error deleting identity:', err)
      return false
    }
  }, [])

  return {
    createIdentity,
    updateIdentity,
    deleteIdentity
  }
}
