import React, { useMemo, useEffect, useState } from 'react'
import { View } from 'react-native'
import { ScreenContent } from 'components/layout/ScreenContent'
import { YStack, XStack, Text, H3, Separator, Button, Card } from 'tamagui'
import { useSerieACalendar, formatGameDate } from 'hooks/useSerieACalendar'
import { SoccerFieldHorizontal, SoccerFieldMobile } from 'components/lineup'
import { useMedia } from 'tamagui'
import { useTeamDayByComponents } from 'hooks/useTeam'
import { TeamKeyHelper } from 'types/team'
import { LineupFieldData, LineupPlayerSlot, LineupPlayerInfo } from 'types/lineup'
import type { GameWrapper, LiveTeam, LivePlayer } from 'types/game'
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

  // For now, try to derive basketId/email from game if present (best-effort); these will be null if not available
  const homeBasket = game?.homeBasketId ?? null
  const awayBasket = game?.awayBasketId ?? null
  const homeEmail = game?.homeOwnerEmail ?? null
  const awayEmail = game?.awayOwnerEmail ?? null

  const homeTeamDay = useTeamDayByComponents({ group, year, basketId: homeBasket, day: null, email: homeEmail, refreshInterval: 0 })
  const awayTeamDay = useTeamDayByComponents({ group, year, basketId: awayBasket, day: null, email: awayEmail, refreshInterval: 0 })

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

  const fieldData: LineupFieldData | null = useMemo(() => {
    if (!game) return null

    // If we have enhanced team/day data, map players to lineup structure
    const mapTeam = (teamResult: any, teamLabel: string): any => {
      // Prefer the live team returned by the game API
      const liveTeam: LiveTeam | undefined = gameWrapper?.teams?.find(t => (t.name ?? '').toLowerCase() === (teamLabel ?? '').toLowerCase())

      if (liveTeam) {
        // Convert LiveTeam players into a simplified enhanced.players shape expected below
        const enhanced = {
          players: (liveTeam.players || []).map((lp: any) => {
            const p = lp.current || {}
            return {
              key: p.key ?? null,
              name: p.name ?? 'Giocatore',
              role: p.role ?? p.position ?? 'CEN',
              roleLabel: '',
              fantasyRoleLabel: undefined,
              team: { name: liveTeam.name },
              number: p.number ?? null,
              isCaptain: p.isCaptain ?? false,
              vote: lp.vote ?? null,
              imageKey: p.imageKey ?? null,
              availability: p.availability ?? null,
              participation: p.participation ?? null
            }
          })
        }

        const roleOrder = ['POR','DIF','CEN','ATT']
        const rows = roleOrder.map((r, idx) => {
          const slots: LineupPlayerSlot[] = (enhanced.players || [])
            .filter((p: any) => p.role && p.role.toString().startsWith(r.substring(0,3)))
            .slice(0, 4)
            .map((p: any, i: number) => ({ slotId: `${r}-${i}`, player: mapPlayerToLineupInfo(p) }))

          return { rowId: `row-${idx}`, slots }
        })

        const bench = (enhanced.players || []).slice(11, 16).map((p: any, i: number) => ({ slotId: `bench-${i}`, player: mapPlayerToLineupInfo(p) }))
        const tribune = (enhanced.players || []).slice(16, 22).map((p: any, i: number) => ({ slotId: `tribune-${i}`, player: mapPlayerToLineupInfo(p) }))

        return {
          teamName: teamLabel,
          moduleLabel: '4-3-3',
          rows,
          bench,
          tribune
        }
      }

      // Fallback to existing teamResult (from useTeamDayByComponents)
      if (!teamResult || (!teamResult.team && !teamResult.enhancedTeam)) {
        return {
          teamName: teamLabel,
          moduleLabel: '4-3-3',
          rows: [],
          bench: [],
          tribune: []
        }
      }

      const enhanced = teamResult.enhancedTeam || teamResult.team

      // Simple mapping: group players by role into rows; this is coarse and can be improved
      const roleOrder = ['POR','DIF','CEN','ATT']

      const rows = roleOrder.map((r, idx) => {
        const slots: LineupPlayerSlot[] = (enhanced.players || [])
          .filter((p: any) => p.role && p.role.toString().startsWith(r.substring(0,3)))
          .slice(0, 4)
          .map((p: any, i: number) => ({ slotId: `${r}-${i}`, player: mapPlayerToLineupInfo(p) }))

        return { rowId: `row-${idx}`, slots }
      })

      const bench = (enhanced.players || []).slice(11, 16).map((p: any, i: number) => ({ slotId: `bench-${i}`, player: mapPlayerToLineupInfo(p) }))
      const tribune = (enhanced.players || []).slice(16, 22).map((p: any, i: number) => ({ slotId: `tribune-${i}`, player: mapPlayerToLineupInfo(p) }))

      return {
        teamName: teamLabel,
        moduleLabel: '4-3-3',
        rows,
        bench,
        tribune
      }
    }

    const homeLabel = typeof game.home === 'string' ? game.home : (game.home?.name ?? '')
    const awayLabel = typeof game.away === 'string' ? game.away : (game.away?.name ?? '')

    return {
      home: mapTeam(homeTeamDay, homeLabel),
      away: mapTeam(awayTeamDay, awayLabel)
    }
  }, [game, homeTeamDay, awayTeamDay, calendar])

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
              {fieldData ? (
                media.sm ? (
                  <SoccerFieldMobile data={fieldData} />
                ) : (
                  <SoccerFieldHorizontal data={fieldData} />
                )
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

// TODO: Replace with real mapping from API/game to LineupFieldData
function mockFieldDataForGame(game: any) {
  // Build a minimal LineupFieldData shaped object from game data
  return {
    home: {
      teamName: game.home,
      moduleLabel: '4-3-3',
      rows: [],
      bench: [],
      tribune: [],
    },
    away: {
      teamName: game.away,
      moduleLabel: '4-3-3',
      rows: [],
      bench: [],
      tribune: [],
    },
  }
}

// Minimal mapper from team player object to LineupPlayerInfo used by field components
function mapPlayerToLineupInfo(p: any): LineupPlayerInfo {
  return {
    id: p?.key ?? p?.name ?? `p-${Math.random().toString(36).substr(2, 6)}`,
    name: p?.name ?? 'Giocatore',
    key: p?.key ?? null,
    role: p?.role ?? 'CEN',
    roleLabel: p?.roleLabel ?? '',
    fantasyRoleLabel: p?.fantasyRoleLabel ?? undefined,
    teamName: p?.team?.name ?? '',
    jerseyNumber: p?.number ?? null,
    isCaptain: p?.isCaptain ?? false,
    status: 'starter',
    vote: p?.vote?.value ?? null,
    voteIsFinal: p?.vote?.isFinal ?? false,
    match: null,
    events: [],
    note: null,
    imageKey: p?.imageKey ?? null,
    availability: p?.availability ?? null,
    participation: p?.participation ?? null
  }
}
