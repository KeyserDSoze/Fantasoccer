import { useMemo, useState } from 'react';
import { Button, Card, Text, XStack, YStack } from 'tamagui';
import type { LivePlayer, LiveTeam } from 'types/game';
import { FantaSoccerRole } from 'types/team';
import { Role } from 'types/group';
import { PlayerAvailabilityIndicators } from './PlayerAvailabilityIndicators';

export type QuickFormationStatus = 'starter' | 'firstBench' | 'secondBench' | 'tribune';

interface MobileQuickFormationViewProps {
  team: LiveTeam;
  hasLocalChanges: boolean;
  isSaving: boolean;
  onMove: (player: LivePlayer, status: QuickFormationStatus) => void;
  onSave: () => void;
  onClose: () => void;
}

const ROLE_ORDER: Role[] = [Role.GoalKeeper, Role.Defensor, Role.Midfielder, Role.Forward];
const STATUS_ORDER: QuickFormationStatus[] = ['starter', 'firstBench', 'secondBench', 'tribune'];
const STATUS_LABELS: Record<QuickFormationStatus, string> = {
  starter: 'T',
  firstBench: '1',
  secondBench: '2',
  tribune: 'F',
};
const STATUS_COLORS: Record<QuickFormationStatus, string> = {
  starter: '#166534',
  firstBench: '#b45309',
  secondBench: '#1d4ed8',
  tribune: '#4b5563',
};
const STATUS_BACKGROUNDS: Record<QuickFormationStatus, string> = {
  starter: 'rgba(34, 197, 94, 0.2)',
  firstBench: 'rgba(245, 158, 11, 0.2)',
  secondBench: 'rgba(59, 130, 246, 0.2)',
  tribune: 'rgba(107, 114, 128, 0.2)',
};

const getRoleOrder = (player: LivePlayer) => {
  const role = (player.current as any)?.role ?? Role.Undefined;
  const index = ROLE_ORDER.indexOf(role);
  return index === -1 ? ROLE_ORDER.length : index;
};

const getStatus = (position: FantaSoccerRole): QuickFormationStatus => {
  switch (position) {
    case FantaSoccerRole.GoalKeeper:
    case FantaSoccerRole.Defensor:
    case FantaSoccerRole.Midfielder:
    case FantaSoccerRole.Forward:
      return 'starter';
    case FantaSoccerRole.BackupGoalKeeper:
    case FantaSoccerRole.FirstBackupDefensor:
    case FantaSoccerRole.FirstBackupMidfielder:
    case FantaSoccerRole.FirstBackupForward:
      return 'firstBench';
    case FantaSoccerRole.SecondBackupDefensor:
    case FantaSoccerRole.SecondBackupMidfielder:
    case FantaSoccerRole.SecondBackupForward:
      return 'secondBench';
    default:
      return 'tribune';
  }
};

const getTargetStatus = (role: Role, status: QuickFormationStatus): FantaSoccerRole | undefined => {
  if (status === 'tribune') return FantaSoccerRole.Tribune;
  if (role === Role.GoalKeeper)
    return status === 'starter'
      ? FantaSoccerRole.GoalKeeper
      : status === 'firstBench'
        ? FantaSoccerRole.BackupGoalKeeper
        : undefined;
  if (role === Role.Defensor) {
    if (status === 'starter') return FantaSoccerRole.Defensor;
    if (status === 'firstBench') return FantaSoccerRole.FirstBackupDefensor;
    return FantaSoccerRole.SecondBackupDefensor;
  }
  if (role === Role.Midfielder) {
    if (status === 'starter') return FantaSoccerRole.Midfielder;
    if (status === 'firstBench') return FantaSoccerRole.FirstBackupMidfielder;
    return FantaSoccerRole.SecondBackupMidfielder;
  }
  if (role === Role.Forward) {
    if (status === 'starter') return FantaSoccerRole.Forward;
    if (status === 'firstBench') return FantaSoccerRole.FirstBackupForward;
    return FantaSoccerRole.SecondBackupForward;
  }
  return undefined;
};

const sortPlayers = (players: LivePlayer[]) =>
  [...players].sort((a, b) => {
    const roleOrder = getRoleOrder(a) - getRoleOrder(b);
    if (roleOrder !== 0) return roleOrder;
    const statusOrder =
      STATUS_ORDER.indexOf(getStatus(a.currentPosition)) -
      STATUS_ORDER.indexOf(getStatus(b.currentPosition));
    if (statusOrder !== 0) return statusOrder;
    return a.current.name.localeCompare(b.current.name, 'it-IT', { sensitivity: 'base' });
  });

export function MobileQuickFormationView({
  team,
  hasLocalChanges,
  isSaving,
  onMove,
  onSave,
  onClose,
}: MobileQuickFormationViewProps) {
  const sortedPlayers = useMemo(() => sortPlayers(team.players), [team.players]);
  const [rowOrder] = useState(() => sortedPlayers.map((player) => player.current.name));

  const playersByName = new Map(team.players.map((player) => [player.current.name, player]));
  const rows = rowOrder.map((name) => playersByName.get(name)).filter(Boolean) as LivePlayer[];

  return (
    <Card
      padding="$3"
      gap="$3"
      backgroundColor="$color2"
      borderRadius="$4"
      borderWidth={1}
      borderColor="$borderColor">
      <XStack alignItems="center" justifyContent="space-between" gap="$2">
        <YStack flex={1}>
          <Text fontSize="$5" fontWeight="700">
            Vista rapida
          </Text>
          <Text fontSize="$2" color="$color10">
            {team.name}
          </Text>
        </YStack>
        <Button size="$3" onPress={onClose}>
          Chiudi
        </Button>
      </XStack>
      <Text fontSize={10} color="$color10">
        F = Fantagazzetta · G = Gazzetta · verde = titolare · rosso = panchina · + rosso =
        infortunato
      </Text>
      <YStack width="100%" gap={1}>
        <XStack alignItems="stretch" gap={1}>
          <Text flex={1} minWidth={0} padding="$2" fontWeight="700" backgroundColor="$color4">
            Nome
          </Text>
          {STATUS_ORDER.map((status) => (
            <Text
              key={status}
              width={42}
              padding="$2"
              textAlign="center"
              fontSize="$2"
              fontWeight="700"
              color="#fff"
              backgroundColor={STATUS_COLORS[status]}>
              {STATUS_LABELS[status]}
            </Text>
          ))}
        </XStack>
        {rows.map((player) => {
          const role = (player.current as any)?.role ?? Role.Undefined;
          const currentStatus = getStatus(player.currentPosition);
          return (
            <XStack
              key={player.current.name}
              alignItems="stretch"
              gap={1}
              backgroundColor={STATUS_BACKGROUNDS[currentStatus]}>
              <XStack flex={1} minWidth={0} paddingHorizontal="$2" alignItems="center" gap="$1">
                <Text flex={1} minWidth={0} paddingVertical="$2" numberOfLines={1} fontSize="$2">
                  {player.current.name}
                </Text>
                <PlayerAvailabilityIndicators chance={player.chance} />
              </XStack>
              {STATUS_ORDER.map((status) => {
                const target = getTargetStatus(role, status);
                return (
                  <YStack
                    key={status}
                    width={42}
                    padding="$1"
                    backgroundColor={STATUS_BACKGROUNDS[status]}>
                    <Button
                      size="$2"
                      paddingHorizontal={0}
                      backgroundColor={currentStatus === status ? STATUS_COLORS[status] : '$color3'}
                      color={currentStatus === status ? '#fff' : '$color11'}
                      borderWidth={currentStatus === status ? 2 : 0}
                      borderColor="#fff"
                      disabled={target === undefined || isSaving}
                      onPress={() => target !== undefined && onMove(player, status)}>
                      {STATUS_LABELS[status]}
                    </Button>
                  </YStack>
                );
              })}
            </XStack>
          );
        })}
      </YStack>
      <Button
        backgroundColor="$green10"
        color="#fff"
        fontWeight="700"
        disabled={!hasLocalChanges || isSaving}
        onPress={onSave}>
        {isSaving ? 'Salvataggio...' : 'Salva formazione'}
      </Button>
    </Card>
  );
}
