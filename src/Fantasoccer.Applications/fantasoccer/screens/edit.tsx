import React, { useEffect, useState } from 'react'
import { View } from 'react-native'
import { ScreenContent } from 'components/layout/ScreenContent'
import { YStack, Text, Spinner, Card } from 'tamagui'
import { useApp } from 'providers/AppProvider'
import { useAuth } from 'providers/AuthProvider'
import { useNavigation } from '@react-navigation/native'
import { calendarService } from 'services/calendarService'
import { serieAService } from 'services/serieAService'
import type { CalendarGame } from 'types/calendar'
import { formatSeasonFromYear } from 'utils/season'

export default function EditTeamScreen() {
  const { currentGroup, selectedLeagueId, selectedYear, getAvailableLeagues } = useApp()
  const { user } = useAuth()
  const navigation = useNavigation()
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    findAndNavigateToMyTeam()
  }, [currentGroup, selectedLeagueId, selectedYear, user])

  const findAndNavigateToMyTeam = async () => {
    // Verifica prerequisiti
    if (!currentGroup?.id || !selectedLeagueId || !selectedYear || !user?.email) {
      setError('Seleziona un gruppo, una lega e un anno per visualizzare la tua squadra')
      setIsLoading(false)
      return
    }

    setIsLoading(true)
    setError(null)

    try {
      // 1. Carica il calendario della Serie A per determinare la giornata attuale/prossima
      const serieACalendar = await serieAService.getEnhancedCalendarByYear(selectedYear)
      
      if (!serieACalendar) {
        setError('Non è stato possibile caricare il calendario della Serie A')
        setIsLoading(false)
        return
      }

      // 2. Determina quale giornata di Serie A è attualmente in corso o prossima
      const nextDay = serieACalendar.nextDay
      const lastDay = serieACalendar.lastDay
      
      // Usa liveDay se disponibile, altrimenti nextDay, altrimenti lastDay
      const targetSerieADay = nextDay?.serieADay ?? lastDay?.serieADay

      if (!targetSerieADay) {
        setError('Nessuna giornata di Serie A disponibile')
        setIsLoading(false)
        return
      }

      // 3. Carica il calendario della lega
      const calendar = await calendarService.getCalendarByComponents(
        currentGroup.id,
        selectedLeagueId,
        selectedYear
      )

      if (!calendar) {
        setError('Non è stato possibile caricare il calendario della lega')
        setIsLoading(false)
        return
      }

       // 4. Trova la squadra dell'utente, includendo owner principale e co-owner.
       const userEmail = user.email.toLowerCase()
       const userTeamOwners = currentGroup.baskets
         .flatMap(basket => basket.years
           .filter(year => year.year === selectedYear)
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
        const leagueName = getAvailableLeagues().find(l => l.id === selectedLeagueId)?.name || 'questa lega'
        const season = formatSeasonFromYear(selectedYear)
        setError(`Non hai una squadra in ${leagueName} per l'anno ${season}`)
        setIsLoading(false)
        return
      }

      // 5. Trova la partita corrispondente alla giornata di Serie A target
      // Priorità: partita della giornata target, altrimenti prima partita senza risultato, altrimenti ultima
      let targetGame = allGames.find(game => game.number === targetSerieADay)
      
      if (!targetGame) {
        // Se non c'è una partita per quella giornata, cerca la prima senza risultato
        const sortedGames = allGames.sort((a, b) => a.number - b.number)
        targetGame = sortedGames.find(game => !game.result) ?? sortedGames[sortedGames.length - 1]
      }

      if (!targetGame) {
        setError('Nessuna partita disponibile')
        setIsLoading(false)
        return
      }

      // 6. Naviga alla partita trovata
      ;(navigation as any).navigate('Game', {
        groupId: currentGroup.id,
        leagueId: selectedLeagueId,
        year: selectedYear.toString(),
        gameId: targetGame.id
      })
    } catch (err: any) {
      console.error('Error navigating to my team:', err)
      setError(err?.message || 'Errore sconosciuto durante il caricamento della partita')
      setIsLoading(false)
    }
  }

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
