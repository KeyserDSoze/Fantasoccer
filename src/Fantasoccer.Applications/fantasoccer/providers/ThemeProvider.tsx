'use client'

import React, { createContext, useCallback, useContext, useMemo, useState } from 'react'
import { TamaguiProvider, Theme } from 'tamagui'
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

export const ThemeProvider: React.FC<ThemeProviderProps> = ({ children }) => {
  const [theme, setTheme] = useState<ThemeName>('dark')

  const toggleTheme = useCallback(() => {
    setTheme(prev => (prev === 'dark' ? 'light' : 'dark'))
  }, [])

  const contextValue = useMemo(
    () => ({
      theme,
      setTheme,
      toggleTheme,
    }),
    [theme, toggleTheme]
  )

  return (
    <TamaguiProvider config={config} defaultTheme={theme}>
      <ThemeContext.Provider value={contextValue}>
        <Theme name={theme}>{children}</Theme>
      </ThemeContext.Provider>
    </TamaguiProvider>
  )
}
