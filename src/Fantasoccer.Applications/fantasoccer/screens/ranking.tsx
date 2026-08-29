import { useEffect, useMemo, useState } from 'react'
import { AlertTriangle, Trophy } from '@tamagui/lucide-icons-2'
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
import { LeagueRankingWidget } from 'components/rank/LeagueRankingWidget'
import { useApp } from 'providers/AppProvider'
import { useRankByComponents } from 'hooks/useRank'
import { useCalendar } from 'hooks/useCalendar'
import { useGroupLeagues } from 'hooks/useGroup'
import { formatSeasonFromYear } from 'utils/season'

const formatRoundLabel = (roundId: string): string => {
  if (roundId === '@') {
    return 'Classifica generale'
  }

  if (/^[A-Za-z]$/.test(roundId)) {
    return `Girone ${roundId.toUpperCase()}`
  }

  if (/^\d+$/.test(roundId)) {
    return `Girone ${roundId}`
  }

  return roundId.toUpperCase()
}

export default function Ranking() {
  const media = useMedia()
  const containerWidth = media.gtMd ? 960 : '100%'

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

  const shouldSkipRank = !currentGroup || !localLeagueId || localYear == null

  const {
    loading,
    error,
    refetch,
    enhancedRank,
    getEnhancedRoundRanking,
    availableRounds
  } = useRankByComponents(
    currentGroup?.id ?? null,
    localLeagueId ?? null,
    localYear ?? null,
    {
      refreshInterval: 120000,
      skip: shouldSkipRank
    }
  )

  // Fetch calendar for luck calculation
  const leagueKey = useMemo(() => {
    if (!currentGroup?.id || !localLeagueId || localYear == null) {
      return null
    }
    return {
      group: currentGroup.id,
      league: localLeagueId,
      year: localYear
    }
  }, [currentGroup?.id, localLeagueId, localYear])

  const {
    calendar,
    loading: calendarLoading,
    error: calendarError
  } = useCalendar(leagueKey, {
    refreshInterval: 120000,
    skip: !leagueKey
  })

  // Fetch annual leagues to get settings
  const { getAnnualLeague } = useGroupLeagues(currentGroup?.id ?? null, localYear ?? undefined)

  // Get the annual league to access settings
  const selectedAnnualLeague = useMemo(() => {
    if (!localLeagueId || !localYear) {
      return null
    }
    return getAnnualLeague(localLeagueId, localYear)
  }, [getAnnualLeague, localLeagueId, localYear])

  // Extract money settings from annual league
  const moneyForGoal = useMemo(
    () => selectedAnnualLeague?.settings?.moneyForGoal ?? 0,
    [selectedAnnualLeague]
  )

  const moneyForSufferedGoal = useMemo(
    () => selectedAnnualLeague?.settings?.moneyForSufferedGoal ?? 0,
    [selectedAnnualLeague]
  )

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
    if (shouldSkipRank) {
      setSelectedRoundId(null)
    }
  }, [shouldSkipRank])

  useEffect(() => {
    if (shouldSkipRank) {
      return
    }

    if (!availableRounds || availableRounds.length === 0) {
      setSelectedRoundId(null)
      return
    }

    if (selectedRoundId && availableRounds.includes(selectedRoundId)) {
      return
    }

    const preferredRound = availableRounds.includes('@') ? '@' : availableRounds[0]
    setSelectedRoundId(preferredRound)
  }, [availableRounds, selectedRoundId, shouldSkipRank])

  const roundOptions = useMemo(
    () => {
      if (!availableRounds) {
        return []
      }

      const sortedRounds = [...availableRounds].sort((a, b) => {
        if (a === '@') return -1
        if (b === '@') return 1
        return a.localeCompare(b)
      })

      return sortedRounds.map((roundId) => ({
        id: roundId,
        label: formatRoundLabel(roundId),
        isDefault: roundId === '@'
      }))
    },
    [availableRounds]
  )

  const selectedRoundLabel = useMemo(() => {
    if (!selectedRoundId) {
      return null
    }

    return roundOptions.find((option) => option.id === selectedRoundId)?.label ?? formatRoundLabel(selectedRoundId)
  }, [roundOptions, selectedRoundId])

  const rankedTeams = useMemo(
    () => (selectedRoundId ? getEnhancedRoundRanking(selectedRoundId) : []),
    [getEnhancedRoundRanking, selectedRoundId]
  )

  const roundTeamCount = selectedRoundId
    ? enhancedRank?.roundTeamCounts?.[selectedRoundId] ?? rankedTeams.length
    : rankedTeams.length

  const leagueName = selectedLeague?.name ?? (availableLeagues.length === 1 ? availableLeagues[0]?.name : null)
  const seasonLabel = localYear != null ? formatSeasonFromYear(localYear) : null

  const hasMultipleGroups = userGroups.length > 1
  const hasMultipleLeagues = availableLeagues.length > 1
  const hasMultipleYears = sortedYears.length > 1
  const hasMultipleRounds = roundOptions.length > 1
  const shouldRenderSelectors = hasMultipleGroups || hasMultipleLeagues || hasMultipleYears || hasMultipleRounds
  const isRoundSelectionIncomplete = (availableRounds?.length ?? 0) > 0 && !selectedRoundId

  return (
    <ScreenContent path="screens/ranking.tsx" title="Classifica">
      <YStack
        width={containerWidth}
        maxWidth={960}
        gap="$4"
        alignItems="stretch"
        alignSelf="center"
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
              Per visualizzare la classifica scegli un gruppo dalla barra laterale e assicurati di avere una lega e una
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
                showRoundSection={(availableRounds?.length ?? 0) > 0}
              />
            )}

            <LeagueRankingWidget
              leagueName={leagueName}
              seasonLabel={seasonLabel}
              roundLabel={selectedRoundLabel}
              roundId={selectedRoundId}
              teamCount={roundTeamCount}
              rankedTeams={rankedTeams}
              groupId={currentGroup?.id}
              year={localYear}
              moneyForGoal={moneyForGoal}
              moneyForSufferedGoal={moneyForSufferedGoal}
              calendar={calendar}
              loading={loading}
              error={error}
              isSelectionIncomplete={shouldSkipRank || isRoundSelectionIncomplete}
              onRefresh={() => {
                void refetch()
              }}
              selectionMessage="Seleziona lega, stagione e girone per visualizzare la classifica."
            />
          </YStack>
        )}
      </YStack>
    </ScreenContent>
  )
}
