import { useCallback, useMemo, useState, useEffect } from 'react';
import {
  AlertTriangle,
  Check,
  User,
  ChevronDown,
  ChevronUp,
  Sparkles,
  RadioTower,
} from '@tamagui/lucide-icons-2';
import {
  Button,
  Card,
  H3,
  Input,
  Paragraph,
  Separator,
  Spinner,
  Text,
  XStack,
  YStack,
  useMedia,
} from 'tamagui';
import { ScreenContent } from 'components/layout/ScreenContent';
import { McpConnectionsCard } from 'components/McpConnectionsCard';
import { McpGuideCard } from 'components/mcp/McpGuideCard';
import { useApp } from 'providers/AppProvider';
import { useAuth } from 'providers/AuthProvider';
import { useSettings } from 'hooks/useSettings';
import { useNavigation } from '@react-navigation/native';
import type { NavigationProp } from '../navigation';
import { useGroup } from 'hooks/useGroup';
import { getActualFantacalcioYear } from 'utils/timezoneHelpers';
import {
  VERSION_BANNER_CHANGED_EVENT,
  VERSION_BANNER_ENABLED_KEY,
  setVersionBannerEnabled,
} from 'components/version/VersionUpdateBanner';

export default function SettingsScreen() {
  const navigation = useNavigation<NavigationProp>();
  const media = useMedia();
  const containerWidth = media.gtMd ? 800 : '100%';

  const { currentGroup } = useApp();
  const { user } = useAuth();
  const userEmail = user?.email ?? null;
  const groupId = currentGroup?.id ?? null;
  const [versionBannerEnabled, setVersionBannerEnabledState] = useState(true);

  useEffect(() => {
    const syncVersionBanner = () => {
      setVersionBannerEnabledState(localStorage.getItem(VERSION_BANNER_ENABLED_KEY) !== 'false');
    };
    syncVersionBanner();
    window.addEventListener(VERSION_BANNER_CHANGED_EVENT, syncVersionBanner);
    return () => window.removeEventListener(VERSION_BANNER_CHANGED_EVENT, syncVersionBanner);
  }, []);

  // Get current year
  const currentYear = getActualFantacalcioYear();

  // Get user's team info from the group
  const { group, loading: groupLoading } = useGroup(groupId);

  // Find user's team in the current year's baskets
  const userTeamInfo = useMemo(() => {
    if (!group || !userEmail) return null;

    // Search through all baskets for the user's team
    for (const basket of group.baskets) {
      const yearData = basket.years?.find((y) => y.year === currentYear);
      if (!yearData) continue;

      const team = yearData.teams?.find(
        (t) =>
          t.owner.toLowerCase() === userEmail ||
          t.additionalOwners?.some((owner) => owner.toLowerCase() === userEmail)
      );

      if (team) {
        return {
          basketId: basket.id,
          teamName: team.name,
          owner: team.owner,
          year: currentYear,
        };
      }
    }

    return null;
  }, [group, userEmail, currentYear]);

  const {
    updatingUsername,
    updateUsernameError,
    updateUsername,
    userSettings,
    loadingUserSettings,
    updatingUserSettings,
    updateAutoOptimalFormation,
    updateLiveWidgetEnabled,
  } = useSettings();

  // Local state for inputs
  const [newUsername, setNewUsername] = useState('');
  const [usernameSuccess, setUsernameSuccess] = useState(false);
  const [optimalFormationInfoOpen, setOptimalFormationInfoOpen] = useState(false);

  // Local state for updated username (to avoid full page refresh)
  const [updatedUsername, setUpdatedUsername] = useState<string | null>(null);

  // Display username: use updated username if available, otherwise original
  const displayUsername = updatedUsername ?? user?.username ?? 'Non impostato';

  // Handle username update
  const handleUpdateUsername = useCallback(async () => {
    if (!newUsername.trim()) return;

    const success = await updateUsername(newUsername.trim());

    if (success) {
      // Update local state instead of modifying user object directly
      setUpdatedUsername(newUsername.trim());
      setUsernameSuccess(true);
      setNewUsername('');
      setTimeout(() => setUsernameSuccess(false), 3000);
    }
  }, [newUsername, updateUsername]);

  const isLoading = groupLoading;

  return (
    <ScreenContent path="screens/settings.tsx" title="Impostazioni">
      <YStack
        width={containerWidth}
        maxWidth={800}
        gap="$4"
        alignItems="stretch"
        alignSelf="center"
        paddingBottom="$8">
        <McpGuideCard />
        <Card padding="$4" gap="$2">
          <H3>I tuoi gruppi</H3>
          <Paragraph>Gestisci membri, competizioni e squadre dei tuoi gruppi oppure attiva un nuovo gruppo con un codice.</Paragraph>
          <Button onPress={() => navigation.navigate('GroupWorkspace')}>Gestisci i tuoi gruppi</Button>
          <Button variant="outlined" onPress={() => navigation.navigate('CreateGroup')}>Crea un nuovo gruppo</Button>
        </Card>
        <McpConnectionsCard key={userEmail} email={userEmail} />
        <Card
          padding="$4"
          backgroundColor="$color2"
          borderRadius="$4"
          borderWidth="$0.5"
          borderColor="$borderColor">
          <YStack gap="$4">
            <XStack alignItems="center" gap="$2">
              <RadioTower size="$1.5" color="$red10" />
              <H3 color="$color12">Widget partita</H3>
            </XStack>
            <Separator />
            <XStack alignItems="center" justifyContent="space-between" gap="$3">
              <YStack flex={1} gap="$1">
                <Text color="$color12" fontWeight="600">
                  Live e prossima formazione
                </Text>
                <Text color="$color10" fontSize="$2">
                  Mostra risultati live e conto alla rovescia della prossima giornata in tutte le
                  schermate.
                </Text>
              </YStack>
              <Button
                theme={userSettings.liveWidgetEnabled ? 'green' : 'gray'}
                disabled={loadingUserSettings || updatingUserSettings}
                onPress={() => updateLiveWidgetEnabled(!userSettings.liveWidgetEnabled)}>
                {userSettings.liveWidgetEnabled ? 'Disattiva' : 'Attiva'}
              </Button>
            </XStack>
          </YStack>
        </Card>
        <Card
          padding="$4"
          backgroundColor="$color2"
          borderRadius="$4"
          borderWidth="$0.5"
          borderColor="$borderColor">
          <YStack gap="$4">
            <XStack alignItems="center" gap="$2">
              <Sparkles size="$1.5" color="$blue10" />
              <H3 color="$color12">Aggiornamenti versione</H3>
            </XStack>
            <Separator />
            <XStack alignItems="center" justifyContent="space-between" gap="$3">
              <YStack flex={1} gap="$1">
                <Text color="$color12" fontWeight="600">
                  Mostra il widget delle nuove versioni
                </Text>
                <Text color="$color10" fontSize="$2">
                  Ricevi un avviso quando vengono pubblicati aggiornamenti e nuove patch note.
                </Text>
              </YStack>
              <Button
                theme={versionBannerEnabled ? 'green' : 'gray'}
                onPress={() => {
                  const enabled = !versionBannerEnabled;
                  setVersionBannerEnabled(enabled);
                  setVersionBannerEnabledState(enabled);
                }}>
                {versionBannerEnabled ? 'Disattiva' : 'Riattiva'}
              </Button>
            </XStack>
          </YStack>
        </Card>
        {isLoading ? (
          <Card
            padding="$4"
            backgroundColor="$color2"
            borderRadius="$4"
            borderWidth="$0.5"
            borderColor="$borderColor">
            <XStack alignItems="center" gap="$3" justifyContent="center">
              <Spinner size="small" color="$blue10" />
              <Text color="$color10">Caricamento impostazioni...</Text>
            </XStack>
          </Card>
        ) : !currentGroup ? (
          <Card
            padding="$4"
            backgroundColor="$color2"
            borderRadius="$4"
            borderWidth="$0.5"
            borderColor="$borderColor">
            <Text color="$color12" fontWeight="600" marginBottom="$2">
              Nessun gruppo selezionato
            </Text>
            <Paragraph color="$color10">
              Seleziona un gruppo dalla barra laterale per gestire le impostazioni.
            </Paragraph>
          </Card>
        ) : !userTeamInfo ? (
          <Card
            padding="$4"
            backgroundColor="$yellow3"
            borderRadius="$4"
            borderWidth="$0.5"
            borderColor="$yellow7">
            <XStack alignItems="center" gap="$2">
              <AlertTriangle size="$1.5" color="$yellow10" />
              <Text color="$yellow11" fontWeight="600">
                Nessuna squadra trovata
              </Text>
            </XStack>
            <Paragraph marginTop="$2" color="$yellow11">
              Non hai una squadra registrata in questo gruppo per l&apos;anno corrente (
              {currentYear}).
            </Paragraph>
          </Card>
        ) : (
          <YStack gap="$6">
            <Card
              padding="$4"
              backgroundColor="$color2"
              borderRadius="$4"
              borderWidth="$0.5"
              borderColor="$borderColor">
              <YStack gap="$4">
                <XStack alignItems="center" gap="$2">
                  <Sparkles size="$1.5" color="$purple10" />
                  <H3 color="$color12">AIgoritmo</H3>
                </XStack>
                <Separator />
                <XStack alignItems="center" justifyContent="space-between" gap="$3">
                  <YStack flex={1} gap="$1">
                    <Text color="$color12" fontWeight="600">
                      Formazione ottimale automatica
                    </Text>
                    <Text color="$color10" fontSize="$2">
                      Applica automaticamente la migliore formazione alla nuova giornata.
                    </Text>
                  </YStack>
                  <Button
                    theme={userSettings.autoOptimalFormation ? 'green' : 'gray'}
                    disabled={loadingUserSettings || updatingUserSettings}
                    onPress={() => updateAutoOptimalFormation(!userSettings.autoOptimalFormation)}>
                    {userSettings.autoOptimalFormation ? 'Disattiva' : 'Attiva'}
                  </Button>
                </XStack>
                <XStack
                  alignItems="center"
                  justifyContent="space-between"
                  paddingVertical="$2"
                  pressStyle={{ opacity: 0.75 }}
                  onPress={() => setOptimalFormationInfoOpen((open) => !open)}>
                  <Text color="$purple10" fontWeight="600">
                    Come funziona la formazione ottimale?
                  </Text>
                  {optimalFormationInfoOpen ? (
                    <ChevronUp size="$1.5" color="$purple10" />
                  ) : (
                    <ChevronDown size="$1.5" color="$purple10" />
                  )}
                </XStack>
                {optimalFormationInfoOpen && (
                  <Card
                    padding="$3"
                    backgroundColor="$color3"
                    borderRadius="$3"
                    borderWidth="$0.5"
                    borderColor="$purple7">
                    <YStack gap="$3">
                      <Text color="$color12" fontWeight="600">
                        L&apos;algoritmo sceglie la formazione per ogni nuova giornata.
                      </Text>
                      <Text color="$color11" fontSize="$2">
                        1. Considera solo i giocatori attivi e verifica chi è indicato titolare da
                        Fantagazzetta o Gazzetta. Gli indisponibili e chi non è previsto titolare
                        non entrano nell&apos;undici, ma possono restare in panchina o in tribuna.
                      </Text>
                      <Text color="$color11" fontSize="$2">
                        2. Valuta media fantavoto, rendimento recente, voto atteso, stato di
                        disponibilità e forza della squadra.
                      </Text>
                      <Text color="$color11" fontSize="$2">
                        3. Considera la partita: casa o trasferta, avversario e tier dinamico della
                        classifica ufficiale di Serie A. Le prime 5 squadre sono T1, le successive 5
                        T2, poi T3 e T4. Affrontare in trasferta una squadra di tier superiore pesa
                        negativamente in modo significativo.
                      </Text>
                      <Text color="$color11" fontSize="$2">
                        4. Confronta i moduli validi e sceglie quello con il punteggio complessivo
                        migliore. Privilegia moduli offensivi come il 3-4-3, ma riduce il rischio se
                        un reparto non avrebbe almeno una riserva indicata come probabile titolare.
                      </Text>
                    </YStack>
                  </Card>
                )}
              </YStack>
            </Card>
          </YStack>
        )}
        {/* Username Section */}
        <Card
          padding="$4"
          backgroundColor="$color2"
          borderRadius="$4"
          borderWidth="$0.5"
          borderColor="$borderColor">
          <YStack gap="$4">
            <XStack alignItems="center" gap="$2">
              <User size="$1.5" color="$blue10" />
              <H3 color="$color12">Username</H3>
            </XStack>

            <Separator />

            <YStack gap="$3">
              <YStack gap="$2">
                <Text fontSize="$3" color="$color11">
                  Username attuale: <Text fontWeight="600">{displayUsername}</Text>
                </Text>
                <Text fontSize="$2" color="$color10">
                  Lo username deve essere tra 3 e 20 caratteri e può contenere solo lettere, numeri
                  e underscore (_).
                </Text>
              </YStack>

              <XStack gap="$2" flexWrap="wrap">
                <Input
                  flex={1}
                  minWidth={200}
                  placeholder="Nuovo username"
                  value={newUsername}
                  onChangeText={setNewUsername}
                  disabled={updatingUsername}
                  autoCapitalize="none"
                  autoCorrect={false}
                />
                <Button
                  icon={Check}
                  onPress={handleUpdateUsername}
                  disabled={updatingUsername || !newUsername.trim()}
                  theme="blue">
                  {updatingUsername ? 'Aggiornamento...' : 'Cambia Username'}
                </Button>
              </XStack>

              {updateUsernameError && (
                <XStack
                  padding="$2"
                  backgroundColor="$red3"
                  borderRadius="$2"
                  gap="$2"
                  alignItems="center">
                  <AlertTriangle size="$1" color="$red10" />
                  <Text color="$red11" fontSize="$2">
                    {updateUsernameError}
                  </Text>
                </XStack>
              )}

              {usernameSuccess && (
                <XStack
                  padding="$2"
                  backgroundColor="$green3"
                  borderRadius="$2"
                  gap="$2"
                  alignItems="center">
                  <Check size="$1" color="$green10" />
                  <Text color="$green11" fontSize="$2">
                    Username aggiornato con successo!
                  </Text>
                </XStack>
              )}
            </YStack>
          </YStack>
        </Card>
      </YStack>
    </ScreenContent>
  );
}
