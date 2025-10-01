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

  // Ottengo i valori correnti dall'app per il fallback
  const { selectedLeagueId, selectedYear } = useApp()
  const { group: currentGroup } = useCurrentGroup()
  const currentGroupId = typeof currentGroup === 'string' ? currentGroup : currentGroup?.id ?? null

  const game = useMemo(() => {
    // Se abbiamo un gameId dalla URL, cerchiamo il game corrispondente
    if (routeGameId) {
      // Cerca prima nei liveGames
      if (liveGames) {
        const found = liveGames.find(g => getRealGameId(g) === routeGameId)
        if (found) return found
      }
      
      // Cerca in nextDay
      if (nextDay && nextDay.games) {
        const found = nextDay.games.find(g => getRealGameId(g) === routeGameId)
        if (found) return found
      }
      
      // Cerca in liveDay
      if (liveDay && liveDay.games) {
        const found = liveDay.games.find(g => getRealGameId(g) === routeGameId)
        if (found) return found
      }
    }

    // fallback: prefer live, then nextDay first game, or last completed
    if (isLive && liveGames && liveGames.length > 0) return liveGames[0]
    if (nextDay && nextDay.games && nextDay.games.length > 0) return nextDay.games[0]
    if (liveDay && liveDay.games && liveDay.games.length > 0) return liveDay.games[0]
    return null
  }, [isLive, liveGames, nextDay, liveDay, routeGameId])


  const title = game ? `${game.home.name} vs ${game.away.name}` : 'Partita'

  const navigation = useNavigation()

  // Fetch team/day data for both sides when we have a game
  // Use the current group context (set by AppProvider) to derive group and year
  

  // legacy team/day hooks removed — we will prefer server-provided GameWrapper via gameService

  // gameService result (live teams + extra info) — prefer this when available
  const [gameWrapper, setGameWrapper] = useState<GameWrapper | null>(null)
  const [gameLoading, setGameLoading] = useState(false)
  const [gameError, setGameError] = useState<string | null>(null)

  useEffect(() => {
    // Usa i parametri dalla URL se disponibili, altrimenti fallback ai valori correnti
    const gameId = routeGameId ?? (game ? getRealGameId(game) : null)
    const leagueId = routeLeagueId ?? selectedLeagueId
    const year = routeYear ? parseInt(routeYear) : selectedYear
    const groupId = routeGroupId ?? currentGroupId
    
    if (!gameId || !groupId || !leagueId || !year) {
      setGameWrapper(null)
      return
    }

    let mounted = true
    setGameLoading(true)
    setGameError(null)

    gameService.getGame({ groupId, leagueId, year, gameId })
      .then(result => { if (mounted) setGameWrapper(result) })
      .catch(err => { if (mounted) setGameError(err?.message ?? String(err)) })
      .finally(() => { if (mounted) setGameLoading(false) })

    return () => { mounted = false }
  }, [routeGameId, routeLeagueId, routeYear, routeGroupId, selectedLeagueId, selectedYear, currentGroupId, game?.home.abbreviation, game?.away.abbreviation])

  // field rendering is driven directly by server GameWrapper (gameWrapper state)

  // Compute played score and MVP from gameService wrapper if available
  const playedScore = gameWrapper?.game?.result ? `${gameWrapper.game.result.homeGoals}-${gameWrapper.game.result.awayGoals}` : null
  const playedMVP = useMemo(() => {
    if (!gameWrapper?.teams) return null
    let best: { name: string; value: number } | null = null
    for (const t of gameWrapper.teams) {
      for (const p of (t.players || [])) {
        const val = (p as any).finalValue ?? null
        if (typeof val === 'number') {
          if (!best || val > best.value) best = { name: p.current?.name ?? 'Giocatore', value: val }
        }
      }
    }
    return best ? `${best.name} (${best.value})` : null
  }, [gameWrapper])

  // Memoize the options to prevent excessive API calls
  const playedGameOptions = useMemo(() => ({
    group: routeGroupId ?? currentGroupId,
    league: routeLeagueId ?? selectedLeagueId ?? null,
    year: routeYear ? parseInt(routeYear) : selectedYear ?? null,
    gameId: routeGameId ?? (game ? getRealGameId(game) : null),
    refreshInterval: 0
  }), [routeGroupId, routeLeagueId, routeYear, routeGameId, currentGroupId, selectedLeagueId, selectedYear, game?.home.abbreviation, game?.away.abbreviation])


  return (
    <View style={{ flex: 1 }}>
      <ScreenContent path="screens/game.tsx" title={title}>
        <YStack gap="$4" width="100%">
          <Card padding="$3" backgroundColor="$color2">
            {isLoading ? (
              <Text>Caricamento calendario...</Text>
            ) : error ? (
              <Text color="$red10">{error}</Text>
            ) : !game ? (
              <Text>Nessuna partita disponibile</Text>
            ) : (
              <YStack gap="$2">
                <H3>{game.home.name} vs {game.away.name}</H3>
                <Text>{convertUTCToItalianTimezone(game.date.toISOString())}</Text>
                {playedScore && (
                  <Text fontSize="$4" fontWeight="bold">{playedScore}</Text>
                )}
                {playedMVP && (
                  <Text>MVP: {playedMVP}</Text>
                )}
                <XStack gap="$2">
                  <Button onPress={() => navigation.navigate('Formation' as never)} >Vai alla formazione</Button>
                  <Button variant="outlined" onPress={() => { /* TODO: refetch teams/day */ }}>Aggiorna</Button>
                </XStack>
              </YStack>
            )}
          </Card>

          {game && (
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

// legacy lineup mappers removed — UI now consumes server GameWrapper directly
