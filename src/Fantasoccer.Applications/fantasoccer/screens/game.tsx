import React, { useMemo, useEffect, useState } from 'react'
import { View } from 'react-native'
import { ScreenContent } from 'components/layout/ScreenContent'
import { YStack, XStack, Text, H3, Separator, Button, Card } from 'tamagui'
import { useSerieACalendar } from 'hooks/useSerieACalendar'
import { SoccerField } from 'components/lineup'
import { useMedia } from 'tamagui'
import { type GameWrapper } from 'types/game'
import gameService from 'services/gameService'
import { useNavigation, useRoute } from '@react-navigation/native'
import { formatItalianDate } from 'utils/timezoneHelpers'
import type { RootStackParamList } from '../navigation'
import type { RouteProp } from '@react-navigation/native'
import { TeamLogo } from 'components/team/TeamLogo'
import { TeamBonuses } from 'components/icons/BonusIcons'

export default function GameScreen() {
  const media = useMedia()
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
                {playedScore && (
                  <YStack gap="$3">
                    {/* Layout responsive: mobile vs desktop */}
                    {media.gtSm ? (
                      // Desktop Layout - tutto su una linea
                      <XStack alignItems="center" justifyContent="space-between" gap="$3" padding="$2">
                        {/* Scudetto Home */}
                        <TeamLogo 
                          groupId={routeGroupId || ''} 
                          year={routeYear || ''} 
                          ownerEmail={gameWrapper.game?.homeOwner || ''}
                          size={40}
                        />
                        
                        {/* Nome squadra home */}
                        <Text fontSize="$3" fontWeight="600" textAlign="center" minWidth={120}>
                          {gameWrapper.game?.home}
                        </Text>
                        
                        {/* Punteggio home */}
                        <Text fontSize="$4" fontWeight="bold" minWidth={60} textAlign="center">
                          {gameWrapper.game?.result?.home?.value || 0} pt
                        </Text>
                        
                        {/* Bonus home */}
                        <View style={{ minWidth: 80, alignItems: 'center' }}>
                          <TeamBonuses 
                            hasGoodDefense={homeHasAGoodDefense}
                            hasGoodPeople={homeHasAGoodPeople}
                            hasOwnGoal={homeHasDoneOwnGoal}
                            iconSize={16}
                          />
                        </View>
                        
                        {/* Goal e data centrale */}
                        <YStack alignItems="center" gap="$1" minWidth={100}>
                          <Text fontSize="$6" fontWeight="bold">
                            {gameWrapper.game?.result?.homeGoals} - {gameWrapper.game?.result?.awayGoals}
                          </Text>
                          {gameDate && (
                            <Text fontSize="$2" color="$color11" textAlign="center">
                              {formatItalianDate(gameDate)}
                            </Text>
                          )}
                        </YStack>
                        
                        {/* Bonus away */}
                        <View style={{ minWidth: 80, alignItems: 'center' }}>
                          <TeamBonuses 
                            hasGoodDefense={awayHasAGoodDefense}
                            hasGoodPeople={awayHasAGoodPeople}
                            hasOwnGoal={awayHasDoneOwnGoal}
                            iconSize={16}
                          />
                        </View>
                        
                        {/* Punteggio away */}
                        <Text fontSize="$4" fontWeight="bold" minWidth={60} textAlign="center">
                          {gameWrapper.game?.result?.away?.value || 0} pt
                        </Text>
                        
                        {/* Nome squadra away */}
                        <Text fontSize="$3" fontWeight="600" textAlign="center" minWidth={120}>
                          {gameWrapper.game?.away}
                        </Text>
                        
                        {/* Scudetto Away */}
                        <TeamLogo 
                          groupId={routeGroupId || ''} 
                          year={routeYear || ''} 
                          ownerEmail={gameWrapper.game?.awayOwner || ''}
                          size={40}
                        />
                      </XStack>
                    ) : (
                      // Mobile Layout - layout esistente (verticale)
                      <XStack justifyContent="space-between" alignItems="center" gap="$4">
                        {/* Squadra di casa */}
                        <YStack alignItems="center" gap="$2" flex={1}>
                          <TeamLogo 
                            groupId={routeGroupId || ''} 
                            year={routeYear || ''} 
                            ownerEmail={gameWrapper.game?.homeOwner || ''}
                            size={50}
                          />
                          <Text fontSize="$2" textAlign="center" numberOfLines={2}>
                            {gameWrapper.game?.home}
                          </Text>
                          <YStack alignItems="center" gap="$1">
                            <Text fontSize="$5" fontWeight="bold">
                              {gameWrapper.game?.result?.home?.value || 0} pt
                            </Text>
                            <TeamBonuses 
                              hasGoodDefense={homeHasAGoodDefense}
                              hasGoodPeople={homeHasAGoodPeople}
                              hasOwnGoal={homeHasDoneOwnGoal}
                            />
                          </YStack>
                        </YStack>
                        
                        {/* VS e punteggio goals */}
                        <YStack alignItems="center" gap="$1">
                          <Text fontSize="$6" fontWeight="bold">
                            {gameWrapper.game?.result?.homeGoals} - {gameWrapper.game?.result?.awayGoals}
                          </Text>
                          <Text fontSize="$2" color="$color11">
                            {gameDate && (
                              <Text>{formatItalianDate(gameDate)}</Text>
                            )}
                          </Text>
                        </YStack>
                        
                        {/* Squadra ospite */}
                        <YStack alignItems="center" gap="$2" flex={1}>
                          <TeamLogo 
                            groupId={routeGroupId || ''} 
                            year={routeYear || ''} 
                            ownerEmail={gameWrapper.game?.awayOwner || ''}
                            size={50}
                          />
                          <Text fontSize="$2" textAlign="center" numberOfLines={2}>
                            {gameWrapper.game?.away}
                          </Text>
                          <YStack alignItems="center" gap="$1">
                            <Text fontSize="$5" fontWeight="bold">
                              {gameWrapper.game?.result?.away?.value || 0} pt
                            </Text>
                            <TeamBonuses 
                              hasGoodDefense={awayHasAGoodDefense}
                              hasGoodPeople={awayHasAGoodPeople}
                              hasOwnGoal={awayHasDoneOwnGoal}
                            />
                          </YStack>
                        </YStack>
                      </XStack>
                    )}
                  </YStack>
                )}
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

