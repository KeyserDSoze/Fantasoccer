import React, { useEffect, useEffectEvent, useState } from 'react'
import { View } from 'react-native'
import { ScreenContent } from 'components/layout/ScreenContent'
import { YStack, Text, Spinner, Card } from 'tamagui'
import { useApp } from 'providers/AppProvider'
import { useAuth } from 'providers/AuthProvider'
import { RouteProp, useNavigation, useRoute } from '@react-navigation/native'
import { calendarService } from 'services/calendarService'
import { serieAService } from 'services/serieAService'
import type { CalendarGame } from 'types/calendar'
import type { RootStackParamList } from 'navigation'
import { formatSeasonFromYear } from 'utils/season'

export default function EditTeamScreen() {
  const { currentGroup, selectedLeagueId, selectedYear } = useApp()
  const { user } = useAuth()
  const navigation = useNavigation()
  const route = useRoute<RouteProp<RootStackParamList, 'Edit'>>()
  const targetLeagueId = route.params?.leagueId ?? selectedLeagueId
  const targetYear = route.params?.year ?? selectedYear
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const findAndNavigateToMyTeam = useEffectEvent(async () => {
    // Verifica prerequisiti
    if (!currentGroup?.id || !targetLeagueId || !targetYear || !user?.email) {
      setError('Seleziona un gruppo, una lega e un anno per visualizzare la tua squadra')
      setIsLoading(false)
      return
    }

    setIsLoading(true)
    setError(null)

    try {
      // 1. Carica il calendario della Serie A per determinare la giornata attuale/prossima
      const serieACalendar = await serieAService.getEnhancedCalendarByYear(targetYear)
      
      if (!serieACalendar) {
        setError('Non è stato possibile caricare il calendario della Serie A')
        setIsLoading(false)
        return
      }

      // 2. "La mia formazione" lavora sempre sulla prossima giornata non iniziata.
      const nextDay = serieACalendar.nextDay
      const targetSerieADay = nextDay?.serieADay

      if (!targetSerieADay) {
        setError('Non ci sono altre giornate di Serie A da giocare')
        setIsLoading(false)
        return
      }

      // 3. Carica il calendario della lega
      const calendar = await calendarService.getCalendarByComponents(
        currentGroup.id,
        targetLeagueId,
        targetYear
      )

      if (!calendar) {
        setError('Non è stato possibile caricare il calendario della lega')
        setIsLoading(false)
        return
      }

       // 4. Trova la squadra dell'utente nella lega selezionata, includendo owner e co-owner.
       const userEmail = user.email.toLowerCase()
       const selectedLeague = currentGroup.leagues.find(league => league.id === targetLeagueId)
       const leagueBasketIds = new Set(selectedLeague?.basketsId ?? [])
       const userTeamOwners = currentGroup.baskets
         .filter(basket => leagueBasketIds.has(basket.id))
         .flatMap(basket => basket.years
            .filter(year => year.year === targetYear)
           .flatMap(year => year.teams))
         .filter(team =>
           team.owner.toLowerCase() === userEmail ||
           team.additionalOwners?.some(owner => owner.toLowerCase() === userEmail) === true)
         .map(team => team.owner.toLowerCase())
       const allGames: CalendarGame[] = []
      
      for (const roundDays of Object.values(calendar.rounds)) {
        for (const day of roundDays) {
          if (day.serieADay !== targetSerieADay) 
            continue;
          for (const game of day.games) {
            if (
              userTeamOwners.includes(game.homeOwner.toLowerCase()) ||
              userTeamOwners.includes(game.awayOwner.toLowerCase())
            ) {
              allGames.push(game)
            }
          }
        }
      }

      if (allGames.length === 0) {
        const leagueName = currentGroup.leagues.find(l => l.id === targetLeagueId)?.name || 'questa lega'
        const season = formatSeasonFromYear(targetYear)
        setError(`Non hai una squadra in ${leagueName} per l'anno ${season}`)
        setIsLoading(false)
        return
      }

      // allGames contiene già soltanto la partita dell'utente nella giornata target.
      const targetGame = allGames[0]

      if (!targetGame) {
        setError('Nessuna partita disponibile')
        setIsLoading(false)
        return
      }

      // 6. Naviga alla partita trovata
      ;(navigation as any).navigate('Game', {
        groupId: currentGroup.id,
        leagueId: targetLeagueId,
        year: targetYear.toString(),
        gameId: targetGame.id,
        editMode: 'formation'
      })
    } catch (err: any) {
      console.error('Error navigating to my team:', err)
      setError(err?.message || 'Errore sconosciuto durante il caricamento della partita')
      setIsLoading(false)
    }
  })

  useEffect(() => {
    const timeout = setTimeout(() => void findAndNavigateToMyTeam(), 0)
    return () => clearTimeout(timeout)
  }, [currentGroup, targetLeagueId, targetYear, user?.email])

  return (
    <View style={{ flex: 1 }}>
      <ScreenContent path="screens/edit.tsx" title="La mia formazione">
        <YStack gap="$4" width="100%" maxWidth={600} alignSelf="center" paddingHorizontal="$4" paddingVertical="$6">
          {/* Loading State */}
          {isLoading && (
            <YStack alignItems="center" padding="$6" gap="$3">
              <Spinner size="large" color="$blue10" />
              <Text color="$color11" textAlign="center">
                Ricerca della tua prossima partita...
              </Text>
            </YStack>
          )}

          {/* Error State */}
          {error && !isLoading && (
            <Card backgroundColor="$red2" padding="$4" borderColor="$red8" borderWidth={1}>
              <Text color="$red11" textAlign="center" fontSize="$4" marginBottom="$3">
                ❌ {error}
              </Text>
              <Text color="$red10" textAlign="center" fontSize="$3">
                Verifica di aver selezionato un gruppo, una lega e un anno validi.
              </Text>
            </Card>
          )}
        </YStack>
      </ScreenContent>
    </View>
  )
}
