import React, { useState, useMemo, useCallback } from 'react'
import { View } from 'react-native'
import { ScreenContent } from 'components/layout/ScreenContent'
import { 
  YStack, 
  XStack, 
  Text, 
  H3, 
  H4,
  Card, 
  Spinner, 
  Separator,
  Button,
  useMedia
} from 'tamagui'
import { useToastController } from '@tamagui/toast'
import { LeagueSelectorsWidget } from 'components/LeagueSelectorsWidget'
import { serieAService } from 'services/serieAService'
import { recalculationService } from 'services/recalculationService'
import { RealCalendar, RealDay, RealGame, RealCalendarHelper } from 'types/serieA'
import { useApp } from 'providers/AppProvider'
import { useSerieACalendar } from 'hooks/useSerieACalendar'
import { 
  Calendar as CalendarIcon,
  AlertCircle,
  CheckCircle,
  Clock,
  RefreshCw,
  Save,
  Calculator,
  BarChart3
} from '@tamagui/lucide-icons-2'

export default function SerieAAdminScreen() {
  const media = useMedia()
  const containerWidth = media.gtMd ? 960 : '100%'
  const toast = useToastController()

  // Use app context for year and calendar
  const { selectedYear, setSelectedYear } = useApp()
  const { calendar, isLoading, error: calendarError, refetch } = useSerieACalendar({
    enablePolling: false // Disable auto-refresh for admin page
  })

  const [modifiedGames, setModifiedGames] = useState<Set<string>>(new Set())
  const [recalculatingDay, setRecalculatingDay] = useState<number | null>(null)
  
  // Loading states for each operation
  const [loadingStates, setLoadingStates] = useState<Record<string, boolean>>({})
  
  const setLoading = (key: string, value: boolean) => {
    setLoadingStates(prev => ({ ...prev, [key]: value }))
  }
  
  const isLoadingOp = (key: string) => loadingStates[key] || false
  const isAnyLoading = Object.values(loadingStates).some(v => v)

  // Local calendar state for modifications (overlay on top of the hook's calendar)
  const [localCalendar, setLocalCalendar] = useState<RealCalendar | null>(null)

  // Sync local calendar with hook calendar when it changes
  React.useEffect(() => {
    if (calendar) {
      setLocalCalendar(calendar)
      setModifiedGames(new Set()) // Clear modifications on calendar change
    }
  }, [calendar])

  // Available years
  const availableYears = useMemo(() => {
    const currentYear = new Date().getFullYear()
    return [currentYear - 2, currentYear - 1, currentYear, currentYear + 1]
  }, [])

  // Handle year change
  const handleYearChange = useCallback(async (year: number | null) => {
    if (year === null) return
    await setSelectedYear(year)
    // Calendar will auto-reload from hook
  }, [setSelectedYear])

  // Toggle delayed status
  const toggleDelayed = useCallback((serieADay: number, gameIndex: number) => {
    if (!localCalendar) return

    // Create modified key
    const key = `${serieADay}-${gameIndex}`

    // Update local calendar
    const updatedCalendar: RealCalendar = {
      ...localCalendar,
      days: localCalendar.days.map(day => {
        if (day.serieADay === serieADay) {
          return {
            ...day,
            games: day.games.map((game, idx) => {
              if (idx === gameIndex) {
                return { ...game, delayed: !game.delayed }
              }
              return game
            })
          }
        }
        return day
      })
    }

    setLocalCalendar(updatedCalendar)

    // Mark as modified
    setModifiedGames(prev => {
      const next = new Set(prev)
      next.add(key)
      return next
    })
  }, [localCalendar])

  // Save all changes
  const saveChanges = useCallback(async () => {
    if (!localCalendar || !selectedYear || modifiedGames.size === 0) return

    setLoading('save', true)

    try {
      // For each modified game, update it
      for (const key of modifiedGames) {
        const [serieADayStr, gameIndexStr] = key.split('-')
        const serieADay = parseInt(serieADayStr)
        const gameIndex = parseInt(gameIndexStr)

        const day = localCalendar.days.find(d => d.serieADay === serieADay)
        if (!day) continue

        const game = day.games[gameIndex]
        if (!game) continue

        // Update in backend
        await serieAService.updateGameDelayed(
          selectedYear,
          serieADay,
          gameIndex,
          game.delayed
        )
      }

      // Clear modifications
      setModifiedGames(new Set())
      
      toast.show(`✅ ${modifiedGames.size} partita/e salvata/e con successo`, { variant: 'success' })

      // Reload calendar to ensure consistency
      await refetch()
    } catch (err) {
      toast.show(`❌ Errore nel salvataggio: ${err instanceof Error ? err.message : 'Unknown error'}`, { variant: 'error' })
    } finally {
      setLoading('save', false)
    }
  }, [localCalendar, modifiedGames, selectedYear, refetch, toast])

  // Recalculate day votes
  const recalculateDay = useCallback(async (serieADay: number) => {
    if (!selectedYear) return

    setRecalculatingDay(serieADay)
    setLoading(`recalcDay-${serieADay}`, true)

    try {
      const result = await recalculationService.recalculateDay(selectedYear, serieADay)
      
      if (result) {
        toast.show(`✅ Giornata ${serieADay} ricalcolata con successo`, { variant: 'success' })
        // Reload calendar to show updated data
        await refetch()
      } else {
        toast.show(`❌ Errore nel ricalcolo della giornata ${serieADay}`, { variant: 'error' })
      }
    } catch (err) {
      toast.show(`❌ Errore nel ricalcolo della giornata ${serieADay}: ${err instanceof Error ? err.message : 'Unknown error'}`, { variant: 'error' })
    } finally {
      setLoading(`recalcDay-${serieADay}`, false)
      setRecalculatingDay(null)
    }
  }, [selectedYear, refetch, toast])

  // Recalculate all year stats
  const recalculateYearStats = useCallback(async () => {
    if (!selectedYear) return

    setLoading('recalcYear', true)

    try {
      const result = await recalculationService.recalculateStats(selectedYear)
      
      if (result) {
        toast.show(`✅ Statistiche dell'anno ${selectedYear}/${selectedYear + 1} ricalcolate con successo`, { variant: 'success' })
        // Reload calendar to show updated data
        await refetch()
      } else {
        toast.show('❌ Errore nel ricalcolo delle statistiche', { variant: 'error' })
      }
    } catch (err) {
      toast.show(`❌ Errore nel ricalcolo delle statistiche: ${err instanceof Error ? err.message : 'Unknown error'}`, { variant: 'error' })
    } finally {
      setLoading('recalcYear', false)
    }
  }, [selectedYear, refetch, toast])

  // Statistics
  const statistics = useMemo(() => {
    if (!localCalendar) return { totalGames: 0, delayedGames: 0, playedGames: 0, upcomingGames: 0 }

    let totalGames = 0
    let delayedGames = 0
    let playedGames = 0
    let upcomingGames = 0

    localCalendar.days.forEach(day => {
      day.games.forEach(game => {
        totalGames++
        if (game.delayed) delayedGames++
        if (RealCalendarHelper.isGamePlayed(game)) playedGames++
        else if (!game.delayed) upcomingGames++
      })
    })

    return { totalGames, delayedGames, playedGames, upcomingGames }
  }, [localCalendar])

  return (
    <View style={{ flex: 1 }}>
      <ScreenContent path="screens/serie-a-admin.tsx" title="Serie A Admin">
        <YStack
          width={containerWidth}
          maxWidth={1200}
          gap="$4"
          alignItems="stretch"
          alignSelf="center"
        >
          {/* Header */}
          <Card
            padding="$4"
            backgroundColor="$green3"
            borderRadius="$4"
            borderWidth="$0.5"
            borderColor="$green7"
          >
            <XStack alignItems="center" gap="$3" marginBottom="$2">
              <CalendarIcon size="$2" color="$green10" />
              <H3 color="$green11">Gestione Calendario Serie A</H3>
            </XStack>
            <Text fontSize="$3" color="$green10">
              Gestisci lo stato delle partite rinviate nel calendario della Serie A.
            </Text>
          </Card>

          {/* Year Selector */}
          <LeagueSelectorsWidget
            leagues={[]}
            selectedLeagueId={null}
            years={availableYears}
            selectedYear={selectedYear ?? new Date().getFullYear()}
            onSelectYear={handleYearChange}
            formatYearLabel={(year) => `${year}/${year + 1}`}
            yearHint="Seleziona la stagione da gestire"
            showLeagueSection={false}
            showRoundSection={false}
            title="Selettore Stagione"
          />

          {/* Save Changes Banner */}
          {modifiedGames.size > 0 && (
            <Card
              padding="$3"
              backgroundColor="$orange2"
              borderRadius="$4"
              borderWidth="$1"
              borderColor="$orange7"
            >
              <XStack gap="$3" justifyContent="space-between" alignItems="center">
                <Text fontSize="$3" color="$orange11" fontWeight="600">
                  ⚠️ {modifiedGames.size} partita/e modificata/e - Salvare le modifiche
                </Text>
                <Button
                  size="$3"
                  backgroundColor="$green9"
                  onPress={saveChanges}
                  disabled={isAnyLoading}
                  icon={isLoadingOp('save') ? <Spinner size="small" /> : <Save size="$1" />}
                >
                  {isLoadingOp('save') ? 'Salvataggio...' : 'Salva Modifiche'}
                </Button>
              </XStack>
            </Card>
          )}

          {/* Statistics */}
          {localCalendar && (
            <XStack gap="$3" flexWrap="wrap">
              <Card padding="$3" backgroundColor="$color2" flex={1} minWidth={140}>
                <XStack alignItems="center" gap="$2" marginBottom="$1">
                  <CalendarIcon size="$1" color="$blue10" />
                  <Text fontSize="$2" color="$color11">Totale Partite</Text>
                </XStack>
                <Text fontSize="$7" fontWeight="bold" color="$blue10">
                  {statistics.totalGames}
                </Text>
              </Card>

              <Card padding="$3" backgroundColor="$color2" flex={1} minWidth={140}>
                <XStack alignItems="center" gap="$2" marginBottom="$1">
                  <AlertCircle size="$1" color="$orange10" />
                  <Text fontSize="$2" color="$color11">Rinviate</Text>
                </XStack>
                <Text fontSize="$7" fontWeight="bold" color="$orange10">
                  {statistics.delayedGames}
                </Text>
              </Card>

              <Card padding="$3" backgroundColor="$color2" flex={1} minWidth={140}>
                <XStack alignItems="center" gap="$2" marginBottom="$1">
                  <CheckCircle size="$1" color="$green10" />
                  <Text fontSize="$2" color="$color11">Giocate</Text>
                </XStack>
                <Text fontSize="$7" fontWeight="bold" color="$green10">
                  {statistics.playedGames}
                </Text>
              </Card>

              <Card padding="$3" backgroundColor="$color2" flex={1} minWidth={140}>
                <XStack alignItems="center" gap="$2" marginBottom="$1">
                  <Clock size="$1" color="$purple10" />
                  <Text fontSize="$2" color="$color11">Da Giocare</Text>
                </XStack>
                <Text fontSize="$7" fontWeight="bold" color="$purple10">
                  {statistics.upcomingGames}
                </Text>
              </Card>
            </XStack>
          )}

          {/* Actions */}
          <XStack gap="$2" justifyContent="space-between" flexWrap="wrap">
            <XStack gap="$2">
              <Button
                size="$3"
                variant="outlined"
                onPress={() => refetch()}
                disabled={isLoading || isAnyLoading}
                icon={<RefreshCw size="$1" />}
              >
                Ricarica
              </Button>
            </XStack>

            <XStack gap="$2">
              <Button
                size="$3"
                backgroundColor="$purple9"
                onPress={recalculateYearStats}
                disabled={isAnyLoading || !selectedYear}
                icon={isLoadingOp('recalcYear') ? <Spinner size="small" /> : <BarChart3 size="$1" />}
              >
                {isLoadingOp('recalcYear') ? 'Ricalcolo...' : 'Ricalcola statistiche dei giocatori'}
              </Button>
            </XStack>
          </XStack>

          {/* Calendar Content */}
          {isLoading ? (
            <Card padding="$6" alignItems="center" gap="$3">
              <Spinner size="large" color="$green10" />
              <Text color="$color10">Caricamento calendario...</Text>
            </Card>
          ) : calendarError ? (
            <Card padding="$4" backgroundColor="$red3" borderColor="$red7" borderWidth="$0.5">
              <Text color="$red11" fontWeight="600">Errore: {calendarError}</Text>
            </Card>
          ) : !localCalendar ? (
            <Card padding="$6" alignItems="center">
              <Text color="$color11" fontSize="$4">
                Nessun calendario disponibile
              </Text>
            </Card>
          ) : (
            <YStack gap="$3">
              <Text fontSize="$3" color="$color11">
                Visualizzate {localCalendar.days.length} giornate
              </Text>

              {localCalendar.days.map(day => (
                <DayCard
                  key={day.serieADay}
                  day={day}
                  onToggleDelayed={toggleDelayed}
                  onRecalculateDay={recalculateDay}
                  modifiedGames={modifiedGames}
                  isRecalculating={isLoadingOp(`recalcDay-${day.serieADay}`)}
                />
              ))}
            </YStack>
          )}
        </YStack>
      </ScreenContent>
    </View>
  )
}

// ============================================
// DAY CARD COMPONENT
// ============================================

interface DayCardProps {
  day: RealDay
  onToggleDelayed: (serieADay: number, gameIndex: number) => void
  onRecalculateDay: (serieADay: number) => void
  modifiedGames: Set<string>
  isRecalculating: boolean
}

function DayCard({ day, onToggleDelayed, onRecalculateDay, modifiedGames, isRecalculating }: DayCardProps) {
  const [isExpanded, setIsExpanded] = useState(false)

  const delayedCount = day.games.filter(g => g.delayed).length
  const playedCount = day.games.filter(g => RealCalendarHelper.isGamePlayed(g)).length

  return (
    <Card
      padding="$0"
      backgroundColor="$color2"
      borderRadius="$4"
      borderWidth="$0.5"
      borderColor="$borderColor"
    >
      {/* Header - Clickable */}
      <XStack
        padding="$4"
        gap="$3"
        alignItems="center"
        justifyContent="space-between"
        onPress={() => setIsExpanded(!isExpanded)}
        cursor="pointer"
        hoverStyle={{ backgroundColor: '$color3' }}
        pressStyle={{ backgroundColor: '$color4' }}
      >
        <YStack gap="$1" flex={1}>
          <Text fontSize="$5" fontWeight="bold" color="$color12">
            Giornata {day.serieADay}
          </Text>
          <XStack gap="$3" flexWrap="wrap">
            <Text fontSize="$2" color="$color11">
              {day.games.length} partite
            </Text>
            {playedCount > 0 && (
              <Text fontSize="$2" color="$green10">
                • {playedCount} giocate
              </Text>
            )}
            {delayedCount > 0 && (
              <Text fontSize="$2" color="$orange10" fontWeight="600">
                • {delayedCount} rinviate
              </Text>
            )}
          </XStack>
        </YStack>

        <XStack gap="$2">
          <Button 
            size="$2" 
            backgroundColor="$purple9"
            onPress={(e) => {
              e.stopPropagation()
              onRecalculateDay(day.serieADay)
            }}
            disabled={isRecalculating}
            icon={isRecalculating ? <Spinner size="small" /> : <Calculator size="$0.75" />}
          >
            {isRecalculating ? 'Calc...' : 'Ricalcola'}
          </Button>
          <Button size="$2" variant="outlined">
            {isExpanded ? 'Chiudi' : 'Espandi'}
          </Button>
        </XStack>
      </XStack>

      {/* Expanded Content */}
      {isExpanded && (
        <YStack padding="$4" paddingTop="$0" gap="$2">
          <Separator marginBottom="$2" />

          {day.games.map((game, idx) => (
            <GameRow
              key={idx}
              game={game}
              gameIndex={idx}
              serieADay={day.serieADay}
              onToggleDelayed={onToggleDelayed}
              isModified={modifiedGames.has(`${day.serieADay}-${idx}`)}
            />
          ))}
        </YStack>
      )}
    </Card>
  )
}

// ============================================
// GAME ROW COMPONENT
// ============================================

interface GameRowProps {
  game: RealGame
  gameIndex: number
  serieADay: number
  onToggleDelayed: (serieADay: number, gameIndex: number) => void
  isModified: boolean
}

function GameRow({ game, gameIndex, serieADay, onToggleDelayed, isModified }: GameRowProps) {
  const isPlayed = RealCalendarHelper.isGamePlayed(game)

  const resultText = isPlayed
    ? `${game.homeGoals} - ${game.awayGoals}`
    : game.delayed
    ? 'RINVIATA'
    : new Date(game.date).toLocaleDateString('it-IT', { 
        day: '2-digit', 
        month: '2-digit',
        hour: '2-digit',
        minute: '2-digit'
      })

  return (
    <Card
      padding="$3"
      backgroundColor={isModified ? '$yellow2' : '$color1'}
      borderRadius="$3"
      borderWidth="$0.5"
      borderColor={isModified ? '$yellow7' : game.delayed ? '$orange7' : '$borderColor'}
    >
      <XStack alignItems="center" justifyContent="space-between" gap="$3">
        {/* Teams */}
        <YStack flex={1} gap="$1">
          <XStack alignItems="center" gap="$2">
            <Text fontSize="$3" fontWeight="600" color="$color12">
              {game.home.name}
            </Text>
            {isPlayed && (
              <Text fontSize="$3" fontWeight="bold" color="$blue10">
                {game.homeGoals}
              </Text>
            )}
          </XStack>
          <XStack alignItems="center" gap="$2">
            <Text fontSize="$3" fontWeight="600" color="$color12">
              {game.away.name}
            </Text>
            {isPlayed && (
              <Text fontSize="$3" fontWeight="bold" color="$blue10">
                {game.awayGoals}
              </Text>
            )}
          </XStack>
        </YStack>

        {/* Status */}
        <YStack gap="$1" alignItems="center" minWidth={100}>
          <Text 
            fontSize="$2" 
            color={game.delayed ? '$orange11' : isPlayed ? '$green11' : '$color11'}
            fontWeight="600"
          >
            {resultText}
          </Text>
          {isModified && (
            <Text fontSize="$1" color="$yellow11">
              ⚠️ Modificata
            </Text>
          )}
        </YStack>

        {/* Toggle Button */}
        <Button
          size="$2"
          variant="outlined"
          borderColor={game.delayed ? '$orange8' : '$green8'}
          backgroundColor={game.delayed ? '$orange2' : '$green2'}
          onPress={() => onToggleDelayed(serieADay, gameIndex)}
          disabled={isPlayed}
        >
          <Text
            fontSize="$2"
            color={game.delayed ? '$orange11' : '$green11'}
            fontWeight="600"
          >
            {game.delayed ? 'Non Rinviata' : 'Rinvia'}
          </Text>
        </Button>
      </XStack>
    </Card>
  )
}
