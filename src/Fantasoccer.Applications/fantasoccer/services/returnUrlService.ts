// Service to manage return URL for post-login redirect
// Works on both web (localStorage) and React Native (in-memory storage)
import { Platform } from 'react-native'

const RETURN_URL_KEY = 'fantasoccer_return_url'

// In-memory storage for React Native (fallback if AsyncStorage is not available)
const inMemoryStorage = new Map<string, string>()

// Storage abstraction
const storage = {
  setItem: async (key: string, value: string): Promise<void> => {
    if (Platform.OS === 'web') {
      localStorage.setItem(key, value)
    } else {
      // Try AsyncStorage first, fallback to in-memory
      try {
        const AsyncStorage = await import('@react-native-async-storage/async-storage').then(m => m.default)
        await AsyncStorage.setItem(key, value)
      } catch {
        // AsyncStorage not available, use in-memory
        inMemoryStorage.set(key, value)
      }
    }
  },
  getItem: async (key: string): Promise<string | null> => {
    if (Platform.OS === 'web') {
      return localStorage.getItem(key)
    } else {
      // Try AsyncStorage first, fallback to in-memory
      try {
        const AsyncStorage = await import('@react-native-async-storage/async-storage').then(m => m.default)
        return await AsyncStorage.getItem(key)
      } catch {
        // AsyncStorage not available, use in-memory
        return inMemoryStorage.get(key) || null
      }
    }
  },
  removeItem: async (key: string): Promise<void> => {
    if (Platform.OS === 'web') {
      localStorage.removeItem(key)
    } else {
      // Try AsyncStorage first, fallback to in-memory
      try {
        const AsyncStorage = await import('@react-native-async-storage/async-storage').then(m => m.default)
        await AsyncStorage.removeItem(key)
      } catch {
        // AsyncStorage not available, use in-memory
        inMemoryStorage.delete(key)
      }
    }
  },
}

export const returnUrlService = {
  /**
   * Save the current URL to return to after login
   * @param url - The URL to return to (e.g., "/auction-live/123")
   */
  saveReturnUrl: async (url: string): Promise<void> => {
    try {
      await storage.setItem(RETURN_URL_KEY, url)
      console.log('✅ Return URL saved:', url)
    } catch (error) {
      console.error('❌ Failed to save return URL:', error)
    }
  },

  /**
   * Get the saved return URL
   * @returns The saved URL or null if not found
   */
  getReturnUrl: async (): Promise<string | null> => {
    try {
      const url = await storage.getItem(RETURN_URL_KEY)
      console.log('🔍 Return URL retrieved:', url)
      return url
    } catch (error) {
      console.error('❌ Failed to get return URL:', error)
      return null
    }
  },

  /**
   * Clear the saved return URL
   */
  clearReturnUrl: async (): Promise<void> => {
    try {
      await storage.removeItem(RETURN_URL_KEY)
      console.log('🗑️ Return URL cleared')
    } catch (error) {
      console.error('❌ Failed to clear return URL:', error)
    }
  },

  /**
   * Get and clear the return URL in one operation
   * @returns The saved URL or null if not found
   */
  consumeReturnUrl: async (): Promise<string | null> => {
    try {
      const url = await returnUrlService.getReturnUrl()
      if (url) {
        await returnUrlService.clearReturnUrl()
      }
      return url
    } catch (error) {
      console.error('❌ Failed to consume return URL:', error)
      return null
    }
  },

  /**
   * Parse a return URL and extract route information
   * @param url - The URL to parse (e.g., "/auction-live/123")
   * @returns Object with route name and params, or null if invalid
   */
  parseReturnUrl: (url: string): { routeName: string; params?: any } | null => {
    try {
      const urlParts = url.split('/').filter(Boolean)
      
      if (urlParts.length === 0) return null
      
      // Handle different URL patterns
      if (urlParts[0] === 'auction-live' && urlParts[1]) {
        return {
          routeName: 'AuctionLive',
          params: { auctionId: urlParts[1] },
        }
      }
      
      // Add more URL patterns as needed
      // Example: /game/123 -> { routeName: 'Game', params: { gameId: '123' } }
      
      return null
    } catch (error) {
      console.error('❌ Failed to parse return URL:', error)
      return null
    }
  },
}
