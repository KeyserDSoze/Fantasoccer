// Token storage service for managing backend JWT tokens
import { AppIdentity } from 'types/identity'
import { create } from 'zustand';
import { storage } from '../utils/storage'

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

interface TokenStoreState {
  tokenData: StoredTokenData | null
  isLoading: boolean

  saveToken: (tokenData: StoredTokenData) => Promise<void>
  saveTokenWithExpiration: (tokenData: Omit<StoredTokenData, 'expiresAt'>, options: TokenSaveOptions) => Promise<void>
  loadToken: () => Promise<StoredTokenData | null>
  loadTokenSilent: () => Promise<StoredTokenData | null>
  removeToken: () => Promise<void>

  hasValidToken: () => Promise<boolean>
  getTokenSilent: () => Promise<string | null>
  getTokenString: () => Promise<string | null>
  isTokenNearExpiry: (minutesBeforeExpiry?: number) => Promise<boolean>
  getTokenExpirationInfo: () => Promise<{ expiresAt: number | null; isExpired: boolean; minutesUntilExpiry: number | null }>
  ensureValidToken: () => Promise<string | null>
  forceRefreshToken: () => Promise<string | null>

  saveUserInfo: (user: any) => Promise<void>
  loadUserInfo: () => Promise<any | null>
}

export const useTokenStore = create<TokenStoreState>((set, get) => ({
  tokenData: null,
  isLoading: false,
  saveToken: async (tokenData: StoredTokenData) => {
    try {
      const serialized = JSON.stringify(tokenData)
      await storage.setItem(TOKEN_KEY, serialized)
      set({ tokenData })
      // persist user if present
      if (tokenData.user) {
        try {
          const userSerialized = JSON.stringify(tokenData.user)
          await storage.setItem(USER_KEY, userSerialized)
        } catch {}
      }
    } catch (error) {
      console.error('❌ Failed to save token:', error)
      throw error
    }
  },

  saveTokenWithExpiration: async (tokenData: Omit<StoredTokenData, 'expiresAt'>, options: TokenSaveOptions) => {
    let expiresAt: number
    if (options.expiresAt) expiresAt = options.expiresAt
    else if (options.expiresIn) expiresAt = Date.now() + options.expiresIn * 1000
    else expiresAt = Date.now() + 60 * 60 * 1000

    const complete: StoredTokenData = { ...tokenData, expiresAt }
    await get().saveToken(complete)
  },

    loadToken: async () => {
    try {
      const stored = await storage.getItem(TOKEN_KEY)
      if (!stored) return null
      const tokenData = JSON.parse(stored) as StoredTokenData

      if (tokenData.expiresAt && tokenData.refreshToken) {
        const now = Date.now()
        const fiveMinutesFromNow = now + 5 * 60 * 1000
        if (tokenData.expiresAt <= fiveMinutesFromNow) {
          try {
            const { authService } = await import('./authService')
            const newTokenData = await authService.refreshToken(tokenData.refreshToken)
            const expiresAt = now + newTokenData.expiresIn * 1000
            const refreshed: StoredTokenData = {
              tokenType: newTokenData.tokenType,
              accessToken: newTokenData.accessToken,
              refreshToken: newTokenData.refreshToken,
              expiresAt,
              user: tokenData.user,
            }
            await get().saveToken(refreshed)
            return refreshed
          } catch (refreshError) {
            console.error('❌ Token refresh failed during load:', refreshError)
            await get().removeToken()
            return null
          }
        }
      } else if (tokenData.expiresAt && Date.now() > tokenData.expiresAt) {
        await get().removeToken()
        return null
      }

      set({ tokenData })
      return tokenData
    } catch (error) {
      console.error('❌ Failed to load token:', error)
      await get().removeToken()
      return null
    }
  },

    loadTokenSilent: async () => {
    try {
      const stored = await storage.getItem(TOKEN_KEY)
      if (!stored) return null
      const tokenData = JSON.parse(stored) as StoredTokenData
      set({ tokenData })
      return tokenData
    } catch (error) {
      console.error('❌ Failed to load token silently:', error)
      return null
    }
  },

  removeToken: async () => {
    try {
      await storage.removeItem(TOKEN_KEY)
      await storage.removeItem(USER_KEY)
      set({ tokenData: null })
    } catch (error) {
      console.error('❌ Failed to remove token:', error)
      throw error
    }
  },

  hasValidToken: async () => {
    const t = await get().loadTokenSilent()
    if (!t || !t.expiresAt) return false
    return Date.now() < t.expiresAt
  },

  getTokenSilent: async () => {
    const t = await get().loadTokenSilent()
    return t?.accessToken || null
  },

  getTokenString: async () => {
    const t = await get().loadToken()
    return t?.accessToken || null
  },

  isTokenNearExpiry: async (minutesBeforeExpiry = 5) => {
    const t = await get().loadToken()
    if (!t || !t.expiresAt) return false
    const threshold = Date.now() + minutesBeforeExpiry * 60 * 1000
    return t.expiresAt < threshold
  },

  getTokenExpirationInfo: async () => {
    const t = await get().loadToken()
    if (!t || !t.expiresAt) return { expiresAt: null, isExpired: true, minutesUntilExpiry: null }
    const now = Date.now()
    const minutesUntilExpiry = Math.floor((t.expiresAt - now) / (1000 * 60))
    return {
      expiresAt: t.expiresAt,
      isExpired: t.expiresAt <= now,
      minutesUntilExpiry: minutesUntilExpiry > 0 ? minutesUntilExpiry : 0,
    }
  },

  ensureValidToken: async () => {
    const tokenData = await get().loadToken()
    if (!tokenData) {
      console.log('⚠️ No token data found')
      return null
    }
    
    // Check if token is expired
    const now = Date.now()
    if (tokenData.expiresAt && tokenData.expiresAt <= now) {
      console.log('⚠️ Token expired, attempting refresh...')
      // Token is expired, try to refresh
      try {
        const { authService } = await import('./authService')
        const newTokenData = await authService.refreshToken(tokenData.refreshToken)
        const expiresAt = Date.now() + newTokenData.expiresIn * 1000
        const updated: StoredTokenData = {
          tokenType: newTokenData.tokenType,
          accessToken: newTokenData.accessToken,
          refreshToken: newTokenData.refreshToken,
          expiresAt,
          user: tokenData.user,
        }
        await get().saveToken(updated)
        console.log('✅ Token refreshed successfully')
        return updated.accessToken
      } catch (error) {
        console.error('❌ Token refresh failed:', error)
        await get().removeToken()
        return null
      }
    }
    
    // Check if token is near expiry (within 5 minutes)
    const isNear = await get().isTokenNearExpiry(5)
    if (!isNear) {
      console.log('✅ Token is valid and not near expiry')
      return tokenData.accessToken
    }
    
    // Token is near expiry, try to refresh proactively
    console.log('⏰ Token near expiry, attempting proactive refresh...')
    try {
      const { authService } = await import('./authService')
      const newTokenData = await authService.refreshToken(tokenData.refreshToken)
      const expiresAt = Date.now() + newTokenData.expiresIn * 1000
      const updated: StoredTokenData = {
        tokenType: newTokenData.tokenType,
        accessToken: newTokenData.accessToken,
        refreshToken: newTokenData.refreshToken,
        expiresAt,
        user: tokenData.user,
      }
      await get().saveToken(updated)
      console.log('✅ Token proactively refreshed')
      return updated.accessToken
    } catch (error) {
      console.error('❌ Proactive token refresh failed, using current token:', error)
      // If refresh fails but token is still valid, return current token
      return tokenData.accessToken
    }
  },

  saveUserInfo: async (user : AppIdentity) => {
    try {
      const s = JSON.stringify(user)
      await storage.setItem(USER_KEY, s)
    } catch (error) {
      console.error('❌ Failed to save user info:', error)
    }
  },

  loadUserInfo: async () => {
    try {
      const stored = await storage.getItem(USER_KEY)
      if (!stored) return null
      return JSON.parse(stored)
    } catch (error) {
      console.error('❌ Failed to load user info:', error)
      return null
    }
  },

  // Force token refresh - always refreshes regardless of expiration status
  // Use this before critical operations like SignalR connections
  forceRefreshToken: async (): Promise<string | null> => {
    console.log('🔄 Force refreshing token...')
    const tokenData = await get().loadTokenSilent()
    
    if (!tokenData || !tokenData.refreshToken) {
      console.error('❌ No token or refresh token available for force refresh')
      return null
    }
    
    try {
      const { authService } = await import('./authService')
      const newTokenData = await authService.refreshToken(tokenData.refreshToken)
      const expiresAt = Date.now() + newTokenData.expiresIn * 1000
      const updated: StoredTokenData = {
        tokenType: newTokenData.tokenType,
        accessToken: newTokenData.accessToken,
        refreshToken: newTokenData.refreshToken,
        expiresAt,
        user: tokenData.user,
      }
      await get().saveToken(updated)
      console.log('✅ Token force refreshed successfully')
      return updated.accessToken
    } catch (error) {
      console.error('❌ Force token refresh failed:', error)
      await get().removeToken()
      return null
    }
  },
}))

// Backwards-compatible default export that delegates to the zustand store
export const tokenStorageService = {
  saveToken: (t: StoredTokenData) => useTokenStore.getState().saveToken(t),
  saveTokenWithExpiration: (t: Omit<StoredTokenData, 'expiresAt'>, opts: TokenSaveOptions) => useTokenStore.getState().saveTokenWithExpiration(t, opts),
  loadToken: () => useTokenStore.getState().loadToken(),
  loadTokenSilent: () => useTokenStore.getState().loadTokenSilent(),
  removeToken: () => useTokenStore.getState().removeToken(),
  hasValidToken: () => useTokenStore.getState().hasValidToken(),
  getTokenSilent: () => useTokenStore.getState().getTokenSilent(),
  getTokenString: () => useTokenStore.getState().getTokenString(),
  isTokenNearExpiry: (m?: number) => useTokenStore.getState().isTokenNearExpiry(m),
  getTokenExpirationInfo: () => useTokenStore.getState().getTokenExpirationInfo(),
  ensureValidToken: () => useTokenStore.getState().ensureValidToken(),
  forceRefreshToken: () => useTokenStore.getState().forceRefreshToken(),
  saveUserInfo: (u: any) => useTokenStore.getState().saveUserInfo(u),
  loadUserInfo: () => useTokenStore.getState().loadUserInfo(),
}