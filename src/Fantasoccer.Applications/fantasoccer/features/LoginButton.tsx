import { Button, Spinner, XStack, Text } from 'tamagui';
import { useAuth } from '../providers/AuthProvider'
import { useNavigation } from '@react-navigation/native';

interface LoginButtonProps {
  variant?: 'primary' | 'outline'
  size?: '$4' | '$5' | '$6'
  fullWidth?: boolean
  /** Se true, esegue il login direttamente. Se false (default), naviga alla pagina di login */
  directLogin?: boolean
}

export function LoginButton({ 
  variant = 'primary', 
  size = '$5', 
  fullWidth = true,
  directLogin = false 
}: LoginButtonProps) {
  const { login, isLoading } = useAuth()
  const navigation = useNavigation();

  const handlePress = async () => {
    if (directLogin) {
      // Login diretto con Microsoft
      try {
        await login()
      } catch (error) {
        console.error('Login failed:', error)
      }
    } else {
      // Naviga alla pagina di login dedicata
      navigation.navigate('login')
    }
  }

  return (
    <Button
      size={size}
      onPress={handlePress}
      disabled={directLogin && isLoading}
      backgroundColor={variant === 'primary' ? '$blue10' : 'transparent'}
      borderColor={variant === 'outline' ? '$blue10' : 'transparent'}
      borderWidth={variant === 'outline' ? 1 : 0}
      width={fullWidth ? '100%' : 'auto'}
      pressStyle={{ opacity: 0.8 }}
      disabledStyle={{ opacity: 0.5 }}
    >
      <XStack alignItems="center" gap="$2">
        {directLogin && isLoading ? (
          <Spinner color={variant === 'primary' ? 'white' : '$blue10'} />
        ) : (
          <>
            <Text
              color={variant === 'primary' ? 'white' : '$blue10'}
              fontWeight="600"
              fontSize="$4"
            >
              {directLogin ? 'Accedi con Microsoft' : 'Accedi'}
            </Text>
          </>
        )}
      </XStack>
    </Button>
  )
}

interface LogoutButtonProps {
  variant?: 'primary' | 'outline' | 'ghost'
  size?: '$3' | '$4' | '$5'
}

export function LogoutButton({ variant = 'outline', size = '$4' }: LogoutButtonProps) {
  const { logout, isLoading } = useAuth()

  const handleLogout = async () => {
    try {
      await logout()
    } catch (error) {
      console.error('Logout failed:', error)
    }
  }

  return (
    <Button
      size={size}
      onPress={handleLogout}
      disabled={isLoading}
      backgroundColor={
        variant === 'primary' ? '$red10' : 
        variant === 'outline' ? 'transparent' : 
        'transparent'
      }
      borderColor={variant === 'outline' ? '$red10' : 'transparent'}
      borderWidth={variant === 'outline' ? 1 : 0}
      pressStyle={{ opacity: 0.8 }}
      disabledStyle={{ opacity: 0.5 }}
    >
      <XStack alignItems="center" gap="$2">
        {isLoading ? (
          <Spinner color={variant === 'primary' ? 'white' : '$red10'} />
        ) : (
          <Text
            color={
              variant === 'primary' ? 'white' : 
              variant === 'outline' ? '$red10' : 
              '#aaa'
            }
            fontWeight="500"
            fontSize="$3"
          >
            Esci
          </Text>
        )}
      </XStack>
    </Button>
  )
}