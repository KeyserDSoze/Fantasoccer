import React, { useMemo, useEffect, useState } from 'react'
import { View } from 'react-native'
import { ScreenContent } from 'components/layout/ScreenContent'
import { YStack, XStack, Text, H3, Separator, Button, Card } from 'tamagui'
import { useSerieACalendar } from 'hooks/useSerieACalendar'
import { SoccerField } from 'components/lineup'
import { useMedia } from 'tamagui'
import type { GameWrapper, LiveTeam } from 'types/game'
import type { RealGame } from 'types/serieA'
import gameService from 'services/gameService'
import { useNavigation, useRoute } from '@react-navigation/native'
import { useCurrentGroup } from 'hooks/useAppData'
import { useApp } from 'providers/AppProvider'
import { convertUTCToItalianTimezone } from 'utils/timezoneHelpers'
import type { RootStackParamList } from '../navigation'
import type { RouteProp } from '@react-navigation/native'

// Helper function per creare un ID unico da un RealGame
const getRealGameId = (game: RealGame): string => {
  return `${game.home.abbreviation}_${game.away.abbreviation}`
}

export default function GameScreen() {
  const media = useMedia()
  const {
    calendar,
    liveDay,
    nextDay,
    liveGames,
    isLive,
    liveSerieADay,
    isLoading,
    error,
  } = useSerieACalendar({ refreshInterval: 60000, enablePolling: true })

  const route = useRoute<RouteProp<RootStackParamList, 'Game'>>()
  const { params } = route

  // Estraggo i parametri dalla URL
  const routeGameId = params?.gameId ?? null
  const routeLeagueId = params?.leagueId ?? null
  const routeYear = params?.year ?? null
  const routeGroupId = params?.groupId ?? null




  const navigation = useNavigation()

  // Fetch team/day data for both sides when we have a game
  // Use the current group context (set by AppProvider) to derive group and year
  

  // legacy team/day hooks removed — we will prefer server-provided GameWrapper via gameService

  // gameService result (live teams + extra info) — prefer this when available
  const [gameWrapper, setGameWrapper] = useState<GameWrapper | null>(null)
  const [gameLoading, setGameLoading] = useState(false)
  const [gameError, setGameError] = useState<string | null>(null)

  useEffect(() => {
    
    if (!routeGameId || !routeGroupId || !routeLeagueId || !routeYear) {
      setGameWrapper(null)
      return
    }

    let mounted = true
    setGameLoading(true)
    setGameError(null)

    gameService.getGame({ groupId: routeGroupId, leagueId: routeLeagueId, year: parseInt(routeYear), gameId: routeGameId })
      .then(result => { if (mounted) setGameWrapper(result) })
      .catch(err => { if (mounted) setGameError(err?.message ?? String(err)) })
      .finally(() => { if (mounted) setGameLoading(false) })

    return () => { mounted = false }
  }, [routeGameId, routeLeagueId, routeYear, routeGroupId])

  // field rendering is driven directly by server GameWrapper (gameWrapper state)

  // Compute played score and MVP from gameService wrapper if available
  const title = gameWrapper ? `${gameWrapper.game?.home} vs ${gameWrapper.game?.away}` : 'Partita'
  const playedScore = gameWrapper?.game?.result ? `${gameWrapper.game.result.homeGoals}-${gameWrapper.game.result.awayGoals}` : null
  const homeHasAGoodDefense = gameWrapper?.game?.result?.home?.defensiveBonus
  const homeHasAGoodPeople = gameWrapper?.game?.result?.home?.goodPeople
  const homeHasDoneOwnGoal = gameWrapper?.game?.result?.home?.ownGoal
  const awayHasAGoodDefense = gameWrapper?.game?.result?.away?.defensiveBonus
  const awayHasAGoodPeople = gameWrapper?.game?.result?.away?.goodPeople
  const awayHasDoneOwnGoal = gameWrapper?.game?.result?.away?.ownGoal

  return (
    <View style={{ flex: 1 }}>
      <ScreenContent path="screens/game.tsx" title={title}>
        <YStack gap="$4" width="100%">
          <Card padding="$3" backgroundColor="$color2">
            {isLoading ? (
              <Text>Caricamento calendario...</Text>
            ) : error ? (
              <Text color="$red10">{error}</Text>
            ) : !gameWrapper ? (
              <Text>Nessuna partita disponibile</Text>
            ) : (
              <YStack gap="$2">
                <H3>{gameWrapper.game?.home} vs {gameWrapper.game?.away}</H3>
                <Text>{convertUTCToItalianTimezone(gameWrapper.game?.date.toISOString())}</Text>
                {playedScore && (
                  <Text fontSize="$4" fontWeight="bold">{playedScore}</Text>
                )}
                <XStack gap="$2">
                  <Button onPress={() => navigation.navigate('Formation' as never)} >Vai alla formazione</Button>
                  <Button variant="outlined" onPress={() => { /* TODO: refetch teams/day */ }}>Aggiorna</Button>
                </XStack>
              </YStack>
            )}
          </Card>

          {gameWrapper && (
            <Card padding="$3" backgroundColor="$color2">
              {gameLoading ? (
                <Text>Caricamento formazione partita...</Text>
              ) : gameWrapper ? (
                  <SoccerField gameWrapper={gameWrapper} />
              ) : (
                <Text>Nessun dato formazione disponibile</Text>
              )}
            </Card>
          )}

          <Separator />

          <Card padding="$3" backgroundColor="$color2">
            <Text fontSize="$3">Dettagli partita e statistiche</Text>
            {/* Placeholder: wire additional details, odds, lineups, events */}
          </Card>
        </YStack>
      </ScreenContent>
    </View>
  )
}

