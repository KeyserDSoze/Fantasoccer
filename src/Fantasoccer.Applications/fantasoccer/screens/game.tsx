import React, { useMemo, useEffect, useState } from 'react'
import { View } from 'react-native'
import { ScreenContent } from 'components/layout/ScreenContent'
import { YStack, XStack, Text, H3, Separator, Button, Card } from 'tamagui'
import { useSerieACalendar, formatGameDate } from 'hooks/useSerieACalendar'
import { SoccerFieldHorizontal } from 'components/lineup'
import { useMedia } from 'tamagui'
import type { GameWrapper, LiveTeam } from 'types/game'
import gameService from 'services/gameService'
import { useNavigation, useRoute } from '@react-navigation/native'
import { useCurrentGroup } from 'hooks/useAppData'
import { useApp } from 'providers/AppProvider'

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

  const route = useRoute()

  // If the calendar widget navigated with a CalendarGame, prefer that
  const routeCalendarGame = (route as any)?.params?.calendarGame ?? null
  const routeDayNumber = (route as any)?.params?.dayNumber ?? null

  const game = useMemo(() => {
    if (routeCalendarGame) {
      return routeCalendarGame
    }

    // fallback: prefer live, then nextDay first game, or last completed
    if (isLive && liveGames && liveGames.length > 0) return liveGames[0]
    if (nextDay && nextDay.games && nextDay.games.length > 0) return nextDay.games[0]
    if (liveDay && liveDay.games && liveDay.games.length > 0) return liveDay.games[0]
    return null
  }, [isLive, liveGames, nextDay, liveDay, routeCalendarGame])


  const title = game ? `${game.home} vs ${game.away}` : 'Partita'

  const navigation = useNavigation()

  // Fetch team/day data for both sides when we have a game
  // Use the current group context (set by AppProvider) to derive group and year
  const { group: currentGroup } = useCurrentGroup()
  const { selectedYear, selectedLeagueId } = useApp()
  const group = currentGroup?.id ?? null
  const year = selectedYear ?? null

  // legacy team/day hooks removed — we will prefer server-provided GameWrapper via gameService

  // gameService result (live teams + extra info) — prefer this when available
  const [gameWrapper, setGameWrapper] = useState<GameWrapper | null>(null)
  const [gameLoading, setGameLoading] = useState(false)
  const [gameError, setGameError] = useState<string | null>(null)

  useEffect(() => {
    // need group, league and year + the calendar game id
    const gid = (route as any)?.params?.calendarGame?.id ?? game?.id
    if (!gid || !group || !selectedLeagueId || !selectedYear) {
      setGameWrapper(null)
      return
    }

    let mounted = true
    setGameLoading(true)
    setGameError(null)

    gameService.getGame({ groupId: group, leagueId: selectedLeagueId, year: selectedYear, gameId: gid })
      .then(result => { if (mounted) setGameWrapper(result) })
      .catch(err => { if (mounted) setGameError(err?.message ?? String(err)) })
      .finally(() => { if (mounted) setGameLoading(false) })

    return () => { mounted = false }
  }, [group, selectedLeagueId, selectedYear, game, route])

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
    group,
    league: selectedLeagueId ?? null,
    year: selectedYear ?? null,
    day: routeDayNumber ?? null,
    gameId: routeCalendarGame?.id ?? null,
    refreshInterval: 0
  }), [group, selectedLeagueId, selectedYear, routeDayNumber, routeCalendarGame?.id])


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
                <H3>{game.home} vs {game.away}</H3>
                <Text>{formatGameDate(game.date ?? null)}</Text>
                <Text>{game.venue ?? ''}</Text>
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
                  <SoccerFieldHorizontal gameWrapper={gameWrapper} />
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
