// Global Loading Provider
import React, { createContext, useContext, useState, ReactNode } from 'react'
import { LoadingOverlay } from '../components/LoadingOverlay'

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
  const [isLoading, setIsLoading] = useState(false)
  const [loadingMessage, setLoadingMessage] = useState("Caricamento...")

  const showLoading = (message?: string) => {
    if (message) setLoadingMessage(message)
    setIsLoading(true)
  }

  const hideLoading = () => {
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

  return (
    <LoadingContext.Provider value={contextValue}>
      <LoadingOverlay 
        isVisible={isLoading} 
        message={loadingMessage}
      >
        {children}
      </LoadingOverlay>
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