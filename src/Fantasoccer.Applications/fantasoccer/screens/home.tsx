import { ScreenContent } from 'components/layout/ScreenContent';
import { StyleSheet, View } from 'react-native';

import { Button, H1, Paragraph, YStack, Text, Separator, H3, XStack, Card, Spinner } from 'tamagui';
import { useNavigation } from '@react-navigation/native';
import type { NavigationProp } from 'navigation';
import { Calendar, TrendingUp, Users } from '@tamagui/lucide-icons-2';
import { LoginButton } from 'features/LoginButton';
import { useAuth } from 'providers/AuthProvider';
import type { AuthUser } from 'providers/AuthProvider';
import { SerieALiveWidget } from 'components/seriea/SerieALiveWidget';
import { HomeLeagueMatchWidget } from 'components/home/HomeLeagueMatchWidget';
import { useApp } from 'providers/AppProvider';

export default function Home() {
  const { isAuthenticated, user } = useAuth();

  return (
    <View style={styles.container}>
      <ScreenContent path="screens/home.tsx" title="Home">
        {isAuthenticated ? <AuthenticatedContent user={user} /> : <WelcomeContent />}
      </ScreenContent>
    </View>
  );
}

function AuthenticatedContent({ user }: { user: AuthUser | null }) {
  const navigation = useNavigation<NavigationProp>();
  const { userGroups, hasLoadedGroups, isGroupsLoading, groupsError, refreshGroups } = useApp();

  if (!hasLoadedGroups || isGroupsLoading) {
    return <YStack width="100%" alignItems="center" gap="$3" padding="$6"><Spinner /><Paragraph>Caricamento dei tuoi gruppi…</Paragraph></YStack>;
  }

  if (groupsError) {
    return <Card width="100%" padding="$4" gap="$3">
      <H3>Non riesco a caricare i gruppi</H3>
      <Paragraph>{groupsError}</Paragraph>
      <Button variant="outlined" alignSelf="flex-start" onPress={() => void refreshGroups()}>Riprova</Button>
    </Card>;
  }

  if (userGroups.length === 0) {
    return <Card width="100%" padding="$5" gap="$4">
      <H1 color="$blue10">Benvenuto{user?.username ? `, ${user.username}` : ''}</H1>
      <Paragraph>
        Il tuo account è pronto, ma non fai ancora parte di un gruppo. Classifiche, formazioni e mercato saranno disponibili dopo l’invito di un amministratore.
      </Paragraph>
      <Paragraph color="$color10">Se sei un amministratore di piattaforma, puoi creare un gruppo dalla gestione gruppi.</Paragraph>
      <Button alignSelf="flex-start" onPress={() => navigation.navigate('GroupWorkspace')}>Apri gestione gruppi</Button>
    </Card>;
  }

  return (
    <YStack gap="$4" width="100%" minWidth={0} flexShrink={0}>
      <HomeLeagueMatchWidget />
      <SerieALiveWidget refreshInterval={10000} enablePolling pollOnlyWhenLive={false} />
    </YStack>
  );
}

function WelcomeContent() {
  const navigation = useNavigation<NavigationProp>();
  return (
    <YStack gap="$6" width="100%" alignItems="center">
      <YStack gap="$4" alignItems="center">
        <H1 textAlign="center" color="$blue10" fontWeight="800" fontSize="$10">
          Benvenuto in Fantasoccer!
        </H1>
        <Paragraph color="$color10" textAlign="center" fontSize="$5" lineHeight="$6">
          Il tuo fantacalcio ti aspetta. Crea la tua squadra, sfida i tuoi amici e vinci il
          campionato!
        </Paragraph>
      </YStack>

      {/* Call to Action */}
      <YStack gap="$3" alignItems="center">
        <Text fontSize="$4" color="$color11" textAlign="center" fontWeight="600">
          Inizia subito la tua avventura!
        </Text>
        <LoginButton variant="primary" size="$6" />
        <Button variant="outlined" size="$3" onPress={() => navigation.navigate('McpGuide')}>
          Come usare Fantasoccer su ChatGPT
        </Button>
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
  );
}

function FeatureCard({
  icon,
  title,
  description,
}: {
  icon: React.ReactNode;
  title: string;
  description: string;
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
      pressStyle={{ scale: 0.98 }}>
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
  );
}

export const styles = StyleSheet.create({
  container: {
    flex: 1,
    padding: 0,
  },
});
