import { useCallback, useMemo, useState } from 'react';
import {
  AlertTriangle,
  Check,
  Copy,
  Image as ImageIcon,
  RefreshCw,
  Users,
  Upload,
} from '@tamagui/lucide-icons-2';
import {
  Button,
  Card,
  Dialog,
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
import * as ImagePicker from 'expo-image-picker';
import { ScreenContent } from 'components/layout/ScreenContent';
import { TeamLogo } from 'components/team/TeamLogo';
import { useApp } from 'providers/AppProvider';
import { useAuth } from 'providers/AuthProvider';
import { useGroup } from 'hooks/useGroup';
import { useTeamByComponents } from 'hooks/useTeam';
import { useSettings } from 'hooks/useSettings';
import { useMyTeams } from 'hooks/useMyTeams';
import { TeamSelector } from 'components/team/TeamSelector';
import {
  reloadTeamPlayerImages,
  type TeamPlayerImageRefreshSummary,
} from 'services/playerImageService';
import { getActualFantacalcioYear } from 'utils/timezoneHelpers';

export default function TeamSettingsScreen() {
  const media = useMedia();
  const { currentGroup } = useApp();
  const { user } = useAuth();
  const groupId = currentGroup?.id ?? null;
  const userEmail = user?.email?.toLowerCase() ?? null;
  const { selectedYear } = useApp();
  const currentYear = selectedYear ?? getActualFantacalcioYear();
  const { group, loading: groupLoading } = useGroup(groupId);
  // Several teams per user and group: always act on the selected one.
  const { teams: myTeams, selected, select } = useMyTeams(group, userEmail, currentYear, 'team-settings');
  const userTeamInfo = useMemo(() => selected ? {
    basketId: selected.basketId,
    teamId: selected.teamId,
    teamName: selected.name,
    owner: selected.owner,
    year: selected.year,
    isDerived: selected.isDerived,
  } : null, [selected]);

  const {
    team,
    loading: rosterLoading,
    getActivePlayers,
  } = useTeamByComponents({
    group: groupId,
    year: userTeamInfo?.year ?? null,
    basketId: userTeamInfo?.basketId ?? null,
    email: userTeamInfo?.owner ?? null,
  });
  const activePlayers = useMemo(() => getActivePlayers(), [getActivePlayers]);

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
  } = useSettings();

  const [newTeamName, setNewTeamName] = useState('');
  const [updatedTeamName, setUpdatedTeamName] = useState<string | null>(null);
  const [teamNameSuccess, setTeamNameSuccess] = useState(false);
  const [logoSuccess, setLogoSuccess] = useState(false);
  const [copyLogoSuccess, setCopyLogoSuccess] = useState(false);
  const [showCopyLogoModal, setShowCopyLogoModal] = useState(false);
  const [logoCacheBuster, setLogoCacheBuster] = useState(Date.now());
  const [refreshingImages, setRefreshingImages] = useState(false);
  const [showRefreshModal, setShowRefreshModal] = useState(false);
  const [refreshError, setRefreshError] = useState<string | null>(null);
  const [refreshSummary, setRefreshSummary] = useState<TeamPlayerImageRefreshSummary | null>(null);
  const [refreshCompletedAt, setRefreshCompletedAt] = useState<number | null>(null);

  const displayTeamName = updatedTeamName ?? userTeamInfo?.teamName ?? team?.name ?? '';
  const isLoading = groupLoading || (!!userTeamInfo && rosterLoading);
  const containerWidth = media.gtMd ? 800 : '100%';

  const handleLogoUpload = useCallback(async () => {
    if (!groupId || !userTeamInfo) return;
    try {
      const { status } = await ImagePicker.requestMediaLibraryPermissionsAsync();
      if (status !== 'granted') {
        alert('Permesso negato per accedere alla galleria');
        return;
      }

      const result = await ImagePicker.launchImageLibraryAsync({
        mediaTypes: ImagePicker.MediaTypeOptions.Images,
        allowsEditing: true,
        aspect: [1, 1],
        quality: 0.8,
      });
      if (result.canceled) return;

      const imageUri = result.assets[0].uri;
      let imageBlob: Blob | File;
      if (typeof window !== 'undefined' && 'fetch' in window) {
        imageBlob = await (await fetch(imageUri)).blob();
      } else {
        imageBlob = {
          uri: imageUri,
          name: imageUri.split('/').pop() || 'logo.png',
          type: 'image/png',
        } as any;
      }

      if (
        await uploadLogo({
          groupId,
          year: userTeamInfo.year,
          owner: userTeamInfo.owner,
          basketId: userTeamInfo.basketId,
          teamId: userTeamInfo.teamId,
          image: imageBlob,
        })
      ) {
        setTimeout(() => {
          setLogoCacheBuster(Date.now());
          setLogoSuccess(true);
          setTimeout(() => setLogoSuccess(false), 3000);
        }, 500);
      }
    } catch (error) {
      console.error('Errore nella selezione o nel caricamento del logo:', error);
    }
  }, [groupId, userTeamInfo, uploadLogo]);

  const handleConfirmCopyLogo = useCallback(async () => {
    if (!groupId || !userTeamInfo) return;
    const success = await copyLogoFromPreviousYear({
      groupId,
      year: userTeamInfo.year,
      owner: userTeamInfo.owner,
      basketId: userTeamInfo.basketId,
      teamId: userTeamInfo.teamId,
    });
    setShowCopyLogoModal(false);
    if (success) {
      setTimeout(() => {
        setLogoCacheBuster(Date.now());
        setCopyLogoSuccess(true);
        setTimeout(() => setCopyLogoSuccess(false), 3000);
      }, 500);
    }
  }, [groupId, userTeamInfo, copyLogoFromPreviousYear]);

  const handleChangeTeamName = useCallback(async () => {
    if (!groupId || !userTeamInfo || !newTeamName.trim()) return;
    const name = newTeamName.trim();
    if (
      await changeTeamName({
        groupId,
        owner: userTeamInfo.owner,
        newName: name,
        basketId: userTeamInfo.basketId,
        year: userTeamInfo.year,
        teamId: userTeamInfo.teamId,
      })
    ) {
      setUpdatedTeamName(name);
      setTeamNameSuccess(true);
      setNewTeamName('');
      setTimeout(() => setTeamNameSuccess(false), 3000);
    }
  }, [groupId, userTeamInfo, newTeamName, changeTeamName]);

  const handleRefreshImages = useCallback(async () => {
    if (!groupId || !userTeamInfo || refreshingImages) return;
    setRefreshingImages(true);
    setRefreshError(null);
    setRefreshSummary(null);
    try {
      const summary = await reloadTeamPlayerImages({
        groupId,
        year: userTeamInfo.year,
        teamId: team?.teamId ?? '',
      });
      setRefreshSummary(summary);
      setRefreshCompletedAt(Date.now());
    } catch (error) {
      setRefreshError(
        error instanceof Error ? error.message : 'Errore durante l’aggiornamento delle immagini.'
      );
    } finally {
      setRefreshingImages(false);
      setShowRefreshModal(false);
    }
  }, [groupId, userTeamInfo, team?.teamId, refreshingImages]);

  return (
    <ScreenContent path="screens/team-settings.tsx" title="Impostazioni squadra">
      <YStack
        width={containerWidth}
        maxWidth={800}
        gap="$4"
        alignItems="stretch"
        alignSelf="center"
        paddingBottom="$8">
        <YStack gap="$1">
          <H3 color="$color12">Gestione squadra</H3>
          <Text color="$color10">Logo, nome e immagini dei giocatori della tua squadra.</Text>
        </YStack>
        <TeamSelector teams={myTeams} value={userTeamInfo?.teamId ?? null} onChange={(teamId) => { select(teamId); setUpdatedTeamName(null); setNewTeamName(''); }} />
        {userTeamInfo?.isDerived && (
          <Paragraph size="$2" color="$color10">
            Squadra derivata: rosa, crediti, asta e mercato appartengono alla squadra originale. Qui puoi gestire nome e logo usati in questa competizione.
          </Paragraph>
        )}

        {isLoading ? (
          <Card
            padding="$4"
            backgroundColor="$color2"
            borderRadius="$4"
            borderWidth="$0.5"
            borderColor="$borderColor">
            <XStack alignItems="center" gap="$3" justifyContent="center">
              <Spinner size="small" color="$blue10" />
              <Text color="$color10">Caricamento squadra...</Text>
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
              Seleziona un gruppo dal menu per gestire la tua squadra.
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
          <>
            <Card
              padding="$4"
              backgroundColor="$color2"
              borderRadius="$4"
              borderWidth="$0.5"
              borderColor="$borderColor">
              <YStack gap="$4">
                <XStack alignItems="center" gap="$2">
                  <Upload size="$1.5" color="$blue10" />
                  <H3 color="$color12">Logo squadra</H3>
                </XStack>
                <Separator />
                <YStack gap="$3" alignItems="center">
                  {groupId && (
                    <TeamLogo
                      groupId={groupId}
                      year={userTeamInfo.year}
                      ownerEmail={userTeamInfo.owner}
                      teamId={userTeamInfo.teamId}
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
                    {uploadingLogo ? 'Caricamento...' : 'Carica nuovo logo'}
                  </Button>
                  <Button
                    icon={Copy}
                    onPress={() => setShowCopyLogoModal(true)}
                    disabled={copyingLogo || uploadingLogo}
                    theme="purple"
                    size="$4">
                    Usa logo anno precedente
                  </Button>
                  {uploadLogoError && <ErrorNotice message={uploadLogoError} />}
                  {logoSuccess && <SuccessNotice message="Logo caricato con successo!" />}
                  {copyLogoError && <ErrorNotice message={copyLogoError} />}
                  {copyLogoSuccess && <SuccessNotice message="Logo copiato con successo!" />}
                </YStack>
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
                  <Users size="$1.5" color="$blue10" />
                  <H3 color="$color12">Nome squadra</H3>
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
                      icon={changingTeamName ? <Spinner /> : Check}
                      onPress={handleChangeTeamName}
                      disabled={changingTeamName || !newTeamName.trim()}
                      theme="blue">
                      {changingTeamName ? 'Aggiornamento...' : 'Cambia nome'}
                    </Button>
                  </XStack>
                  {(changingTeamName || changeTeamNameError || teamNameSuccess) && (
                    <XStack alignItems="center" gap="$2">
                      {changingTeamName ? (
                        <Spinner size="small" color="$blue10" />
                      ) : changeTeamNameError ? (
                        <AlertTriangle size="$1" color="$red10" />
                      ) : (
                        <Check size="$1" color="$green10" />
                      )}
                      <Text
                        color={
                          changeTeamNameError ? '$red11' : teamNameSuccess ? '$green11' : '$color11'
                        }>
                        {changeTeamNameError ??
                          (teamNameSuccess ? 'Nome squadra aggiornato!' : changeTeamNameProgress)}
                      </Text>
                    </XStack>
                  )}
                  {changeTeamNameMessages.length > 0 && !teamNameSuccess && !changingTeamName && (
                    <YStack gap="$1">
                      {changeTeamNameMessages
                        .filter((message) => message.toLowerCase().startsWith('error:'))
                        .map((message, index) => (
                          <Text key={`${index}-${message}`} color="$red11" fontSize="$2">
                            {message.replace(/^error:\s*/i, '')}
                          </Text>
                        ))}
                    </YStack>
                  )}
                </YStack>
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
                  <ImageIcon size="$1.5" color="$blue10" />
                  <H3 color="$color12">Immagini giocatori</H3>
                </XStack>
                <Separator />
                <Text color="$color10">
                  Scarica e aggiorna le foto ufficiali di tutti i giocatori attivi della rosa (
                  {activePlayers.length}).
                </Text>
                <Button
                  icon={refreshingImages ? <Spinner /> : RefreshCw}
                  onPress={() => setShowRefreshModal(true)}
                  disabled={refreshingImages || rosterLoading || activePlayers.length === 0}
                  theme="blue"
                  size="$4">
                  {refreshingImages ? 'Aggiornamento in corso...' : 'Aggiorna tutte le immagini'}
                </Button>
                {refreshingImages && (
                  <XStack gap="$2" alignItems="center">
                    <Spinner size="small" color="$blue10" />
                    <Text color="$color10">
                      Sto aggiornando le foto ufficiali della rosa. Può richiedere qualche minuto.
                    </Text>
                  </XStack>
                )}
                {refreshError && <ErrorNotice message={refreshError} />}
                {refreshSummary && (
                  <YStack
                    gap="$2"
                    padding="$3"
                    borderRadius="$3"
                    backgroundColor={refreshSummary.failedCount === 0 ? '$green2' : '$yellow2'}>
                    <Text
                      fontWeight="700"
                      color={refreshSummary.failedCount === 0 ? '$green11' : '$yellow11'}>
                      {refreshSummary.failedCount === 0
                        ? 'Aggiornamento completato'
                        : 'Aggiornamento completato parzialmente'}
                    </Text>
                    <Text color="$color12">
                      {refreshSummary.succeededCount} aggiornate · {refreshSummary.failedCount} non
                      riuscite su {refreshSummary.total}.
                    </Text>
                    {refreshSummary.failed.length > 0 && (
                      <YStack gap="$1" paddingTop="$2" borderTopWidth={1} borderColor="$yellow7">
                        <Text fontWeight="600" color="$red11">
                          Foto non aggiornate:
                        </Text>
                        {refreshSummary.failed.map((failure) => (
                          <Text key={failure.playerKey} color="$red11" fontSize="$2">
                            • {failure.playerName}
                            {failure.error ? ` — ${failure.error}` : ''}
                          </Text>
                        ))}
                      </YStack>
                    )}
                    {refreshCompletedAt !== null && (
                      <Text color="$color10" fontSize="$1">
                        Terminato alle{' '}
                        {new Date(refreshCompletedAt).toLocaleTimeString('it-IT', {
                          hour: '2-digit',
                          minute: '2-digit',
                        })}
                      </Text>
                    )}
                  </YStack>
                )}
              </YStack>
            </Card>
          </>
        )}
      </YStack>

      <Dialog modal open={showCopyLogoModal} onOpenChange={setShowCopyLogoModal}>
        <Dialog.Portal>
          <Dialog.Overlay
            key="team-logo-copy-overlay"
            transition="quick"
            opacity={0.5}
            enterStyle={{ opacity: 0 }}
            exitStyle={{ opacity: 0 }}
          />
          <Dialog.Content
            bordered
            elevation="$2"
            key="team-logo-copy-content"
            gap="$4"
            maxWidth={500}>
            <Dialog.Title>Conferma utilizzo logo anno precedente</Dialog.Title>
            <Dialog.Description>
              Questo è il logo che verrà utilizzato per la tua squadra. Vuoi procedere?
            </Dialog.Description>
            <YStack alignItems="center" padding="$4" gap="$3">
              {userTeamInfo && groupId && (
                <TeamLogo
                  groupId={groupId}
                  year={userTeamInfo.year - 1}
                  ownerEmail={userTeamInfo.owner}
                  size={media.gtSm ? 150 : 120}
                />
              )}
              <Text fontSize="$3" color="$color11">
                Logo anno {userTeamInfo ? userTeamInfo.year - 1 : ''}
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

      <Dialog modal open={showRefreshModal} onOpenChange={setShowRefreshModal}>
        <Dialog.Portal>
          <Dialog.Overlay
            key="team-image-refresh-overlay"
            transition="quick"
            opacity={0.5}
            enterStyle={{ opacity: 0 }}
            exitStyle={{ opacity: 0 }}
          />
          <Dialog.Content
            bordered
            elevation="$2"
            key="team-image-refresh-content"
            gap="$4"
            maxWidth={500}>
            <Dialog.Title>Aggiornare tutte le immagini?</Dialog.Title>
            <Dialog.Description>
              Verranno aggiornate le foto ufficiali dei {activePlayers.length} giocatori attivi
              della rosa. L’operazione può durare alcuni minuti.
            </Dialog.Description>
            <XStack gap="$3" justifyContent="flex-end">
              <Dialog.Close displayWhenAdapted asChild>
                <Button theme="gray" disabled={refreshingImages}>
                  Annulla
                </Button>
              </Dialog.Close>
              <Button
                theme="blue"
                onPress={() => void handleRefreshImages()}
                disabled={refreshingImages}
                icon={refreshingImages ? <Spinner /> : RefreshCw}>
                {refreshingImages ? 'Aggiornamento...' : 'Avvia aggiornamento'}
              </Button>
            </XStack>
          </Dialog.Content>
        </Dialog.Portal>
      </Dialog>
    </ScreenContent>
  );
}

function ErrorNotice({ message }: { message: string }) {
  return (
    <XStack padding="$2" backgroundColor="$red3" borderRadius="$2" gap="$2" alignItems="center">
      <AlertTriangle size="$1" color="$red10" />
      <Text color="$red11" fontSize="$2">
        {message}
      </Text>
    </XStack>
  );
}

function SuccessNotice({ message }: { message: string }) {
  return (
    <XStack padding="$2" backgroundColor="$green3" borderRadius="$2" gap="$2" alignItems="center">
      <Check size="$1" color="$green10" />
      <Text color="$green11" fontSize="$2">
        {message}
      </Text>
    </XStack>
  );
}
