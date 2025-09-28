import React, { createContext, useContext, useEffect, useState, ReactNode } from 'react'
import { useAuth } from './AuthProvider'
import { groupService } from '../services/groupService'
import { authService } from '../services/authService'
import { useGlobalLoading } from './LoadingProvider'
import { EnhancedGroup, enhanceGroup, Group, League } from 'types/group'

// Types
export interface AppState {
  currentGroup: Group | null
  enhancedGroup: EnhancedGroup | null
  userGroups: Group[]
  isGroupsLoading: boolean
  groupsError: string | null
  selectedLeagueId: string | null
  selectedYear: number | null
}

export interface AppContextType extends AppState {
  refreshGroups: () => Promise<void>
  setCurrentGroup: (groupId: string | null) => Promise<void>
  setSelectedLeague: (leagueId: string | null) => Promise<void>
  setSelectedYear: (year: number | null) => Promise<void>
  getAvailableLeagues: () => League[]
  getAvailableYears: (leagueId?: string) => number[]
  clearError: () => void
}

// Context
const AppContext = createContext<AppContextType | undefined>(undefined)

// Hook
export const useApp = (): AppContextType => {
  const context = useContext(AppContext)
  if (!context) {
    throw new Error('useApp must be used within an AppProvider')
  }
  return context
}

// Provider Props
interface AppProviderProps {
  children: ReactNode
}

export function AppProvider({ children }: AppProviderProps) {
  const { isAuthenticated, user, isLoading: authLoading, updateUser } = useAuth()
  const { showLoading, hideLoading } = useGlobalLoading()
  
  // App state
  const [currentGroup, setCurrentGroupState] = useState<Group | null>(null)
  const [enhancedGroup, setEnhancedGroup] = useState<EnhancedGroup | null>(null)
  const [userGroups, setUserGroups] = useState<Group[]>([])
  const [isGroupsLoading, setIsGroupsLoading] = useState<boolean>(false)
  const [groupsError, setGroupsError] = useState<string | null>(null)
  const [selectedLeagueId, setSelectedLeagueId] = useState<string | null>(null)
  const [selectedYear, setSelectedYear] = useState<number | null>(null)

  // Load user groups when user is authenticated
  const loadUserGroups = async () => {
    if (!isAuthenticated || !user) {
      setUserGroups([])
      setCurrentGroupState(null)
      setEnhancedGroup(null)
      return
    }

    try {
      setIsGroupsLoading(true)
      setGroupsError(null)
      showLoading('Caricamento gruppi...')
      
      // Load all user groups
      const groups = await groupService.getUserGroups()
      setUserGroups(groups)
      
      // Set current group based on user state or first available group
      let targetGroupId: string | null = user.state.groupId
      
      // If no group in user state or group not found, use first available group
      if (!targetGroupId || !groups.find(g => g.id === targetGroupId)) {
        targetGroupId = groups.length > 0 ? groups[0].id : null
      }
      
      if (targetGroupId) {
        await setCurrentGroupInternal(targetGroupId)
      }
      
    } catch (error) {
      console.error('Error loading user groups:', error)
      setGroupsError(error instanceof Error ? error.message : 'Errore nel caricamento dei gruppi')
    } finally {
      setIsGroupsLoading(false)
      hideLoading()
    }
  }

  // Internal method to set current group
  const setCurrentGroupInternal = async (groupId: string) => {
    try {
      const group = userGroups.find(g => g.id === groupId)
      if (group) {
        setCurrentGroupState(group)
        setEnhancedGroup(enhanceGroup(group))
      } else {
        // Group not in cache, fetch it
        const fetchedGroup = await groupService.getGroup(groupId)
        if (fetchedGroup) {
          setCurrentGroupState(fetchedGroup)
          setEnhancedGroup(enhanceGroup(fetchedGroup))
        }
      }
    } catch (error) {
      console.error('Error setting current group:', error)
      setGroupsError(error instanceof Error ? error.message : 'Errore nel caricamento del gruppo')
    }
  }

  // Public method to set current group
  const setCurrentGroup = async (groupId: string | null) => {
    if (!groupId) {
      setCurrentGroupState(null)
      setEnhancedGroup(null)
      return
    }
    
    // Update local state
    await setCurrentGroupInternal(groupId)
    
    // Update backend user state with current local state
    if (user && user.rawData) {
      try {
        // Build updated user with current local state
        const currentUpdatedUser = {
          ...user.rawData,
          state: {
            ...user.rawData.state,
            groupId: groupId,
            leagueId: selectedLeagueId || user.rawData.state.leagueId,
            year: selectedYear || user.rawData.state.year,
            raw: {
              ...user.rawData.state.raw,
              g: groupId,
              l: selectedLeagueId || user.rawData.state.raw.l,
              y: selectedYear || user.rawData.state.raw.y
            }
          },
          raw: {
            ...user.rawData.raw,
            s: {
              ...user.rawData.raw.s,
              g: groupId,
              l: selectedLeagueId || user.rawData.raw.s.l,
              y: selectedYear || user.rawData.raw.s.y
            }
          }
        }
        
        await authService.updateUserState(currentUpdatedUser)
        console.log('✅ User group updated on backend with full state')
        
        // Update the auth context with the complete updated data
        updateUser(currentUpdatedUser)
        
      } catch (error) {
        console.error('❌ Failed to update user group on backend:', error)
        // Note: We don't revert local state to avoid UI flickering
        // The user can try again or the change will sync later
      }
    }
  }

  // Refresh groups
  const refreshGroups = async () => {
    await loadUserGroups()
  }

  // Clear error
  const clearError = () => {
    setGroupsError(null)
  }

  // League and year management functions
  const setSelectedLeague = async (leagueId: string | null) => {
    // Update local state
    setSelectedLeagueId(leagueId)
    
    // When switching league, check if current year is available
    if (leagueId && currentGroup) {
      const league = currentGroup.leagues.find(l => l.id === leagueId)
      if (league && selectedYear) {
        const availableYears = league.years.map(y => y.year)
        if (!availableYears.includes(selectedYear)) {
          // Set to latest available year
          const latestYear = Math.max(...availableYears)
          setSelectedYear(latestYear)
        }
      }
    }
    
    // Update backend user state with current local state
    if (user && user.rawData && leagueId) {
      try {
        // Build updated user with current local state
        const currentUpdatedUser = {
          ...user.rawData,
          state: {
            ...user.rawData.state,
            groupId: currentGroup?.id || user.rawData.state.groupId,
            leagueId: leagueId,
            year: selectedYear || user.rawData.state.year,
            raw: {
              ...user.rawData.state.raw,
              g: currentGroup?.id || user.rawData.state.raw.g,
              l: leagueId,
              y: selectedYear || user.rawData.state.raw.y
            }
          },
          raw: {
            ...user.rawData.raw,
            s: {
              ...user.rawData.raw.s,
              g: currentGroup?.id || user.rawData.raw.s.g,
              l: leagueId,
              y: selectedYear || user.rawData.raw.s.y
            }
          }
        }
        
        await authService.updateUserState(currentUpdatedUser)
        console.log('✅ User league updated on backend with full state')
        
        // Update the auth context with the complete updated data
        updateUser(currentUpdatedUser)
        
      } catch (error) {
        console.error('❌ Failed to update user league on backend:', error)
      }
    }
  }

  const setSelectedYearFunc = async (year: number | null) => {
    // Update local state
    setSelectedYear(year)
    
    // Update backend user state with current local state
    if (user && user.rawData && year !== null) {
      try {
        // Build updated user with current local state
        const currentUpdatedUser = {
          ...user.rawData,
          state: {
            ...user.rawData.state,
            groupId: currentGroup?.id || user.rawData.state.groupId,
            leagueId: selectedLeagueId || user.rawData.state.leagueId,
            year: year,
            raw: {
              ...user.rawData.state.raw,
              g: currentGroup?.id || user.rawData.state.raw.g,
              l: selectedLeagueId || user.rawData.state.raw.l,
              y: year
            }
          },
          raw: {
            ...user.rawData.raw,
            s: {
              ...user.rawData.raw.s,
              g: currentGroup?.id || user.rawData.raw.s.g,
              l: selectedLeagueId || user.rawData.raw.s.l,
              y: year
            }
          }
        }
        
        await authService.updateUserState(currentUpdatedUser)
        console.log('✅ User year updated on backend with full state')
        
        // Update the auth context with the complete updated data
        updateUser(currentUpdatedUser)
        
      } catch (error) {
        console.error('❌ Failed to update user year on backend:', error)
      }
    }
  }

  const getAvailableLeagues = () => {
    return currentGroup?.leagues || []
  }

  const getAvailableYears = (leagueId?: string) => {
    if (!currentGroup) return []
    
    const targetLeagueId = leagueId || selectedLeagueId
    if (!targetLeagueId) return []
    
    const league = currentGroup.leagues.find(l => l.id === targetLeagueId)
    return league?.years.map(y => y.year) || []
  }

  // Effect to load groups when auth state changes
  useEffect(() => {
    if (isAuthenticated && user && !authLoading) {
      loadUserGroups()
    } else if (!isAuthenticated) {
      // Clear groups when user logs out
      setUserGroups([])
      setCurrentGroupState(null)
      setEnhancedGroup(null)
      setGroupsError(null)
      setSelectedLeagueId(null)
      setSelectedYear(null)
    }
  }, [isAuthenticated, user, authLoading])

  // Effect to initialize league and year from user state when user is loaded
  useEffect(() => {
    if (user && user.state) {
      // Initialize selected league and year from user's saved state
      if (user.state.leagueId && user.state.leagueId !== selectedLeagueId) {
        setSelectedLeagueId(user.state.leagueId)
      }
      if (user.state.year && user.state.year !== selectedYear) {
        setSelectedYear(user.state.year)
      }
    }
  }, [user]) // Only depend on user, not on selectedLeagueId/selectedYear to avoid loops

  // Effect to set default league and year when current group changes (only if no user preference)
  useEffect(() => {
    if (currentGroup && currentGroup.leagues.length > 0) {
      // Only set defaults if user doesn't have saved preferences
      const hasUserLeaguePreference = user?.state?.leagueId
      const hasUserYearPreference = user?.state?.year
      
      if (!hasUserLeaguePreference || !currentGroup.leagues.find(l => l.id === user.state.leagueId)) {
        // Set main league as default or first league
        const mainLeague = currentGroup.leagues.find(l => l.isMain) || currentGroup.leagues[0]
        if (!selectedLeagueId || !currentGroup.leagues.find(l => l.id === selectedLeagueId)) {
          setSelectedLeagueId(mainLeague.id)
        }
      }
      
      if (!hasUserYearPreference && selectedLeagueId) {
        // Set latest year as default
        const league = currentGroup.leagues.find(l => l.id === selectedLeagueId)
        if (league && league.years.length > 0 && !selectedYear) {
          const latestYear = Math.max(...league.years.map(y => y.year))
          setSelectedYear(latestYear)
        }
      }
    }
  }, [currentGroup, selectedLeagueId]) // Include selectedLeagueId to handle league changes

  // Note: Global loading state is managed directly in the AuthGuard component

  const contextValue: AppContextType = {
    // State
    currentGroup,
    enhancedGroup,
    userGroups,
    isGroupsLoading,
    groupsError,
    selectedLeagueId,
    selectedYear,
    
    // Actions
    refreshGroups,
    setCurrentGroup,
    setSelectedLeague,
    setSelectedYear: setSelectedYearFunc,
    getAvailableLeagues,
    getAvailableYears,
    clearError
  }

  return (
    <AppContext.Provider value={contextValue}>
      {children}
    </AppContext.Provider>
  )
}