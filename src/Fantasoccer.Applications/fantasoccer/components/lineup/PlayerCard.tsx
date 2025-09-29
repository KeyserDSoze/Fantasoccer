import { ReactElement, useMemo, useState } from 'react'
import {
  Activity,
  AlertTriangle,
  ArrowDownCircle,
  ArrowUpCircle,
  Award,
  CircleSlash,
  HeartPulse,
  Home,
  Newspaper,
  Plane,
  Shield,
  Skull,
  Star,
  Target,
  TrendingDown,
  TrendingUp,
  Trophy,
  Umbrella,
} from '@tamagui/lucide-icons'
import { Card, Image, Paragraph, Text, XStack, YStack, useMedia } from 'tamagui'
import type { IconProps } from '@tamagui/helpers-icon'

import type {
  LineupPlayerInfo,
  PlayerAvailabilityStatus,
  PlayerEventIndicator,
  PlayerEventType,
} from 'types/lineup'
import { getPlayerImageSources } from 'utils/playerImage'

export type PlayerCardVariant = 'starter' | 'bench' | 'tribune'

export interface PlayerCardProps {
  player: LineupPlayerInfo
  variant?: PlayerCardVariant
  showTeamName?: boolean
  showRole?: boolean
  showFantasyRole?: boolean
}

interface EventIconDescriptor {
  icon: (props: IconProps) => ReactElement
  background: string
  color: string
  label: string
}

const EVENT_ICON_MAP: Record<PlayerEventType, EventIconDescriptor> = {
  goal: {
    icon: Trophy,
    background: '$green4',
    color: '$green11',
    label: 'Gol',
  },
  assist: {
    icon: Target,
    background: '$blue4',
    color: '$blue11',
    label: 'Assist',
  },
  penaltyScored: {
    icon: Award,
    background: '$purple4',
    color: '$purple11',
    label: 'Rigore',
  },
  penaltySaved: {
    icon: Shield,
    background: '$teal4',
    color: '$teal11',
    label: 'Rig. Parato',
  },
  penaltyMissed: {
    icon: ArrowDownCircle,
    background: '$orange4',
    color: '$orange11',
    label: 'Rig. Sbagliato',
  },
  yellowCard: {
    icon: AlertTriangle,
    background: '$yellow4',
    color: '$yellow11',
    label: 'Giallo',
  },
  redCard: {
    icon: Skull,
    background: '$red4',
    color: '$red11',
    label: 'Rosso',
  },
  ownGoal: {
    icon: Umbrella,
    background: '$orange4',
    color: '$orange11',
    label: 'Autogol',
  },
  cleanSheet: {
    icon: Shield,
    background: '$green4',
    color: '$green11',
    label: 'Porta inviolata',
  },
  substitutionIn: {
    icon: ArrowUpCircle,
    background: '$blue4',
    color: '$blue11',
    label: 'Entrato',
  },
  substitutionOut: {
    icon: ArrowDownCircle,
    background: '$gray4',
    color: '$gray11',
    label: 'Uscito',
  },
  injury: {
    icon: Skull,
    background: '$red4',
    color: '$red11',
    label: 'Infortunio',
  },
  manOfTheMatch: {
    icon: Star,
    background: '$yellow4',
    color: '$yellow11',
    label: 'MVP',
  },
  other: {
    icon: Star,
    background: '$gray3',
    color: '$gray11',
    label: 'Evento',
  },
}

const VARIANT_STYLE: Record<PlayerCardVariant, { imageSize: number; padding: string; gap: string }> = {
  starter: { imageSize: 120, padding: '$4', gap: '$3' },
  bench: { imageSize: 88, padding: '$3', gap: '$2' },
  tribune: { imageSize: 76, padding: '$3', gap: '$2' },
}

const AVAILABILITY_STATUS_STYLES: Record<
  PlayerAvailabilityStatus,
  { label: string; background: string; color: string }
> = {
  starter: { label: 'Titolare', background: '$green4', color: '$green11' },
  probable: { label: 'Probabile', background: '$lime4', color: '$lime11' },
  doubtful: { label: 'In dubbio', background: '$yellow4', color: '$yellow11' },
  out: { label: 'Fuori', background: '$red4', color: '$red11' },
  bench: { label: 'Panchina', background: '$blue4', color: '$blue11' },
  unknown: { label: 'Da valutare', background: '$gray4', color: '$gray11' },
}

const getAvailabilityStatusStyle = (status?: PlayerAvailabilityStatus | null) => {
  if (!status) {
    return null
  }

  return AVAILABILITY_STATUS_STYLES[status] ?? AVAILABILITY_STATUS_STYLES.unknown
}

const formatSignedMetric = (value: number, precision = 1): string => {
  const formatted = value.toFixed(precision)
  return value > 0 ? `+${formatted}` : formatted
}

const formatProbability = (value?: number | null): string | null => {
  if (value == null) {
    return null
  }

  if (!Number.isFinite(value)) {
    return null
  }

  const normalized = value > 1 ? value : value * 100
  return `${Math.round(normalized)}%`
}

const getMetricColors = (value: number) => {
  if (value > 0) {
    return { background: '$green4', color: '$green11' }
  }
  if (value < 0) {
    return { background: '$red4', color: '$red11' }
  }
  return { background: '$gray4', color: '$gray11' }
}

const formatScore = (goalsFor?: number | null, goalsAgainst?: number | null): string | null => {
  if (goalsFor == null || goalsAgainst == null) {
    return null
  }
  return `${goalsFor} - ${goalsAgainst}`
}

const mapEventsToIndicators = (events?: PlayerEventIndicator[]): PlayerEventIndicator[] => {
  if (!events) {
    return []
  }

  return events.filter((event) => Boolean(EVENT_ICON_MAP[event.type]))
}

export function PlayerCard({
  player,
  variant = 'starter',
  showTeamName = true,
  showRole = true,
  showFantasyRole = false,
}: PlayerCardProps) {
  const { match } = player
  const media = useMedia()
  const [useFallbackImage, setUseFallbackImage] = useState(false)

  const { src, fallback } = useMemo(
    () =>
      getPlayerImageSources({
        key: player.imageKey ?? player.key ?? undefined,
        name: player.name,
      }),
    [player.imageKey, player.key, player.name]
  )

  const imageUri = useFallbackImage ? fallback : src
  const variantStyle = VARIANT_STYLE[variant]
  const scoreLabel = formatScore(match?.result?.goalsFor, match?.result?.goalsAgainst)
  const showScore = Boolean(scoreLabel)
  const opponentLabel = match ? `${match.isHome ? 'vs' : '@'} ${match.opponent}` : null
  const matchStatusLabel = match?.status === 'live' ? 'LIVE' : match?.status === 'finished' ? 'Finale' : match?.kickoff
  const events = useMemo(() => mapEventsToIndicators(player.events), [player.events])

  const availability = player.availability ?? null
  const availabilityStatusStyle = getAvailabilityStatusStyle(availability?.status ?? null)
  const availabilitySources = availability?.sources ?? []
  const availabilityDescription = availability?.description ?? availability?.statusNote ?? null
  const lastGamePositiveness = availability?.lastGamePositiveness ?? null
  const trendScore = availability?.trend ?? null

  const participation = player.participation ?? null

  const HomeAwayIcon = match?.isHome ? Home : Plane
  const isUpcomingMatch = match?.status === 'scheduled'
  const isLiveMatch = match?.status === 'live'
  const isFinishedMatch = match?.status === 'finished'
  const isLiveOrFinished = Boolean(isLiveMatch || isFinishedMatch)

  const voteValue = player.vote
  const voteLabel = voteValue != null ? voteValue.toFixed(1) : null
  const shouldShowVote = Boolean(isLiveOrFinished && voteLabel != null)
  const isPositiveVote = (voteValue ?? 0) >= 6

  const didNotPlay = Boolean(isLiveOrFinished && participation?.played === false)
  const isAwaitingVote = Boolean(isLiveOrFinished && !didNotPlay && !shouldShowVote)
  const availabilitySourcesCount = availabilitySources.length
  const shouldShowAvailabilityDetails = Boolean(
    isUpcomingMatch &&
      (availabilitySourcesCount > 0 || availabilityDescription || lastGamePositiveness != null || trendScore != null)
  )

  const showCaptainBadge = player.isCaptain
  const jerseyNumberLabel = player.jerseyNumber != null ? `#${player.jerseyNumber}` : null

  const playerStatusLabel = (() => {
    switch (player.status) {
      case 'bench':
        return 'Panchina'
      case 'tribune':
        return 'Tribuna'
      case 'out':
        return 'Fuori lista'
      case 'injured':
        return 'Infortunato'
      default:
        return null
    }
  })()

  return (
    <Card
      elevate={variant === 'starter'}
      backgroundColor="$color2"
      borderRadius="$4"
      borderWidth={1}
      borderColor="$borderColor"
      padding={variantStyle.padding}
      width="100%"
      maxWidth={variant === 'starter' && media.gtMd ? 320 : 280}
      gap={variantStyle.gap}
    >
      <XStack justifyContent="space-between" alignItems="center" gap="$2">
        <YStack gap={showRole || showTeamName ? '$1' : '$0'}>
          <Text fontSize="$4" fontWeight="700" color="$color12" numberOfLines={1}>
            {player.name}
          </Text>
          <XStack gap="$2" alignItems="center">
            {showRole && (
              <Text fontSize="$2" color="$color10">
                {player.roleLabel}
              </Text>
            )}
            {showFantasyRole && player.fantasyRoleLabel && (
              <Text fontSize="$2" color="$color9">
                {player.fantasyRoleLabel}
              </Text>
            )}
            {showTeamName && (
              <Text fontSize="$2" color="$color9">
                {player.teamName}
              </Text>
            )}
          </XStack>
        </YStack>
        <YStack alignItems="flex-end" gap="$1">
          {availabilityStatusStyle && availability?.status !== 'unknown' && (
            <XStack
              paddingHorizontal="$2"
              paddingVertical="$1"
              borderRadius="$3"
              backgroundColor={availabilityStatusStyle.background}
              alignItems="center"
              gap="$1"
            >
              <Newspaper size="$1" color={availabilityStatusStyle.color} />
              <Text fontSize="$2" color={availabilityStatusStyle.color} fontWeight="600">
                {availabilityStatusStyle.label}
              </Text>
            </XStack>
          )}
          {playerStatusLabel ? (
            <Text fontSize="$2" color="$red10" fontWeight="600">
              {playerStatusLabel}
            </Text>
          ) : null}
          {jerseyNumberLabel && (
            <Text fontSize="$2" color="$color10">
              {jerseyNumberLabel}
            </Text>
          )}
        </YStack>
      </XStack>

      <XStack gap="$3" alignItems="center" justifyContent="space-between">
        <YStack alignItems="center" gap="$2">
          <YStack position="relative">
            <Image
              source={{ uri: imageUri }}
              width={variantStyle.imageSize}
              height={variantStyle.imageSize}
              borderRadius={variantStyle.imageSize / 2}
              resizeMode="cover"
              onError={() => setUseFallbackImage(true)}
            />
            {showCaptainBadge && (
              <XStack
                position="absolute"
                bottom={8}
                left="50%"
                marginLeft={-20}
                paddingHorizontal="$2"
                paddingVertical="$1"
                borderRadius="$3"
                backgroundColor="rgba(255, 215, 0, 0.95)"
                alignItems="center"
                gap="$1"
              >
                <Star size="$1" color="$color1" />
                <Text fontSize="$2" fontWeight="700" color="$color1">
                  Capitano
                </Text>
              </XStack>
            )}
          </YStack>
          {shouldShowVote && voteLabel && (
            <YStack alignItems="center" gap="$1">
              <XStack
                paddingHorizontal="$2"
                paddingVertical="$1"
                borderRadius="$3"
                backgroundColor={isPositiveVote ? '$green4' : '$red4'}
              >
                <Text fontSize="$3" fontWeight="700" color={isPositiveVote ? '$green11' : '$red11'}>
                  {voteLabel}
                </Text>
              </XStack>
              {player.voteIsFinal === false && (
                <Text fontSize="$1" color="$color9">
                  Provvisorio
                </Text>
              )}
            </YStack>
          )}
        </YStack>

        {match && (
          <YStack gap="$2" flex={1}>
            <XStack gap="$2" alignItems="center" justifyContent="flex-end">
              <XStack
                paddingHorizontal="$2"
                paddingVertical="$1"
                borderRadius="$3"
                backgroundColor={match.isHome ? '$green4' : '$purple4'}
                alignItems="center"
                gap="$1.5"
              >
                <HomeAwayIcon size="$1" color={match.isHome ? '$green11' : '$purple11'} />
                <Text fontSize="$2" fontWeight="600" color={match.isHome ? '$green11' : '$purple11'}>
                  {match.isHome ? 'Casa' : 'Trasferta'}
                </Text>
              </XStack>
              {showScore && (
                <XStack
                  paddingHorizontal="$2"
                  paddingVertical="$1"
                  borderRadius="$3"
                  backgroundColor={match.status === 'live' ? '$yellow4' : '$color3'}
                  alignItems="center"
                  gap="$1"
                >
                  <Text fontSize="$3" fontWeight="700" color="$color12">
                    {scoreLabel}
                  </Text>
                  {match?.result?.isLive && (
                    <Text fontSize="$2" color="$red10" fontWeight="600">
                      {match.result.minute ? `${match.result.minute}'` : 'LIVE'}
                    </Text>
                  )}
                </XStack>
              )}
            </XStack>

            <YStack gap="$1" alignItems="flex-end">
              {opponentLabel && (
                <Text fontSize="$3" fontWeight="600" color="$color12">
                  {opponentLabel}
                </Text>
              )}
              {matchStatusLabel && (
                <Text fontSize="$2" color="$color10">
                  {matchStatusLabel}
                </Text>
              )}
            </YStack>
          </YStack>
        )}
      </XStack>

      {shouldShowAvailabilityDetails && (
        <YStack gap="$2" backgroundColor="$color3" borderRadius="$4" padding="$3">
          {availabilitySourcesCount > 0 && (
            <XStack gap="$2" flexWrap="wrap">
              {availabilitySources.map((source, index) => {
                const statusStyle = getAvailabilityStatusStyle(source.status) ?? AVAILABILITY_STATUS_STYLES.unknown
                const probabilityLabel = formatProbability(source.probability)
                const sourceLabel = source.label ?? source.source.charAt(0).toUpperCase()
                return (
                  <XStack
                    key={`${source.source}-${index}`}
                    gap="$1"
                    alignItems="center"
                    paddingHorizontal="$2"
                    paddingVertical="$1"
                    borderRadius="$3"
                    backgroundColor={statusStyle.background}
                  >
                    <Newspaper size="$1" color={statusStyle.color} />
                    <Text fontSize="$2" fontWeight="600" color={statusStyle.color}>
                      {sourceLabel}
                    </Text>
                    <Text fontSize="$2" color={statusStyle.color}>
                      {statusStyle.label}
                    </Text>
                    {probabilityLabel && (
                      <Text fontSize="$2" color={statusStyle.color}>
                        {probabilityLabel}
                      </Text>
                    )}
                  </XStack>
                )
              })}
            </XStack>
          )}

          {(lastGamePositiveness != null || trendScore != null) && (
            <XStack gap="$2" flexWrap="wrap">
              {lastGamePositiveness != null && Number.isFinite(lastGamePositiveness) && (
                <XStack
                  gap="$2"
                  alignItems="center"
                  paddingHorizontal="$2"
                  paddingVertical="$1"
                  borderRadius="$3"
                  backgroundColor={getMetricColors(lastGamePositiveness).background}
                >
                  <HeartPulse size="$1" color={getMetricColors(lastGamePositiveness).color} />
                  <Text fontSize="$2" fontWeight="600" color={getMetricColors(lastGamePositiveness).color}>
                    Positività {formatSignedMetric(lastGamePositiveness, 1)}
                  </Text>
                </XStack>
              )}
              {trendScore != null && Number.isFinite(trendScore) && (
                <XStack
                  gap="$2"
                  alignItems="center"
                  paddingHorizontal="$2"
                  paddingVertical="$1"
                  borderRadius="$3"
                  backgroundColor={getMetricColors(trendScore).background}
                >
                  {(trendScore ?? 0) > 0 ? (
                    <TrendingUp size="$1" color={getMetricColors(trendScore).color} />
                  ) : (trendScore ?? 0) < 0 ? (
                    <TrendingDown size="$1" color={getMetricColors(trendScore).color} />
                  ) : (
                    <Activity size="$1" color={getMetricColors(trendScore).color} />
                  )}
                  <Text fontSize="$2" fontWeight="600" color={getMetricColors(trendScore).color}>
                    Trend {formatSignedMetric(trendScore, 0)}
                  </Text>
                </XStack>
              )}
            </XStack>
          )}

          {availabilityDescription && (
            <Paragraph fontSize="$2" color="$color11">
              {availabilityDescription}
            </Paragraph>
          )}
        </YStack>
      )}

      {didNotPlay && (
        <XStack
          gap="$2"
          alignItems="center"
          borderRadius="$4"
          padding="$3"
          backgroundColor="$red3"
        >
          <CircleSlash size="$2" color="$red11" />
          <YStack gap="$1">
            <Text fontSize="$3" fontWeight="600" color="$red11">
              Non ha giocato
            </Text>
            {participation?.reason && (
              <Text fontSize="$2" color="$red11">
                {participation.reason}
              </Text>
            )}
          </YStack>
        </XStack>
      )}

      {isAwaitingVote && (
        <XStack
          gap="$2"
          alignItems="center"
          borderRadius="$4"
          padding="$3"
          backgroundColor="$yellow3"
        >
          <Activity size="$2" color="$yellow11" />
          <YStack gap="$1">
            <Text fontSize="$3" fontWeight="600" color="$yellow11">
              In attesa del voto
            </Text>
            <Text fontSize="$2" color="$yellow11">
              La partita è in corso o appena terminata.
            </Text>
          </YStack>
        </XStack>
      )}

      {player.note && (
        <Paragraph fontSize="$2" color="$color10" lineHeight={18}>
          {player.note}
        </Paragraph>
      )}

      {isLiveOrFinished && events.length > 0 && (
        <XStack gap="$2" flexWrap="wrap">
          {events.map((event, index) => {
            const descriptor = EVENT_ICON_MAP[event.type]
            const IconComponent = descriptor.icon
            const count = event.count && event.count > 1 ? `x${event.count}` : null
            return (
              <XStack
                key={`${event.type}-${index}`}
                gap="$1"
                paddingHorizontal="$2"
                paddingVertical="$1"
                borderRadius="$3"
                alignItems="center"
                backgroundColor={descriptor.background}
              >
                <IconComponent size="$1" color={descriptor.color} />
                <Text fontSize="$2" color={descriptor.color} fontWeight="600">
                  {count ?? descriptor.label}
                </Text>
                {count && (
                  <Text fontSize="$2" color={descriptor.color}>
                    {descriptor.label}
                  </Text>
                )}
              </XStack>
            )
          })}
        </XStack>
      )}
    </Card>
  )
}
