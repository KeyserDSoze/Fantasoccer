// Token storage service for managing backend JWT tokens
import { Platform } from 'react-native'

const TOKEN_KEY = 'fantasoccer_backend_token'
const USER_KEY = 'fantasoccer_user_info'

export interface StoredTokenData {
  tokenType: string      // "Bearer"
  accessToken: string    // The actual JWT token to use for API calls
  refreshToken: string   // Token used to refresh when accessToken expires
  expiresAt: number      // Timestamp when token expires
  user?: any            // User information (optional, can be stored separately)
}

export interface TokenSaveOptions {
  expiresIn?: number     // Token expiration in seconds (if provided, will calculate expiresAt)
  expiresAt?: number     // Absolute expiration timestamp (if provided, will use this)
}

class TokenStorageService {
  // Save backend token to storage
  async saveToken(tokenData: StoredTokenData): Promise<void> {
    try {
      const dataToStore = JSON.stringify(tokenData)
      
      if (Platform.OS === 'web') {
        // Web: use localStorage
        localStorage.setItem(TOKEN_KEY, dataToStore)
        console.log('💾 Token saved to localStorage')
      } else {
        // React Native: use AsyncStorage
        const AsyncStorage = await import('@react-native-async-storage/async-storage')
        await AsyncStorage.default.setItem(TOKEN_KEY, dataToStore)
        console.log('💾 Token saved to AsyncStorage')
      }
    } catch (error) {
      console.error('❌ Failed to save token:', error)
      throw error
    }
  }

  // Convenience method to save token with automatic expiration calculation
  async saveTokenWithExpiration(
    tokenData: Omit<StoredTokenData, 'expiresAt'>, 
    options: TokenSaveOptions
  ): Promise<void> {
    let expiresAt: number

    if (options.expiresAt) {
      expiresAt = options.expiresAt
    } else if (options.expiresIn) {
      expiresAt = Date.now() + (options.expiresIn * 1000)
    } else {
      // Default to 1 hour if no expiration info provided
      expiresAt = Date.now() + (60 * 60 * 1000)
      console.warn('⚠️ No expiration info provided, defaulting to 1 hour')
    }

    const completeTokenData: StoredTokenData = {
      ...tokenData,
      expiresAt
    }

    await this.saveToken(completeTokenData)
  }

  // Load backend token from storage
  async loadToken(): Promise<StoredTokenData | null> {
    try {
      let storedData: string | null = null

      if (Platform.OS === 'web') {
        // Web: use localStorage
        storedData = localStorage.getItem(TOKEN_KEY)
      } else {
        // React Native: use AsyncStorage
        const AsyncStorage = await import('@react-native-async-storage/async-storage')
        storedData = await AsyncStorage.default.getItem(TOKEN_KEY)
      }

      if (!storedData) {
        console.log('🔍 No stored token found')
        return null
      }

      const tokenData = JSON.parse(storedData) as StoredTokenData
      
      // Check if token is expired
      if (tokenData.expiresAt && Date.now() > tokenData.expiresAt) {
        console.log('⏰ Stored token is expired, removing...')
        await this.removeToken()
        return null
      }

      console.log('✅ Valid token loaded from storage')
      return tokenData
    } catch (error) {
      console.error('❌ Failed to load token:', error)
      // If there's an error parsing, remove the invalid data
      await this.removeToken()
      return null
    }
  }

  // Remove token from storage
  async removeToken(): Promise<void> {
    try {
      if (Platform.OS === 'web') {
        // Web: use localStorage
        localStorage.removeItem(TOKEN_KEY)
        localStorage.removeItem(USER_KEY)
        console.log('🗑️ Token removed from localStorage')
      } else {
        // React Native: use AsyncStorage
        const AsyncStorage = await import('@react-native-async-storage/async-storage')
        await AsyncStorage.default.removeItem(TOKEN_KEY)
        await AsyncStorage.default.removeItem(USER_KEY)
        console.log('🗑️ Token removed from AsyncStorage')
      }
    } catch (error) {
      console.error('❌ Failed to remove token:', error)
      throw error
    }
  }

  // Check if we have a valid stored token
  async hasValidToken(): Promise<boolean> {
    const tokenData = await this.loadToken()
    return tokenData !== null
  }

  // Get just the access token string
  async getTokenString(): Promise<string | null> {
    const tokenData = await this.loadToken()
    return tokenData?.accessToken || null
  }

  // Check if token is close to expiration (within specified minutes, default 5)
  async isTokenNearExpiry(minutesBeforeExpiry: number = 5): Promise<boolean> {
    const tokenData = await this.loadToken()
    if (!tokenData || !tokenData.expiresAt) {
      return false
    }
    
    const thresholdTime = Date.now() + (minutesBeforeExpiry * 60 * 1000)
    return tokenData.expiresAt < thresholdTime
  }

  // Get current token expiration info
  async getTokenExpirationInfo(): Promise<{ expiresAt: number | null; isExpired: boolean; minutesUntilExpiry: number | null }> {
    const tokenData = await this.loadToken()
    if (!tokenData || !tokenData.expiresAt) {
      return { expiresAt: null, isExpired: true, minutesUntilExpiry: null }
    }
    
    const now = Date.now()
    const minutesUntilExpiry = Math.floor((tokenData.expiresAt - now) / (1000 * 60))
    
    return {
      expiresAt: tokenData.expiresAt,
      isExpired: tokenData.expiresAt <= now,
      minutesUntilExpiry: minutesUntilExpiry > 0 ? minutesUntilExpiry : 0
    }
  }

  // Refresh token automatically if needed and update storage
  async ensureValidToken(): Promise<string | null> {
    const tokenData = await this.loadToken()
    if (!tokenData) {
      return null
    }

    // If token is not close to expiry, return current token
    const isNearExpiry = await this.isTokenNearExpiry(5) // 5 minutes threshold
    if (!isNearExpiry) {
      return tokenData.accessToken
    }

    // Token is near expiry or expired, try to refresh
    try {
      console.log('🔄 Token refresh needed, refreshing...')
      
      // Import authService dynamically to avoid circular dependency
      const { authService } = await import('./authService')
      const newTokenData = await authService.refreshToken(tokenData.refreshToken)
      
      // Calculate new expiration
      const expiresAt = Date.now() + (newTokenData.expiresIn * 1000)
      
      // Save new token
      const updatedTokenData: StoredTokenData = {
        tokenType: newTokenData.tokenType,
        accessToken: newTokenData.accessToken,
        refreshToken: newTokenData.refreshToken,
        expiresAt,
        user: tokenData.user // Preserve user data
      }
      
      await this.saveToken(updatedTokenData)
      console.log('✅ Token refreshed automatically')
      
      return updatedTokenData.accessToken
    } catch (error) {
      console.error('❌ Auto token refresh failed:', error)
      // Remove invalid token
      await this.removeToken()
      return null
    }
  }

  // Save user info separately (for quick access)
  async saveUserInfo(user: any): Promise<void> {
    try {
      const userDataToStore = JSON.stringify(user)
      
      if (Platform.OS === 'web') {
        localStorage.setItem(USER_KEY, userDataToStore)
      } else {
        const AsyncStorage = await import('@react-native-async-storage/async-storage')
        await AsyncStorage.default.setItem(USER_KEY, userDataToStore)
      }
      console.log('💾 User info saved to storage')
    } catch (error) {
      console.error('❌ Failed to save user info:', error)
    }
  }

  // Load user info
  async loadUserInfo(): Promise<any | null> {
    try {
      let storedData: string | null = null

      if (Platform.OS === 'web') {
        storedData = localStorage.getItem(USER_KEY)
      } else {
        const AsyncStorage = await import('@react-native-async-storage/async-storage')
        storedData = await AsyncStorage.default.getItem(USER_KEY)
      }

      if (!storedData) {
        return null
      }

      return JSON.parse(storedData)
    } catch (error) {
      console.error('❌ Failed to load user info:', error)
      return null
    }
  }
}

export const tokenStorageService = new TokenStorageService()