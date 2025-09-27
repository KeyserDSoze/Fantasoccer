import { useMemo } from 'react'
import {
  Button,
  Card,
  H3,
  Paragraph,
  Spinner,
  Text,
  XStack,
  YStack
} from 'tamagui'
import { useSerieACalendar, useSerieARank, formatGameDate, getDayStatusText } from 'hooks/useSerieACalendar'
import { RealCalendarHelper } from 'types/serieA'

export type SerieALiveWidgetProps = {
  refreshInterval?: number
  enablePolling?: boolean
  pollOnlyWhenLive?: boolean
}

export function SerieALiveWidget({
  refreshInterval,
  enablePolling,
  pollOnlyWhenLive
}: SerieALiveWidgetProps) {
  const state = useSerieACalendar({
    refreshInterval,
    enablePolling,
    pollOnlyWhenLive
  })

  const {
    rank,
    isLoading: isRankLoading,
    error: rankError,
    refetch: refetchRank,
    lastUpdated: rankLastUpdated
  } = useSerieARank({ refreshInterval: 180000, enablePolling: true })

  const {
    liveDay,
    nextDay,
    liveGames,
    isLive,
    liveSerieADay,
    isLoading,
    error,
    refetch,
    lastUpdated
  } = state

  const displayDay = isLive ? liveDay ?? null : nextDay ?? liveDay ?? null
  const displayGames = displayDay?.games ?? []
  const dayNumber = displayDay?.serieADay ?? (liveSerieADay > 0 ? liveSerieADay : null)
  const sectionTitle = dayNumber ? `Serie A • Giornata ${dayNumber}` : 'Serie A • Calendario'
  const statusText = getDayStatusText(isLive, liveDay ?? null, nextDay ?? null)

  const liveGameKeys = useMemo(
    () => new Set((liveGames ?? []).map(game => `${game.home.name}-${game.away.name}-${game.date ?? ''}`)),
    [liveGames]
  )

  const hasGames = displayGames.length > 0
  const rankTeams = rank?.teams ?? []

  return (
    <YStack gap="$3" width="100%">
      <XStack justifyContent="space-between" alignItems="center" gap="$2" flexWrap="wrap">
        <H3 color="$color12">{sectionTitle}</H3>
        <Button size="$2" variant="outlined" disabled={isLoading} onPress={() => { void refetch() }}>
          Aggiorna
        </Button>
      </XStack>

      <Paragraph color="$color10" fontSize="$3">
        {statusText}
      </Paragraph>

      {error && (
        <Card backgroundColor="$red3" borderColor="$red7" borderWidth="$0.5" padding="$3" borderRadius="$4">
          <Text color="$red11" fontSize="$3">
            {error}
          </Text>
        </Card>
      )}

      {isLoading && !hasGames && !error && (
        <Card
          padding="$4"
          backgroundColor="$color2"
          borderRadius="$4"
          borderWidth="$0.5"
          borderColor="$borderColor"
        >
          <XStack justifyContent="center" alignItems="center" gap="$3">
            <Spinner size="large" color="$blue10" />
            <Text color="$color10" fontSize="$3">
              Caricamento calendario...
            </Text>
          </XStack>
        </Card>
      )}

      {hasGames ? (
        <YStack gap="$3">
          {displayGames.map(game => {
            const key = `${game.home.name}-${game.away.name}-${game.date ?? 'n.d.'}`
            const isGameLive = liveGameKeys.has(key)
            const hasScore = game.homeGoals != null && game.awayGoals != null
            const kickoffLabel = formatGameDate(game.date)
            const isPlayed = RealCalendarHelper.isGamePlayed(game)
            const showScore = hasScore && (isGameLive || isPlayed)
            const statusLabel = isGameLive ? 'LIVE' : isPlayed ? 'Risultato finale' : 'Da giocare'
            const statusColor = isGameLive ? '$red10' : isPlayed ? '$color10' : '$color9'

            return (
              <Card
                key={key}
                padding="$3"
                backgroundColor="$color2"
                borderRadius="$4"
                borderWidth="$0.5"
                borderColor={isGameLive ? '$red7' : '$borderColor'}
              >
                <YStack gap="$2">
                  <XStack alignItems="center" width="100%" gap="$4">
                    <Text fontWeight="700" color="$color12" flex={1} textAlign="left" numberOfLines={1}>
                      {game.home.name}
                    </Text>

                    <YStack alignItems="center" gap="$1" width={150}>
                      {showScore ? (
                        <Text
                          fontWeight="800"
                          fontSize="$5"
                          color={isGameLive ? '$red10' : '$color12'}
                          textAlign="center"
                        >
                          {game.homeGoals} - {game.awayGoals}
                        </Text>
                      ) : (
                        <Text fontWeight="700" fontSize="$5" color="$color10" textAlign="center">
                          vs
                        </Text>
                      )}

                      <Text fontSize="$3" color="$color10" textAlign="center">
                        {kickoffLabel}
                      </Text>

                      <Text
                        fontSize="$2"
                        fontWeight={isGameLive ? '700' : '600'}
                        color={statusColor}
                        textAlign="center"
                      >
                        {statusLabel}
                      </Text>

                      {game.delayed && (
                        <Text fontSize="$2" fontWeight="600" color="$yellow10">
                          Rinviata
                        </Text>
                      )}
                    </YStack>

                    <Text fontWeight="700" color="$color12" flex={1} textAlign="right" numberOfLines={1}>
                      {game.away.name}
                    </Text>
                  </XStack>
                </YStack>
              </Card>
            )
          })}
        </YStack>
      ) : !isLoading && !error ? (
        <Card
          padding="$4"
          backgroundColor="$color2"
          borderRadius="$4"
          borderWidth="$0.5"
          borderColor="$borderColor"
        >
          <Text color="$color10" fontSize="$3">
            Nessuna partita disponibile al momento. Riprova più tardi.
          </Text>
        </Card>
      ) : null}

      {lastUpdated && (
        <Text fontSize="$2" color="$color9">
          Ultimo aggiornamento: {lastUpdated.toLocaleTimeString('it-IT', { hour: '2-digit', minute: '2-digit' })}
        </Text>
      )}

      <SeparatorSection
        title="Classifica Serie A"
        onRefresh={() => { void refetchRank() }}
        isLoading={isRankLoading}
      />

      {rankError && (
        <Card backgroundColor="$red3" borderColor="$red7" borderWidth="$0.5" padding="$3" borderRadius="$4">
          <Text color="$red11" fontSize="$3">
            {rankError}
          </Text>
        </Card>
      )}

      {!rankError && (
        <Card
          padding="$3"
          backgroundColor="$color2"
          borderRadius="$4"
          borderWidth="$0.5"
          borderColor="$borderColor"
        >
          {isRankLoading && rankTeams.length === 0 ? (
            <XStack justifyContent="center" alignItems="center" gap="$3" paddingVertical="$3">
              <Spinner size="small" color="$blue10" />
              <Text color="$color10" fontSize="$3">
                Caricamento classifica...
              </Text>
            </XStack>
          ) : (
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
          )}

          {rankLastUpdated && (
            <Text fontSize="$2" color="$color9" marginTop="$3">
              Aggiornata alle {rankLastUpdated.toLocaleTimeString('it-IT', { hour: '2-digit', minute: '2-digit' })}
            </Text>
          )}
        </Card>
      )}
    </YStack>
  )
}

type SeparatorSectionProps = {
  title: string
  onRefresh?: () => void
  isLoading?: boolean
}

function SeparatorSection({ title, onRefresh, isLoading }: SeparatorSectionProps) {
  return (
    <XStack justifyContent="space-between" alignItems="center" gap="$2" width="100%">
      <H3 color="$color12">{title}</H3>
      {onRefresh && (
        <Button size="$2" variant="outlined" disabled={isLoading} onPress={onRefresh}>
          Aggiorna
        </Button>
      )}
    </XStack>
  )
}

