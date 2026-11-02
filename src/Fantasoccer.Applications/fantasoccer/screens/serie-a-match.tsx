import { useMemo, useState } from 'react'
import { useNavigation, useRoute, type RouteProp } from '@react-navigation/native'
import { AlertTriangle, ArrowLeft } from '@tamagui/lucide-icons-2'
import { Button, Card, ScrollView, Separator, Text, XStack, YStack, useMedia } from 'tamagui'
import { ScreenContent } from 'components/layout/ScreenContent'
import { SplashScreen } from 'components/layout/SplashScreen'
import { SoccerField } from 'components/lineup'
import { LiveScore } from 'components/live/LiveScore'
import { MatchEvents } from 'components/seriea/MatchEvents'
import { matchStatusLabel } from 'components/seriea/SerieAMatchRow'
import { useApp } from 'providers/AppProvider'
import { useFieldZoom } from 'hooks/useFieldZoom'
import { useSerieAMatch } from 'hooks/useSerieAMatch'
import type { RootStackParamList } from '../navigation'
import type { EventPlayer } from 'utils/matchEvents'
import { lineupNote, toFieldGame } from 'utils/serieAMatch'
import { formatItalianDate } from 'utils/timezoneHelpers'
import { RealCalendarHelper } from 'types/serieA'
import { formatSeasonFromYear } from 'utils/season'

const parse = (value?: string | number | null) => {
  const number = value === undefined || value === null ? NaN : Number(value)
  return Number.isInteger(number) ? number : null
}

export default function SerieAMatchScreen() {
  const { params } = useRoute<RouteProp<RootStackParamList, 'SerieAMatch'>>()
  // Another match reuses this screen: start from scratch rather than showing the previous lineup.
  return <SerieAMatchContent key={JSON.stringify([params?.year, params?.day, params?.ordinal])} />
}

function SerieAMatchContent() {
  const { params } = useRoute<RouteProp<RootStackParamList, 'SerieAMatch'>>()
  const navigation = useNavigation<any>()
  const media = useMedia()
  const { currentGroup, selectedLeagueId } = useApp()
  const year = parse(params?.year); const day = parse(params?.day); const ordinal = parse(params?.ordinal)

  const leagues = currentGroup?.leagues ?? []
  const requestedLeague = params?.leagueId ?? selectedLeagueId ?? leagues.find(value => value.isMain)?.id ?? null
  // The league of the user (groups load after the screen opens) unless another is chosen here; null = no fantasy teams.
  const [chosenLeague, setLeagueId] = useState<string | null | undefined>(undefined)
  const leagueId = chosenLeague !== undefined ? chosenLeague : (leagues.some(value => value.id === requestedLeague) ? requestedLeague : null)
  const groupId = leagueId ? (params?.groupId ?? currentGroup?.id ?? null) : null

  const { match, isLoading, error } = useSerieAMatch({ year, day, ordinal, groupId, leagueId: groupId ? leagueId : null })
  const { zoomLevel, increaseZoom, decreaseZoom, resetZoom, minZoom, maxZoom } = useFieldZoom({ isMobile: media.sm })

  const fieldGame = useMemo(() => (match ? toFieldGame(match) : null), [match])
  const sources = match ? [match.homeLineupSource, match.awayLineupSource] : []
  const note = match ? lineupNote(sources) : null
  const hasLineup = sources.some(source => source !== 'unavailable')
  const events = useMemo(() => {
    const side = (index: number): EventPlayer[] => (match?.lineup.teams[index]?.players ?? [])
      .filter(player => player.vote)
      .map(player => ({ key: player.current.playerId ?? player.current.name, name: player.current.name, vote: player.vote! }))
    return { home: side(0), away: side(1) }
  }, [match])

  const back = () => (navigation.canGoBack() ? navigation.goBack() : navigation.navigate('SerieA'))
  const game = match?.game
  const playing = match?.isPlaying ?? false
  const hasScore = game?.homeGoals != null && game?.awayGoals != null
  const showScore = !!game && hasScore && (playing || RealCalendarHelper.isGamePlayed(game))

  return (
    <ScreenContent title="Partita Serie A" path="serie-a/match">
      <YStack gap="$4" width="100%" maxWidth={1400} alignSelf="center">
        <XStack>
          <Button size="$3" chromeless icon={ArrowLeft} onPress={back} accessibilityLabel="Torna alla Serie A">Serie A</Button>
        </XStack>

        {year === null || day === null || ordinal === null ? (
          <Text color="$color10">Partita non valida. Aprila dalla sezione Serie A.</Text>
        ) : isLoading && !match ? (
          <SplashScreen compact message="Caricamento partita…" steps={[{ id: 'match', label: 'Formazioni e voti', status: 'pending' }]} />
        ) : error && !match ? (
          <YStack alignItems="center" gap="$3" paddingVertical="$6">
            <AlertTriangle size={32} color="$red10" />
            <Text color="$color10" textAlign="center">{error}</Text>
          </YStack>
        ) : match && game && fieldGame ? (
          <>
            <Card padding="$3" backgroundColor="$color2" borderRadius="$4" gap="$2">
              <Text fontSize="$2" color="$color10" textAlign="center">
                {formatSeasonFromYear(match.year)} · {match.serieADay}ª giornata
              </Text>
              <XStack alignItems="center" justifyContent="space-between" gap="$3">
                <Text flex={1} fontSize="$6" fontWeight="700" color="$color12" textAlign="center" numberOfLines={2}>{game.home.name}</Text>
                <YStack alignItems="center" gap="$1" minWidth={110}>
                  <LiveScore active={playing}>
                    <Text fontSize="$8" fontWeight="800" color={playing ? '$green10' : '$color12'}>
                      {showScore ? `${game.homeGoals} - ${game.awayGoals}` : 'vs'}
                    </Text>
                  </LiveScore>
                  <Text fontSize="$3" color="$color10" textAlign="center">
                    {game.date ? formatItalianDate(new Date(game.date).toISOString()) : 'Da definire'}
                  </Text>
                  {!playing && <Text fontSize="$2" color={game.delayed ? '$yellow10' : '$color10'}>{matchStatusLabel(game, playing)}</Text>}
                </YStack>
                <Text flex={1} fontSize="$6" fontWeight="700" color="$color12" textAlign="center" numberOfLines={2}>{game.away.name}</Text>
              </XStack>
            </Card>

            {leagues.length > 0 && (
              <Card padding="$3" backgroundColor="$color2" borderRadius="$4" gap="$2">
                <Text fontSize="$2" fontWeight="700" color="$color10">Squadre fanta</Text>
                <ScrollView horizontal showsHorizontalScrollIndicator={false}>
                  <XStack gap="$2">
                    <Button size="$3" theme={leagueId === null ? 'blue' : undefined} variant={leagueId === null ? undefined : 'outlined'}
                      onPress={() => setLeagueId(null)} accessibilityState={{ selected: leagueId === null }}>Nessuna</Button>
                    {leagues.map(league => (
                      <Button key={league.id} size="$3" theme={leagueId === league.id ? 'blue' : undefined} variant={leagueId === league.id ? undefined : 'outlined'}
                        onPress={() => setLeagueId(league.id)} accessibilityState={{ selected: leagueId === league.id }}>{league.name}</Button>
                    ))}
                  </XStack>
                </ScrollView>
              </Card>
            )}

            <Card padding="$3" backgroundColor="$color2" borderRadius="$4" gap="$3">
              {note && <Text fontSize="$3" color="$color11" textAlign="center">{note}</Text>}
              {hasLineup && (
                <SoccerField
                  gameWrapper={fieldGame}
                  year={match.year}
                  editMode="view"
                  customZoom={zoomLevel}
                  zoomControls={{ zoomLevel, onIncrease: increaseZoom, onDecrease: decreaseZoom, onReset: resetZoom, minZoom, maxZoom }}
                />
              )}
            </Card>

            {hasLineup && (
              <Card padding="$3" backgroundColor="$color2" borderRadius="$4" gap="$2">
                <Text fontSize="$5" fontWeight="700" color="$color12">Eventi</Text>
                <Separator />
                <MatchEvents includeSubstitutions home={{ name: game.home.name, players: events.home }} away={{ name: game.away.name, players: events.away }} />
              </Card>
            )}
          </>
        ) : null}
      </YStack>
    </ScreenContent>
  )
}
