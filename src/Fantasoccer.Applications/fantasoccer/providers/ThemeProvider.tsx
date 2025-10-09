'use client'

import React, { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react'
import { TamaguiProvider, Theme } from 'tamagui'
import { Platform } from 'react-native'
import config from '../tamagui.config'
import { storage } from '../utils/storage'

type ThemeName = 'light' | 'dark'

type ThemeContextValue = {
  theme: ThemeName
  setTheme: (theme: ThemeName) => void
  toggleTheme: () => void
}

const ThemeContext = createContext<ThemeContextValue | undefined>(undefined)

export const useAppTheme = (): ThemeContextValue => {
  const context = useContext(ThemeContext)
  if (!context) {
    throw new Error('useAppTheme must be used within ThemeProvider')
  }
  return context
}

interface ThemeProviderProps {
  children: React.ReactNode
}

const THEME_STORAGE_KEY = 'app-theme-preference'

// Helper to get/set theme from cross-platform storage (web: localStorage, React Native: AsyncStorage)
const getStoredTheme = async (): Promise<ThemeName | null> => {
  try {
    const stored = await storage.getItem(THEME_STORAGE_KEY)
    return stored === 'light' || stored === 'dark' ? stored : null
  } catch {
    return null
  }
}

const setStoredTheme = async (theme: ThemeName): Promise<void> => {
  try {
    await storage.setItem(THEME_STORAGE_KEY, theme)
  } catch (error) {
    console.error('Failed to save theme preference:', error)
  }
}

export const ThemeProvider: React.FC<ThemeProviderProps> = ({ children }) => {
  const [theme, setThemeState] = useState<ThemeName>('dark') // Default to dark while loading
  const [isLoaded, setIsLoaded] = useState(false)

  // Load theme from storage on mount
  useEffect(() => {
    const loadTheme = async () => {
      const storedTheme = await getStoredTheme()
      if (storedTheme) {
        setThemeState(storedTheme)
      }
      setIsLoaded(true)
    }
    loadTheme()
  }, [])

  const setTheme = useCallback((newTheme: ThemeName) => {
    setThemeState(newTheme)
    setStoredTheme(newTheme) // async, fire and forget
  }, [])

  const toggleTheme = useCallback(() => {
    setTheme(theme === 'dark' ? 'light' : 'dark')
  }, [theme, setTheme])

  const contextValue = useMemo(
    () => ({
      theme,
      setTheme,
      toggleTheme,
    }),
    [theme, setTheme, toggleTheme]
  )

  return (
    <TamaguiProvider config={config} defaultTheme={theme}>
      <ThemeContext.Provider value={contextValue}>
        <Theme name={theme}>{children}</Theme>
      </ThemeContext.Provider>
    </TamaguiProvider>
  )
}
