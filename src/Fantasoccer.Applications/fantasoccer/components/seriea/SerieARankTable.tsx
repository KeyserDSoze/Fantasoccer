import { Text, XStack, YStack } from 'tamagui'
import type { RealRankedTeam } from 'types/serieA'

export function SerieARankTable({ teams: rankTeams }: { teams: RealRankedTeam[] }) {
  return (
    <YStack gap="$2">
      <XStack justifyContent="space-between" alignItems="center" paddingHorizontal="$2">
        <Text fontSize="$3" fontWeight="700" color="$color10" flex={1}>
          Squadra
        </Text>
        <XStack gap="$3" alignItems="center">
          <Text fontSize="$3" fontWeight="700" color="$color10" width={36} textAlign="center">
            G
          </Text>
          <Text fontSize="$3" fontWeight="700" color="$color10" width={36} textAlign="center">
            Pt
          </Text>
          <Text fontSize="$3" fontWeight="700" color="$color10" width={48} textAlign="center">
            Diff
          </Text>
        </XStack>
      </XStack>

      <YStack gap="$1">
        {rankTeams.map((team, index) => (
          <XStack
            key={`${team.name}-${index}`}
            justifyContent="space-between"
            alignItems="center"
            paddingVertical="$1.5"
            paddingHorizontal="$2"
            backgroundColor={index % 2 === 0 ? '$color1' : 'transparent'}
            borderRadius="$2"
          >
            <XStack alignItems="center" gap="$2" flex={1}>
              <Text fontSize="$3" fontWeight="700" color="$color9" width={24} textAlign="center">
                {index + 1}
              </Text>
              <Text fontSize="$3" fontWeight="700" color="$color12" flex={1} numberOfLines={1}>
                {team.name}
              </Text>
            </XStack>

            <XStack gap="$3" alignItems="center">
              <Text fontSize="$3" color="$color10" width={36} textAlign="center">
                {team.played}
              </Text>
              <Text fontSize="$3" fontWeight="700" color="$color12" width={36} textAlign="center">
                {team.points}
              </Text>
              <Text fontSize="$3" color={team.goalDifference >= 0 ? '$color10' : '$red10'} width={48} textAlign="center">
                {team.goalDifference >= 0 ? `+${team.goalDifference}` : team.goalDifference}
              </Text>
            </XStack>
          </XStack>
        ))}
      </YStack>
    </YStack>
  )
}
