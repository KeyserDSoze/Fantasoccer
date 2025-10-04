import { useState, useMemo } from 'react'
import { View, Pressable } from 'react-native'
import {
  YStack,
  XStack,
  Text,
  Card,
  Separator,
  Spinner,
  H3,
  H5,
  ScrollView,
  useMedia,
} from 'tamagui'
import {
  Trophy,
  Star,
  Users,
  Award,
  TrendingUp,
  AlertTriangle,
  Flame,
  Medal,
  Crown,
  Target,
} from '@tamagui/lucide-icons'
import { ScreenContent } from 'components/layout/ScreenContent'
import { useApp } from 'providers/AppProvider'
import { useHallOfFame } from 'hooks/useHallOfFame'
import { RankedTeam, WinningTeam, WinningPlayer, HallOfFameHelper } from 'types/hallOfFame'
import { useNavigation } from '@react-navigation/native'
import type { NavigationProp } from 'navigation'

// Helper to format season from year number
const formatSeasonFromYear = (yearNumber: number): string => {
  const baseYear = 2011
  const startYear = baseYear + yearNumber
  const endYear = startYear + 1
  return `${startYear}/${String(endYear).slice(-2)}`
}

// Helper to format multiple years
const formatYears = (years: number[]): string => {
  if (years.length === 0) return '-'
  return years.map(formatSeasonFromYear).join(', ')
}

// Helper to get role color
const getRoleColor = (role: string): string => {
  switch (role?.toLowerCase()) {
    case 'portiere':
    case 'goalkeeper':
      return '$orange10'
    case 'difensore':
    case 'defensor':
      return '$blue10'
    case 'centrocampista':
    case 'midfielder':
      return '$green10'
    case 'attaccante':
    case 'forward':
      return '$red10'
    default:
      return '$gray10'
  }
}

export default function HallOfFameScreen() {
  const { currentGroup, selectedLeagueId } = useApp()
  const navigation = useNavigation<NavigationProp>()
  const media = useMedia()
  const containerWidth = media.gtMd ? 1200 : '100%'

  const { hallOfFame, loading, error, refetch } = useHallOfFame(
    currentGroup?.id ?? null,
    selectedLeagueId ?? null
  )

  // Sort all-time rankings by points
  const sortedRankings = useMemo(() => {
    if (!hallOfFame?.allTimeRankings) return []
    return HallOfFameHelper.sortByPoints(hallOfFame.allTimeRankings)
  }, [hallOfFame])

  // Sort winning teams by total wins
  const sortedWinningTeams = useMemo(() => {
    if (!hallOfFame?.winningTeams) return []
    return HallOfFameHelper.sortWinningTeamsByWins(hallOfFame.winningTeams)
  }, [hallOfFame])

  // Get top 6 winning players
  const topWinningPlayers = useMemo(() => {
    if (!hallOfFame?.winningPlayers) return []
    const sorted = HallOfFameHelper.sortWinningPlayersByWins(hallOfFame.winningPlayers)
    return sorted.slice(0, 6)
  }, [hallOfFame])

  // Navigate to record game
  const navigateToRecordGame = () => {
    if (!hallOfFame?.recordGame || !currentGroup || !selectedLeagueId) return

    navigation.navigate('Game', {
      groupId: currentGroup.id,
      leagueId: selectedLeagueId,
      year: hallOfFame.recordGame.year.toString(),
      gameId: hallOfFame.recordGame.game.id,
    })
  }

  return (
    <View style={{ flex: 1 }}>
      <ScreenContent path="screens/hall-of-fame.tsx" title="Hall of Fame">
        <YStack gap="$4" width="100%" maxWidth={containerWidth} alignSelf="center" paddingHorizontal="$4">
          {/* Header */}
          <YStack gap="$2" alignItems="center">
            <H3 color="$yellow10" fontWeight="700" textAlign="center">
              <Trophy size={32} /> Hall of Fame
            </H3>
            <Text color="$color11" textAlign="center" fontSize="$3">
              I migliori di sempre della tua lega
            </Text>
          </YStack>

          <Separator />

          {/* Loading */}
          {loading && (
            <YStack alignItems="center" padding="$6" gap="$3">
              <Spinner size="large" color="$yellow10" />
              <Text color="$color11">Caricamento Hall of Fame...</Text>
            </YStack>
          )}

          {/* Error */}
          {error && !loading && (
            <Card backgroundColor="$red2" padding="$4" borderColor="$red8" borderWidth={1}>
              <XStack gap="$3" alignItems="center">
                <AlertTriangle size={24} color="$red10" />
                <YStack flex={1}>
                  <Text color="$red11" fontWeight="600">
                    Errore nel caricamento
                  </Text>
                  <Text color="$red10" fontSize="$2" marginTop="$1">
                    {error}
                  </Text>
                </YStack>
              </XStack>
            </Card>
          )}

          {/* No data */}
          {!loading && !error && !hallOfFame && (
            <Card backgroundColor="$orange2" padding="$4" borderColor="$orange8" borderWidth={1}>
              <XStack gap="$3" alignItems="center">
                <AlertTriangle size={24} color="$orange10" />
                <YStack flex={1}>
                  <Text color="$orange11" fontWeight="600">
                    Nessun dato disponibile
                  </Text>
                  <Text color="$orange10" fontSize="$2" marginTop="$1">
                    Seleziona un gruppo e una lega dalla barra di navigazione per visualizzare la Hall of Fame.
                  </Text>
                </YStack>
              </XStack>
            </Card>
          )}

          {/* Main content */}
          {!loading && hallOfFame && (
            <YStack gap="$6">
              {/* All-Time Rankings Section */}
              <YStack gap="$3">
                <XStack gap="$2" alignItems="center">
                  <Crown size={24} color="$yellow10" />
                  <H5 color="$color12" fontWeight="700">
                    Classifica Totale
                  </H5>
                </XStack>
                
                <Card padding="$4" backgroundColor="$color2" borderWidth={1} borderColor="$borderColor">
                  <ScrollView horizontal showsHorizontalScrollIndicator={false}>
                    <YStack gap="$2" minWidth="100%">
                      {/* Table Header */}
                      <XStack
                        paddingVertical="$2"
                        paddingHorizontal="$3"
                        backgroundColor="$color3"
                        borderRadius="$2"
                      >
                        <Text fontSize="$2" fontWeight="700" color="$color11" width={40} textAlign="center">
                          #
                        </Text>
                        <Text fontSize="$2" fontWeight="700" color="$color11" flex={1} minWidth={150}>
                          Squadra
                        </Text>
                        <Text fontSize="$2" fontWeight="700" color="$color11" width={60} textAlign="center">
                          Punti
                        </Text>
                        <Text fontSize="$2" fontWeight="700" color="$color11" width={50} textAlign="center">
                          V
                        </Text>
                        <Text fontSize="$2" fontWeight="700" color="$color11" width={50} textAlign="center">
                          P
                        </Text>
                        <Text fontSize="$2" fontWeight="700" color="$color11" width={50} textAlign="center">
                          S
                        </Text>
                        <Text fontSize="$2" fontWeight="700" color="$color11" width={70} textAlign="center">
                          Reti
                        </Text>
                        <Text fontSize="$2" fontWeight="700" color="$color11" width={80} textAlign="center">
                          Valore
                        </Text>
                      </XStack>

                      {/* Table Rows */}
                      {sortedRankings.map((team, index) => (
                        <RankingRow key={team.owner} team={team} position={index + 1} />
                      ))}
                    </YStack>
                  </ScrollView>
                </Card>
              </YStack>

              {/* Winners Section */}
              <YStack gap="$3">
                <XStack gap="$2" alignItems="center">
                  <Medal size={24} color="$green10" />
                  <H5 color="$color12" fontWeight="700">
                    Albo d'Oro
                  </H5>
                </XStack>

                <Card padding="$4" backgroundColor="$color2" borderWidth={1} borderColor="$borderColor">
                  <YStack gap="$2">
                    {/* Table Header */}
                    <XStack
                      paddingVertical="$2"
                      paddingHorizontal="$3"
                      backgroundColor="$color3"
                      borderRadius="$2"
                    >
                      <Text fontSize="$2" fontWeight="700" color="$color11" flex={1}>
                        Squadra
                      </Text>
                      <Text fontSize="$2" fontWeight="700" color="$color11" flex={1}>
                        Vincitore
                      </Text>
                      <Text fontSize="$2" fontWeight="700" color="$color11" width={80} textAlign="center">
                        Vittorie
                      </Text>
                      <Text fontSize="$2" fontWeight="700" color="$color11" flex={2}>
                        Anni
                      </Text>
                    </XStack>

                    {/* Table Rows */}
                    {sortedWinningTeams.map((team) => (
                      <WinningTeamRow key={team.owner} team={team} />
                    ))}
                  </YStack>
                </Card>
              </YStack>

              {/* Winning Players Section */}
              <YStack gap="$3">
                <XStack gap="$2" alignItems="center">
                  <Star size={24} color="$blue10" />
                  <H5 color="$color12" fontWeight="700">
                    Giocatori Vincenti
                  </H5>
                </XStack>

                <Card padding="$4" backgroundColor="$color2" borderWidth={1} borderColor="$borderColor">
                  <YStack gap="$2">
                    {/* Table Header */}
                    <XStack
                      paddingVertical="$2"
                      paddingHorizontal="$3"
                      backgroundColor="$color3"
                      borderRadius="$2"
                    >
                      <Text fontSize="$2" fontWeight="700" color="$color11" flex={1}>
                        Giocatore
                      </Text>
                      <Text fontSize="$2" fontWeight="700" color="$color11" width={80} textAlign="center">
                        Vittorie
                      </Text>
                      <Text fontSize="$2" fontWeight="700" color="$color11" flex={2}>
                        Anni
                      </Text>
                    </XStack>

                    {/* Table Rows */}
                    {topWinningPlayers.map((player) => (
                      <WinningPlayerRow key={player.player.name} player={player} />
                    ))}
                  </YStack>
                </Card>
              </YStack>

              {/* Record Game Section */}
              {hallOfFame.recordGame && (
                <Pressable onPress={navigateToRecordGame}>
                  <Card
                    padding="$4"
                    backgroundColor="$purple2"
                    borderWidth={2}
                    borderColor="$purple8"
                    pressStyle={{ scale: 0.98, opacity: 0.9 }}
                    cursor="pointer"
                  >
                    <YStack gap="$3">
                      <XStack gap="$2" alignItems="center">
                        <Flame size={28} color="$purple10" />
                        <H5 color="$purple11" fontWeight="700">
                          Partita della Vita
                        </H5>
                      </XStack>

                      <YStack gap="$2">
                        <Text fontSize="$5" fontWeight="600" color="$color12" textAlign="center">
                          {hallOfFame.recordGame.game.home} vs {hallOfFame.recordGame.game.away}
                        </Text>

                        {hallOfFame.recordGame.game.result && (
                          <>
                            <XStack gap="$4" justifyContent="center" alignItems="center">
                              <YStack alignItems="center" gap="$1">
                                <Text fontSize="$7" fontWeight="700" color="$green10">
                                  {hallOfFame.recordGame.game.result.home.value.toFixed(2)}
                                </Text>
                                <Text fontSize="$2" color="$color10">
                                  {hallOfFame.recordGame.game.result.homeGoals} gol
                                </Text>
                              </YStack>

                              <Text fontSize="$6" fontWeight="700" color="$color11">
                                -
                              </Text>

                              <YStack alignItems="center" gap="$1">
                                <Text fontSize="$7" fontWeight="700" color="$red10">
                                  {hallOfFame.recordGame.game.result.away.value.toFixed(2)}
                                </Text>
                                <Text fontSize="$2" color="$color10">
                                  {hallOfFame.recordGame.game.result.awayGoals} gol
                                </Text>
                              </YStack>
                            </XStack>

                            <Text fontSize="$2" color="$purple10" textAlign="center" marginTop="$2">
                              Stagione {formatSeasonFromYear(hallOfFame.recordGame.year)}
                            </Text>
                          </>
                        )}
                      </YStack>

                      <Text fontSize="$2" color="$purple10" textAlign="center" fontWeight="500">
                        👆 Tap per vedere i dettagli della partita
                      </Text>
                    </YStack>
                  </Card>
                </Pressable>
              )}

              {/* Record Player (if available) */}
              {hallOfFame.recordPlayer && (
                <Card padding="$4" backgroundColor="$orange2" borderWidth={1} borderColor="$orange8">
                  <YStack gap="$3">
                    <XStack gap="$2" alignItems="center">
                      <Award size={24} color="$orange10" />
                      <H5 color="$orange11" fontWeight="700">
                        Giocatore Record
                      </H5>
                    </XStack>

                    <XStack gap="$3" justifyContent="space-between" alignItems="center">
                      <YStack flex={1}>
                        <Text fontSize="$5" fontWeight="600" color="$color12">
                          {hallOfFame.recordPlayer.player.name}
                        </Text>
                        <Text fontSize="$3" color="$color10">
                          {hallOfFame.recordPlayer.player.team}
                        </Text>
                      </YStack>
                      <YStack alignItems="flex-end">
                        <Text fontSize="$6" fontWeight="700" color="$orange10">
                          {hallOfFame.recordPlayer.points.toFixed(2)}
                        </Text>
                        <Text fontSize="$2" color="$color10">
                          punti totali
                        </Text>
                      </YStack>
                    </XStack>
                  </YStack>
                </Card>
              )}

              {/* Player with Most Points in Year (if available) */}
              {hallOfFame.playerWithMostPointsInYear && (
                <Card padding="$4" backgroundColor="$blue2" borderWidth={1} borderColor="$blue8">
                  <YStack gap="$3">
                    <XStack gap="$2" alignItems="center">
                      <Target size={24} color="$blue10" />
                      <H5 color="$blue11" fontWeight="700">
                        Migliore della Stagione
                      </H5>
                    </XStack>

                    <XStack gap="$3" justifyContent="space-between" alignItems="center">
                      <YStack flex={1}>
                        <Text fontSize="$5" fontWeight="600" color="$color12">
                          {hallOfFame.playerWithMostPointsInYear.player.name}
                        </Text>
                        <Text fontSize="$3" color="$color10">
                          {hallOfFame.playerWithMostPointsInYear.player.team}
                        </Text>
                        <Text fontSize="$2" color="$blue10" marginTop="$1">
                          Stagione {formatSeasonFromYear(hallOfFame.playerWithMostPointsInYear.year)}
                        </Text>
                      </YStack>
                      <YStack alignItems="flex-end">
                        <Text fontSize="$6" fontWeight="700" color="$blue10">
                          {hallOfFame.playerWithMostPointsInYear.points.toFixed(2)}
                        </Text>
                        <Text fontSize="$2" color="$color10">
                          punti
                        </Text>
                      </YStack>
                    </XStack>
                  </YStack>
                </Card>
              )}
            </YStack>
          )}

          {/* Info */}
          {!loading && hallOfFame && (
            <Card backgroundColor="$blue2" padding="$3" marginTop="$2">
              <Text fontSize="$2" color="$blue11" textAlign="center">
                💡 Questa è la Hall of Fame di tutti i tempi della tua lega. Include statistiche cumulative 
                di tutte le stagioni disputate.
              </Text>
            </Card>
          )}
        </YStack>
      </ScreenContent>
    </View>
  )
}

// ============================================
// SUB-COMPONENTS
// ============================================

function RankingRow({ team, position }: { team: RankedTeam; position: number }) {
  const goalDiff = HallOfFameHelper.getGoalDifference(team)
  const isTop3 = position <= 3

  return (
    <XStack
      paddingVertical="$2"
      paddingHorizontal="$3"
      backgroundColor={isTop3 ? '$yellow2' : 'transparent'}
      borderRadius="$2"
      borderWidth={isTop3 ? 1 : 0}
      borderColor={isTop3 ? '$yellow8' : 'transparent'}
    >
      <Text fontSize="$3" fontWeight={isTop3 ? '700' : '500'} color={isTop3 ? '$yellow10' : '$color11'} width={40} textAlign="center">
        {position === 1 && '🥇'}
        {position === 2 && '🥈'}
        {position === 3 && '🥉'}
        {position > 3 && position}
      </Text>
      <YStack flex={1} minWidth={150}>
        <Text fontSize="$3" fontWeight="600" color="$color12">
          {team.name}
        </Text>
        <Text fontSize="$1" color="$color10">
          {team.owner}
        </Text>
      </YStack>
      <Text fontSize="$3" fontWeight="700" color="$green10" width={60} textAlign="center">
        {team.points.toFixed(1)}
      </Text>
      <Text fontSize="$3" color="$color11" width={50} textAlign="center">
        {team.victories}
      </Text>
      <Text fontSize="$3" color="$color11" width={50} textAlign="center">
        {team.draws}
      </Text>
      <Text fontSize="$3" color="$color11" width={50} textAlign="center">
        {team.defeats}
      </Text>
      <Text fontSize="$3" color={goalDiff >= 0 ? '$green10' : '$red10'} width={70} textAlign="center">
        {goalDiff >= 0 ? '+' : ''}{goalDiff}
      </Text>
      <Text fontSize="$3" color="$blue10" width={80} textAlign="center">
        {team.valueAssets}
      </Text>
    </XStack>
  )
}

function WinningTeamRow({ team }: { team: WinningTeam }) {
  const totalWins = HallOfFameHelper.getTotalWins(team)
  const allYears = HallOfFameHelper.getAllYears(team.wins)

  return (
    <XStack paddingVertical="$2" paddingHorizontal="$3" borderBottomWidth={1} borderBottomColor="$borderColor">
      <Text fontSize="$3" fontWeight="600" color="$color12" flex={1}>
        {team.teamName}
      </Text>
      <Text fontSize="$3" color="$color11" flex={1}>
        {team.owner}
      </Text>
      <Text fontSize="$3" fontWeight="700" color="$green10" width={80} textAlign="center">
        {totalWins}
      </Text>
      <Text fontSize="$2" color="$color10" flex={2}>
        {formatYears(allYears)}
      </Text>
    </XStack>
  )
}

function WinningPlayerRow({ player }: { player: WinningPlayer }) {
  const totalWins = HallOfFameHelper.getTotalPlayerWins(player)
  const allYears = HallOfFameHelper.getAllYears(player.wins)

  return (
    <XStack paddingVertical="$2" paddingHorizontal="$3" borderBottomWidth={1} borderBottomColor="$borderColor">
      <YStack flex={1}>
        <Text fontSize="$3" fontWeight="600" color="$color12">
          {player.player.name}
        </Text>
        <Text fontSize="$2" color="$color10">
          {player.player.team}
        </Text>
      </YStack>
      <Text fontSize="$3" fontWeight="700" color="$green10" width={80} textAlign="center">
        {totalWins}
      </Text>
      <Text fontSize="$2" color="$color10" flex={2}>
        {formatYears(allYears)}
      </Text>
    </XStack>
  )
}
