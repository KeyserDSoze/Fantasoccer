'use client'

import React, { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react'
import { TamaguiProvider, Theme } from 'tamagui'
import { Platform } from 'react-native'
import config from '../tamagui.config'

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

// Helper to get/set theme from localStorage (web) or AsyncStorage (mobile)
const getStoredTheme = (): ThemeName | null => {
  if (Platform.OS === 'web') {
    try {
      const stored = localStorage.getItem(THEME_STORAGE_KEY)
      return stored === 'light' || stored === 'dark' ? stored : null
    } catch {
      return null
    }
  }
  // For mobile, we would use AsyncStorage here
  return null
}

const setStoredTheme = (theme: ThemeName): void => {
  if (Platform.OS === 'web') {
    try {
      localStorage.setItem(THEME_STORAGE_KEY, theme)
    } catch (error) {
      console.error('Failed to save theme preference:', error)
    }
  }
  // For mobile, we would use AsyncStorage here
}

export const ThemeProvider: React.FC<ThemeProviderProps> = ({ children }) => {
  const [theme, setThemeState] = useState<ThemeName>(() => {
    // Initialize from stored preference or default to 'dark'
    return getStoredTheme() || 'dark'
  })

  const setTheme = useCallback((newTheme: ThemeName) => {
    setThemeState(newTheme)
    setStoredTheme(newTheme)
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
