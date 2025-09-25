'use client'

import {
  XStack,
  YStack,
} from '@my/ui'
import { useAuth } from '../provider/AuthProvider'
import { NavigationBar } from '../components/NavigationBar'
import { FantasoccerLogoCompact } from '../components/FantasoccerLogo'
import { ReactNode } from 'react'

interface LayoutPageProps {
  children: ReactNode
}

export function LayoutPage({ children }: LayoutPageProps) {
  const { isAuthenticated, user, isLoading } = useAuth()

  return (
    <YStack flex={1} bg="$background">
      <XStack
        width="100%"
        justify="space-between"
        alignItems="center"
        p="$2"
        bg="$color2"
        borderBottomWidth="$0.5"
        borderBottomColor="$borderColor"
      >
        <XStack alignItems="center" gap="$3">
          <FantasoccerLogoCompact 
            width={44}
            height={44}
            color="$blue10"
          />
        </XStack>

        <XStack alignItems="center" gap="$3">
          {isAuthenticated && (
            <NavigationBar />
          )}
        </XStack>
      </XStack>

      <YStack flex={1} justify="center" items="center" gap="$6" p="$4">
        {children}
      </YStack>
    </YStack>
  )
}
