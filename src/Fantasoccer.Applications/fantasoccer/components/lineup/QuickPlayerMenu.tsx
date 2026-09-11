import { useEffect, useMemo, useState } from 'react';
import { Dialog, Button, Text, XStack, YStack } from 'tamagui';
import type { LivePlayer } from 'types/game';
import { FantaSoccerRole } from 'types/team';
import { Role } from 'types/group';
import { getAvailableFieldPosition } from '../../utils/teamValidation';

type QuickMenuView = 'actions' | 'field' | 'bench';

interface QuickPlayerMenuProps {
  open: boolean;
  player: LivePlayer | null;
  allPlayers: LivePlayer[];
  anchor: { x: number; y: number };
  onClose: () => void;
  onMoveToPosition: (position: FantaSoccerRole) => void;
  onSwap: (targetPlayer: LivePlayer) => void;
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
}: QuickPlayerMenuProps) {
  const [view, setView] = useState<QuickMenuView>('actions');

  useEffect(() => {
    if (open) setView('actions');
  }, [open, player]);

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
    <Dialog modal open={open} onOpenChange={(nextOpen) => !nextOpen && onClose()}>
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
          width={460}
          maxWidth="94%"
          gap="$3"
          zIndex={100000}
          style={
            {
              position: 'fixed',
              left: anchor.x,
              top: anchor.y,
              transform:
                anchor.y > 240 ? 'translate(-50%, calc(-100% - 12px))' : 'translate(-50%, 12px)',
            } as any
          }>
          <XStack justifyContent="space-between" alignItems="center">
            <YStack gap="$1" flex={1}>
              <Dialog.Title fontSize="$5">Azioni rapide</Dialog.Title>
              <Text color="$color10">
                {playerName} • {ROLE_LABELS[playerRole as Role]} • {positionLabel(currentPosition)}
              </Text>
            </YStack>
            <Button size="$2" chromeless onPress={onClose}>
              Chiudi
            </Button>
          </XStack>

          {view === 'actions' && (
            <YStack gap="$2">
              <Text fontSize="$3" fontWeight="700">
                Scegli la destinazione
              </Text>
              {!isStarter(currentPosition) && (
                <Button onPress={() => setView('field')} backgroundColor="$green10" color="$white1">
                  Sposta tra i titolari
                </Button>
              )}
              <Button onPress={() => setView('bench')} backgroundColor="$orange10" color="$white1">
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
            </YStack>
          )}

          {view === 'field' && (
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

          {view === 'bench' && (
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
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog>
  );
}
