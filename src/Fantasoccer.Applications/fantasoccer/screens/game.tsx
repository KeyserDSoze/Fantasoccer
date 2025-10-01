import React, { useMemo, useEffect, useState } from 'react'
import { View } from 'react-native'
import { ScreenContent } from 'components/layout/ScreenContent'
import { YStack, XStack, Text, H3, Separator, Button, Card } from 'tamagui'
import { useSerieACalendar } from 'hooks/useSerieACalendar'
import { SoccerField } from 'components/lineup'
import { type GameWrapper } from 'types/game'
import gameService from 'services/gameService'
import { useNavigation, useRoute } from '@react-navigation/native'
import { formatItalianDate } from 'utils/timezoneHelpers'
import type { RootStackParamList } from '../navigation'
import type { RouteProp } from '@react-navigation/native'

export default function GameScreen() {
  const {
    calendar,
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

  // Compute played score and bonuses from gameService wrapper if available
  const title = gameWrapper ? `${gameWrapper.game?.home} vs ${gameWrapper.game?.away}` : 'Partita'
  const playedScore = gameWrapper?.game?.result ? `${gameWrapper.game.result.homeGoals}-${gameWrapper.game.result.awayGoals}` : null
  
  // Trova la data della partita dal calendario di Serie A usando serieADay
  const gameDate = useMemo(() => {
    if (!gameWrapper?.serieADay || !calendar) return null
    
    // Cerca la giornata nel calendario che corrisponde a serieADay
    const targetDay = calendar.days?.find(day => day.serieADay === gameWrapper.serieADay)
    
    if (!targetDay || !targetDay.games || targetDay.games.length === 0) return null
    
    // Prende la prima partita in ordine di tempo di quella giornata
    const sortedGames = [...targetDay.games].sort((a, b) => a.date.getTime() - b.date.getTime())
    return sortedGames[0]?.date || null
  }, [gameWrapper?.serieADay, calendar])
  
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
                {gameDate && (
                  <Text>{formatItalianDate(gameDate)}</Text>
                )}
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

