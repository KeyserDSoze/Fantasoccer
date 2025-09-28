// Global Loading Provider
import React, { createContext, useContext, useState, ReactNode, useEffect } from 'react'
import { View, StyleSheet } from 'react-native'
import { SplashScreen } from '../components/layout/SplashScreen'

interface LoadingContextType {
  isLoading: boolean
  loadingMessage: string
  showLoading: (message?: string) => void
  hideLoading: () => void
  withLoading: <T>(promise: Promise<T>, message?: string) => Promise<T>
}

const LoadingContext = createContext<LoadingContextType | undefined>(undefined)

interface LoadingProviderProps {
  children: ReactNode
}

export const LoadingProvider: React.FC<LoadingProviderProps> = ({ children }) => {
  const [isLoading, setIsLoading] = useState(true)
  const [loadingMessage, setLoadingMessage] = useState("Caricamento...")

  const showLoading = (message?: string) => {
    if (message) setLoadingMessage(message)
    console.debug('[LoadingProvider] showLoading', { message })
    setIsLoading(true)
  }

  const hideLoading = () => {
    console.debug('[LoadingProvider] hideLoading')
    setIsLoading(false)
  }

  const withLoading = async <T,>(
    promise: Promise<T>, 
    message?: string
  ): Promise<T> => {
    showLoading(message)
    try {
      const result = await promise
      hideLoading()
      return result
    } catch (error) {
      hideLoading()
      throw error
    }
  }

  const contextValue: LoadingContextType = {
    isLoading,
    loadingMessage,
    showLoading,
    hideLoading,
    withLoading
  }

  // Dev-only fallback: auto-hide the splash if it's still visible after a timeout
  useEffect(() => {
    if (process.env.NODE_ENV === 'development') {
      const timer = setTimeout(() => {
        if (isLoading) {
          // eslint-disable-next-line no-console
          console.warn('[LoadingProvider] dev fallback: hiding loading after timeout')
          setIsLoading(false)
        }
      }, 8000)
      return () => clearTimeout(timer)
    }
    return
  }, [isLoading])

  return (
    <LoadingContext.Provider value={contextValue}>
      {children}
      {isLoading && (
        <View style={styles.overlay} pointerEvents="auto">
          <SplashScreen message={loadingMessage} showSpinner={true} />
        </View>
      )}
    </LoadingContext.Provider>
  )
}

// Hook per usare il loading context
export const useGlobalLoading = (): LoadingContextType => {
  const context = useContext(LoadingContext)
  if (!context) {
    throw new Error('useGlobalLoading must be used within a LoadingProvider')
  }
  return context
}

const styles = StyleSheet.create({
  overlay: {
    ...StyleSheet.absoluteFillObject,
    zIndex: 1000,
  },
})