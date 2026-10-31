import { ScreenContent } from 'components/layout/ScreenContent';
import { StyleSheet, View } from 'react-native';

import { Button, Paragraph, YStack, H3, Card, Spinner } from 'tamagui';
import { useNavigation } from '@react-navigation/native';
import type { NavigationProp } from 'navigation';
import { useAuth } from 'providers/AuthProvider';
import type { AuthUser } from 'providers/AuthProvider';
import { SerieALiveWidget } from 'components/seriea/SerieALiveWidget';
import { HomeLeagueMatchWidget } from 'components/home/HomeLeagueMatchWidget';
import { useApp } from 'providers/AppProvider';
import { GroupCreationContent } from '../components/groups/GroupCreationContent';
import { MCP_ENTRY_POINTS } from '../configurations/features';

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
    return <YStack width="100%" gap="$4">
      <GroupCreationContent key={user?.email} showForm={false} />
      <Button variant="outlined" alignSelf="flex-start" onPress={() => void refreshGroups()}>Sono stato invitato · Aggiorna i gruppi</Button>
    </YStack>;
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
    <YStack gap="$4" width="100%">
      <GroupCreationContent showForm={false} />
      {MCP_ENTRY_POINTS ? (
        <Button variant="outlined" size="$3" alignSelf="center" onPress={() => navigation.navigate('McpGuide')}>
          Come usare Fantasoccer su ChatGPT
        </Button>
      ) : null}
    </YStack>
  );
}

export const styles = StyleSheet.create({
  container: {
    flex: 1,
    padding: 0,
  },
});
