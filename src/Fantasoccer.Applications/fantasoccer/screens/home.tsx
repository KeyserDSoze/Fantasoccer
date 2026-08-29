import { ScreenContent } from 'components/layout/ScreenContent';
import { StyleSheet, View } from 'react-native';

import { H1, Paragraph, YStack, Text, Separator, H3, XStack, Card } from 'tamagui';
import { Calendar, TrendingUp, Users, ShieldCheck } from '@tamagui/lucide-icons-2';
import { LoginButton } from 'features/LoginButton';
import { useAuth } from 'providers/AuthProvider';
import type { AuthUser } from 'providers/AuthProvider';
import { SerieALiveWidget } from 'components/seriea/SerieALiveWidget';

export default function Home() {
  const { isAuthenticated, user } = useAuth();

  return (
    <View style={styles.container}>
      <ScreenContent path="screens/home.tsx" title="Home">
        {isAuthenticated ? (
          <AuthenticatedContent user={user} />
        ) : (
          <WelcomeContent />
        )}
      </ScreenContent>
    </View>
  );
}

function AuthenticatedContent({ user }: { user: AuthUser | null }) {
  const displayName = user?.displayName || user?.name || 'Fantallenatore';

  return (
    <YStack gap="$6" width="100%">
      <SerieALiveWidget refreshInterval={10000} enablePolling pollOnlyWhenLive={false} />
    </YStack>
  );
}


function WelcomeContent() {
  return (
    <YStack gap="$6" width="100%" alignItems="center">
      <YStack gap="$4" alignItems="center">
        <H1 textAlign="center" color="$blue10" fontWeight="800" fontSize="$10">
          Benvenuto in Fantasoccer!
        </H1>
        <Paragraph color="$color10" textAlign="center" fontSize="$5" lineHeight="$6">
          Il tuo fantacalcio ti aspetta. Crea la tua squadra, sfida i tuoi amici 
          e vinci il campionato!
        </Paragraph>
      </YStack>

      {/* Call to Action */}
      <YStack gap="$3" alignItems="center">
        <Text fontSize="$4" color="$color11" textAlign="center" fontWeight="600">
          Inizia subito la tua avventura!
        </Text>
        <LoginButton variant="primary" size="$6"/>
      </YStack>

      <Separator width="100%" />

      <YStack gap="$4" width="100%">
        <H3 textAlign="center" color="$color12" marginBottom="$2">
          Cosa puoi fare con Fantasoccer
        </H3>
        
        <XStack gap="$4" justifyContent="center" flexWrap="wrap">
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
      <YStack gap="$3" alignItems="center">
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

export const styles = StyleSheet.create({
  container: {
    flex: 1,
    padding: 0,
  },
});
