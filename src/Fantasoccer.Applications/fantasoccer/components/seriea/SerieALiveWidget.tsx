import { useMemo, useState } from 'react'
import { AlertTriangle, CalendarRange, RefreshCw, User, ChevronDown, ChevronUp } from '@tamagui/lucide-icons-2'
import {
  Button,
  Card,
  H3,
  H5,
  Separator,
  Text,
  XStack,
  YStack,
  ScrollView
} from 'tamagui'
import { Pressable } from 'react-native'
import { useNavigation } from '@react-navigation/native'
import { useSerieACalendar, useSerieARank, getDayStatusText } from 'hooks/useSerieACalendar'
import { useSerieALiveVotes } from 'hooks/useSerieALiveVotes'
import { RealCalendarHelper, type RealDay, type RealGame, type VotedRealPlayer } from 'types/serieA'
import { formatItalianDate } from 'utils/timezoneHelpers'
import { MatchEvents } from 'components/seriea/MatchEvents'
import { SerieARankTable } from 'components/seriea/SerieARankTable'
import { SerieAMatchRow } from 'components/seriea/SerieAMatchRow'
import type { EventPlayer } from 'utils/matchEvents'
import { ResponsiveWidgetPair } from 'components/layout/ResponsiveWidgetPair'
import { SplashScreen } from 'components/layout/SplashScreen'

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

  // Hook per i voti live - sempre attivo quando c'è un displayDay
  const {
    votes: liveVotes,
    isLoading: isVotesLoading,
    error: votesError,
    lastUpdated: votesLastUpdated
  } = useSerieALiveVotes({
    liveDay: displayDay, // Usa displayDay per mostrare eventi anche se non ci sono partite live
    isLive,
    enablePolling: enablePolling ?? true
  })

  // Giocatori che hanno fatto qualcosa: l'ordine per importanza lo decide MatchEvents.
  const liveEvents = useMemo(
    () => (liveVotes?.players ?? []).filter(player => player.vote?.hasDoneSomething),
    [liveVotes]
  )

  // Eventi live raggruppati per partita e divisi per squadra
  const eventsByGame = useMemo(() => {
    if (!liveEvents || liveEvents.length === 0 || !displayDay) {
      return new Map<string, { home: EventPlayer[], away: EventPlayer[] }>()
    }

    const map = new Map<string, { home: EventPlayer[], away: EventPlayer[] }>()

    displayDay.games.forEach(game => {
      const key = buildGameKey(game)
      const homeEvents: EventPlayer[] = []
      const awayEvents: EventPlayer[] = []

      liveEvents.forEach((player: VotedRealPlayer) => {
        const entry = { key: `${player.playerId ?? player.name}`, name: player.name, vote: player.vote }
        if (game.home.name === player.realTeam?.name) {
          homeEvents.push(entry)
        } else if (game.away.name === player.realTeam?.name) {
          awayEvents.push(entry)
        }
      })

      if (homeEvents.length > 0 || awayEvents.length > 0) {
        map.set(key, { home: homeEvents, away: awayEvents })
      }
    })

    return map
  }, [liveEvents, displayDay])

  const hasGames = displayGames.length > 0
  const showLoadingState = isLoading && !hasGames && !error
  const showErrorState = Boolean(error)
  const showEmptyState = !showLoadingState && !showErrorState && !hasGames
  const rankTeams = rank?.teams ?? []

  const calendarCard = (
    <Card elevation="$2" backgroundColor="$color1" gap="$3">
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
          <SplashScreen compact message="Caricamento Serie A…" steps={[{ id: 'serie-a', label: 'Calendario e partite Serie A', status: 'pending' }]} />
        </YStack>
      ) : showEmptyState ? (
        <YStack gap="$3" alignItems="center" paddingVertical="$4">
          <Text color="$color10" fontSize="$3" textAlign="center">
            Nessuna partita disponibile al momento. Riprova più tardi.
          </Text>
        </YStack>
      ) : displayDay ? (
        <YStack gap="$3" paddingBottom="$2">
          <SerieADayCard day={displayDay} eventsByGame={eventsByGame} />
        </YStack>
      ) : null}

    </Card>
  )

  const rankingCard = (
    <Card
      padding="$3"
      backgroundColor="$color2"
      borderRadius="$4"
      borderWidth="$0.5"
      borderColor="$borderColor"
      gap="$3"
    >
      <XStack justifyContent="space-between" alignItems="center" gap="$2">
        <H3 color="$color12">Classifica Serie A</H3>
      </XStack>

      <Separator />

      {rankError ? (
        <YStack gap="$3" alignItems="center" paddingVertical="$4">
          <AlertTriangle size={32} color="$red10" />
          <Text color="$color10" textAlign="center">
            {rankError}
          </Text>
          <Button icon={RefreshCw} variant="outlined" onPress={() => { void refetchRank() }}>
            Riprova
          </Button>
        </YStack>
      ) : isRankLoading && rankTeams.length === 0 ? (
        <XStack justifyContent="center" alignItems="center" gap="$3" paddingVertical="$3">
          <SplashScreen compact message="Caricamento classifica…" steps={[{ id: 'rank', label: 'Classifica Serie A', status: 'pending' }]} />
        </XStack>
      ) : (
        <SerieARankTable teams={rankTeams} />
      )}

      {rankLastUpdated && (
        <Text fontSize="$2" color="$color9">
          Aggiornata alle {rankLastUpdated.toLocaleTimeString('it-IT', { hour: '2-digit', minute: '2-digit' })}
        </Text>
      )}
    </Card>
  )

  return (
    <ResponsiveWidgetPair testID="home-serie-a-widgets">
      {calendarCard}
      {rankingCard}
    </ResponsiveWidgetPair>
  )
}

type SerieADayCardProps = {
  day: RealDay
  eventsByGame: Map<string, { home: EventPlayer[], away: EventPlayer[] }>
}

function SerieADayCard({ day, eventsByGame }: SerieADayCardProps) {
  const navigation = useNavigation<any>()
  const openMatch = (game: RealGame, index: number) =>
    navigation.navigate('SerieAMatch', { year: String(day.year), day: String(day.serieADay), ordinal: String(game.ordinal ?? index) })
  // State per gestire l'accordion: di default aperto solo per le partite in corso
  const [expandedGames, setExpandedGames] = useState<Set<string>>(() => {
    const initialExpanded = new Set<string>()
    day.games.forEach(game => {
      const key = buildGameKey(game)
      if (RealCalendarHelper.isGamePlaying(game)) {
        initialExpanded.add(key)
      }
    })
    return initialExpanded
  })

  const toggleGame = (gameKey: string) => {
    setExpandedGames(prev => {
      const next = new Set(prev)
      if (next.has(gameKey)) {
        next.delete(gameKey)
      } else {
        next.add(gameKey)
      }
      return next
    })
  }

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
            {day.serieADay}° Giornata Serie A
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
        {day.games.map((game, index) => {
          const key = buildGameKey(game)
          const isGameLive = RealCalendarHelper.isGamePlaying(game)
          const gameEvents = eventsByGame.get(key)
          const hasEvents = gameEvents && (gameEvents.home.length > 0 || gameEvents.away.length > 0)
          const isExpanded = expandedGames.has(key)

          return (
            <YStack key={key} gap="$2">
              <Card
                paddingHorizontal="$2"
                paddingVertical="$1.5"
                backgroundColor="$color1"
                borderRadius="$3"
                borderWidth="$0.25"
                borderColor={isGameLive ? '$green7' : '$color3'}
              >
                <XStack alignItems="center" gap="$2">
                  <Pressable
                    style={{ flex: 1 }}
                    accessibilityRole="button"
                    accessibilityLabel={`Apri la partita ${game.home.name} - ${game.away.name}`}
                    onPress={() => openMatch(game, index)}
                  >
                    <SerieAMatchRow game={game} />
                  </Pressable>
                  {hasEvents && (
                    <Pressable
                      accessibilityRole="button"
                      accessibilityLabel={isExpanded ? 'Nascondi gli eventi' : 'Mostra gli eventi'}
                      hitSlop={8}
                      onPress={() => toggleGame(key)}
                    >
                      {isExpanded ? <ChevronUp size={20} color="$color11" /> : <ChevronDown size={20} color="$color11" />}
                    </Pressable>
                  )}
                </XStack>
              </Card>

              {hasEvents && isExpanded && (
                <YStack paddingHorizontal="$1">
                  <MatchEvents
                    home={{ name: game.home.name, players: gameEvents.home }}
                    away={{ name: game.away.name, players: gameEvents.away }}
                  />
                </YStack>
              )}
            </YStack>
          )
        })}
      </YStack>
    </Card>
  )
}

function buildGameKey(game: RealGame): string {
  return `${game.home.name}-${game.away.name}-${game.date ?? ''}`
}
