import { Text, XStack, YStack } from 'tamagui'
import { LiveScore } from 'components/live/LiveScore'
import { RealCalendarHelper, type RealGame } from 'types/serieA'
import { formatItalianDate } from 'utils/timezoneHelpers'

export function matchStatusLabel(game: RealGame, playing: boolean) {
  if (game.delayed) return 'Rinviata'
  if (playing) return 'LIVE'
  return RealCalendarHelper.isGamePlayed(game) ? 'Finale' : 'Da giocare'
}

// One Serie A match: the score pulses (with a LIVE label) only while the match is actually being played.
export function SerieAMatchRow({ game }: { game: RealGame }) {
  const playing = RealCalendarHelper.isGamePlaying(game)
  const kickoff = game.date ? formatItalianDate(new Date(game.date).toISOString()) : 'Da definire'
  const hasScore = game.homeGoals != null && game.awayGoals != null
  const showScore = hasScore && (playing || RealCalendarHelper.isGamePlayed(game))
  const status = matchStatusLabel(game, playing)
  return (
    <XStack alignItems="center" justifyContent="space-between" gap="$3" paddingVertical="$1.5">
      <YStack flex={1}>
        <Text color="$color12" fontWeight="700" numberOfLines={1}>{game.home.name}</Text>
      </YStack>
      <YStack flexShrink={0} width={120} alignItems="center" gap="$1">
        <LiveScore active={playing}>
          <Text fontWeight="800" fontSize="$5" color={playing ? '$green10' : showScore ? '$color12' : '$color10'} textAlign="center">
            {showScore ? `${game.homeGoals} - ${game.awayGoals}` : 'vs'}
          </Text>
        </LiveScore>
        <Text fontSize="$3" color="$color10" textAlign="center">{kickoff}</Text>
        {!playing && (
          <Text fontSize="$2" fontWeight="600" color={game.delayed ? '$yellow10' : '$color10'} textAlign="center">{status}</Text>
        )}
      </YStack>
      <YStack flex={1} alignItems="flex-end">
        <Text color="$color12" fontWeight="700" numberOfLines={1}>{game.away.name}</Text>
      </YStack>
    </XStack>
  )
}
