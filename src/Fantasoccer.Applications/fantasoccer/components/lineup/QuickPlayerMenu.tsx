import { useMemo, useRef, useState } from 'react';
import { useWindowDimensions } from 'react-native';
import { Dialog, Button, Text, XStack, YStack, Separator, ScrollView, Spinner } from 'tamagui';
import {
  RefreshCw,
  Eye,
  ArrowLeft,
  ChevronRight,
  X,
  Image as ImageIcon,
} from '@tamagui/lucide-icons-2';
import type { LivePlayer } from 'types/game';
import { FantaSoccerRole } from 'types/team';
import { Role } from 'types/group';
import { getAvailableFieldPosition } from '../../utils/teamValidation';

type QuickMenuView = 'actions' | 'field' | 'bench' | 'image';

interface QuickPlayerMenuProps {
  open: boolean;
  player: LivePlayer | null;
  allPlayers: LivePlayer[];
  anchor: { x: number; y: number };
  onClose: () => void;
  onMoveToPosition: (position: FantaSoccerRole) => void;
  onSwap: (targetPlayer: LivePlayer) => void;
  canEditFormation: boolean;
  onViewPlayer: () => void;
  onReloadImage?: () => Promise<void>;
}

const BENCH_POSITIONS: Record<Role, FantaSoccerRole[]> = {
  [Role.GoalKeeper]: [FantaSoccerRole.BackupGoalKeeper],
  [Role.Defensor]: [FantaSoccerRole.FirstBackupDefensor, FantaSoccerRole.SecondBackupDefensor],
  [Role.Midfielder]: [
    FantaSoccerRole.FirstBackupMidfielder,
    FantaSoccerRole.SecondBackupMidfielder,
  ],
  [Role.Forward]: [FantaSoccerRole.FirstBackupForward, FantaSoccerRole.SecondBackupForward],
  [Role.Undefined]: [],
};

const ROLE_LABELS: Record<Role, string> = {
  [Role.GoalKeeper]: 'Portiere',
  [Role.Defensor]: 'Difensore',
  [Role.Midfielder]: 'Centrocampista',
  [Role.Forward]: 'Attaccante',
  [Role.Undefined]: 'Giocatore',
};

const positionLabel = (position: FantaSoccerRole) => {
  switch (position) {
    case FantaSoccerRole.GoalKeeper:
      return 'Titolare';
    case FantaSoccerRole.Defensor:
      return 'Titolare';
    case FantaSoccerRole.Midfielder:
      return 'Titolare';
    case FantaSoccerRole.Forward:
      return 'Titolare';
    case FantaSoccerRole.BackupGoalKeeper:
      return 'Primo portiere di riserva';
    case FantaSoccerRole.FirstBackupDefensor:
    case FantaSoccerRole.FirstBackupMidfielder:
    case FantaSoccerRole.FirstBackupForward:
      return 'Primo panchinaro';
    case FantaSoccerRole.SecondBackupDefensor:
    case FantaSoccerRole.SecondBackupMidfielder:
    case FantaSoccerRole.SecondBackupForward:
      return 'Secondo panchinaro';
    case FantaSoccerRole.Tribune:
      return 'Tribuna';
    default:
      return 'Posizione';
  }
};

const isStarter = (position: FantaSoccerRole) =>
  position >= FantaSoccerRole.GoalKeeper && position <= FantaSoccerRole.Forward;

export function QuickPlayerMenu({
  open,
  player,
  allPlayers,
  anchor,
  onClose,
  onMoveToPosition,
  onSwap,
  canEditFormation,
  onViewPlayer,
  onReloadImage,
}: QuickPlayerMenuProps) {
  const [view, setView] = useState<QuickMenuView>('actions');
  const [reloading, setReloading] = useState(false);
  const [reloadError, setReloadError] = useState<string | null>(null);
  const [contentHeight, setContentHeight] = useState(480);
  const reloadInProgress = useRef(false);
  const { width, height } = useWindowDimensions();
  const menuWidth = Math.min(340, width - 24);
  const menuHeight = Math.min(480, height - 24);
  const left = Math.max(12, Math.min(anchor.x, width - menuWidth - 12));
  const top = Math.max(
    12,
    Math.min(anchor.y + 8, height - Math.min(contentHeight, menuHeight) - 12)
  );

  const handleReload = async () => {
    if (!onReloadImage || reloadInProgress.current) return;
    reloadInProgress.current = true;
    setReloading(true);
    setReloadError(null);
    try {
      await onReloadImage();
      onClose();
    } catch (error) {
      setReloadError(error instanceof Error ? error.message : 'Impossibile aggiornare la foto.');
    } finally {
      reloadInProgress.current = false;
      setReloading(false);
    }
  };

  const playerRole: Role = (player?.current as any)?.role ?? Role.Undefined;
  const occupiedByPosition = useMemo(
    () => new Map(allPlayers.map((candidate) => [candidate.currentPosition, candidate])),
    [allPlayers]
  );
  const fieldPlayers = allPlayers.filter(
    (candidate) =>
      candidate !== player &&
      isStarter(candidate.currentPosition) &&
      ((candidate.current as any)?.role ?? Role.Undefined) === playerRole
  );
  const availableFieldPosition = player
    ? getAvailableFieldPosition(playerRole, allPlayers)
    : undefined;
  const benchPositions = BENCH_POSITIONS[playerRole] ?? [];
  const playerName = (player?.current as any)?.name ?? 'Giocatore';
  const currentPosition: FantaSoccerRole = player?.currentPosition ?? FantaSoccerRole.Tribune;

  if (!player) return null;

  const renderTargetButton = (
    label: string,
    targetPlayer: LivePlayer | undefined,
    targetPosition: FantaSoccerRole,
    color: string
  ) => {
    const isCurrent = currentPosition === targetPosition;
    return (
      <Button
        key={`${targetPosition}-${targetPlayer?.current?.name ?? 'empty'}`}
        disabled={isCurrent}
        backgroundColor={isCurrent ? '$color3' : color}
        color={isCurrent ? '$color10' : '$white1'}
        justifyContent="space-between"
        onPress={() => {
          if (targetPlayer) onSwap(targetPlayer);
          else onMoveToPosition(targetPosition);
        }}>
        <Text color={isCurrent ? '$color10' : '$white1'}>
          {label}
          {targetPlayer ? `: ${targetPlayer.current.name}` : ' (slot libero)'}
        </Text>
        <Text color={isCurrent ? '$color10' : '$white1'} fontSize="$2">
          {isCurrent ? 'Posizione attuale' : targetPlayer ? 'Scambia' : 'Sposta'}
        </Text>
      </Button>
    );
  };

  return (
    <Dialog modal open={open} onOpenChange={(nextOpen) => !nextOpen && !reloading && onClose()}>
      <Dialog.Portal>
        <Dialog.Overlay
          key="quick-menu-overlay"
          backgroundColor="transparent"
          transition="quick"
          enterStyle={{ opacity: 0 }}
          exitStyle={{ opacity: 0 }}
        />
        <Dialog.Content
          key="quick-menu-content"
          bordered
          elevate
          width={menuWidth}
          maxHeight={menuHeight}
          padding="$3"
          gap="$2"
          borderRadius="$5"
          backgroundColor="$color2"
          onLayout={(event) => setContentHeight(event.nativeEvent.layout.height)}
          zIndex={100000}
          style={
            {
              position: 'fixed',
              left,
              top,
              transform: 'none',
            } as any
          }>
          <XStack justifyContent="space-between" alignItems="center">
            <YStack gap="$1" flex={1}>
              <Dialog.Title fontSize="$4" numberOfLines={1}>
                {playerName}
              </Dialog.Title>
              <Text color="$color10" fontSize="$2">
                {ROLE_LABELS[playerRole as Role]} • {positionLabel(currentPosition)}
              </Text>
            </YStack>
            <Button
              size="$2"
              chromeless
              icon={X}
              aria-label="Chiudi menu"
              disabled={reloading}
              onPress={onClose}
            />
          </XStack>
          <Dialog.Description fontSize="$2" color="$color10">
            {view === 'image'
              ? 'Aggiorna la foto ufficiale del giocatore'
              : 'Azioni sulla carta giocatore'}
          </Dialog.Description>
          <Separator />

          <ScrollView maxHeight={menuHeight - 110}>
            {view === 'actions' && (
              <YStack gap="$2">
                <Button
                  icon={RefreshCw}
                  iconAfter={ChevronRight}
                  justifyContent="flex-start"
                  backgroundColor="$blue3"
                  hoverStyle={{ backgroundColor: '$blue4' }}
                  color="$blue11"
                  disabled={!onReloadImage}
                  onPress={() => setView('image')}>
                  Forza reload immagine
                </Button>
                <Button icon={Eye} justifyContent="flex-start" chromeless onPress={onViewPlayer}>
                  Dettagli giocatore
                </Button>
                {canEditFormation && (
                  <>
                    <Separator marginVertical="$1" />
                    <Text fontSize="$2" color="$color10" fontWeight="700">
                      FORMAZIONE
                    </Text>
                    {!isStarter(currentPosition) && (
                      <Button
                        onPress={() => setView('field')}
                        backgroundColor="$green10"
                        color="$white1">
                        Sposta tra i titolari
                      </Button>
                    )}
                    <Button
                      onPress={() => setView('bench')}
                      backgroundColor="$orange10"
                      color="$white1">
                      Sposta in panchina
                    </Button>
                    {currentPosition !== FantaSoccerRole.Tribune ? (
                      <Button
                        onPress={() => onMoveToPosition(FantaSoccerRole.Tribune)}
                        backgroundColor="$gray8"
                        color="$white1">
                        Sposta in tribuna
                      </Button>
                    ) : null}
                  </>
                )}
              </YStack>
            )}

            {view === 'image' && (
              <YStack gap="$3">
                <Button
                  size="$2"
                  chromeless
                  icon={ArrowLeft}
                  justifyContent="flex-start"
                  disabled={reloading}
                  onPress={() => setView('actions')}>
                  Torna alle azioni
                </Button>
                <XStack gap="$2" alignItems="center">
                  <ImageIcon size={20} color="$blue10" />
                  <Text fontWeight="700">Foto ufficiale Lega Serie A</Text>
                </XStack>
                <Text fontSize="$3" color="$color11">
                  Riscarica la foto più recente disponibile e sostituisce quella salvata, anche se
                  già presente.
                </Text>
                <YStack backgroundColor="$blue2" borderRadius="$3" padding="$3" gap="$1">
                  <Text fontSize="$2" color="$blue11">
                    La foto è condivisa con tutti gli utenti.
                  </Text>
                  <Text fontSize="$2" color="$blue11">
                    Non modifica la formazione e non consuma cambi live.
                  </Text>
                </YStack>
                {reloadError && (
                  <Text color="$red11" fontSize="$3" role="alert">
                    {reloadError}
                  </Text>
                )}
                <Button
                  backgroundColor="$blue10"
                  color="$white1"
                  icon={reloading ? undefined : RefreshCw}
                  disabled={reloading}
                  onPress={() => void handleReload()}>
                  {reloading && <Spinner size="small" color="$white1" />}
                  {reloading
                    ? 'Aggiornamento foto…'
                    : reloadError
                      ? 'Riprova aggiornamento'
                      : 'Aggiorna foto'}
                </Button>
              </YStack>
            )}

            {view === 'field' && canEditFormation && (
              <YStack gap="$2">
                <Button size="$2" chromeless onPress={() => setView('actions')}>
                  ← Destinazioni
                </Button>
                <Text fontSize="$3" fontWeight="700">
                  Titolari {ROLE_LABELS[playerRole as Role]}
                </Text>
                {fieldPlayers.map((candidate) =>
                  renderTargetButton('Titolare', candidate, candidate.currentPosition, '$green9')
                )}
                {availableFieldPosition !== undefined &&
                  renderTargetButton('Titolare', undefined, availableFieldPosition, '$green10')}
                {fieldPlayers.length === 0 && availableFieldPosition === undefined && (
                  <Text color="$color10">Nessun titolare o slot libero disponibile.</Text>
                )}
              </YStack>
            )}

            {view === 'bench' && canEditFormation && (
              <YStack gap="$2">
                <Button size="$2" chromeless onPress={() => setView('actions')}>
                  ← Destinazioni
                </Button>
                <Text fontSize="$3" fontWeight="700">
                  Panchina {ROLE_LABELS[playerRole as Role]}
                </Text>
                {benchPositions.map((position) =>
                  renderTargetButton(
                    positionLabel(position),
                    occupiedByPosition.get(position),
                    position,
                    '$orange10'
                  )
                )}
                {benchPositions.length === 0 && (
                  <Text color="$color10">
                    Nessuna posizione di panchina disponibile per il ruolo.
                  </Text>
                )}
              </YStack>
            )}
          </ScrollView>
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog>
  );
}
