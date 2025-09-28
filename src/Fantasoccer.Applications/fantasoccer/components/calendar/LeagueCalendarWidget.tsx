import { type FC } from 'react'
import { AlertTriangle, CalendarRange, RefreshCw } from '@tamagui/lucide-icons'
import {
  Button,
  Card,
  Separator,
  Spinner,
  Text,
  XStack,
  YStack,
} from 'tamagui'
import type { CalendarDay, CalendarGame } from 'types/calendar'

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
}

const defaultSelectionMessage = 'Seleziona lega, stagione e girone per visualizzare il calendario.'
const defaultEmptyMessage = 'Nessuna giornata disponibile per i filtri selezionati.'
const defaultLoadingMessage = 'Caricamento calendario...'
const defaultRefreshLabel = 'Aggiorna calendario'
const defaultErrorActionLabel = 'Riprova'

const GameRow: FC<{ game: CalendarGame }> = ({ game }) => {
  let hasResult = Boolean(game.result)
  const homeGoals = game.result?.homeGoals ?? '—'
  const awayGoals = game.result?.awayGoals ?? '—'
  const homePoints = game.result?.home.value ?? '—'
  const awayPoints = game.result?.away.value ?? '—'
  if (homePoints === 0 && awayPoints === 0) {
    hasResult = false
  }
  const isCancelled = game.result?.isCancelled ?? false

  return (
    <XStack
      alignItems="center"
      justifyContent="space-between"
      gap="$3"
      paddingVertical="$1.5"
    >
      <YStack flex={1} gap="$1">
        <Text color="$color12" fontWeight="700" numberOfLines={1}>
          {game.home}
        </Text>
        <Text color="$color10" fontSize="$2" numberOfLines={1}>
          Allenatore: {game.homeOwner}
        </Text>
      </YStack>

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

      <YStack flex={1} gap="$1" alignItems="flex-end">
        <Text color="$color12" fontWeight="700" numberOfLines={1}>
          {game.away}
        </Text>
        <Text color="$color10" fontSize="$2" numberOfLines={1}>
          Allenatore: {game.awayOwner}
        </Text>
      </YStack>
    </XStack>
  )
}

const DayCard: FC<{ day: CalendarDay }> = ({ day }) => (
  <Card
    key={day.number}
    padding="$3"
    backgroundColor="$color2"
    borderRadius="$4"
    borderWidth="$0.5"
    borderColor="$borderColor"
    gap="$3"
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
          <GameRow game={game} />
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
}) => {
  const showSelectionIncomplete = Boolean(isSelectionIncomplete)
  const showError = Boolean(error)
  const showLoading = Boolean(loading)
  const hasDays = days.length > 0

  return (
    <Card padding="$3" elevate backgroundColor="$color1" gap="$3">
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
          {days.map((day) => (
            <DayCard key={day.number} day={day} />
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
