import { FantasoccerLogo } from './FantasoccerLogo'
import { YStack, Text, Spinner } from 'tamagui'

interface SplashScreenProps {
  message?: string
  showSpinner?: boolean
}

export const SplashScreen: React.FC<SplashScreenProps> = ({ 
  message = "Caricamento...", 
  showSpinner = true 
}) => {
  return (
    <YStack 
      flex={1} 
      justifyContent="center" 
      alignItems="center" 
      backgroundColor="$background"
      gap="$4"
    >
      {/* Logo principale */}
      <FantasoccerLogo 
        width={120}
        height={120}
        color="$blue10"
      />
      
      {/* Titolo */}
      <Text 
        fontSize="$8" 
        fontWeight="bold" 
        color="$blue10"
        textAlign="center"
      >
        FantaZone
      </Text>
      
      {/* Messaggio di caricamento */}
      <YStack alignItems="center" gap="$3" marginTop="$4">
        {showSpinner && (
          <Spinner size="large" color="$blue10" />
        )}
        <Text 
          fontSize="$4" 
          color="$color11" 
          textAlign="center"
          opacity={0.8}
        >
          {message}
        </Text>
      </YStack>
    </YStack>
  )
}