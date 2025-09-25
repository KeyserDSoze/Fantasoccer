'use client'

// Global Loading Overlay Component
import React, { useEffect, useState } from 'react'
import { YStack, XStack, Text, AnimatePresence } from '@my/ui'

interface LoadingOverlayProps {
  isVisible: boolean
  message?: string
  children?: React.ReactNode
}

export const LoadingOverlay: React.FC<LoadingOverlayProps> = ({ 
  isVisible, 
  message = "Caricamento...",
  children 
}) => {
  const [bounceDirection, setBounceDirection] = useState(1)

  // Animazione bounce per il pallone
  useEffect(() => {
    if (!isVisible) return

    const interval = setInterval(() => {
      setBounceDirection(prev => prev * -1)
    }, 800) // Bounce ogni 800ms

    return () => clearInterval(interval)
  }, [isVisible])

  return (
    <>
      {/* Contenuto normale */}
      {children}
      
      {/* Overlay di Loading */}
      <AnimatePresence>
        {isVisible && (
          <YStack
            position="fixed"
            top={0}
            left={0}
            right={0}
            bottom={0}
            backgroundColor="rgba(0, 0, 0, 0.7)"
            justifyContent="center"
            alignItems="center"
            zIndex={9999}
            animation="quick"
            enterStyle={{
              opacity: 0,
              scale: 0.9,
            }}
            exitStyle={{
              opacity: 0,
              scale: 0.9,
            }}
          >
            {/* Container del loading */}
            <YStack
              backgroundColor="$background"
              padding="$6"
              borderRadius="$4"
              alignItems="center"
              gap="$4"
              minWidth={200}
              shadowColor="$shadowColor"
              shadowOffset={{ width: 0, height: 4 }}
              shadowOpacity={0.3}
              shadowRadius={8}
              elevation={8}
            >
              {/* Pallone animato */}
              <XStack justifyContent="center" alignItems="center" height={60}>
                <Text
                  fontSize={40}
                  animation="bouncy"
                  style={{
                    transform: [
                      { 
                        translateY: bounceDirection * 10,
                        scale: 1 + (bounceDirection * 0.1)
                      }
                    ]
                  }}
                >
                  ⚽
                </Text>
              </XStack>

              {/* Messaggio di loading */}
              <Text
                fontSize="$5"
                fontWeight="600"
                color="$color12"
              >
                {message}
              </Text>

              {/* Punti animati */}
              <XStack gap="$2" alignItems="center">
                {[0, 1, 2].map((index) => (
                  <Text
                    key={index}
                    fontSize="$4"
                    color="$color10"
                    animation="quick"
                    style={{
                      opacity: (Date.now() + index * 200) % 1200 < 400 ? 1 : 0.3
                    }}
                  >
                    •
                  </Text>
                ))}
              </XStack>
            </YStack>
          </YStack>
        )}
      </AnimatePresence>
    </>
  )
}

// Hook per gestire facilmente il loading state
export const useLoading = () => {
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

  return {
    isLoading,
    loadingMessage,
    showLoading,
    hideLoading,
    withLoading
  }
}