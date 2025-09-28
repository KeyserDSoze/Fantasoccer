import { useEffect, useMemo, useState } from 'react'
import { AlertTriangle, Trophy } from '@tamagui/lucide-icons'
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
import { LeagueRankingWidget } from 'components/rank/LeagueRankingWidget'
import { useApp } from 'providers/AppProvider'
import { useRankByComponents } from 'hooks/useRank'

const formatSeasonFromYear = (yearNumber: number): string => {
  const baseYear = 2011
  const startYear = baseYear + yearNumber
  const endYear = startYear + 1
  return `${startYear}/${String(endYear).slice(-2)}`
}

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

  const shouldSkipRank = !currentGroup || !selectedLeagueId || selectedYear == null

  const {
    loading,
    error,
    refetch,
    enhancedRank,
    getEnhancedRoundRanking,
    availableRounds
  } = useRankByComponents(
    currentGroup?.id ?? null,
    selectedLeagueId ?? null,
    selectedYear ?? null,
    {
      refreshInterval: 120000,
      skip: shouldSkipRank
    }
  )

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
  const seasonLabel = selectedYear != null ? formatSeasonFromYear(selectedYear) : null

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
                showRoundSection={(availableRounds?.length ?? 0) > 0}
              />
            )}

            <LeagueRankingWidget
              leagueName={leagueName}
              seasonLabel={seasonLabel}
              roundLabel={selectedRoundLabel}
              teamCount={roundTeamCount}
              rankedTeams={rankedTeams}
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
