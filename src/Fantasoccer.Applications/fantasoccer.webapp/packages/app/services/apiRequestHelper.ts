// API Request Helper with automatic token refresh
// Uses authService for authentication and apiConfig for URL management
import { authService, TokenExchangeResponse } from './authService'
import { getApiConfig, getCommonHeaders, ApiError } from './apiConfig'
import { tokenStorageService } from './tokenStorageService'

export interface ApiRequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE' | 'PATCH'
  body?: any
  headers?: Record<string, string>
  skipAuth?: boolean // Skip authorization header
}

export class ApiRequestHelper {
  /**
   * Make an authenticated API request with automatic token refresh
   * @param url - The API endpoint URL (relative to base URL)
   * @param options - Request options
   * @returns Response data
   */
  static async makeRequest<T = any>(url: string, options: ApiRequestOptions = {}): Promise<T> {
    const { method = 'GET', body, headers = {}, skipAuth = false } = options

    // Get current token data
    const tokenData = await tokenStorageService.loadToken()
    if (!tokenData && !skipAuth) {
      throw new ApiError('UNAUTHORIZED') // User needs to login
    }

    // Prepare headers using apiConfig helper
    const requestHeaders = {
      ...getCommonHeaders(false), // Get base headers without auth
      ...headers // Override with custom headers
    }

    // Add authorization header if we have a token and not skipping auth
    if (tokenData && !skipAuth) {
      requestHeaders['Authorization'] = `${tokenData.tokenType} ${tokenData.accessToken}`
    }

    // Get full URL using apiConfig
    const config = getApiConfig()
    const fullUrl = url.startsWith('http') ? url : `${config.baseUrl}${url}`

    console.log(`🔄 Making ${method} request to:`, fullUrl)
    if (tokenData && !skipAuth) {
      console.log('🎫 Using access token:', tokenData.accessToken.substring(0, 20) + '...')
    }

    // Make request
    const response = await fetch(fullUrl, {
      method,
      headers: requestHeaders,
      body: body ? JSON.stringify(body) : undefined,
    })

    console.log(`📊 Response status: ${response.status}`)

    // Handle unauthorized response
    if (response.status === 401 && tokenData && !skipAuth) {
      console.log('🔄 Access token expired, attempting refresh...')
      
      try {
        // Try to refresh token using authService
        const newTokenData = await authService.refreshToken(tokenData.refreshToken)
        
        // Update storage
        await tokenStorageService.saveToken({
          tokenType: newTokenData.tokenType,
          accessToken: newTokenData.accessToken,
          refreshToken: newTokenData.refreshToken,
          expiresAt: Date.now() + (newTokenData.expiresIn * 1000),
        })
        
        console.log('✅ Token refreshed, retrying request...')
        
        // Retry request with new token
        requestHeaders['Authorization'] = `${newTokenData.tokenType} ${newTokenData.accessToken}`
        
        const retryResponse = await fetch(fullUrl, {
          method,
          headers: requestHeaders,
          body: body ? JSON.stringify(body) : undefined,
        })
        
        if (!retryResponse.ok) {
          const errorText = await retryResponse.text()
          throw new ApiError(`Request failed after refresh: ${retryResponse.status} ${errorText}`, retryResponse.status, errorText)
        }
        
        return await retryResponse.json()
        
      } catch (refreshError) {
        console.error('❌ Token refresh failed:', refreshError)
        await tokenStorageService.removeToken()
        throw new ApiError('REFRESH_FAILED') // Trigger re-login
      }
    }

    // Handle other errors
    if (!response.ok) {
      const errorText = await response.text()
      console.error(`❌ Request failed:`, errorText)
      
      if (response.status === 401) {
        throw new ApiError('UNAUTHORIZED', response.status, errorText)
      }
      
      throw new ApiError(`Request failed: ${response.status} ${errorText}`, response.status, errorText)
    }

    // Parse response
    const contentType = response.headers.get('content-type')
    if (contentType && contentType.includes('application/json')) {
      return await response.json()
    } else {
      return await response.text() as any
    }
  }

  /**
   * GET request
   */
  static async get<T = any>(url: string, headers?: Record<string, string>): Promise<T> {
    return this.makeRequest<T>(url, { method: 'GET', headers })
  }

  /**
   * POST request
   */
  static async post<T = any>(url: string, body?: any, headers?: Record<string, string>): Promise<T> {
    return this.makeRequest<T>(url, { method: 'POST', body, headers })
  }

  /**
   * PUT request
   */
  static async put<T = any>(url: string, body?: any, headers?: Record<string, string>): Promise<T> {
    return this.makeRequest<T>(url, { method: 'PUT', body, headers })
  }

  /**
   * DELETE request
   */
  static async delete<T = any>(url: string, headers?: Record<string, string>): Promise<T> {
    return this.makeRequest<T>(url, { method: 'DELETE', headers })
  }

  /**
   * Check if user is authenticated and tokens are valid
   */
  static async isAuthenticated(): Promise<boolean> {
    try {
      const tokenData = await tokenStorageService.loadToken()
      if (!tokenData) return false

      // Check if token is expired
      if (Date.now() > tokenData.expiresAt) {
        console.log('🔄 Token expired, attempting refresh...')
        
        try {
          // Try to refresh token using authService
          await authService.refreshToken(tokenData.refreshToken)
          return true
        } catch {
          return false
        }
      }

      return true
    } catch {
      return false
    }
  }
}

// Export singleton instance
export const apiRequest = ApiRequestHelper