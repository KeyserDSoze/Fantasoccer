import type { ReactNode } from 'react'
import { Text, XStack, YStack, useMedia } from 'tamagui'
import { SoccerBallSVG } from 'components/lineup/icons/SoccerBallSVG'
import { AssistSVG } from 'components/lineup/icons/AssistSVG'
import { BehaviourCardSVG } from 'components/lineup/icons/BehaviourCardSVG'
import { formatVote, playerEvents, sortByImportance, voteBand, type EventPlayer, type MatchEvent, type MatchEventKind } from 'utils/matchEvents'

const ICON_SIZE = 14

// Tints are translucent so the chip reads on both light and dark themes; the text always uses the theme colour.
const STYLES: Record<MatchEventKind, { tint: string; icon: () => ReactNode }> = {
  goal: { tint: '34,197,94', icon: () => <SoccerBallSVG primaryColor="#ffffff" secondaryColor="#111827" size={ICON_SIZE} /> },
  penalty: { tint: '59,130,246', icon: () => <SoccerBallSVG primaryColor="#111827" secondaryColor="#ffffff" size={ICON_SIZE} /> },
  assist: { tint: '168,85,247', icon: () => <AssistSVG color="#a855f7" size={ICON_SIZE} /> },
  stoppedPenalty: { tint: '14,165,233', icon: () => <SoccerBallSVG primaryColor="#0ea5e9" secondaryColor="#ffffff" size={ICON_SIZE} /> },
  redCard: { tint: '239,68,68', icon: () => <BehaviourCardSVG color="#ef4444" size={11} /> },
  wrongedPenalty: { tint: '249,115,22', icon: () => <SoccerBallSVG primaryColor="#f97316" secondaryColor="#ffffff" size={ICON_SIZE} /> },
  ownGoal: { tint: '245,158,11', icon: () => <SoccerBallSVG primaryColor="#fbbf24" secondaryColor="#ffffff" size={ICON_SIZE} /> },
  sufferedGoal: { tint: '239,68,68', icon: () => <SoccerBallSVG primaryColor="#ef4444" secondaryColor="#ffffff" size={ICON_SIZE} /> },
  yellowCard: { tint: '234,179,8', icon: () => <BehaviourCardSVG color="#facc15" size={11} /> },
  out: { tint: '148,163,184', icon: () => <Text fontSize="$2" color="$red10" accessible={false}>↓</Text> },
  in: { tint: '148,163,184', icon: () => <Text fontSize="$2" color="$green10" accessible={false}>↑</Text> },
}

export function EventBadge({ event }: { event: MatchEvent }) {
  const style = STYLES[event.kind]
  const text = event.count > 1 && event.kind !== 'in' && event.kind !== 'out' ? `${event.label} ×${event.count}` : event.label
  return (
    <XStack
      alignItems="center" gap="$1" paddingHorizontal="$1.5" paddingVertical={2} borderRadius="$2" borderWidth={1}
      backgroundColor={`rgba(${style.tint},0.16)` as any} borderColor={`rgba(${style.tint},0.5)` as any}
      accessibilityLabel={text}>
      {style.icon()}
      <Text fontSize="$1" fontWeight="600" color="$color12">{text}</Text>
    </XStack>
  )
}

export function VoteChip({ vote }: { vote: EventPlayer['vote'] }) {
  const band = voteBand(vote)
  return (
    <XStack
      minWidth={34} height={22} alignItems="center" justifyContent="center" paddingHorizontal={6} borderRadius="$2"
      backgroundColor={band.background as any} accessibilityLabel={vote.hasVote ? `Voto ${formatVote(vote)}, ${band.label}` : 'Senza voto'}>
      <Text fontSize="$2" fontWeight="800" color="white">{formatVote(vote)}</Text>
    </XStack>
  )
}

export function EventRow({ player, includeSubstitutions }: { player: EventPlayer; includeSubstitutions: boolean }) {
  const events = playerEvents(player.vote, includeSubstitutions)
  if (events.length === 0) return null
  return (
    <XStack alignItems="center" gap="$2" paddingVertical={3} paddingHorizontal="$1.5" borderRadius="$2" backgroundColor="$color2" flexWrap="wrap">
      <VoteChip vote={player.vote} />
      <Text fontSize="$3" fontWeight="700" color="$color12" flexShrink={1} numberOfLines={1}>{player.name}</Text>
      <XStack gap="$1" flexWrap="wrap" flex={1} justifyContent="flex-end">
        {events.map(event => <EventBadge key={event.kind} event={event} />)}
      </XStack>
    </XStack>
  )
}

type Side = { name: string; players: EventPlayer[] }

function Column({ side, includeSubstitutions }: { side: Side; includeSubstitutions: boolean }) {
  const ordered = sortByImportance(side.players, includeSubstitutions)
  return (
    <YStack flex={1} gap="$1" minWidth={0}>
      <Text fontSize="$2" fontWeight="800" color="$color11" textTransform="uppercase" numberOfLines={1}>{side.name}</Text>
      {ordered.length > 0
        ? ordered.map(player => <EventRow key={player.key} player={player} includeSubstitutions={includeSubstitutions} />)
        : <Text fontSize="$2" color="$color10">Nessun evento</Text>}
    </YStack>
  )
}

// Events of a real match, grouped by club. Ordered by importance; no minutes, because the feed carries none.
export function MatchEvents({ home, away, includeSubstitutions = false }: { home: Side; away: Side; includeSubstitutions?: boolean }) {
  const media = useMedia()
  const Container = media.gtSm ? XStack : YStack
  return (
    <Container gap="$3" alignItems="stretch">
      <Column side={home} includeSubstitutions={includeSubstitutions} />
      <Column side={away} includeSubstitutions={includeSubstitutions} />
    </Container>
  )
}
