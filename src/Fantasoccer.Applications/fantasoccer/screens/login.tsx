import { Text, YStack, Spinner, H1, H2, Card, Button } from 'tamagui';
import { useAuth } from '../providers/AuthProvider'
import { LoginButton } from '../features/LoginButton'
import { FantasoccerLogo } from '../components/FantasoccerLogo'
import { useNavigation } from '@react-navigation/native';

function HomeButton() {
  const navigation = useNavigation();

  return (
    <Button
      variant="outlined"
      size="$3"
      onPress={() => navigation.navigate('home')}
      chromeless
    >
      <Text fontSize="$3" color="#888">
        ← Torna alla Home
      </Text>
    </Button>
  )
}

export function Login() {
  const { isLoading, error } = useAuth()

  return (
    <YStack flex={1} justifyContent="center" alignItems="center" padding="$4" backgroundColor="$background">
      <YStack maxWidth={400} width="100%" gap="$6" alignItems="center">
        
        {/* App Logo/Title */}
        <YStack alignItems="center" gap="$4">
          <FantasoccerLogo 
            width={120}
            height={120}
            color="$blue10"
          />
          <YStack alignItems="center" gap="$2">
            <H1 color="$blue10" fontWeight="800">
              Fantasoccer
            </H1>
            <Text fontSize="$4" color="#888" textAlign="center">
              La tua lega fantacalcio
            </Text>
          </YStack>
        </YStack>

        {/* Login Card */}
        <Card padding="$6" width="100%" backgroundColor="$background" borderRadius="$6" elevate>
          <YStack gap="$4" alignItems="center">
            
            <YStack alignItems="center" gap="$2">
              <H2 fontSize="$6" fontWeight="600" color="#aaa">
                Benvenuto
              </H2>
              <Text fontSize="$4" color="#888" textAlign="center">
                Accedi con il tuo account Microsoft per continuare
              </Text>
            </YStack>

            {/* Error Message */}
            {error && (
              <Card backgroundColor="$red2" borderColor="$red8" borderWidth={1} padding="$3" width="100%">
                <Text color="$red11" fontSize="$3" textAlign="center">
                  {error}
                </Text>
              </Card>
            )}

            {/* Loading State */}
            {isLoading ? (
              <YStack alignItems="center" gap="$3" paddingVertical="$4">
                <Spinner size="large" color="$blue10" />
                <Text color="#888" fontSize="$3">
                  Accesso in corso...
                </Text>
              </YStack>
            ) : (
              <YStack gap="$3" width="100%">
                {/* Login con Microsoft */}
                <LoginButton variant="primary" size="$5" fullWidth directLogin />
                
                {/* Placeholder per futuri provider */}
                {/* <LoginButton variant="outline" size="$5" fullWidth provider="google">
                  Accedi con Google
                </LoginButton> */}
                
                {/* Separatore per futuri provider */}
                {/* <XStack alignItems="center" gap="$3" marginVertical="$2">
                  <YStack flex={1} height={1} backgroundColor="$gray6" />
                  <Text fontSize="$2" color="$gray9">oppure</Text>
                  <YStack flex={1} height={1} backgroundColor="$gray6" />
                </XStack> */}
              </YStack>
            )}

            {/* Info Text */}
            <Text fontSize="$2" color="#666" textAlign="center" maxWidth="90%">
              Utilizzando l'accesso Microsoft, accetti i nostri termini di servizio e la privacy policy.
            </Text>
          </YStack>
        </Card>

        {/* Additional Info */}
        <YStack alignItems="center" gap="$2">
          <Text fontSize="$3" color="#888" textAlign="center">
            Primo accesso? Il tuo account verrà creato automaticamente.
          </Text>
          
          <HomeButton />
        </YStack>
      </YStack>
    </YStack>
  )
}