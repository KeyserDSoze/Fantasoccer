// Authentication service - handles all auth-related API calls
import { buildApiUrl, getCommonHeaders, ApiError } from './apiConfig'
import { tokenStorageService } from './tokenStorageService'

// Response types
export interface TokenExchangeResponse {
  tokenType: string      // "Bearer"
  accessToken: string    // The actual JWT token to use for API calls
  expiresIn: number      // Token expiration in seconds (e.g., 3600)
  refreshToken: string   // Token used to refresh when accessToken expires
}

export interface UserInfoResponse {
  u: string              // Username
  e: string              // Email
  s: {                   // State
    g: string            // Current group ID
    l: string            // Current league ID
    y: number            // Year
    m: string            // Working email (may be different from main email)
  }
  g: Array<{            // Groups array
    i: string            // Group ID
    n: string            // Group name
    r: number            // Role flags (Reader=1, Participant=2, Admin=4)
  }>
}

// Enum for Identity Roles (as flags)
export enum IdentityRole {
  None = 0,
  Reader = 1,
  Participant = 2,
  Admin = 4,
  SuperAdmin = 8,
}

// Helper functions for role checking
export const hasRole = (userRole: number, role: IdentityRole): boolean => {
  return (userRole & role) === role
}

export const getUserRoleNames = (roleFlags: number): string[] => {
  const roles: string[] = []
  if (hasRole(roleFlags, IdentityRole.Reader)) roles.push('Reader')
  if (hasRole(roleFlags, IdentityRole.Participant)) roles.push('Participant')
  if (hasRole(roleFlags, IdentityRole.Admin)) roles.push('Admin')
  return roles
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
        console.log('🔄 Exchanging OAuth code with PKCE for backend token...')
        console.log('🔐 Code Verifier:', options.codeVerifier)
      } else {
        // MSAL flow: send MSAL token directly
        url = buildApiUrl('/api/Authentication/Social/Token', {
          code: authCode,
          provider: '2'
        })
        console.log('🔄 Exchanging MSAL token for backend token...')
      }
      
      console.log('📍 API URL:', url.replace(/code_verifier=[^&]*/, 'code_verifier=***')) // Hide verifier in logs
      console.log('🎫 Auth Code (first 20 chars):', authCode.substring(0, 20) + '...')

      const response = await fetch(url, {
        method: 'GET',
        headers: getCommonHeaders(),
      })

      console.log('📊 Exchange response status:', response.status)
      console.log('📊 Exchange response headers:', Object.fromEntries(response.headers.entries()))

      if (!response.ok) {
        const errorText = await response.text()
        console.error('❌ Token exchange failed:', errorText)
        throw new ApiError(`Token exchange failed: ${response.status} ${response.statusText}`, response.status, errorText)
      }

      const data = await response.json()
      console.log('✅ Token exchange successful!')
      console.log('📄 Backend token response:', data)

      return data
    } catch (error) {
      console.error('❌ Token exchange error:', error)
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
      console.log('🔄 Refreshing access token...')
      
      // Use provider=0 and refreshToken as code parameter
      const url = buildApiUrl('/api/Authentication/Social/Token', {
        code: refreshToken,
        provider: '0'
      })
      
      console.log('📍 Refresh API URL:', url.replace(/code=[^&]*/, 'code=***'))
      console.log('🔄 Refresh Token (first 20 chars):', refreshToken.substring(0, 20) + '...')

      const response = await fetch(url, {
        method: 'GET',
        headers: getCommonHeaders(),
      })

      console.log('📊 Refresh response status:', response.status)

      if (!response.ok) {
        const errorText = await response.text()
        console.error('❌ Token refresh failed:', errorText)
        throw new ApiError(`Token refresh failed: ${response.status} ${response.statusText}`, response.status, errorText)
      }

      const data = await response.json()
      console.log('✅ Token refresh successful!')
      console.log('📄 New token expires in:', data.expiresIn, 'seconds')

      return data
    } catch (error) {
      console.error('❌ Token refresh error:', error)
      if (error instanceof ApiError) throw error
      throw new ApiError(`Failed to refresh token: ${error instanceof Error ? error.message : 'Unknown error'}`)
    }
  }

  // Get user information using backend JWT token with automatic refresh
  async getUserInfo(accessToken: string, refreshToken?: string): Promise<UserInfoResponse> {
    try {
      const url = buildApiUrl('/api/Authentication/Social/User')
      
      console.log('🔄 Fetching user info from backend...')
      console.log('📍 API URL:', url)
      console.log('🎫 Access Token (first 20 chars):', accessToken.substring(0, 20) + '...')

      const response = await fetch(url, {
        method: 'GET',
        headers: getCommonHeaders(true, accessToken),
      })

      console.log('📊 User info response status:', response.status)

      // If unauthorized and we have a refresh token, try to refresh
      if (response.status === 401 && refreshToken) {
        console.log('🔄 Access token expired, attempting refresh...')
        
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
          console.log('🔄 Retrying user info request with refreshed token...')
          return await this.getUserInfo(newTokenData.accessToken)
          
        } catch (refreshError) {
          console.error('❌ Token refresh failed, user needs to re-login:', refreshError)
          throw new ApiError('REFRESH_FAILED') // Special error to trigger re-login
        }
      }

      if (!response.ok) {
        const errorText = await response.text()
        console.error('❌ User info fetch failed:', errorText)
        
        if (response.status === 401) {
          throw new ApiError('UNAUTHORIZED') // Trigger re-login
        }
        
        throw new ApiError(`User info fetch failed: ${response.status} ${response.statusText}`, response.status, errorText)
      }

      const data = await response.json()
      console.log('✅ User info fetch successful!')
      console.log('👤 User data:', data)

      return data
    } catch (error) {
      console.error('❌ User info fetch error:', error)
      
      // More specific error details
      if (error instanceof Error) {
        console.error('Error name:', error.name)
        console.error('Error message:', error.message)
      }
      
      if (error instanceof ApiError) throw error
      throw new ApiError(`Failed to fetch user info: ${error instanceof Error ? error.message : 'Unknown error'}`)
    }
  }

  // Check if user has specific role in any group
  hasUserRole(user: UserInfoResponse, role: IdentityRole): boolean {
    return user.g.some(group => hasRole(group.r, role))
  }

  // Get user's current year from state
  getUserYear(user: UserInfoResponse): number {
    return user.s.y
  }

  // Get user's current group ID
  getUserGroupId(user: UserInfoResponse): string {
    return user.s.g
  }

  // Get user's current league ID
  getUserLeagueId(user: UserInfoResponse): string {
    return user.s.l
  }
}

// Export singleton instance
export const authService = new AuthService()