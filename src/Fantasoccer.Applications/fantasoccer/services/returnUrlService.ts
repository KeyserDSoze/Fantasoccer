// Service to manage return URL for post-login redirect
// Works on both web (localStorage) and React Native (AsyncStorage with in-memory fallback)
import { storage } from '../utils/storage'

const RETURN_URL_KEY = 'fantasoccer_return_url'

export const returnUrlService = {
  /**
   * Save the current URL to return to after login
   * @param url - The URL to return to (e.g., "/auction/123")
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
   * @param url - The URL to parse (e.g., "/auction/123")
   * @returns Object with route name and params, or null if invalid
   */
  parseReturnUrl: (url: string): { routeName: string; params?: any } | null => {
    try {
      const urlParts = url.split('/').filter(Boolean)
      
      if (urlParts.length === 0) return null
      
      // Handle different URL patterns
      if (urlParts[0] === 'auction' && urlParts[1]) {
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
