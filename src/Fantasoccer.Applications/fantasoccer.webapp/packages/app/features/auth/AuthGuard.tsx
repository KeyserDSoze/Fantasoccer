import React from 'react'
import { YStack, Spinner, Text } from '@my/ui'
import { useAuth } from '../../auth'
import { useApp } from '../../provider/AppProvider'
import { LoginScreen } from './LoginScreen'

interface AuthGuardProps {
  children: React.ReactNode
  fallback?: React.ReactNode
}

export function AuthGuard({ children, fallback }: AuthGuardProps) {
  const { isAuthenticated, isLoading: authLoading } = useAuth()
  const { isGroupsLoading, groupsError } = useApp()

  // Calculate loading state - show loading if either auth or groups are loading
  const isLoading = authLoading || (isAuthenticated && isGroupsLoading)

  // Show loading spinner while checking authentication or loading groups
  if (isLoading) {
    return (
      <YStack flex={1} justifyContent="center" alignItems="center" backgroundColor="$background">
        <YStack alignItems="center" gap="$3">
          <Spinner size="large" color="$blue10" />
          <Text color="$gray10" fontSize="$4">
            {authLoading ? 'Verifica autenticazione...' : 'Caricamento dati utente...'}
          </Text>
        </YStack>
      </YStack>
    )
  }

  // Show login screen if not authenticated
  if (!isAuthenticated) {
    return fallback || <LoginScreen />
  }

  // Show error if groups failed to load
  if (groupsError) {
    return (
      <YStack flex={1} justifyContent="center" alignItems="center" backgroundColor="$background">
        <YStack alignItems="center" gap="$3" paddingHorizontal="$4">
          <Text color="$red10" fontSize="$5" fontWeight="bold">
            Errore nel caricamento
          </Text>
          <Text color="$gray10" fontSize="$4" textAlign="center">
            {groupsError}
          </Text>
          <Text color="$gray8" fontSize="$3" textAlign="center">
            Riprova più tardi o contatta il supporto
          </Text>
        </YStack>
      </YStack>
    )
  }

  // User is authenticated and groups are loaded, render protected content
  return <>{children}</>
}