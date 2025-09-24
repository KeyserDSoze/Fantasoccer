import {
  Button,
  H1,
  H3,
  Paragraph,
  Separator,
  SwitchThemeButton,
  XStack,
  YStack,
  Text,
  Card,
} from '@my/ui'
import { Users, Calendar, TrendingUp } from '@tamagui/lucide-icons'
import { Platform } from 'react-native'
import { useAuth } from '../../auth/AuthProvider'
import { LoginButton, UserProfile } from '../auth'
import { TokenSystemExample } from '../../components/TokenSystemExample'
import { NavigationBar } from '../../components/NavigationBar'
import { SerieADayCard } from '../../components/SerieADayCard'

export function HomeScreen({ pagesMode = false }: { pagesMode?: boolean }) {
  const { isAuthenticated, user, isLoading } = useAuth()

  return (
    <YStack flex={1} bg="$background">
      {/* Header with Login */}
      <XStack
        width="100%"
        justify="space-between"
        align="center"
        p="$2"
        bg="$color2"
        borderBottomWidth="$0.5"
        borderBottomColor="$borderColor"
      >
        <XStack align="center" gap="$3">
          <Text 
            fontSize={34}
            lineHeight={44}
            >
            ⚽
            </Text>
        </XStack>

        <XStack align="center" gap="$3">
          {isAuthenticated ? (
            <NavigationBar />
          ) : (
            Platform.OS === 'web' && <SwitchThemeButton />
          )}
        </XStack>
      </XStack>

      <YStack flex={1} justify="center" items="center" gap="$6" p="$4">
        {!isAuthenticated ? (
          <WelcomeContent />
        ) : (
          <AuthenticatedContent user={user} />
        )}
      </YStack>
    </YStack>
  )
}

function WelcomeContent() {
  return (
    <YStack gap="$6" maxWidth={600} align="center">
      <YStack gap="$4" align="center">
        <H1 textAlign="center" color="$blue10" fontWeight="800" fontSize="$10">
          Benvenuto in Fantasoccer! ⚽
        </H1>
        <Paragraph color="$color10" textAlign="center" fontSize="$5" lineHeight="$6">
          Il tuo fantacalcio ti aspetta. Crea la tua squadra, sfida i tuoi amici 
          e vinci il campionato!
        </Paragraph>
      </YStack>

      <Separator width="100%" />

      <YStack gap="$4" width="100%">
        <H3 textAlign="center" color="$color12" marginBottom="$2">
          Cosa puoi fare con Fantasoccer
        </H3>
        
        <XStack gap="$4" justify="center" flexWrap="wrap">
          <FeatureCard
            icon={<Users size="$3" color="$blue10" />}
            title="Crea Leghe"
            description="Organizza campionati con i tuoi amici"
          />
          <FeatureCard
            icon={<TrendingUp size="$3" color="$green10" />}
            title="Statistiche"
            description="Analizza le prestazioni dei giocatori"
          />
          <FeatureCard
            icon={<Calendar size="$3" color="$yellow10" />}
            title="Giornate"
            description="Gestisci le formazioni per ogni turno"
          />
        </XStack>
      </YStack>

      <YStack gap="$3" align="center">
        <LoginButton />
      </YStack>
    </YStack>
  )
}

function AuthenticatedContent({ user }: { user: any }) {
  return (
    <YStack gap="$6" maxWidth={800} align="center" width="100%">
      <YStack gap="$4" width="100%">
        <SerieADayCard />
      </YStack>

      <TokenSystemExample />

    </YStack>
  )
}

function FeatureCard({ icon, title, description }: { 
  icon: React.ReactNode, 
  title: string, 
  description: string 
}) {
  return (
    <Card 
      padding="$4" 
      backgroundColor="$color2" 
      borderRadius="$4" 
      minWidth={200}
      maxWidth={250}
      borderWidth="$0.5"
      borderColor="$borderColor"
      hoverStyle={{ backgroundColor: '$color3', scale: 1.02 }}
      pressStyle={{ scale: 0.98 }}
    >
      <YStack gap="$3" align="center">
        {icon}
        <Text fontSize="$5" fontWeight="bold" color="$color12">
          {title}
        </Text>
        <Text fontSize="$3" color="$color10" textAlign="center">
          {description}
        </Text>
      </YStack>
    </Card>
  )
}
