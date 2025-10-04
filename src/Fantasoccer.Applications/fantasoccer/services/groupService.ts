// Group service using Repository Framework
import { authService } from './authService'
import { tokenStorageService } from './tokenStorageService'
import { 
  Group, 
  GroupRaw, 
  mapRawGroupToGroup,
  mapGroupToRaw,
  enhanceGroup, 
  EnhancedGroup,
  AnnualLeague,
  League,
  Basket,
  UserOfAGroup,
  GroupHelper,
} from 'types/group'
import { RepositoryServices } from 'rystem.repository.client'

export class GroupService {

  // ============================================
  // MAIN GROUP OPERATIONS (using Repository Framework)
  // ============================================

  /**
   * Get a specific group by ID
   */
  async getGroup(groupId: string): Promise<Group | null> {
    try {
      // Get the repository instance
      const repository = RepositoryServices
        .Repository<GroupRaw, string>("Group")
      
      // Fetch the group data
      const result = await repository.get(groupId)
      
      if (result) {
        // Map raw data to clean interface
        return mapRawGroupToGroup(result)
      }
      
      return null
    } catch (error) {
      console.error('Error fetching group:', error)
      return null
    }
  }

  /**
   * Get enhanced group with computed properties
   */
  async getEnhancedGroup(groupId: string): Promise<EnhancedGroup | null> {
    const group = await this.getGroup(groupId)
    
    if (!group) {
      return null
    }

    return enhanceGroup(group)
  }

  /**
   * Update a group
   * @param groupId - Group ID
   * @param group - Updated group object
   */
  async updateGroup(groupId: string, group: Group): Promise<Group | null> {
    try {
      const repository = RepositoryServices
        .Repository<GroupRaw, string>("Group")
      
      // Map clean interface back to raw format
      const rawGroup = mapGroupToRaw(group)
      
      // Update the group
      const result = await repository.update(groupId, rawGroup)
      
      if (result && 'value' in result) {
        // Return the updated group
        return mapRawGroupToGroup((result as any).value as GroupRaw)
      }
      
      return null
    } catch (error) {
      console.error('Error updating group:', error)
      return null
    }
  }

  /**
   * Get all groups for current user
   */
  async getUserGroups(): Promise<Group[]> {
    try {
      // Get current user's access token from storage
      const accessToken = await tokenStorageService.getTokenString()
      if (!accessToken) {
        console.warn('No valid access token available')
        return []
      }

      // Get current user info to get user identity
      const user = await authService.getUserInfo(accessToken)
      
      if (!user) {
        console.warn('User info not available')
        return []
      }

      // Extract group IDs from user's groups
      const groupIds = user.groups.map(group => group.id)
      
      if (groupIds.length === 0) {
        console.info('User has no groups')
        return []
      }

      // Fetch full group data for each group ID
      const groupPromises = groupIds.map(groupId => this.getGroup(groupId))
      const groups = await Promise.all(groupPromises)
      
      // Filter out any null results (failed fetches)
      return groups.filter((group): group is Group => group !== null)
      
    } catch (error) {
      console.error('Error fetching user groups:', error)
      return []
    }
  }

  // ============================================
  // DERIVED OPERATIONS (using main operations)
  // ============================================

  /**
   * Get leagues for a specific group
   */
  async getGroupLeagues(groupId: string): Promise<League[]> {
    const group = await this.getGroup(groupId)
    return group?.leagues || []
  }

  /**
   * Get a specific league from a group
   */
  async getLeague(groupId: string, leagueId: string): Promise<League | null> {
    const group = await this.getGroup(groupId)
    return group?.leagues.find(l => l.id === leagueId) || null
  }

  /**
   * Get annual league for specific year
   */
  async getAnnualLeague(groupId: string, leagueId: string, year: number): Promise<AnnualLeague | null> {
    const group = await this.getGroup(groupId)
    return group ? GroupHelper.getAnnualLeague(group, leagueId, year) : null
  }

  /**
   * Get all annual leagues for a specific year
   */
  async getAnnualLeagues(groupId: string, year: number): Promise<AnnualLeague[]> {
    const group = await this.getGroup(groupId)
    return group ? GroupHelper.getAnnualLeagues(group, year) : []
  }

  /**
   * Get baskets for a specific group
   */
  async getGroupBaskets(groupId: string): Promise<Basket[]> {
    const group = await this.getGroup(groupId)
    return group?.baskets || []
  }

  /**
   * Get a specific basket from a group
   */
  async getBasket(groupId: string, basketId: string): Promise<Basket | null> {
    const group = await this.getGroup(groupId)
    return group?.baskets.find(b => b.id === basketId) || null
  }

  /**
   * Get basket ID for a specific owner
   */
  async getBasketIdForOwner(groupId: string, owner: string, year?: number): Promise<string | null> {
    const group = await this.getGroup(groupId)
    return group ? GroupHelper.getBasketId(group, owner, year) : null
  }

  /**
   * Get users for a specific group
   */
  async getGroupUsers(groupId: string): Promise<UserOfAGroup[]> {
    const group = await this.getGroup(groupId)
    return group?.users || []
  }

  /**
   * Get active users for a group
   */
  async getActiveUsers(groupId: string): Promise<UserOfAGroup[]> {
    const group = await this.getGroup(groupId)
    return group ? GroupHelper.getActiveUsers(group) : []
  }

  /**
   * Get admin users for a group
   */
  async getAdminUsers(groupId: string): Promise<UserOfAGroup[]> {
    const group = await this.getGroup(groupId)
    return group ? GroupHelper.getAdminUsers(group) : []
  }

  // ============================================
  // UTILITY METHODS
  // ============================================

  /**
   * Check if user is owner of a team
   */
  async isTeamOwner(groupId: string, basketId: string, year: number, email: string): Promise<boolean> {
    const basket = await this.getBasket(groupId, basketId)
    if (!basket) return false

    const yearlyBasket = basket.years.find(y => y.year === year)
    if (!yearlyBasket) return false

    return yearlyBasket.teams.some(team => 
      GroupHelper.isOwner(team, email)
    )
  }

  /**
   * Get user's team in a specific basket and year
   */
  async getUserTeam(groupId: string, basketId: string, year: number, email: string) {
    const basket = await this.getBasket(groupId, basketId)
    if (!basket) return null

    const yearlyBasket = basket.years.find(y => y.year === year)
    if (!yearlyBasket) return null

    return yearlyBasket.teams.find(team => 
      GroupHelper.isOwner(team, email)
    ) || null
  }

  // ============================================
  // HELPER METHODS (matching serieAService pattern)
  // ============================================

  /**
   * Helper method to check if a group is available
   */
  async isGroupAvailable(groupId: string): Promise<boolean> {
    const group = await this.getGroup(groupId)
    return group !== null
  }

  /**
   * Get available group years for a specific group
   */
  async getGroupAvailableYears(groupId: string): Promise<number[]> {
    const group = await this.getGroup(groupId)
    if (!group) return []

    const years = new Set<number>()
    
    // Collect years from leagues
    group.leagues.forEach(league => {
      league.years.forEach(year => years.add(year.year))
    })
    
    // Collect years from baskets
    group.baskets.forEach(basket => {
      basket.years.forEach(year => years.add(year.year))
    })

    return Array.from(years).sort((a, b) => b - a) // Most recent first
  }
}

// Export a default instance
export const groupService = new GroupService()