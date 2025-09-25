// Additional hooks for easier access to group data
import { useApp } from '../provider/AppProvider'
import { Group, EnhancedGroup, League, Basket, UserOfAGroup } from '../types/group'

/**
 * Hook to get current group information
 */
export const useCurrentGroup = () => {
  const { currentGroup, enhancedGroup } = useApp()
  
  return {
    group: currentGroup,
    enhancedGroup,
    isLoaded: currentGroup !== null
  }
}

/**
 * Hook to get user's groups from app state
 */
export const useAppUserGroups = () => {
  const { userGroups, isGroupsLoading, groupsError, refreshGroups } = useApp()
  
  return {
    groups: userGroups,
    loading: isGroupsLoading,
    error: groupsError,
    refresh: refreshGroups
  }
}

/**
 * Hook to get current group leagues
 */
export const useCurrentGroupLeagues = (): League[] => {
  const { currentGroup } = useApp()
  return currentGroup?.leagues || []
}

/**
 * Hook to get current group baskets
 */
export const useCurrentGroupBaskets = (): Basket[] => {
  const { currentGroup } = useApp()
  return currentGroup?.baskets || []
}

/**
 * Hook to get current group users
 */
export const useCurrentGroupUsers = (): UserOfAGroup[] => {
  const { enhancedGroup } = useApp()
  return enhancedGroup?.activeUsers || []
}

/**
 * Hook to get current group admins
 */
export const useCurrentGroupAdmins = (): UserOfAGroup[] => {
  const { enhancedGroup } = useApp()
  return enhancedGroup?.adminUsers || []
}

/**
 * Hook to get a specific league from current group
 */
export const useCurrentGroupLeague = (leagueId: string): League | null => {
  const { currentGroup } = useApp()
  return currentGroup?.leagues.find(league => league.id === leagueId) || null
}

/**
 * Hook to get a specific basket from current group
 */
export const useCurrentGroupBasket = (basketId: string): Basket | null => {
  const { currentGroup } = useApp()
  return currentGroup?.baskets.find(basket => basket.id === basketId) || null
}