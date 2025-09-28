import { useMemo } from 'react'
import { AlertTriangle, CalendarRange, RefreshCw } from '@tamagui/lucide-icons'
import {
  Button,
  Card,
  H3,
  Separator,
  Spinner,
  Text,
  XStack,
  YStack
} from 'tamagui'
import { useSerieACalendar, useSerieARank, formatGameDate, getDayStatusText } from 'hooks/useSerieACalendar'
import { RealCalendarHelper, type RealDay, type RealGame } from 'types/serieA'

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

  const displayDay = liveDay ?? nextDay ?? null
  const displayGames = displayDay?.games ?? []
  const dayNumber = displayDay?.serieADay ?? (liveSerieADay > 0 ? liveSerieADay : null)
  const statusText = getDayStatusText(isLive, liveDay ?? null, nextDay ?? null)
  const pendingGamesCount = displayDay
    ? displayDay.games.filter(game => !RealCalendarHelper.isGamePlayed(game) && !game.delayed).length
    : 0

  const liveGameKeys = useMemo(
    () => new Set((liveGames ?? []).map(buildGameKey)),
    [liveGames]
  )

  const hasGames = displayGames.length > 0
  const showLoadingState = isLoading && !hasGames && !error
  const showErrorState = Boolean(error)
  const showEmptyState = !showLoadingState && !showErrorState && !hasGames
  const rankTeams = rank?.teams ?? []

  return (
    <YStack gap="$3" width="100%">
      <Card padding="$3" elevate backgroundColor="$color1" gap="$3">
        <XStack alignItems="center" gap="$2">
          <CalendarRange size="$1.5" color="$purple10" />
          <YStack gap="$1" flex={1}>
            <Text fontSize="$5" fontWeight="700" color="$color12">
              Serie A • Calendario
            </Text>
            {dayNumber && (
              <Text fontSize="$3" color="$color10">
                Giornata {dayNumber}
              </Text>
            )}
            <Text fontSize="$2" color="$color10">
              {statusText}
            </Text>
            <Text fontSize="$2" color="$color10">
              {pendingGamesCount} partite in attesa di risultato
            </Text>
            {lastUpdated && (
              <Text fontSize="$2" color="$color9">
                Ultimo aggiornamento: {lastUpdated.toLocaleTimeString('it-IT', { hour: '2-digit', minute: '2-digit' })}
              </Text>
            )}
          </YStack>
          {isLoading && (
            <Spinner size="small" color="$blue10" />
          )}
        </XStack>

        <Separator />

        {showErrorState ? (
          <YStack gap="$3" alignItems="center" paddingVertical="$4">
            <AlertTriangle size={32} color="$red10" />
            <Text color="$color10" textAlign="center">
              {error}
            </Text>
            <Button icon={RefreshCw} variant="outlined" onPress={() => { void refetch() }}>
              Riprova
            </Button>
          </YStack>
        ) : showLoadingState ? (
          <YStack gap="$3" alignItems="center" paddingVertical="$4">
            <Spinner size="small" color="$blue10" />
            <Text color="$color10">Caricamento calendario...</Text>
          </YStack>
        ) : showEmptyState ? (
          <YStack gap="$3" alignItems="center" paddingVertical="$4">
            <Text color="$color10" fontSize="$3" textAlign="center">
              Nessuna partita disponibile al momento. Riprova più tardi.
            </Text>
            <Button variant="outlined" onPress={() => { void refetch() }}>
              Aggiorna calendario
            </Button>
          </YStack>
        ) : displayDay ? (
          <YStack gap="$3" paddingBottom="$2">
            <SerieADayCard day={displayDay} liveGameKeys={liveGameKeys} />
          </YStack>
        ) : null}

        {hasGames && (
          <>
            <Separator marginTop="$2" />
            <XStack justifyContent="flex-end">
              <Button size="$2" variant="outlined" icon={RefreshCw} onPress={() => { void refetch() }}>
                Aggiorna calendario
              </Button>
            </XStack>
          </>
        )}
      </Card>

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

type SerieADayCardProps = {
  day: RealDay
  liveGameKeys: Set<string>
}

function SerieADayCard({ day, liveGameKeys }: SerieADayCardProps) {
  const totalGames = day.games.length
  const pendingGames = day.games.filter(game => !RealCalendarHelper.isGamePlayed(game) && !game.delayed).length

  return (
    <Card
      padding="$3"
      backgroundColor="$color2"
      borderRadius="$4"
      borderWidth="$0.5"
      borderColor="$borderColor"
      gap="$3"
    >
      <XStack justifyContent="space-between" alignItems="center">
        <YStack gap="$1">
          <Text color="$color12" fontSize="$4" fontWeight="700">
            Giornata Serie A {day.serieADay}
          </Text>
          <Text color="$color10" fontSize="$2">
            {pendingGames > 0
              ? `${pendingGames} partite ancora da giocare`
              : 'Tutte le partite concluse'}
          </Text>
        </YStack>
        <Text color="$color10" fontWeight="600">
          {totalGames} partite
        </Text>
      </XStack>

      <Separator />

      <YStack gap="$2">
        {day.games.map(game => {
          const key = buildGameKey(game)
          const isGameLive = liveGameKeys.has(key)

          return (
            <Card
              key={key}
              paddingHorizontal="$2"
              paddingVertical="$1.5"
              backgroundColor="$color1"
              borderRadius="$3"
              borderWidth="$0.25"
              borderColor={isGameLive ? '$red7' : '$color3'}
            >
              <SerieAGameRow game={game} isLive={isGameLive} />
            </Card>
          )
        })}
      </YStack>
    </Card>
  )
}

type SerieAGameRowProps = {
  game: RealGame
  isLive: boolean
}

function SerieAGameRow({ game, isLive }: SerieAGameRowProps) {
  const kickoffLabel = formatGameDate(game.date)
  const isPlayed = RealCalendarHelper.isGamePlayed(game)
  const hasScore = game.homeGoals != null && game.awayGoals != null
  const showScore = hasScore && (isLive || isPlayed)
  const statusLabel = game.delayed
    ? 'Rinviata'
    : isLive
      ? 'LIVE'
      : isPlayed
        ? 'Risultato finale'
        : 'Da giocare'
  const statusColor = game.delayed ? '$yellow10' : isLive ? '$red10' : isPlayed ? '$color10' : '$color9'

  return (
    <XStack
      alignItems="center"
      justifyContent="space-between"
      gap="$3"
      paddingVertical="$1.5"
    >
      <YStack flex={1} gap="$1">
        <Text color="$color12" fontWeight="700" numberOfLines={1}>
          {game.home.name}
        </Text>
      </YStack>

      <YStack flexShrink={0} width={120} alignItems="center" gap="$1">
        {showScore ? (
          <Text fontWeight="800" fontSize="$5" color={isLive ? '$red10' : '$color12'} textAlign="center">
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

        <Text fontSize="$2" fontWeight={isLive ? '700' : '600'} color={statusColor} textAlign="center">
          {statusLabel}
        </Text>
      </YStack>

      <YStack flex={1} gap="$1" alignItems="flex-end">
        <Text color="$color12" fontWeight="700" numberOfLines={1}>
          {game.away.name}
        </Text>
      </YStack>
    </XStack>
  )
}

function buildGameKey(game: RealGame): string {
  return `${game.home.name}-${game.away.name}-${game.date ?? ''}`
}

