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
import { LeagueSelectorsWidget } from 'components/rank/LeagueSelectorsWidget'
import { LeagueCalendarWidget } from 'components/calendar/LeagueCalendarWidget'
import { useApp } from 'providers/AppProvider'
import { useCalendar } from 'hooks/useCalendar'
import { RankHelper } from 'types/rank'

const formatSeasonFromYear = (yearNumber: number): string => {
  const baseYear = 2011
  const startYear = baseYear + yearNumber
  const endYear = startYear + 1
  return `${startYear}/${String(endYear).slice(-2)}`
}

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
  const containerWidth = media.gtMd ? 960 : '100%'

  const {
    currentGroup,
    userGroups,
    selectedLeagueId,
    selectedYear,
    setSelectedLeague,
    setSelectedYear,
    getAvailableLeagues,
    getAvailableYears,
    isGroupsLoading,
    groupsError
  } = useApp()

  const availableLeagues = getAvailableLeagues()
  const selectedLeague = availableLeagues.find((league) => league.id === selectedLeagueId) ?? null
  const availableYears = selectedLeagueId ? getAvailableYears(selectedLeagueId) : []
  const sortedYears = useMemo(() => [...availableYears].sort((a, b) => b - a), [availableYears])
  const leagueOptions = useMemo(
    () => availableLeagues.map((league) => ({ id: league.id, name: league.name, isMain: league.isMain })),
    [availableLeagues]
  )

  const [selectedRoundId, setSelectedRoundId] = useState<string | null>(null)

  const shouldSkipCalendar = !currentGroup || !selectedLeagueId || selectedYear == null

  const leagueKey = useMemo(() => {
    if (!currentGroup || !selectedLeagueId || selectedYear == null) {
      return null
    }

    return RankHelper.createLeagueKey(currentGroup.id, selectedLeagueId, selectedYear)
  }, [currentGroup, selectedLeagueId, selectedYear])

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
  const seasonLabel = selectedYear != null ? formatSeasonFromYear(selectedYear) : null

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
        width={containerWidth}
        maxWidth={960}
        gap="$4"
        alignItems="stretch"
        alignSelf="center"
      >
        <Card
          padding="$4"
          backgroundColor="$color2"
          borderRadius="$4"
          borderWidth="$0.5"
          borderColor="$borderColor"
          alignItems="stretch"
        >
          <XStack alignItems="center" gap="$3" marginBottom="$2">
            <CalendarRange size="$2" color="$purple10" />
            <H2 color="$color12">Calendario incontri</H2>
          </XStack>

          <Paragraph color="$color10">
            Consulta il calendario delle giornate per la lega selezionata. Usa i selettori per cambiare lega,
            stagione o girone quando disponibili.
          </Paragraph>
        </Card>

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
                selectedLeagueId={selectedLeagueId}
                onSelectLeague={(leagueId) => {
                  void setSelectedLeague(leagueId)
                }}
                years={sortedYears}
                selectedYear={selectedYear}
                onSelectYear={(year) => {
                  void setSelectedYear(year)
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
