import { useAuth } from '../../providers/AuthProvider'
import { NavigationBar } from './NavigationBar'
import { ReactNode } from 'react'
import { Image } from 'react-native'
import { XStack, YStack } from 'tamagui'

interface LayoutPageProps {
  children: ReactNode
}
export const LayoutPage: React.FC<LayoutPageProps> = ({ children }) =>{
  const { isAuthenticated } = useAuth()
  return (
    <YStack flex={1} bg="$background">
      <XStack
        width="100%"
        justifyContent="space-between"
        alignItems="center"
        p="$2"
        bg="$color2"
        borderBottomWidth="$0.5"
        borderBottomColor="$borderColor"
      >
        <XStack alignItems="center" gap="$3">
          <Image 
            source={require('../../assets/favicon.png')}
            style={{ width: 44, height: 44 }}
            resizeMode="contain"
          />
        </XStack>

        <XStack alignItems="center" gap="$3">
          {isAuthenticated && (
            <NavigationBar />
          )}
        </XStack>
      </XStack>

      <YStack flex={1} justifyContent="center" alignItems="center" gap="$6" p="$4">
        {children}
      </YStack>
    </YStack>
  )
}
