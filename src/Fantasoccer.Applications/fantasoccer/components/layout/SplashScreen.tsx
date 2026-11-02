import { FantasoccerLogo } from './FantasoccerLogo'
import { YStack, XStack, Text, Spinner } from 'tamagui'
import { Check, AlertTriangle } from '@tamagui/lucide-icons-2'

export interface LoadingStep {
  id: string
  label: string
  status: 'pending' | 'done' | 'error'
}

interface SplashScreenProps {
  message?: string
  showSpinner?: boolean
  compact?: boolean
  steps?: LoadingStep[]
}

export const SplashScreen: React.FC<SplashScreenProps> = ({ 
  message = "Caricamento...", 
  showSpinner = true,
  compact = false,
  steps = [],
}) => {
  return (
    <YStack 
      flex={compact ? undefined : 1}
      minHeight={compact ? 180 : undefined}
      padding={compact ? '$4' : undefined}
      justifyContent="center" 
      alignItems="center" 
      backgroundColor={compact ? 'transparent' : '$background'}
      gap="$4"
    >
      {/* Logo principale */}
      <FantasoccerLogo 
        width={compact ? 64 : 120}
        height={compact ? 64 : 120}
        color="$blue10"
      />
      
      {/* Titolo */}
      <Text 
        fontSize={compact ? '$5' : '$8'}
        fontWeight="bold" 
        color="$blue10"
        textAlign="center"
      >
        FantaZone
      </Text>
      
      {/* Messaggio di caricamento */}
      <YStack alignItems="center" gap="$3" marginTop={compact ? '$1' : '$4'}>
        {showSpinner && steps.length === 0 && (
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
        {steps.length > 0 && <YStack gap="$3" alignSelf="stretch" accessibilityLiveRegion="polite">
          {steps.map(step => <XStack key={step.id} gap="$2" alignItems="center">
            {step.status === 'done' ? <Check size={18} color="$green10" />
              : step.status === 'error' ? <AlertTriangle size={18} color="$orange10" />
              : <Spinner size="small" color="$blue10" />}
            <Text fontSize="$3" color={step.status === 'done' ? '$green10' : '$color11'}>
              {step.label}{step.status === 'done' ? ' · pronto' : step.status === 'error' ? ' · non disponibile' : '…'}
            </Text>
          </XStack>)}
        </YStack>}
      </YStack>
    </YStack>
  )
}
