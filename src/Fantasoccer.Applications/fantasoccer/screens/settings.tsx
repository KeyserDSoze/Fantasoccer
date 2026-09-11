import { useCallback, useMemo, useState, useEffect } from 'react';
import {
  AlertTriangle,
  Check,
  Upload,
  User,
  Users,
  ChevronDown,
  ChevronUp,
  Copy,
  Sparkles,
} from '@tamagui/lucide-icons-2';
import {
  Button,
  Card,
  Dialog,
  H3,
  H4,
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
import { TeamLogo } from 'components/team/TeamLogo';
import { useApp } from 'providers/AppProvider';
import { useAuth } from 'providers/AuthProvider';
import { useSettings } from 'hooks/useSettings';
import { useGroup } from 'hooks/useGroup';
import { getActualFantacalcioYear } from 'utils/timezoneHelpers';
import * as ImagePicker from 'expo-image-picker';

export default function SettingsScreen() {
  const media = useMedia();
  const containerWidth = media.gtMd ? 800 : '100%';

  const { currentGroup } = useApp();
  const { user } = useAuth();
  const userEmail = user?.email ?? null;
  const groupId = currentGroup?.id ?? null;

  // Get current year
  const currentYear = getActualFantacalcioYear();

  // Get user's team info from the group
  const { group, loading: groupLoading, refetch: refetchGroup } = useGroup(groupId);

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
    uploadingLogo,
    uploadLogoError,
    uploadLogo,
    copyingLogo,
    copyLogoError,
    copyLogoFromPreviousYear,
    changingTeamName,
    changeTeamNameError,
    changeTeamNameMessages,
    changeTeamNameProgress,
    changeTeamName,
    updatingUsername,
    updateUsernameError,
    updateUsername,
    userSettings,
    loadingUserSettings,
    updatingUserSettings,
    updateAutoOptimalFormation,
  } = useSettings();

  // Local state for inputs
  const [newTeamName, setNewTeamName] = useState('');
  const [newUsername, setNewUsername] = useState('');
  const [teamNameSuccess, setTeamNameSuccess] = useState(false);
  const [usernameSuccess, setUsernameSuccess] = useState(false);
  const [logoSuccess, setLogoSuccess] = useState(false);
  const [copyLogoSuccess, setCopyLogoSuccess] = useState(false);
  const [messagesOpen, setMessagesOpen] = useState(false);
  const [optimalFormationInfoOpen, setOptimalFormationInfoOpen] = useState(false);
  const [showCopyLogoModal, setShowCopyLogoModal] = useState(false);

  // Local state for updated team name (to avoid full page refresh)
  const [updatedTeamName, setUpdatedTeamName] = useState<string | null>(null);

  // Local state for updated username (to avoid full page refresh)
  const [updatedUsername, setUpdatedUsername] = useState<string | null>(null);

  // Cache buster for team logo (timestamp when logo was last updated)
  const [logoCacheBuster, setLogoCacheBuster] = useState<number>(Date.now());

  // Display team name: use updated name if available, otherwise original
  const displayTeamName = updatedTeamName ?? userTeamInfo?.teamName ?? '';

  // Display username: use updated username if available, otherwise original
  const displayUsername = updatedUsername ?? user?.username ?? 'Non impostato';

  // Handle logo upload
  const handleLogoUpload = useCallback(async () => {
    if (!groupId || !userTeamInfo) return;

    try {
      // Request permission to access media library
      const { status } = await ImagePicker.requestMediaLibraryPermissionsAsync();

      if (status !== 'granted') {
        alert('Permesso negato per accedere alla galleria');
        return;
      }

      // Launch image picker
      const result = await ImagePicker.launchImageLibraryAsync({
        mediaTypes: ImagePicker.MediaTypeOptions.Images,
        allowsEditing: true,
        aspect: [1, 1],
        quality: 0.8,
      });

      if (result.canceled) return;

      const imageUri = result.assets[0].uri;

      // Convert image to blob for web, or use file for native
      let imageBlob: Blob | File;

      if (typeof window !== 'undefined' && 'fetch' in window) {
        // Web: fetch the image and convert to blob
        const response = await fetch(imageUri);
        imageBlob = await response.blob();
      } else {
        // React Native: create a file-like object
        const filename = imageUri.split('/').pop() || 'logo.png';
        imageBlob = {
          uri: imageUri,
          name: filename,
          type: 'image/png',
        } as any;
      }

      const success = await uploadLogo({
        groupId,
        year: userTeamInfo.year,
        owner: userTeamInfo.owner,
        basketId: userTeamInfo.basketId,
        image: imageBlob,
      });

      if (success) {
        // Wait a bit for blob storage to propagate, then update cache buster
        setTimeout(() => {
          setLogoCacheBuster(Date.now());
          setLogoSuccess(true);
          setTimeout(() => setLogoSuccess(false), 3000);
        }, 500);
      }
    } catch (error) {
      console.error('Error selecting/uploading logo:', error);
    }
  }, [groupId, userTeamInfo, uploadLogo]);

  // Handle copy logo from previous year - show preview modal
  const handleShowCopyLogoPreview = useCallback(() => {
    setShowCopyLogoModal(true);
  }, []);

  // Confirm and copy logo from previous year
  const handleConfirmCopyLogo = useCallback(async () => {
    if (!groupId || !userTeamInfo) return;

    try {
      const success = await copyLogoFromPreviousYear({
        groupId,
        year: userTeamInfo.year,
        owner: userTeamInfo.owner,
        basketId: userTeamInfo.basketId,
      });

      if (success) {
        // Close modal first
        setShowCopyLogoModal(false);

        // Wait a bit for blob storage to propagate, then update cache buster
        setTimeout(() => {
          setLogoCacheBuster(Date.now());
          setCopyLogoSuccess(true);
          setTimeout(() => setCopyLogoSuccess(false), 3000);
        }, 500);
      } else {
        // Close modal even on failure
        setShowCopyLogoModal(false);
      }
    } catch (error) {
      console.error('Error copying logo:', error);
      setShowCopyLogoModal(false);
    }
  }, [groupId, userTeamInfo, copyLogoFromPreviousYear]);

  // Handle team name change
  const handleChangeTeamName = useCallback(async () => {
    if (!groupId || !userTeamInfo || !newTeamName.trim()) return;

    const success = await changeTeamName({
      groupId,
      owner: userTeamInfo.owner,
      newName: newTeamName.trim(),
      basketId: userTeamInfo.basketId,
    });

    if (success) {
      // Update local state instead of refetching the entire group
      setUpdatedTeamName(newTeamName.trim());
      setTeamNameSuccess(true);
      setNewTeamName('');
      setTimeout(() => setTeamNameSuccess(false), 3000);
    }
  }, [groupId, userTeamInfo, newTeamName, changeTeamName]);

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

            {/* Team Logo Section */}
            <Card
              padding="$4"
              backgroundColor="$color2"
              borderRadius="$4"
              borderWidth="$0.5"
              borderColor="$borderColor">
              <YStack gap="$4">
                <XStack alignItems="center" gap="$2">
                  <Upload size="$1.5" color="$blue10" />
                  <H3 color="$color12">Logo Squadra</H3>
                </XStack>

                <Separator />

                <YStack gap="$3" alignItems="center">
                  {groupId && (
                    <TeamLogo
                      groupId={groupId}
                      year={userTeamInfo.year.toString()}
                      ownerEmail={userTeamInfo.owner}
                      size={media.gtSm ? 120 : 96}
                      cacheBuster={logoCacheBuster}
                    />
                  )}

                  <Text fontSize="$5" color="$color11" textAlign="center">
                    {displayTeamName}
                  </Text>

                  <Button
                    icon={Upload}
                    onPress={handleLogoUpload}
                    disabled={uploadingLogo}
                    theme="blue"
                    size="$4">
                    {uploadingLogo ? 'Caricamento...' : 'Carica Nuovo Logo'}
                  </Button>

                  <Button
                    icon={Copy}
                    onPress={handleShowCopyLogoPreview}
                    disabled={copyingLogo || uploadingLogo}
                    theme="purple"
                    size="$4">
                    Usa Logo Anno Precedente
                  </Button>

                  {uploadLogoError && (
                    <XStack
                      padding="$2"
                      backgroundColor="$red3"
                      borderRadius="$2"
                      gap="$2"
                      alignItems="center">
                      <AlertTriangle size="$1" color="$red10" />
                      <Text color="$red11" fontSize="$2">
                        {uploadLogoError}
                      </Text>
                    </XStack>
                  )}

                  {logoSuccess && (
                    <XStack
                      padding="$2"
                      backgroundColor="$green3"
                      borderRadius="$2"
                      gap="$2"
                      alignItems="center">
                      <Check size="$1" color="$green10" />
                      <Text color="$green11" fontSize="$2">
                        Logo caricato con successo!
                      </Text>
                    </XStack>
                  )}

                  {copyLogoError && (
                    <XStack
                      padding="$2"
                      backgroundColor="$red3"
                      borderRadius="$2"
                      gap="$2"
                      alignItems="center">
                      <AlertTriangle size="$1" color="$red10" />
                      <Text color="$red11" fontSize="$2">
                        {copyLogoError}
                      </Text>
                    </XStack>
                  )}

                  {copyLogoSuccess && (
                    <XStack
                      padding="$2"
                      backgroundColor="$green3"
                      borderRadius="$2"
                      gap="$2"
                      alignItems="center">
                      <Check size="$1" color="$green10" />
                      <Text color="$green11" fontSize="$2">
                        Logo copiato con successo!
                      </Text>
                    </XStack>
                  )}
                </YStack>
              </YStack>
            </Card>

            {/* Team Name Section */}
            <Card
              padding="$4"
              backgroundColor="$color2"
              borderRadius="$4"
              borderWidth="$0.5"
              borderColor="$borderColor">
              <YStack gap="$4">
                <XStack alignItems="center" gap="$2">
                  <Users size="$1.5" color="$blue10" />
                  <H3 color="$color12">Nome Squadra</H3>
                </XStack>

                <Separator />

                <YStack gap="$3">
                  <YStack gap="$2">
                    <Text fontSize="$3" color="$color11">
                      Nome attuale: <Text fontWeight="600">{displayTeamName}</Text>
                    </Text>
                    <Text fontSize="$2" color="$color10">
                      Il nome deve essere tra 3 e 30 caratteri e unico nel gruppo.
                    </Text>
                  </YStack>

                  <XStack gap="$2" flexWrap="wrap">
                    <Input
                      flex={1}
                      minWidth={200}
                      placeholder="Nuovo nome squadra"
                      value={newTeamName}
                      onChangeText={setNewTeamName}
                      disabled={changingTeamName}
                    />
                    <Button
                      icon={Check}
                      onPress={handleChangeTeamName}
                      disabled={changingTeamName || !newTeamName.trim()}
                      theme="blue">
                      {changingTeamName ? 'Aggiornamento...' : 'Cambia Nome'}
                    </Button>
                  </XStack>

                  {(changingTeamName || changeTeamNameMessages.length > 0) && (
                    <>
                      {/* Status bar: mostra l'ultimo evento e permette di aprire lo storico */}
                      <XStack
                        alignItems="center"
                        justifyContent="space-between"
                        paddingTop="$2"
                        marginTop="$2">
                        <XStack alignItems="center" gap="$2" flex={1}>
                          {changingTeamName ? (
                            <Spinner size="small" color="$blue10" />
                          ) : changeTeamNameError ? (
                            <AlertTriangle size="$1" color="$red10" />
                          ) : (
                            <Check size="$1" color="$green10" />
                          )}
                          <Text
                            color={
                              changeTeamNameError
                                ? '$red11'
                                : teamNameSuccess
                                  ? '$green11'
                                  : '$color11'
                            }
                            numberOfLines={1}
                            ellipsizeMode="tail"
                            fontWeight="500">
                            {changingTeamName
                              ? (changeTeamNameProgress ?? 'Avviando...')
                              : changeTeamNameMessages.length > 0
                                ? changeTeamNameMessages[changeTeamNameMessages.length - 1]
                                : ''}
                          </Text>
                        </XStack>

                        {changeTeamNameMessages.length > 1 && (
                          <Button
                            size="$3"
                            onPress={() => setMessagesOpen((s) => !s)}
                            icon={messagesOpen ? ChevronUp : ChevronDown}
                          />
                        )}
                      </XStack>

                      {messagesOpen && changeTeamNameMessages.length > 1 && (
                        <YStack
                          padding="$3"
                          backgroundColor="$blue3"
                          borderRadius="$3"
                          gap="$1"
                          marginTop="$2"
                          maxHeight={160}
                          overflow="scroll">
                          {/* Mostra solo i primi N-1 messaggi, l'ultimo è già visibile nel titolo sopra */}
                          {changeTeamNameMessages.slice(0, -1).map((msg, idx) => (
                            <Text key={idx} color="$blue11" fontSize="$2">
                              • {msg}
                            </Text>
                          ))}
                        </YStack>
                      )}
                    </>
                  )}
                </YStack>
              </YStack>
            </Card>

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
                      Lo username deve essere tra 3 e 20 caratteri e può contenere solo lettere,
                      numeri e underscore (_).
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
        )}
      </YStack>

      {/* Copy Logo Preview Modal */}
      <Dialog modal open={showCopyLogoModal} onOpenChange={setShowCopyLogoModal}>
        <Dialog.Portal>
          <Dialog.Overlay
            key="overlay"
            transition="quick"
            opacity={0.5}
            enterStyle={{ opacity: 0 }}
            exitStyle={{ opacity: 0 }}
          />
          <Dialog.Content
            bordered
            elevation="$2"
            key="content"
            animateOnly={['transform', 'opacity']}
            transition={[
              'quick',
              {
                opacity: {
                  overshootClamping: true,
                },
              },
            ]}
            enterStyle={{ x: 0, y: -20, opacity: 0, scale: 0.9 }}
            exitStyle={{ x: 0, y: 10, opacity: 0, scale: 0.95 }}
            gap="$4"
            maxWidth={500}>
            <Dialog.Title>Conferma Utilizzo Logo Anno Precedente</Dialog.Title>
            <Dialog.Description>
              Questo è il logo che verrà utilizzato per la tua squadra. Vuoi procedere?
            </Dialog.Description>

            {/* Logo Preview */}
            <YStack alignItems="center" padding="$4" gap="$3">
              {userTeamInfo && groupId && (
                <TeamLogo
                  groupId={groupId}
                  year={(userTeamInfo.year - 1).toString()}
                  ownerEmail={userTeamInfo.owner}
                  size={media.gtSm ? 150 : 120}
                />
              )}
              <Text fontSize="$3" color="$color11">
                Logo Anno {userTeamInfo ? userTeamInfo.year - 1 : ''}
              </Text>
            </YStack>

            <XStack gap="$3" justifyContent="flex-end">
              <Dialog.Close displayWhenAdapted asChild>
                <Button theme="gray" aria-label="Annulla">
                  Annulla
                </Button>
              </Dialog.Close>
              <Button
                theme="blue"
                onPress={handleConfirmCopyLogo}
                disabled={copyingLogo}
                icon={copyingLogo ? <Spinner /> : Copy}>
                {copyingLogo ? 'Copiando...' : 'Conferma'}
              </Button>
            </XStack>
          </Dialog.Content>
        </Dialog.Portal>
      </Dialog>
    </ScreenContent>
  );
}
