'use client'

import React, { useEffect, useState, useMemo } from 'react'
import { 
  YStack, 
  XStack, 
  Text, 
  Spinner,
  Button,
  View,
  H2,
  Card,
  Separator
} from 'tamagui'
import { RefreshCw, Trophy, TrendingUp, Users } from '@tamagui/lucide-icons'
import { useApp } from '../../providers/AppProvider'
import { RankingTable } from '../../components/RankingTable'
import { rankService } from '../../services/rankService'
import { EnhancedRankedTeam, Rank, LeagueKey, RankHelper } from '../../types/rank'

export default function RankingPage() {
  const { 
    currentGroup, 
    selectedLeagueId, 
    selectedYear,
    getAvailableLeagues
  } = useApp()

  // State
  const [ranking, setRanking] = useState<Rank | null>(null)
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [lastRefresh, setLastRefresh] = useState<Date | null>(null)

  // Memoized computed values
  const availableLeagues = useMemo(() => getAvailableLeagues(), [getAvailableLeagues])
  
  const selectedLeague = useMemo(() => {
    if (!currentGroup || !selectedLeagueId) return null
    return currentGroup.leagues.find(l => l.id === selectedLeagueId) || null
  }, [currentGroup, selectedLeagueId])

  const canLoadRanking = useMemo(() => {
    return currentGroup && selectedLeagueId && selectedYear
  }, [currentGroup, selectedLeagueId, selectedYear])

  const leagueKey = useMemo((): LeagueKey | null => {
    if (!currentGroup || !selectedLeagueId || !selectedYear) return null
    
    return RankHelper.createLeagueKey(
      currentGroup.id,
      selectedLeagueId,
      selectedYear
    )
  }, [currentGroup, selectedLeagueId, selectedYear])

  // Enhanced teams with positions
  const enhancedTeams = useMemo((): EnhancedRankedTeam[] => {
    if (!ranking || !selectedLeagueId) return []
    
    const leagueTeams = ranking.rankedTeams[selectedLeagueId] || []
    const sortedTeams = RankHelper.getTeamsSortedByPoints(ranking, selectedLeagueId)
    
    return sortedTeams.map((team, index) => ({
      ...team,
      goalDifference: RankHelper.getGoalDifference(team),
      totalGamesPlayed: RankHelper.getTotalGamesPlayed(team),
      pointsPerGame: RankHelper.getPointsPerGame(team),
      position: index + 1,
      valueAssets: team.money + team.plusMoney
    }))
  }, [ranking, selectedLeagueId])

  // Statistics
  const stats = useMemo(() => {
    if (!enhancedTeams.length) return null

    const totalTeams = enhancedTeams.length
    const totalPoints = enhancedTeams.reduce((sum, team) => sum + team.point, 0)
    const averagePoints = totalTeams > 0 ? totalPoints / totalTeams : 0
    const leader = enhancedTeams[0]

    return {
      totalTeams,
      totalPoints,
      averagePoints: Math.round(averagePoints * 100) / 100,
      leader: leader ? {
        name: leader.name,
        owner: leader.owner,
        points: leader.point,
        advantage: enhancedTeams.length > 1 ? leader.point - enhancedTeams[1].point : 0
      } : null
    }
  }, [enhancedTeams])

  // Load ranking data
  const loadRanking = async () => {
    if (!leagueKey) {
      setError('Parametri mancanti per caricare la classifica')
      return
    }

    setIsLoading(true)
    setError(null)

    try {
      const rankData = await rankService.getRank(leagueKey)
      
      if (rankData) {
        setRanking(rankData)
        setLastRefresh(new Date())
      } else {
        setError('Classifica non trovata per i parametri selezionati')
        setRanking(null)
      }
    } catch (err) {
      console.error('Errore nel caricamento della classifica:', err)
      setError(err instanceof Error ? err.message : 'Errore nel caricamento della classifica')
      setRanking(null)
    } finally {
      setIsLoading(false)
    }
  }

  // Load ranking when parameters change
  useEffect(() => {
    if (canLoadRanking) {
      loadRanking()
    } else {
      setRanking(null)
      setError(null)
    }
  }, [leagueKey]) // eslint-disable-line react-hooks/exhaustive-deps

  // Refresh handler
  const handleRefresh = () => {
    if (canLoadRanking) {
      loadRanking()
    }
  }

  // Get current Serie A day
  const currentDay = ranking?.serieADay || 0

  return (
    <YStack flex={1} padding="$4" space="$4" backgroundColor="$background">
      {/* Header */}
      <YStack space="$2">
        <XStack alignItems="center" justifyContent="space-between">
          <H2 color="$color" flexShrink={1}>
            📊 Classifica
          </H2>
          
          <Button
            size="$3"
            variant="outlined"
            onPress={handleRefresh}
            disabled={isLoading || !canLoadRanking}
            icon={isLoading ? <Spinner size="small" /> : <RefreshCw size="$1" />}
          >
            Aggiorna
          </Button>
        </XStack>

        {/* Context Information */}
        {(currentGroup || selectedLeague || selectedYear) && (
          <XStack space="$2" flexWrap="wrap">
            {currentGroup && (
              <Text fontSize="$2" color="$gray10">
                Gruppo: <Text fontWeight="600">{currentGroup.name}</Text>
              </Text>
            )}
            {selectedLeague && (
              <Text fontSize="$2" color="$gray10">
                • Lega: <Text fontWeight="600">{selectedLeague.name}</Text>
              </Text>
            )}
            {selectedYear && (
              <Text fontSize="$2" color="$gray10">
                • Anno: <Text fontWeight="600">{selectedYear}</Text>
              </Text>
            )}
            {currentDay > 0 && (
              <Text fontSize="$2" color="$gray10">
                • Giornata: <Text fontWeight="600">{currentDay}</Text>
              </Text>
            )}
          </XStack>
        )}

        {lastRefresh && (
          <Text fontSize="$2" color="$gray10">
            Ultimo aggiornamento: {lastRefresh.toLocaleString('it-IT')}
          </Text>
        )}
      </YStack>

      {/* Missing Parameters */}
      {!canLoadRanking && (
        <Card padding="$4" borderRadius="$4" backgroundColor="$yellow2" borderColor="$yellow7" borderWidth={1}>
          <YStack space="$2" alignItems="center">
            <Text fontSize="$4" fontWeight="600" color="$yellow11" textAlign="center">
              ⚠️ Parametri Mancanti
            </Text>
            <Text fontSize="$3" color="$yellow11" textAlign="center">
              Per visualizzare la classifica seleziona gruppo, lega e anno dalla barra di navigazione
            </Text>
          </YStack>
        </Card>
      )}

      {/* Error State */}
      {error && (
        <Card padding="$4" borderRadius="$4" backgroundColor="$red2" borderColor="$red7" borderWidth={1}>
          <YStack space="$2" alignItems="center">
            <Text fontSize="$4" fontWeight="600" color="$red11" textAlign="center">
              ❌ Errore
            </Text>
            <Text fontSize="$3" color="$red11" textAlign="center">
              {error}
            </Text>
            {canLoadRanking && (
              <Button
                size="$3"
                variant="outlined"
                onPress={handleRefresh}
                borderColor="$red7"
                color="$red11"
              >
                Riprova
              </Button>
            )}
          </YStack>
        </Card>
      )}

      {/* Statistics Cards */}
      {stats && !error && (
        <XStack space="$3" flexWrap="wrap">
          <Card flex={1} minWidth={150} padding="$3" borderRadius="$3">
            <YStack space="$1" alignItems="center">
              <Users size="$1" color="$blue10" />
              <Text fontSize="$2" color="$gray10">Squadre</Text>
              <Text fontSize="$5" fontWeight="600" color="$color">
                {stats.totalTeams}
              </Text>
            </YStack>
          </Card>

          <Card flex={1} minWidth={150} padding="$3" borderRadius="$3">
            <YStack space="$1" alignItems="center">
              <TrendingUp size="$1" color="$green10" />
              <Text fontSize="$2" color="$gray10">Media Punti</Text>
              <Text fontSize="$5" fontWeight="600" color="$color">
                {stats.averagePoints}
              </Text>
            </YStack>
          </Card>

          {stats.leader && (
            <Card flex={1} minWidth={200} padding="$3" borderRadius="$3">
              <YStack space="$1" alignItems="center">
                <Trophy size="$1" color="$yellow10" />
                <Text fontSize="$2" color="$gray10">Leader</Text>
                <Text fontSize="$4" fontWeight="600" color="$color" textAlign="center" numberOfLines={1}>
                  {stats.leader.name}
                </Text>
                <Text fontSize="$3" color="$gray10" textAlign="center">
                  {stats.leader.points} pts
                  {stats.leader.advantage > 0 && (
                    <Text color="$green10"> (+{stats.leader.advantage})</Text>
                  )}
                </Text>
              </YStack>
            </Card>
          )}
        </XStack>
      )}

      {/* Ranking Table */}
      {canLoadRanking && currentGroup && selectedYear && (
        <YStack flex={1} space="$2">
          {enhancedTeams.length > 0 && (
            <Text fontSize="$3" color="$gray10">
              {enhancedTeams.length} squadre • Ordinata per punti
            </Text>
          )}
          
          <RankingTable
            teams={enhancedTeams}
            groupId={currentGroup.id}
            year={selectedYear}
            showPosition={true}
            isLoading={isLoading}
            emptyMessage={
              error 
                ? "Impossibile caricare la classifica" 
                : "Nessuna squadra trovata per questa lega"
            }
            maxHeight={500}
            stickyFirstColumn={true}
          />
        </YStack>
      )}

      {/* No Data Available */}
      {canLoadRanking && !isLoading && !error && enhancedTeams.length === 0 && (
        <Card padding="$4" borderRadius="$4" backgroundColor="$gray2">
          <YStack space="$2" alignItems="center">
            <Text fontSize="$4" fontWeight="600" color="$gray11" textAlign="center">
              📋 Nessun Dato
            </Text>
            <Text fontSize="$3" color="$gray10" textAlign="center">
              La classifica per questa lega non è ancora disponibile
            </Text>
          </YStack>
        </Card>
      )}
    </YStack>
  )
}