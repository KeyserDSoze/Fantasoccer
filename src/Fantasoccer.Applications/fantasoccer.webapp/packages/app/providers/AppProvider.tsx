'use client'

import React, { createContext, useContext, useEffect, useState, ReactNode } from 'react'
import { useAuth } from '../auth'
import { groupService } from '../services/groupService'
import { Group, EnhancedGroup, enhanceGroup } from '../types/group'
import { useGlobalLoading } from './LoadingProvider'

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
  setSelectedLeague: (leagueId: string | null) => void
  setSelectedYear: (year: number | null) => void
  getAvailableLeagues: () => import('../types/group').League[]
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
  const { isAuthenticated, user, isLoading: authLoading } = useAuth()
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
    
    await setCurrentGroupInternal(groupId)
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
  const setSelectedLeague = (leagueId: string | null) => {
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
  }

  const setSelectedYearFunc = (year: number | null) => {
    setSelectedYear(year)
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

  // Effect to set default league and year when current group changes
  useEffect(() => {
    if (currentGroup && currentGroup.leagues.length > 0) {
      // Set main league as default or first league
      const mainLeague = currentGroup.leagues.find(l => l.isMain) || currentGroup.leagues[0]
      if (!selectedLeagueId || !currentGroup.leagues.find(l => l.id === selectedLeagueId)) {
        setSelectedLeagueId(mainLeague.id)
        
        // Set latest year as default
        if (mainLeague.years.length > 0) {
          const latestYear = Math.max(...mainLeague.years.map(y => y.year))
          setSelectedYear(latestYear)
        }
      }
    }
  }, [currentGroup])

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