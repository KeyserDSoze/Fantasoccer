import React from 'react'
import { YStack, Spinner, Text } from '@my/ui'
import { useAuth } from '../../auth'
import { LoginScreen } from './LoginScreen'

interface AuthGuardProps {
  children: React.ReactNode
  fallback?: React.ReactNode
}

export function AuthGuard({ children, fallback }: AuthGuardProps) {
  const { isAuthenticated, isLoading } = useAuth()

  // Show loading spinner while checking authentication
  if (isLoading) {
    return (
      <YStack flex={1} justifyContent="center" alignItems="center" backgroundColor="$background">
        <YStack alignItems="center" gap="$3">
          <Spinner size="large" color="$blue10" />
          <Text color="$gray10" fontSize="$4">
            Verifica autenticazione...
          </Text>
        </YStack>
      </YStack>
    )
  }

  // Show login screen if not authenticated
  if (!isAuthenticated) {
    return fallback || <LoginScreen />
  }

  // User is authenticated, render protected content
  return <>{children}</>
}