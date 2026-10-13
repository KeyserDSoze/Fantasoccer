import { Plus } from '@tamagui/lucide-icons-2';
import { Text, XStack, YStack } from 'tamagui';
import type { Chance } from '../../types/chance';
import { ChanceType } from '../../types/chance';

interface PlayerAvailabilityIndicatorsProps {
  chance?: Chance | null;
  direction?: 'row' | 'column';
}

const INDICATOR_SIZE = 18;

export function PlayerAvailabilityIndicators({
  chance,
  direction = 'row',
}: PlayerAvailabilityIndicatorsProps) {
  if (!chance) return null;

  if (chance.status === ChanceType.Injury) {
    return (
      <YStack
        width={INDICATOR_SIZE}
        height={INDICATOR_SIZE}
        borderRadius={4}
        backgroundColor="#dc2626"
        alignItems="center"
        justifyContent="center"
        accessibilityRole="image"
        accessibilityLabel={`Infortunato${chance.description ? `: ${chance.description}` : ''}`}>
        <Plus size={14} color="#fff" strokeWidth={3} />
      </YStack>
    );
  }

  const Stack = direction === 'column' ? YStack : XStack;

  return (
    <Stack gap={2} alignItems="center" flexShrink={0}>
      <YStack
        width={INDICATOR_SIZE}
        height={INDICATOR_SIZE}
        borderRadius={4}
        backgroundColor={chance.fantagazzetta ? '#16a34a' : '#ef4444'}
        alignItems="center"
        justifyContent="center"
        accessibilityRole="image"
        accessibilityLabel={`Fantagazzetta: ${chance.fantagazzetta ? 'titolare' : 'panchina'}`}>
        <Text fontSize={10} fontWeight="800" color="#fff">
          F
        </Text>
      </YStack>
      <YStack
        width={INDICATOR_SIZE}
        height={INDICATOR_SIZE}
        borderRadius={4}
        backgroundColor={chance.gazzetta ? '#16a34a' : '#ef4444'}
        alignItems="center"
        justifyContent="center"
        accessibilityRole="image"
        accessibilityLabel={`Gazzetta: ${chance.gazzetta ? 'titolare' : 'panchina'}`}>
        <Text fontSize={10} fontWeight="800" color="#fff">
          G
        </Text>
      </YStack>
    </Stack>
  );
}
