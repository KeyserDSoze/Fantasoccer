import { useCallback, useEffect, useMemo, useState } from 'react'
import { AlertTriangle, CalendarRange } from '@tamagui/lucide-icons'
import {
  Card,
  H2,
  Paragraph,
  Spinner,
  Text,
  XStack,
  YStack,
  useMedia
} from 'tamagui'

import { ScreenContent } from 'components/layout/ScreenContent'
import { LeagueSelectorsWidget } from 'components/LeagueSelectorsWidget'
import { LeagueCalendarWidget } from 'components/calendar/LeagueCalendarWidget'
import { useApp } from 'providers/AppProvider'
import { useCalendar } from 'hooks/useCalendar'
import { RankHelper } from 'types/rank'
import { formatSeasonFromYear } from 'utils/season'

const formatRoundLabel = (roundId: string): string => {
  if (roundId === '@') {
    return 'Tutte le giornate'
  }

  if (/^[A-Za-z]$/.test(roundId)) {
    return `Girone ${roundId.toUpperCase()}`
  }

  if (/^\d+$/.test(roundId)) {
    return `Girone ${roundId}`
  }

  return roundId.toUpperCase()
}

export default function CalendarScreen() {
  const media = useMedia()
  const isWideLayout = Boolean(media.gtMd)

  const {
    currentGroup,
    userGroups,
    selectedLeagueId,
    selectedYear,
    getAvailableLeagues,
    getAvailableYears,
    isGroupsLoading,
    groupsError
  } = useApp()

  const [localLeagueId, setLocalLeagueId] = useState<string | null>(selectedLeagueId)
  const [localYear, setLocalYear] = useState<number | null>(selectedYear ?? null)

  useEffect(() => {
    setLocalLeagueId(selectedLeagueId ?? null)
  }, [selectedLeagueId])

  useEffect(() => {
    setLocalYear(selectedYear ?? null)
  }, [selectedYear])

  const availableLeagues = getAvailableLeagues()
  const selectedLeague = availableLeagues.find((league) => league.id === localLeagueId) ?? null
  const availableYears = localLeagueId ? getAvailableYears(localLeagueId) : []
  const sortedYears = useMemo(() => [...availableYears].sort((a, b) => b - a), [availableYears])
  const leagueOptions = useMemo(
    () => availableLeagues.map((league) => ({ id: league.id, name: league.name, isMain: league.isMain })),
    [availableLeagues]
  )

  const [selectedRoundId, setSelectedRoundId] = useState<string | null>(null)

  const shouldSkipCalendar = !currentGroup || !localLeagueId || localYear == null

  const leagueKey = useMemo(() => {
    if (!currentGroup || !localLeagueId || localYear == null) {
      return null
    }

    return RankHelper.createLeagueKey(currentGroup.id, localLeagueId, localYear)
  }, [currentGroup, localLeagueId, localYear])

  const {
    loading,
    error,
    refetch,
    roundKeys,
    pendingGames,
    getRound
  } = useCalendar(leagueKey, {
    refreshInterval: 120000,
    skip: shouldSkipCalendar
  })

  useEffect(() => {
    if (!localLeagueId) {
      if (localYear !== null) {
        setLocalYear(null)
      }
      return
    }

    if (availableYears.length === 0) {
      if (localYear !== null) {
        setLocalYear(null)
      }
      return
    }

    if (localYear == null || !availableYears.includes(localYear)) {
      const latestYear = Math.max(...availableYears)
      setLocalYear(latestYear)
    }
  }, [localLeagueId, availableYears, localYear])

  useEffect(() => {
    if (shouldSkipCalendar) {
      setSelectedRoundId(null)
    }
  }, [shouldSkipCalendar])

  useEffect(() => {
    if (shouldSkipCalendar) {
      return
    }

    if (!roundKeys || roundKeys.length === 0) {
      setSelectedRoundId(null)
      return
    }

    if (selectedRoundId && roundKeys.includes(selectedRoundId)) {
      return
    }

    const preferredRound = roundKeys.includes('@') ? '@' : roundKeys[0]
    setSelectedRoundId(preferredRound)
  }, [roundKeys, selectedRoundId, shouldSkipCalendar])

  const roundOptions = useMemo(() => {
    if (!roundKeys) {
      return []
    }

    const sortedRounds = [...roundKeys].sort((a, b) => {
      if (a === '@') return -1
      if (b === '@') return 1
      return a.localeCompare(b)
    })

    return sortedRounds.map((roundId) => ({
      id: roundId,
      label: formatRoundLabel(roundId),
      isDefault: roundId === '@'
    }))
  }, [roundKeys])

  const selectedRoundLabel = useMemo(() => {
    if (!selectedRoundId) {
      return null
    }

    return roundOptions.find((option) => option.id === selectedRoundId)?.label ?? formatRoundLabel(selectedRoundId)
  }, [roundOptions, selectedRoundId])

  const selectedRoundDays = useMemo(() => {
    if (!selectedRoundId) {
      return []
    }

    return getRound(selectedRoundId)
  }, [getRound, selectedRoundId])

  const leagueName = selectedLeague?.name ?? (availableLeagues.length === 1 ? availableLeagues[0]?.name : null)
  const seasonLabel = localYear != null ? formatSeasonFromYear(localYear) : null

  const hasMultipleGroups = userGroups.length > 1
  const hasMultipleLeagues = availableLeagues.length > 1
  const hasMultipleYears = sortedYears.length > 1
  const hasMultipleRounds = roundOptions.length > 1
  const shouldRenderSelectors = hasMultipleGroups || hasMultipleLeagues || hasMultipleYears || hasMultipleRounds
  const isSelectionIncomplete = (roundKeys?.length ?? 0) > 0 && !selectedRoundId

  const handleRefresh = useCallback(() => {
    void refetch()
  }, [refetch])

  return (
    <ScreenContent path="screens/calendar.tsx" title="Calendario">
      <YStack
        width="100%"
        maxWidth={isWideLayout ? '100%' : 960}
        gap="$4"
        alignItems="stretch"
        alignSelf="stretch"
      >
        {isGroupsLoading ? (
          <Card
            padding="$4"
            backgroundColor="$color2"
            borderRadius="$4"
            borderWidth="$0.5"
            borderColor="$borderColor"
          >
            <XStack alignItems="center" gap="$3" justifyContent="center">
              <Spinner size="small" color="$blue10" />
              <Text color="$color10">Caricamento dati della lega...</Text>
            </XStack>
          </Card>
        ) : groupsError ? (
          <Card
            padding="$4"
            backgroundColor="$red3"
            borderRadius="$4"
            borderWidth="$0.5"
            borderColor="$red7"
          >
            <XStack alignItems="center" gap="$2">
              <AlertTriangle size="$1.5" color="$red10" />
              <Text color="$red11" fontWeight="600">
                {groupsError}
              </Text>
            </XStack>
            <Paragraph marginTop="$2" color="$red11">
              Prova ad aggiornare la pagina o selezionare nuovamente il gruppo dalla barra di navigazione.
            </Paragraph>
          </Card>
        ) : !currentGroup ? (
          <Card
            padding="$4"
            backgroundColor="$color2"
            borderRadius="$4"
            borderWidth="$0.5"
            borderColor="$borderColor"
          >
            <Text color="$color12" fontWeight="600" marginBottom="$2">
              Nessun gruppo selezionato
            </Text>
            <Paragraph color="$color10">
              Per visualizzare il calendario scegli un gruppo dalla barra laterale e assicurati di avere una lega e una
              stagione associate.
            </Paragraph>
          </Card>
        ) : (
          <YStack gap="$4">
            {shouldRenderSelectors && (
              <LeagueSelectorsWidget
                leagues={leagueOptions}
                selectedLeagueId={localLeagueId}
                onSelectLeague={(leagueId) => {
                  setLocalLeagueId(leagueId)
                }}
                years={sortedYears}
                selectedYear={localYear}
                onSelectYear={(year) => {
                  setLocalYear(year)
                }}
                formatYearLabel={formatSeasonFromYear}
                emptyLeagueLabel={leagueName ?? '—'}
                rounds={roundOptions}
                selectedRoundId={selectedRoundId}
                onSelectRound={(roundId) => {
                  setSelectedRoundId(roundId)
                }}
                showRoundSection={(roundKeys?.length ?? 0) > 0}
                title="Filtri calendario"
                roundHint="Scegli il girone da visualizzare"
              />
            )}

            <LeagueCalendarWidget
              leagueName={leagueName}
              seasonLabel={seasonLabel}
              roundLabel={selectedRoundLabel}
              pendingGamesCount={pendingGames.length}
              days={selectedRoundDays}
              groupId={currentGroup?.id}
              leagueId={localLeagueId}
              year={localYear}
              loading={loading}
              error={error}
              isSelectionIncomplete={shouldSkipCalendar || isSelectionIncomplete}
              onRefresh={handleRefresh}
              selectionMessage="Seleziona lega, stagione e girone per visualizzare il calendario."
              emptyMessage="Nessuna giornata disponibile per i filtri selezionati."
              loadingMessage="Caricamento calendario..."
              refreshLabel="Aggiorna calendario"
              errorActionLabel="Riprova"
            />
          </YStack>
        )}
      </YStack>
    </ScreenContent>
  )
}
