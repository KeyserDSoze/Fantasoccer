import React from 'react'
import { Card, Text, YStack, XStack, Avatar, Separator } from '@my/ui'
import { useAuth } from '../../auth'
import { LogoutButton } from './LoginButton'

export function UserProfile() {
  const { user, isAuthenticated } = useAuth()

  if (!isAuthenticated || !user) {
    return null
  }

  const getInitials = (name: string) => {
    return name
      .split(' ')
      .map(word => word.charAt(0))
      .join('')
      .toUpperCase()
      .slice(0, 2)
  }

  return (
    <Card padding="$4" backgroundColor="$background" borderRadius="$4">
      <YStack gap="$3">
        <XStack alignItems="center" gap="$3">
          <Avatar circular size="$6">
            <Avatar.Image
              src={`https://ui-avatars.com/api/?name=${encodeURIComponent(user.name)}&background=007ACC&color=fff`}
            />
            <Avatar.Fallback backgroundColor="$blue10">
              <Text color="white" fontWeight="600" fontSize="$5">
                {getInitials(user.name)}
              </Text>
            </Avatar.Fallback>
          </Avatar>
          
          <YStack flex={1}>
            <Text fontSize="$5" fontWeight="600" color="$gray12">
              {user.displayName || user.name}
            </Text>
            <Text fontSize="$3" color="$gray10">
              {user.email}
            </Text>
          </YStack>
        </XStack>

        <Separator />

        <XStack justifyContent="flex-end">
          <LogoutButton variant="outline" size="$3" />
        </XStack>
      </YStack>
    </Card>
  )
}