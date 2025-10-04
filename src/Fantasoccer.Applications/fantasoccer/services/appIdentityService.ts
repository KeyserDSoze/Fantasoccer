// AppIdentity service using Repository Framework
import { RepositoryServices } from 'rystem.repository.client'
import { 
  AppIdentity, 
  AppIdentityRaw, 
  AppIdentityState,
  GroupForIdentity,
  mapAppIdentity,
  mapAppIdentityToRaw,
  updateAppIdentityState,
  addGroupToAppIdentity,
  removeGroupFromAppIdentity,
  IdentityRole
} from 'types/identity'

export class AppIdentityService {

  // ============================================
  // MAIN APPIDENTITY OPERATIONS (using Repository Framework)
  // ============================================

  /**
   * Get all user identities (admin/super-admin only)
   * Uses repository.query() to fetch all entries
   */
  async getAllIdentities(): Promise<AppIdentity[]> {
    try {
      const repository = RepositoryServices
        .Repository<AppIdentityRaw, string>("AppIdentity")
      
      // Query all identities - query() returns all items when called without parameters
      const results = await repository.query().execute();
      
      if (results && Array.isArray(results) && results.length > 0) {
        // Map all raw data to clean interfaces
        return results.map(x => mapAppIdentity(x.value))
      }
      
      return []
    } catch (error) {
      console.error('Error fetching all identities:', error)
      return []
    }
  }

  /**
   * Get a specific user identity by email
   * @param email - User's email address (used as key)
   */
  async getAppIdentity(email: string): Promise<AppIdentity | null> {
    try {
      // Get the repository instance
      const repository = RepositoryServices
        .Repository<AppIdentityRaw, string>("AppIdentity")
      
      // Fetch the identity data
      const result = await repository.get(email)
      
      if (result) {
        // Map raw data to clean interface
        return mapAppIdentity(result)
      }
      
      return null
    } catch (error) {
      console.error('Error fetching app identity:', error)
      return null
    }
  }

  /**
   * Update user identity
   * @param email - User's email address (used as key)
   * @param identity - Updated identity object
   */
  async updateAppIdentity(email: string, identity: AppIdentity): Promise<boolean> {
    try {
      const repository = RepositoryServices
        .Repository<AppIdentityRaw, string>("AppIdentity")
      
      // Map clean interface back to raw format
      const rawIdentity = mapAppIdentityToRaw(identity)
      
      // Update the identity
      const result = await repository.update(email, rawIdentity)
      
      return result !== null
    } catch (error) {
      console.error('Error updating app identity:', error)
      return false
    }
  }

  /**
   * Insert new user identity
   * @param identity - New identity object
   */
  async insertAppIdentity(identity: AppIdentity): Promise<boolean> {
    try {
      const repository = RepositoryServices
        .Repository<AppIdentityRaw, string>("AppIdentity")
      
      // Map clean interface to raw format
      const rawIdentity = mapAppIdentityToRaw(identity)
      
      // Insert the identity
      const result = await repository.insert(identity.email, rawIdentity)
      
      return result !== null
    } catch (error) {
      console.error('Error inserting app identity:', error)
      return false
    }
  }

  /**
   * Delete user identity
   * @param email - User's email address (used as key)
   */
  async deleteAppIdentity(email: string): Promise<boolean> {
    try {
      const repository = RepositoryServices
        .Repository<AppIdentityRaw, string>("AppIdentity")
      
      // Delete the identity
      await repository.delete(email)
      
      return true
    } catch (error) {
      console.error('Error deleting app identity:', error)
      return false
    }
  }

  // ============================================
  // STATE MANAGEMENT OPERATIONS
  // ============================================

  /**
   * Update user's state (group, league, year, etc.)
   * @param email - User's email address
   * @param stateUpdates - Partial state updates
   */
  async updateUserState(
    email: string, 
    stateUpdates: Partial<AppIdentityState>
  ): Promise<boolean> {
    try {
      // Get current identity
      const currentIdentity = await this.getAppIdentity(email)
      
      if (!currentIdentity) {
        console.error('Cannot update state: user identity not found')
        return false
      }

      // Create updated identity with new state
      const updatedIdentity = updateAppIdentityState(currentIdentity, stateUpdates)
      
      // Save updated identity
      return await this.updateAppIdentity(email, updatedIdentity)
    } catch (error) {
      console.error('Error updating user state:', error)
      return false
    }
  }

  /**
   * Update user's selected group
   */
  async updateUserGroup(email: string, groupId: string): Promise<boolean> {
    return this.updateUserState(email, { groupId })
  }

  /**
   * Update user's selected league
   */
  async updateUserLeague(email: string, leagueId: string): Promise<boolean> {
    return this.updateUserState(email, { leagueId })
  }

  /**
   * Update user's selected year
   */
  async updateUserYear(email: string, year: number): Promise<boolean> {
    return this.updateUserState(email, { year })
  }

  /**
   * Update user's working email (me or leader email)
   */
  async updateUserWorkingEmail(email: string, workingEmail: string): Promise<boolean> {
    return this.updateUserState(email, { meOrLeaderEmail: workingEmail })
  }

  // ============================================
  // GROUP MANAGEMENT OPERATIONS
  // ============================================

  /**
   * Add a group to user's groups list
   */
  async addUserGroup(
    email: string, 
    newGroup: { id: string; name: string; role: IdentityRole }
  ): Promise<boolean> {
    try {
      // Get current identity
      const currentIdentity = await this.getAppIdentity(email)
      
      if (!currentIdentity) {
        console.error('Cannot add group: user identity not found')
        return false
      }

      // Check if group already exists
      const groupExists = currentIdentity.groups.some(g => g.id === newGroup.id)
      if (groupExists) {
        console.warn('Group already exists in user groups')
        return false
      }

      // Create updated identity with new group
      const updatedIdentity = addGroupToAppIdentity(currentIdentity, newGroup)
      
      // Save updated identity
      return await this.updateAppIdentity(email, updatedIdentity)
    } catch (error) {
      console.error('Error adding user group:', error)
      return false
    }
  }

  /**
   * Remove a group from user's groups list
   */
  async removeUserGroup(email: string, groupIdToRemove: string): Promise<boolean> {
    try {
      // Get current identity
      const currentIdentity = await this.getAppIdentity(email)
      
      if (!currentIdentity) {
        console.error('Cannot remove group: user identity not found')
        return false
      }

      // Check if group exists
      const groupExists = currentIdentity.groups.some(g => g.id === groupIdToRemove)
      if (!groupExists) {
        console.warn('Group not found in user groups')
        return false
      }

      // Create updated identity without the group
      const updatedIdentity = removeGroupFromAppIdentity(currentIdentity, groupIdToRemove)
      
      // Save updated identity
      return await this.updateAppIdentity(email, updatedIdentity)
    } catch (error) {
      console.error('Error removing user group:', error)
      return false
    }
  }

  /**
   * Update user's role in a specific group
   */
  async updateUserRoleInGroup(
    email: string, 
    groupId: string, 
    newRole: IdentityRole
  ): Promise<boolean> {
    try {
      // Get current identity
      const currentIdentity = await this.getAppIdentity(email)
      
      if (!currentIdentity) {
        console.error('Cannot update role: user identity not found')
        return false
      }

      // Find the group and update its role
      const updatedGroups = currentIdentity.groups.map(group => {
        if (group.id === groupId) {
          return {
            ...group,
            role: newRole,
            raw: {
              ...group.raw,
              r: newRole
            }
          }
        }
        return group
      })

      // Check if group was found
      const groupFound = updatedGroups.some(g => g.id === groupId)
      if (!groupFound) {
        console.warn('Group not found in user groups')
        return false
      }

      // Create updated identity with new groups
      const updatedIdentity: AppIdentity = {
        ...currentIdentity,
        groups: updatedGroups,
        raw: {
          ...currentIdentity.raw,
          g: updatedGroups.map(g => g.raw)
        }
      }
      
      // Save updated identity
      return await this.updateAppIdentity(email, updatedIdentity)
    } catch (error) {
      console.error('Error updating user role in group:', error)
      return false
    }
  }

  // ============================================
  // QUERY OPERATIONS
  // ============================================

  /**
   * Get all groups for a user
   */
  async getUserGroups(email: string): Promise<GroupForIdentity[]> {
    try {
      const identity = await this.getAppIdentity(email)
      return identity?.groups ?? []
    } catch (error) {
      console.error('Error fetching user groups:', error)
      return []
    }
  }

  /**
   * Get user's current state
   */
  async getUserState(email: string): Promise<AppIdentityState | null> {
    try {
      const identity = await this.getAppIdentity(email)
      return identity?.state ?? null
    } catch (error) {
      console.error('Error fetching user state:', error)
      return null
    }
  }

  /**
   * Check if user has a specific role in a group
   */
  async userHasRoleInGroup(
    email: string, 
    groupId: string, 
    role: IdentityRole
  ): Promise<boolean> {
    try {
      const groups = await this.getUserGroups(email)
      const group = groups.find(g => g.id === groupId)
      
      if (!group) {
        return false
      }

      // Check if user has the role (bitwise AND)
      return (group.role & role) === role
    } catch (error) {
      console.error('Error checking user role in group:', error)
      return false
    }
  }

  /**
   * Check if user is admin in a group
   */
  async userIsAdminInGroup(email: string, groupId: string): Promise<boolean> {
    return this.userHasRoleInGroup(email, groupId, IdentityRole.Admin)
  }

  /**
   * Check if user is participant in a group
   */
  async userIsParticipantInGroup(email: string, groupId: string): Promise<boolean> {
    return this.userHasRoleInGroup(email, groupId, IdentityRole.Participant)
  }
}

// Export singleton instance
export const appIdentityService = new AppIdentityService()
