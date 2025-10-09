// Cross-platform storage utility
// Works on web (localStorage) and React Native (AsyncStorage with in-memory fallback)
import { Platform } from 'react-native'

// In-memory storage fallback for React Native when AsyncStorage is not available
const inMemoryStorage = new Map<string, string>()

/**
 * Cross-platform storage API
 * - Web: Uses localStorage
 * - React Native: Attempts to use AsyncStorage, falls back to in-memory Map
 */
export const storage = {
  /**
   * Get an item from storage
   * @param key - The key to retrieve
   * @returns The value or null if not found
   */
  getItem: async (key: string): Promise<string | null> => {
    try {
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
    } catch (error) {
      console.error(`❌ Storage getItem failed for key "${key}":`, error)
      return null
    }
  },

  /**
   * Set an item in storage
   * @param key - The key to set
   * @param value - The value to store
   */
  setItem: async (key: string, value: string): Promise<void> => {
    try {
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
    } catch (error) {
      console.error(`❌ Storage setItem failed for key "${key}":`, error)
      throw error
    }
  },

  /**
   * Remove an item from storage
   * @param key - The key to remove
   */
  removeItem: async (key: string): Promise<void> => {
    try {
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
    } catch (error) {
      console.error(`❌ Storage removeItem failed for key "${key}":`, error)
      throw error
    }
  },

  /**
   * Clear all items from storage
   */
  clear: async (): Promise<void> => {
    try {
      if (Platform.OS === 'web') {
        localStorage.clear()
      } else {
        // Try AsyncStorage first, fallback to in-memory
        try {
          const AsyncStorage = await import('@react-native-async-storage/async-storage').then(m => m.default)
          await AsyncStorage.clear()
        } catch {
          // AsyncStorage not available, use in-memory
          inMemoryStorage.clear()
        }
      }
    } catch (error) {
      console.error('❌ Storage clear failed:', error)
      throw error
    }
  },

  /**
   * Get all keys from storage
   * @returns Array of all keys
   */
  getAllKeys: async (): Promise<readonly string[]> => {
    try {
      if (Platform.OS === 'web') {
        return Object.keys(localStorage)
      } else {
        // Try AsyncStorage first, fallback to in-memory
        try {
          const AsyncStorage = await import('@react-native-async-storage/async-storage').then(m => m.default)
          return await AsyncStorage.getAllKeys()
        } catch {
          // AsyncStorage not available, use in-memory
          return Array.from(inMemoryStorage.keys())
        }
      }
    } catch (error) {
      console.error('❌ Storage getAllKeys failed:', error)
      return []
    }
  },
}
