// Authentication service - handles all auth-related API calls
import { 
  AppIdentity, 
  AppIdentityRaw, 
  mapAppIdentity, 
  mapAppIdentityToRaw 
} from '../types/identity'
import { buildApiUrl, getCommonHeaders, ApiError } from '../configurations/apiConfig'
import { tokenStorageService } from './tokenStorageService'
import { RepositoryServices } from 'rystem.repository.client'

// Response types
export interface TokenExchangeResponse {
  tokenType: string      // "Bearer"
  accessToken: string    // The actual JWT token to use for API calls
  expiresIn: number      // Token expiration in seconds (e.g., 3600)
  refreshToken: string   // Token used to refresh when accessToken expires
}

// PKCE Helper for OAuth flow
export interface PKCEOptions {
  isPKCE?: boolean
  codeVerifier?: string
}

export class AuthService {
  // Exchange authorization code or MSAL token for backend JWT token
  async exchangeToken(authCode: string, options: PKCEOptions = {}): Promise<TokenExchangeResponse> {
    try {
      let url: string
      
      if (options.isPKCE && options.codeVerifier) {
        // PKCE flow: include code_verifier
        url = buildApiUrl('/api/Authentication/Social/Token', {
          code: authCode,
          provider: '2',
          code_verifier: options.codeVerifier
        })
      } else {
        // MSAL flow: send MSAL token directly
        url = buildApiUrl('/api/Authentication/Social/Token', {
          code: authCode,
          provider: '2'
        })
      }

      const response = await fetch(url, {
        method: 'GET',
        headers: getCommonHeaders(),
      })

      if (!response.ok) {
        const errorText = await response.text()
        throw new ApiError(`Token exchange failed: ${response.status} ${response.statusText}`, response.status, errorText)
      }

      const data = await response.json()
      return data
    } catch (error) {
      if (error instanceof ApiError) throw error
      throw new ApiError(`Failed to exchange token: ${error instanceof Error ? error.message : 'Unknown error'}`)
    }
  }

  // Exchange OAuth authorization code using PKCE flow
  async exchangeTokenWithPKCE(authCode: string, codeVerifier: string): Promise<TokenExchangeResponse> {
    return this.exchangeToken(authCode, { isPKCE: true, codeVerifier })
  }

  // Refresh access token using refresh token
  async refreshToken(refreshToken: string): Promise<TokenExchangeResponse> {
    try {
      // Use provider=0 and refreshToken as code parameter
      const url = buildApiUrl('/api/Authentication/Social/Token', {
        code: refreshToken,
        provider: '0'
      })

      const response = await fetch(url, {
        method: 'GET',
        headers: getCommonHeaders(),
      })

      if (!response.ok) {
        const errorText = await response.text()
        throw new ApiError(`Token refresh failed: ${response.status} ${response.statusText}`, response.status, errorText)
      }

      const data = await response.json()
      return data
    } catch (error) {
      if (error instanceof ApiError) throw error
      throw new ApiError(`Failed to refresh token: ${error instanceof Error ? error.message : 'Unknown error'}`)
    }
  }

  // Get user information using backend JWT token with automatic refresh
  async getUserInfo(accessToken: string, refreshToken?: string): Promise<AppIdentity> {
    try {
      const url = buildApiUrl('/api/Authentication/Social/User')

      const response = await fetch(url, {
        method: 'GET',
        headers: getCommonHeaders(true, accessToken),
      })

      // If unauthorized and we have a refresh token, try to refresh
      if (response.status === 401 && refreshToken) {
        try {
          const newTokenData = await this.refreshToken(refreshToken)
          
          // Update storage with new tokens
          await tokenStorageService.saveToken({
            tokenType: newTokenData.tokenType,
            accessToken: newTokenData.accessToken,
            refreshToken: newTokenData.refreshToken,
            expiresAt: Date.now() + (newTokenData.expiresIn * 1000),
          })
          
          // Retry the original request with new token
          return await this.getUserInfo(newTokenData.accessToken)
          
        } catch (refreshError) {
          throw new ApiError('REFRESH_FAILED') // Special error to trigger re-login
        }
      }

      if (!response.ok) {
        const errorText = await response.text()
        
        if (response.status === 401) {
          throw new ApiError('UNAUTHORIZED') // Trigger re-login
        }
        
        throw new ApiError(`User info fetch failed: ${response.status} ${response.statusText}`, response.status, errorText)
      }

      const rawData: AppIdentityRaw = await response.json()
      return mapAppIdentity(rawData)
    } catch (error) {
      if (error instanceof ApiError) throw error
      throw new ApiError(`Failed to fetch user info: ${error instanceof Error ? error.message : 'Unknown error'}`)
    }
  }
  // Update user state and groups
  async updateUserState(updatedUser: AppIdentity): Promise<boolean> {
    const repository = RepositoryServices
      .Repository<AppIdentityRaw, string>("AppIdentity");

    const userId = updatedUser.email;
    const result = await repository.update(userId, mapAppIdentityToRaw(updatedUser));
    if (!result) 
        return false;
    return result.isOk;
  }

  // Helper methods for updating specific parts of user state
  
  // Update user's current year
  async updateUserYear(user: AppIdentity, newYear: number): Promise<boolean> {
    const updatedUser: AppIdentity = {
      ...user,
      state: {
        ...user.state,
        year: newYear,
        raw: {
          ...user.state.raw,
          y: newYear
        }
      },
      raw: {
        ...user.raw,
        s: {
          ...user.raw.s,
          y: newYear
        }
      }
    }
    return this.updateUserState(updatedUser)
  }

  // Update user's current league
  async updateUserLeague(user: AppIdentity, newLeagueId: string): Promise<boolean> {
    const updatedUser: AppIdentity = {
      ...user,
      state: {
        ...user.state,
        leagueId: newLeagueId,
        raw: {
          ...user.state.raw,
          l: newLeagueId
        }
      },
      raw: {
        ...user.raw,
        s: {
          ...user.raw.s,
          l: newLeagueId
        }
      }
    }
    return this.updateUserState(updatedUser)
  }

  // Update user's current group
  async updateUserGroup(user: AppIdentity, newGroupId: string): Promise<boolean> {
    const updatedUser: AppIdentity = {
      ...user,
      state: {
        ...user.state,
        groupId: newGroupId,
        raw: {
          ...user.state.raw,
          g: newGroupId
        }
      },
      raw: {
        ...user.raw,
        s: {
          ...user.raw.s,
          g: newGroupId
        }
      }
    }
    return this.updateUserState(updatedUser)
  }

  // Update user's working email
  async updateUserWorkingEmail(user: AppIdentity, newWorkingEmail: string): Promise<boolean> {
    const updatedUser: AppIdentity = {
      ...user,
      state: {
        ...user.state,
        meOrLeaderEmail: newWorkingEmail,
        raw: {
          ...user.state.raw,
          m: newWorkingEmail
        }
      },
      raw: {
        ...user.raw,
        s: {
          ...user.raw.s,
          m: newWorkingEmail
        }
      }
    }
    return this.updateUserState(updatedUser)
  }

  // Add a group to user's groups array
  async addUserGroup(user: AppIdentity, newGroup: { id: string, name: string, role: number }): Promise<boolean> {
    const newMappedGroup = {
      id: newGroup.id,
      name: newGroup.name,
      role: newGroup.role,
      raw: {
        i: newGroup.id,
        n: newGroup.name,
        r: newGroup.role
      }
    }
    
    const updatedUser: AppIdentity = {
      ...user,
      groups: [...user.groups, newMappedGroup],
      raw: {
        ...user.raw,
        g: [...user.raw.g, newMappedGroup.raw]
      }
    }
    return this.updateUserState(updatedUser)
  }

  // Remove a group from user's groups array
  async removeUserGroup(user: AppIdentity, groupIdToRemove: string): Promise<boolean> {
    const updatedUser: AppIdentity = {
      ...user,
      groups: user.groups.filter(group => group.id !== groupIdToRemove),
      raw: {
        ...user.raw,
        g: user.raw.g.filter(group => group.i !== groupIdToRemove)
      }
    }
    return this.updateUserState(updatedUser)
  }

  // Update a specific group in user's groups array
  async updateUserGroupInfo(user: AppIdentity, groupId: string, updatedGroup: { name?: string, role?: number }): Promise<boolean> {
    const updatedUser: AppIdentity = {
      ...user,
      groups: user.groups.map(group => 
        group.id === groupId 
          ? {
              ...group,
              ...(updatedGroup.name && { name: updatedGroup.name }),
              ...(updatedGroup.role !== undefined && { role: updatedGroup.role }),
              raw: {
                ...group.raw,
                ...(updatedGroup.name && { n: updatedGroup.name }),
                ...(updatedGroup.role !== undefined && { r: updatedGroup.role })
              }
            }
          : group
      ),
      raw: {
        ...user.raw,
        g: user.raw.g.map(group => 
          group.i === groupId 
            ? {
                ...group,
                ...(updatedGroup.name && { n: updatedGroup.name }),
                ...(updatedGroup.role !== undefined && { r: updatedGroup.role })
              }
            : group
        )
      }
    }
    return this.updateUserState(updatedUser)
  }
}

// Export singleton instance
export const authService = new AuthService()