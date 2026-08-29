import { type FC, useMemo } from 'react'
import { AlertTriangle, CalendarRange, RefreshCw } from '@tamagui/lucide-icons-2'
import {
  Button,
  Card,
  Separator,
  Spinner,
  Text,
  XStack,
  YStack,
  useMedia,
} from 'tamagui'
import type { CalendarDay, CalendarGame } from 'types/calendar'
import { useNavigation } from '@react-navigation/native'
import { useCurrentGroup } from 'hooks'
import { useApp } from 'providers/AppProvider'
import { TeamLogo } from '../team/TeamLogo'

export type LeagueCalendarWidgetProps = {
  leagueName?: string | null
  seasonLabel?: string | null
  roundLabel?: string | null
  pendingGamesCount?: number
  days: CalendarDay[]
  loading?: boolean
  error?: string | null
  isSelectionIncomplete?: boolean
  onRefresh?: () => void
  selectionMessage?: string
  emptyMessage?: string
  loadingMessage?: string
  refreshLabel?: string
  errorActionLabel?: string
  // Navigation parameters (optional, fallback to global context if not provided)
  groupId?: string | null
  leagueId?: string | null
  year?: number | null
}

const defaultSelectionMessage = 'Seleziona lega, stagione e girone per visualizzare il calendario.'
const defaultEmptyMessage = 'Nessuna giornata disponibile per i filtri selezionati.'
const defaultLoadingMessage = 'Caricamento calendario...'
const defaultRefreshLabel = 'Aggiorna calendario'
const defaultErrorActionLabel = 'Riprova'

const GameRow: FC<{ 
  game: CalendarGame
  dayNumber?: number
  groupId?: string | null
  leagueId?: string | null
  year?: number | null
}> = ({ game, dayNumber, groupId: propGroupId, leagueId: propLeagueId, year: propYear }) => {
  let hasResult = Boolean(game.result)
  const homeGoals = game.result?.homeGoals ?? '—'
  const awayGoals = game.result?.awayGoals ?? '—'
  const homePoints = game.result?.home.value ?? '—'
  const awayPoints = game.result?.away.value ?? '—'
  if (homePoints === 0 && awayPoints === 0) {
    hasResult = false
  }
  const isCancelled = game.result?.isCancelled ?? false
  const { group: currentGroup } = useCurrentGroup()
  const { selectedYear, selectedLeagueId } = useApp()
  
  // Use props if provided, otherwise fallback to global context
  const group = propGroupId ?? currentGroup?.id ?? null
  const leagueId = propLeagueId ?? selectedLeagueId ?? null
  const year = propYear ?? selectedYear ?? null
  
  const navigation = useNavigation()
  const logoSize = 28

  const onPress = () => {
    navigation.navigate("Game", { 
      gameId: game.id, 
      leagueId: leagueId, 
      year: year?.toString(), 
      groupId: group 
    })
  }

  return (
    <Button onPress={onPress} chromeless paddingHorizontal={0} paddingVertical={0}>
      <XStack
        alignItems="center"
        justifyContent="space-between"
        gap="$2"
        paddingVertical="$2"
        width="100%"
      >
        {/* Home team - logo + name */}
        <XStack flex={1} gap="$2" alignItems="center" minWidth={0}>
          {group && year && game.homeOwner && (
            <YStack flexShrink={0}>
              <TeamLogo
                groupId={group}
                year={year}
                ownerEmail={game.homeOwner}
                size={logoSize}
              />
            </YStack>
          )}
          <Text 
            color="$color12" 
            fontWeight="700" 
            numberOfLines={1}
            flexShrink={1}
            flexGrow={1}
          >
            {game.home}
          </Text>
        </XStack>

        {/* Score in the middle */}
        <YStack flexShrink={0} width={80} alignItems="center" gap="$1">
          <Text color="$color11" fontWeight="600">
            {!isCancelled ? (hasResult ? `${homeGoals} - ${awayGoals}` : 'VS') : "Annullata"}
          </Text>
          {hasResult && (
            <Text color="$color9" fontSize="$2">
              {homePoints} • {awayPoints}
            </Text>
          )}
        </YStack>

        {/* Away team - name + logo */}
        <XStack flex={1} gap="$2" alignItems="center" justifyContent="flex-end" minWidth={0}>
          <Text 
            color="$color12" 
            fontWeight="700" 
            numberOfLines={1}
            flexShrink={1}
            flexGrow={1}
          >
            {game.away}
          </Text>
          {group && year && game.awayOwner && (
            <YStack flexShrink={0}>
              <TeamLogo
                groupId={group}
                year={year}
                ownerEmail={game.awayOwner}
                size={logoSize}
              />
            </YStack>
          )}
        </XStack>
      </XStack>
    </Button>
  )
}

const DayCard: FC<{ 
  day: CalendarDay
  groupId?: string | null
  leagueId?: string | null
  year?: number | null
}> = ({ day, groupId, leagueId, year }) => (
  <Card
    padding="$3"
    backgroundColor="$color2"
    borderRadius="$4"
    borderWidth="$0.5"
    borderColor="$borderColor"
    gap="$3"
    flex={1}
    minWidth={0}
  >
    <XStack justifyContent="space-between" alignItems="center">
      <YStack gap="$1">
        <Text color="$color12" fontSize="$4" fontWeight="700">
          Giornata {day.number}
        </Text>
        <Text color="$color10" fontSize="$2">
          Serie A: giornata {day.serieADay}
        </Text>
      </YStack>
      <Text color="$color10" fontWeight="600">
        {day.games.length} partite
      </Text>
    </XStack>

    <Separator />

    <YStack gap="$2">
      {day.games.map((game) => (
        <Card
          key={game.id}
          paddingHorizontal="$2"
          paddingVertical="$1.5"
          backgroundColor="$color1"
          borderRadius="$3"
          borderWidth="$0.25"
          borderColor="$color3"
        >
          <GameRow 
            game={game} 
            dayNumber={day.number}
            groupId={groupId}
            leagueId={leagueId}
            year={year}
          />
        </Card>
      ))}
    </YStack>
  </Card>
)

export const LeagueCalendarWidget: FC<LeagueCalendarWidgetProps> = ({
  leagueName,
  seasonLabel,
  roundLabel,
  pendingGamesCount = 0,
  days,
  loading,
  error,
  isSelectionIncomplete,
  onRefresh,
  selectionMessage = defaultSelectionMessage,
  emptyMessage = defaultEmptyMessage,
  loadingMessage = defaultLoadingMessage,
  refreshLabel = defaultRefreshLabel,
  errorActionLabel = defaultErrorActionLabel,
  groupId,
  leagueId,
  year,
}) => {
  const showSelectionIncomplete = Boolean(isSelectionIncomplete)
  const showError = Boolean(error)
  const showLoading = Boolean(loading)
  const hasDays = days.length > 0
  const media = useMedia()

  const cardsPerRow = useMemo(() => {
    if (media.gtMd) {
      return 3
    }
    if (media.gtSm) {
      return 2
    }
    return 1
  }, [media.gtMd, media.gtSm])

  const chunkedDays = useMemo(() => {
    if (!hasDays) {
      return [] as CalendarDay[][]
    }

    const size = Math.max(cardsPerRow, 1)
    const chunks: CalendarDay[][] = []

    for (let index = 0; index < days.length; index += size) {
      chunks.push(days.slice(index, index + size))
    }

    return chunks
  }, [days, cardsPerRow, hasDays])

  return (
    <Card padding="$3" elevation="$2" backgroundColor="$color1" gap="$3" width="100%">
      <XStack alignItems="center" gap="$2">
        <CalendarRange size="$1.5" color="$purple10" />
        <YStack gap="$1" flex={1}>
          <Text fontSize="$5" fontWeight="700" color="$color12">
            {leagueName ?? 'Calendario'}
          </Text>
          {seasonLabel && (
            <Text fontSize="$3" color="$color10">
              Stagione {seasonLabel}
            </Text>
          )}
          {roundLabel && (
            <Text fontSize="$3" color="$color10">
              Girone: {roundLabel}
            </Text>
          )}
          <Text fontSize="$2" color="$color10">
            {pendingGamesCount} partite in attesa di risultato
          </Text>
        </YStack>
        {showLoading && (
          <Spinner size="small" color="$blue10" />
        )}
      </XStack>

      <Separator />

      {showSelectionIncomplete ? (
        <YStack gap="$3" alignItems="center" paddingVertical="$4">
          <AlertTriangle size={32} color="$yellow10" />
          <Text color="$color10" textAlign="center">
            {selectionMessage}
          </Text>
        </YStack>
      ) : showError ? (
        <YStack gap="$3" alignItems="center" paddingVertical="$4">
          <AlertTriangle size={32} color="$red10" />
          <Text color="$color10" textAlign="center">
            {error}
          </Text>
          {onRefresh && (
            <Button icon={RefreshCw} variant="outlined" onPress={onRefresh}>
              {errorActionLabel}
            </Button>
          )}
        </YStack>
      ) : showLoading && !hasDays ? (
        <YStack gap="$3" alignItems="center" paddingVertical="$4">
          <Spinner size="small" color="$blue10" />
          <Text color="$color10">{loadingMessage}</Text>
        </YStack>
      ) : !hasDays ? (
        <YStack gap="$3" alignItems="center" paddingVertical="$4">
          <Text color="$color10" fontSize="$3" textAlign="center">
            {emptyMessage}
          </Text>
          {onRefresh && (
            <Button variant="outlined" onPress={onRefresh}>
              {refreshLabel}
            </Button>
          )}
        </YStack>
      ) : (
        <YStack gap="$3" paddingBottom="$2">
          {chunkedDays.map((rowDays, rowIndex) => (
            <XStack
              key={`calendar-row-${rowIndex}`}
              gap="$3"
              width="100%"
              alignItems="stretch"
            >
              {rowDays.map((day) => (
                <DayCard 
                  key={day.number} 
                  day={day}
                  groupId={groupId}
                  leagueId={leagueId}
                  year={year}
                />
              ))}
            </XStack>
          ))}
        </YStack>
      )}

      {onRefresh && hasDays && (
        <>
          <Separator marginTop="$2" />
          <XStack justifyContent="flex-end">
            <Button size="$2" variant="outlined" icon={RefreshCw} onPress={onRefresh}>
              {refreshLabel}
            </Button>
          </XStack>
        </>
      )}
    </Card>
  )
}
