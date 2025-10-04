import { useMemo, useState } from 'react'
import { AlertTriangle, CalendarRange, RefreshCw, User, ChevronDown, ChevronUp } from '@tamagui/lucide-icons'
import {
  Button,
  Card,
  H3,
  H5,
  Separator,
  Spinner,
  Text,
  XStack,
  YStack,
  useMedia,
  ScrollView
} from 'tamagui'
import { Pressable } from 'react-native'
import { useSerieACalendar, useSerieARank, getDayStatusText } from 'hooks/useSerieACalendar'
import { useSerieALiveVotes } from 'hooks/useSerieALiveVotes'
import { RealCalendarHelper, type RealDay, type RealGame, type VotedRealPlayer, type Vote } from 'types/serieA'
import { formatItalianDate } from 'utils/timezoneHelpers'
import { SoccerBallSVG } from 'components/lineup/icons/SoccerBallSVG'
import { AssistSVG } from 'components/lineup/icons/AssistSVG'
import { BehaviourCardSVG } from 'components/lineup/icons/BehaviourCardSVG'

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
  const media = useMedia()
  const isDesktop = media.gtMd ?? false
  const sectionGap = isDesktop ? '$4' : '$3'

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

  const liveGameKeys = useMemo(
    () => new Set((liveGames ?? []).map(buildGameKey)),
    [liveGames]
  )

  // Eventi live - giocatori che hanno fatto qualcosa ordinati per importanza
  const liveEvents = useMemo(() => {
    if (!liveVotes || !liveVotes.players || liveVotes.players.length === 0) {
      return []
    }

    // Filtra solo giocatori con eventi (hasDoneSomething = true)
    const playersWithEvents = liveVotes.players.filter(p => p.vote?.hasDoneSomething)

    // Ordina per importanza eventi
    return playersWithEvents.sort((a, b) => {
      const va = a.vote
      const vb = b.vote

      // 1. Gol normali (più importanti)
      if (va.goals !== vb.goals) return vb.goals - va.goals
      
      // 2. Rigori segnati
      if (va.penalties !== vb.penalties) return vb.penalties - va.penalties
      
      // 3. Assist
      if (va.assists !== vb.assists) return vb.assists - va.assists
      
      // 4. Rigori parati
      if (va.stoppedPenalties !== vb.stoppedPenalties) return vb.stoppedPenalties - va.stoppedPenalties
      
      // 5. Cartellini rossi (negativi ma importanti)
      const aRedCard = va.status === 2 ? 1 : 0 // Behaviour.RedCard = 2
      const bRedCard = vb.status === 2 ? 1 : 0
      if (aRedCard !== bRedCard) return bRedCard - aRedCard
      
      // 6. Rigori sbagliati
      if (va.wrongedPenalties !== vb.wrongedPenalties) return vb.wrongedPenalties - va.wrongedPenalties
      
      // 7. Autogol
      if (va.ownGoals !== vb.ownGoals) return vb.ownGoals - va.ownGoals
      
      // 8. Gol subiti
      if (va.sufferedGoals !== vb.sufferedGoals) return vb.sufferedGoals - va.sufferedGoals
      
      // 9. Cartellini gialli
      const aYellowCard = va.status === 1 ? 1 : 0 // Behaviour.YellowCard = 1
      const bYellowCard = vb.status === 1 ? 1 : 0
      if (aYellowCard !== bYellowCard) return bYellowCard - aYellowCard

      return 0
    })
  }, [liveVotes])

  // Eventi live raggruppati per partita e divisi per squadra
  const eventsByGame = useMemo(() => {
    if (!liveEvents || liveEvents.length === 0 || !displayDay) {
      return new Map<string, { home: VotedRealPlayer[], away: VotedRealPlayer[] }>()
    }

    const map = new Map<string, { home: VotedRealPlayer[], away: VotedRealPlayer[] }>()

    displayDay.games.forEach(game => {
      const key = buildGameKey(game)
      const homeEvents: VotedRealPlayer[] = []
      const awayEvents: VotedRealPlayer[] = []

      liveEvents.forEach(player => {
        if (game.home.name === player.realTeam.name) {
          homeEvents.push(player)
        } else if (game.away.name === player.realTeam.name) {
          awayEvents.push(player)
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
  const showLiveEvents = isLive && liveEvents.length > 0

  const calendarCard = (
    <Card elevate backgroundColor="$color1" gap="$3">
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
          <SerieADayCard day={displayDay} liveGameKeys={liveGameKeys} eventsByGame={eventsByGame} />
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
        <Button size="$2" variant="outlined" disabled={isRankLoading} onPress={() => { void refetchRank() }}>
          Aggiorna
        </Button>
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
        <Text fontSize="$2" color="$color9">
          Aggiornata alle {rankLastUpdated.toLocaleTimeString('it-IT', { hour: '2-digit', minute: '2-digit' })}
        </Text>
      )}
    </Card>
  )

  return (
    <YStack gap={sectionGap} width="100%">
      {isDesktop ? (
        <XStack gap={sectionGap} width="100%" alignItems="stretch">
          <YStack flex={1}>{calendarCard}</YStack>
          <YStack flex={1}>{rankingCard}</YStack>
        </XStack>
      ) : (
        <YStack gap={sectionGap} width="100%">
          {calendarCard}
          {rankingCard}
        </YStack>
      )}
    </YStack>
  )
}

type SerieADayCardProps = {
  day: RealDay
  liveGameKeys: Set<string>
  eventsByGame: Map<string, { home: VotedRealPlayer[], away: VotedRealPlayer[] }>
}

function SerieADayCard({ day, liveGameKeys, eventsByGame }: SerieADayCardProps) {
  // State per gestire l'accordion: di default aperto solo per le partite live
  const [expandedGames, setExpandedGames] = useState<Set<string>>(() => {
    const initialExpanded = new Set<string>()
    day.games.forEach(game => {
      const key = buildGameKey(game)
      if (liveGameKeys.has(key)) {
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
        {day.games.map(game => {
          const key = buildGameKey(game)
          const isGameLive = liveGameKeys.has(key)
          const gameEvents = eventsByGame.get(key)
          const hasEvents = gameEvents && (gameEvents.home.length > 0 || gameEvents.away.length > 0)
          const isExpanded = expandedGames.has(key)

          return (
            <YStack key={key} gap="$2">
              <Pressable onPress={() => hasEvents && toggleGame(key)}>
                <Card
                  paddingHorizontal="$2"
                  paddingVertical="$1.5"
                  backgroundColor="$color1"
                  borderRadius="$3"
                  borderWidth="$0.25"
                  borderColor={isGameLive ? '$red7' : '$color3'}
                >
                  <XStack alignItems="center" gap="$2">
                    <YStack flex={1}>
                      <SerieAGameRow game={game} isLive={isGameLive} />
                    </YStack>
                    {hasEvents && (
                      <YStack paddingRight="$2">
                        {isExpanded ? (
                          <ChevronUp size={20} color="$color11" />
                        ) : (
                          <ChevronDown size={20} color="$color11" />
                        )}
                      </YStack>
                    )}
                  </XStack>
                </Card>
              </Pressable>
              
              {/* Eventi live per questa partita - layout a colonne */}
              {hasEvents && isExpanded && (
                <YStack gap="$2" paddingHorizontal="$2">
                  <Text fontSize="$2" fontWeight="700" color="$red11">
                    ⚡ Eventi Live
                  </Text>
                  <XStack gap="$3">
                    {/* Colonna Casa */}
                    <YStack flex={1} gap="$1.5" backgroundColor="$color3" padding="$2" borderRadius="$3">
                      <Text fontSize="$1" fontWeight="800" color="$color11" textAlign="center">
                        {game.home.name}
                      </Text>
                      {gameEvents.home.length > 0 ? (
                        gameEvents.home.map((player, index) => (
                          <LiveEventRow key={`home-${player.name}-${index}`} player={player} />
                        ))
                      ) : (
                        <Text fontSize="$1" color="$color9" textAlign="center" paddingVertical="$2">
                          Nessun evento
                        </Text>
                      )}
                    </YStack>
                    
                    {/* Colonna Trasferta */}
                    <YStack flex={1} gap="$1.5" backgroundColor="$color3" padding="$2" borderRadius="$3">
                      <Text fontSize="$1" fontWeight="800" color="$color11" textAlign="center">
                        {game.away.name}
                      </Text>
                      {gameEvents.away.length > 0 ? (
                        gameEvents.away.map((player, index) => (
                          <LiveEventRow key={`away-${player.name}-${index}`} player={player} />
                        ))
                      ) : (
                        <Text fontSize="$1" color="$color9" textAlign="center" paddingVertical="$2">
                          Nessun evento
                        </Text>
                      )}
                    </YStack>
                  </XStack>
                </YStack>
              )}
            </YStack>
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
  const kickoffLabel = formatItalianDate(game.date.toString())
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

// ============================================
// LIVE EVENT ROW COMPONENT
// ============================================

type LiveEventRowProps = {
  player: VotedRealPlayer
}

function LiveEventRow({ player }: LiveEventRowProps) {
  const vote = player.vote
  const media = useMedia()
  const isMobile = !media.gtMd

  if (!vote) return null

  // Raccoglie tutti gli eventi del giocatore
  const events: Array<{ icon: React.ReactNode; label: string; count: number; color: string }> = []

  // Gol normali
  if (vote.goals > 0) {
    events.push({
      icon: <SoccerBallSVG primaryColor="#ffffff" secondaryColor="#000000" size={16} />,
      label: vote.goals === 1 ? 'Gol' : `${vote.goals} Gol`,
      count: vote.goals,
      color: '$green10'
    })
  }

  // Rigori segnati
  if (vote.penalties > 0) {
    events.push({
      icon: <SoccerBallSVG primaryColor="#000000" secondaryColor="#ffffff" size={16} />,
      label: vote.penalties === 1 ? 'Rigore' : `${vote.penalties} Rigori`,
      count: vote.penalties,
      color: '$blue10'
    })
  }

  // Assist
  if (vote.assists > 0) {
    events.push({
      icon: <AssistSVG color="#000" size={16} />,
      label: vote.assists === 1 ? 'Assist' : `${vote.assists} Assist`,
      count: vote.assists,
      color: '$purple10'
    })
  }

  // Rigori parati
  if (vote.stoppedPenalties > 0) {
    events.push({
      icon: <SoccerBallSVG primaryColor="#3b82f6" secondaryColor="#ffffff" size={16} />,
      label: vote.stoppedPenalties === 1 ? 'Rigore Parato' : `${vote.stoppedPenalties} Rigori Parati`,
      count: vote.stoppedPenalties,
      color: '$blue10'
    })
  }

  // Cartellini
  if (vote.status === 2) { // RedCard
    events.push({
      icon: <BehaviourCardSVG color="#ef4444" size={16} />,
      label: 'Cartellino Rosso',
      count: 1,
      color: '$red10'
    })
  } else if (vote.status === 1) { // YellowCard
    events.push({
      icon: <BehaviourCardSVG color="#fbbf24" size={16} />,
      label: 'Cartellino Giallo',
      count: 1,
      color: '$yellow10'
    })
  }

  // Rigori sbagliati
  if (vote.wrongedPenalties > 0) {
    events.push({
      icon: <SoccerBallSVG primaryColor="#c113d1ff" secondaryColor="#ffffff" size={16} />,
      label: vote.wrongedPenalties === 1 ? 'Rigore Sbagliato' : `${vote.wrongedPenalties} Rigori Sbagliati`,
      count: vote.wrongedPenalties,
      color: '$purple10'
    })
  }

  // Autogol
  if (vote.ownGoals > 0) {
    events.push({
      icon: <SoccerBallSVG primaryColor="#fbbf24" secondaryColor="#ffffff" size={16} />,
      label: vote.ownGoals === 1 ? 'Autogol' : `${vote.ownGoals} Autogol`,
      count: vote.ownGoals,
      color: '$orange10'
    })
  }

  // Gol subiti
  if (vote.sufferedGoals > 0) {
    events.push({
      icon: <SoccerBallSVG primaryColor="#ef4444" secondaryColor="#ffffff" size={16} />,
      label: vote.sufferedGoals === 1 ? 'Gol Subito' : `${vote.sufferedGoals} Gol Subiti`,
      count: vote.sufferedGoals,
      color: '$red10'
    })
  }

  if (events.length === 0) return null

  // Layout mobile: verticale con nome sopra e eventi sotto
  if (isMobile) {
    return (
      <Card
        paddingHorizontal="$2"
        paddingVertical="$2"
        backgroundColor="$color1"
        borderRadius="$3"
        borderWidth="$0.25"
        borderColor="$color3"
      >
        <YStack gap="$2">
          {/* Nome giocatore */}
          <Text fontSize="$3" fontWeight="700" color="$color12" numberOfLines={1}>
            {player.name}
          </Text>

          {/* Eventi e voto */}
          <XStack alignItems="center" justifyContent="space-between" gap="$2">
            {/* Eventi */}
            <XStack gap="$1.5" flexWrap="wrap" flex={1}>
              {events.map((event, index) => (
                <XStack
                  key={`${player.name}-event-${index}`}
                  alignItems="center"
                  gap="$1"
                  paddingHorizontal="$2"
                  paddingVertical="$1"
                  borderRadius="$2"
                  backgroundColor="$color2"
                >
                  {event.icon}
                  <Text fontSize="$2" fontWeight="600" color={event.color}>
                    {event.count > 1 ? event.count : ''}
                  </Text>
                </XStack>
              ))}
            </XStack>

            {/* Vote badge */}
            <YStack
              minWidth={44}
              paddingHorizontal="$2"
              paddingVertical="$1"
              borderRadius="$2"
              backgroundColor={vote.isFinal ? '$green10' : '$orange10'}
              alignItems="center"
              flexShrink={0}
            >
              <Text fontSize="$4" fontWeight="800" color="#fff">
                {vote.value.toFixed(1)}
              </Text>
            </YStack>
          </XStack>
        </YStack>
      </Card>
    )
  }

  // Layout desktop: orizzontale senza nome squadra
  return (
    <Card
      paddingHorizontal="$3"
      paddingVertical="$2"
      backgroundColor="$color1"
      borderRadius="$3"
      borderWidth="$0.25"
      borderColor="$color3"
    >
      <XStack alignItems="center" justifyContent="space-between" gap="$3">
        {/* Player name only */}
        <Text fontSize="$3" fontWeight="700" color="$color12" numberOfLines={1} flex={1}>
          {player.name}
        </Text>

        {/* Events */}
        <XStack gap="$2" flexWrap="wrap" maxWidth={200} justifyContent="flex-end">
          {events.map((event, index) => (
            <XStack
              key={`${player.name}-event-${index}`}
              alignItems="center"
              gap="$1"
              paddingHorizontal="$2"
              paddingVertical="$1"
              borderRadius="$2"
              backgroundColor="$color2"
            >
              {event.icon}
              <Text fontSize="$2" fontWeight="600" color={event.color}>
                {event.count > 1 ? event.count : ''}
              </Text>
            </XStack>
          ))}
        </XStack>

        {/* Vote badge */}
        <YStack
          minWidth={48}
          paddingHorizontal="$2"
          paddingVertical="$1"
          borderRadius="$2"
          backgroundColor={vote.isFinal ? '$green10' : '$orange10'}
          alignItems="center"
          flexShrink={0}
        >
          <Text fontSize="$4" fontWeight="800" color="#fff">
            {vote.value.toFixed(1)}
          </Text>
        </YStack>
      </XStack>
    </Card>
  )
}

