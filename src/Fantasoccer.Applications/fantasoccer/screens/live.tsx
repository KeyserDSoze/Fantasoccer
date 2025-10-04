import { useCallback, useEffect, useMemo, useState } from 'react'
import { AlertTriangle, RefreshCw, Zap } from '@tamagui/lucide-icons'
import { Button, Card, Paragraph, Spinner, Text, XStack, YStack, useMedia } from 'tamagui'

import { ScreenContent } from 'components/layout/ScreenContent'
import { LeagueSelectorsWidget } from 'components/LeagueSelectorsWidget'
import { LeagueCalendarWidget } from 'components/calendar/LeagueCalendarWidget'
import { LeagueRankingWidget } from 'components/rank/LeagueRankingWidget'
import { useApp } from 'providers/AppProvider'
import { useLiveGroup } from 'hooks/useLiveGroup'
import { enhanceRankWithTeamPositions } from 'types/rank'
import type { EnhancedRankedTeam } from 'types/rank'
import type { CalendarDay } from 'types/calendar'

type RoundLabelFormatter = (roundId: string) => string

const formatRoundLabel: RoundLabelFormatter = (roundId) => {
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

export default function LiveScreen() {
  const {
    currentGroup,
    userGroups,
    selectedLeagueId,
    getAvailableLeagues,
    isGroupsLoading,
    groupsError,
  } = useApp()

  const media = useMedia()
  const isWideLayout = Boolean(media.gtMd)

  const [localLeagueId, setLocalLeagueId] = useState<string | null>(selectedLeagueId ?? null)

  useEffect(() => {
    setLocalLeagueId(selectedLeagueId ?? null)
  }, [selectedLeagueId])

  const availableLeagues = getAvailableLeagues()

  const groupId = currentGroup?.id ?? null

  const {
    enhancedLiveGroup,
    loading: liveLoading,
    refreshing: liveRefreshing,
    error: liveError,
    refetch: refetchLive,
  } = useLiveGroup(groupId, {
    refreshInterval: 15000,
  })

  const liveLeagues = useMemo(() => enhancedLiveGroup?.leaguesWithRounds ?? [], [enhancedLiveGroup])

  useEffect(() => {
    if (liveLeagues.length === 0) {
      return
    }

    if (localLeagueId && liveLeagues.some((league) => league.id === localLeagueId)) {
      return
    }

    const fallbackFromSelection = selectedLeagueId
      ? liveLeagues.find((league) => league.id === selectedLeagueId)
      : undefined
    const nextLeague = fallbackFromSelection?.id ?? liveLeagues[0]?.id ?? null

    if (nextLeague && nextLeague !== localLeagueId) {
      setLocalLeagueId(nextLeague)
    }
  }, [liveLeagues, localLeagueId, selectedLeagueId])

  const leagueOptions = useMemo(() => {
    if (liveLeagues.length === 0) {
      return []
    }

    const leagueMap = new Map(availableLeagues.map((league) => [league.id, league]))

    return liveLeagues.map((league) => {
      const metadata = leagueMap.get(league.id)
      return {
        id: league.id,
        name: metadata?.name ?? league.name,
        isMain: metadata?.isMain,
      }
    })
  }, [availableLeagues, liveLeagues])

  const selectedLiveLeague = useMemo(
    () => liveLeagues.find((league) => league.id === localLeagueId) ?? null,
    [liveLeagues, localLeagueId]
  )

  const liveDays = useMemo(() => {
    if (!selectedLiveLeague) {
      return []
    }

    return selectedLiveLeague.roundKeys
      .map((roundKey) => selectedLiveLeague.rounds[roundKey])
      .filter((day): day is CalendarDay => Boolean(day))
  }, [selectedLiveLeague])

  const enhancedRoundMap = useMemo(() => {
    if (!selectedLiveLeague?.rank) {
      return {}
    }

    return enhanceRankWithTeamPositions(selectedLiveLeague.rank)
  }, [selectedLiveLeague])

  const rankingRoundId = useMemo(() => {
    if (!selectedLiveLeague?.rank) {
      return null
    }

    if (selectedLiveLeague.rank.rounds['@']) {
      return '@'
    }

    const availableRounds = selectedLiveLeague.enhancedRank?.availableRounds
    if (availableRounds && availableRounds.length > 0) {
      return availableRounds[0]
    }

    const fallbackKeys = Object.keys(selectedLiveLeague.rank.rounds)
    return fallbackKeys.length > 0 ? fallbackKeys[0] : null
  }, [selectedLiveLeague])

  const rankedTeams: EnhancedRankedTeam[] = useMemo(() => {
    if (!rankingRoundId) {
      return []
    }

    return enhancedRoundMap[rankingRoundId] ?? []
  }, [enhancedRoundMap, rankingRoundId])

  const rankingRoundLabel = useMemo(() => (rankingRoundId ? formatRoundLabel(rankingRoundId) : null), [rankingRoundId])

  const rankingTeamCount = useMemo(() => {
    if (!rankingRoundId) {
      return rankedTeams.length
    }
    return selectedLiveLeague?.enhancedRank?.roundTeamCounts?.[rankingRoundId] ?? rankedTeams.length
  }, [rankedTeams.length, rankingRoundId, selectedLiveLeague?.enhancedRank?.roundTeamCounts])

  const pendingGamesCount = selectedLiveLeague?.pendingGames.length ?? 0

  const serieADayLabel = selectedLiveLeague?.rank?.serieADay
    ? `Serie A giornata ${selectedLiveLeague.rank.serieADay}`
    : null

  // Extract the latest year from the group's baskets
  const liveYear = useMemo(() => {
    if (!currentGroup?.baskets) {
      return null
    }

    const allYears = currentGroup.baskets
      .flatMap(basket => basket.years?.map(y => y.year) ?? [])
      .filter(year => year != null)

    return allYears.length > 0 ? Math.max(...allYears) : null
  }, [currentGroup])

  const handleLeagueSelect = useCallback(
    (leagueId: string) => {
      setLocalLeagueId(leagueId)
    },
    []
  )

  const isSelectorVisible = leagueOptions.length > 0
  const showContent = Boolean(selectedLiveLeague)

  return (
    <ScreenContent path="screens/live.tsx" title="Live">
      <YStack
        width="100%"
        maxWidth={isWideLayout ? '100%' : 960}
        gap="$4"
        alignItems="stretch"
        alignSelf={isWideLayout ? 'stretch' : 'center'}
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
        ) : !currentGroup || userGroups.length === 0 ? (
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
              Per visualizzare il live seleziona un gruppo dalla barra laterale e assicurati di avere una lega associata.
            </Paragraph>
          </Card>
        ) : (
          <YStack gap="$4">
            {isSelectorVisible && (
              <LeagueSelectorsWidget
                leagues={leagueOptions}
                selectedLeagueId={localLeagueId}
                onSelectLeague={handleLeagueSelect}
                years={[]}
                selectedYear={null}
                showYearSection={false}
                showRoundSection={false}
                title="Seleziona la lega live"
                leagueHint="Scegli la lega da monitorare in tempo reale"
                emptyLeagueLabel="Nessuna lega con dati live"
              />
            )}

            <Card
              padding="$4"
              backgroundColor="$color2"
              borderRadius="$4"
              borderWidth="$0.5"
              borderColor="$borderColor"
              gap="$3"
            >
              <XStack alignItems="center" gap="$3" flexWrap="wrap">
                <Zap size="$1.5" color="$yellow10" />
                <YStack gap="$1" flex={1}>
                  <Text fontSize="$5" fontWeight="700" color="$color12">
                    Aggiornamento live
                  </Text>
                  <Text color="$color10" fontSize="$3">
                    {pendingGamesCount > 0
                      ? `${pendingGamesCount} partite ancora in corso`
                      : 'Nessuna partita in sospeso: tutti i risultati sono aggiornati'}
                  </Text>
                </YStack>
                <Button
                  size="$2"
                  icon={RefreshCw}
                  variant="outlined"
                  onPress={() => {
                    void refetchLive()
                  }}
                  disabled={liveLoading || liveRefreshing}
                >
                  Aggiorna
                </Button>
              </XStack>
              {liveLoading && (
                <XStack alignItems="center" gap="$2">
                  <Spinner size="small" color="$blue10" />
                  <Text color="$color10">Sincronizzazione dati live...</Text>
                </XStack>
              )}
              {liveError && (
                <Text color="$red10" fontSize="$2">
                  {liveError}
                </Text>
              )}
            </Card>

            {!showContent ? (
              <Card
                padding="$4"
                backgroundColor="$color2"
                borderRadius="$4"
                borderWidth="$0.5"
                borderColor="$borderColor"
              >
                <Text color="$color10" textAlign="center">
                  Non ci sono dati live disponibili per la lega selezionata al momento.
                </Text>
              </Card>
            ) : isWideLayout ? (
              <XStack gap="$4" alignItems="stretch" flexWrap="nowrap">
                <YStack flex={1} minWidth={0} gap="$4">
                  <LeagueCalendarWidget
                    leagueName={selectedLiveLeague?.name}
                    seasonLabel={serieADayLabel}
                    roundLabel={rankingRoundLabel}
                    pendingGamesCount={pendingGamesCount}
                    days={liveDays}
                    loading={liveLoading && liveDays.length === 0}
                    error={liveError}
                    isSelectionIncomplete={!localLeagueId}
                    onRefresh={() => {
                      void refetchLive()
                    }}
                    selectionMessage="Seleziona una lega per visualizzare il calendario live."
                    emptyMessage="Nessuna giornata live disponibile per la lega selezionata."
                    loadingMessage="Caricamento calendario live..."
                    refreshLabel="Aggiorna live"
                    errorActionLabel="Riprova"
                    groupId={groupId}
                    leagueId={localLeagueId}
                    year={liveYear}
                  />
                </YStack>
                <YStack flex={1} minWidth={0} gap="$4">
                  <LeagueRankingWidget
                    leagueName={selectedLiveLeague?.name}
                    seasonLabel={serieADayLabel}
                    roundLabel={rankingRoundLabel}
                    teamCount={rankingTeamCount}
                    rankedTeams={rankedTeams}
                    loading={liveLoading && rankedTeams.length === 0}
                    error={liveError}
                    isSelectionIncomplete={!localLeagueId}
                    onRefresh={() => {
                      void refetchLive()
                    }}
                    selectionMessage="Seleziona una lega per visualizzare la classifica live."
                    emptyMessage="La classifica live non è disponibile per la lega selezionata."
                    loadingMessage="Caricamento classifica live..."
                    refreshLabel="Aggiorna live"
                    errorActionLabel="Riprova"
                  />
                </YStack>
              </XStack>
            ) : (
              <YStack gap="$4">
                <LeagueCalendarWidget
                  leagueName={selectedLiveLeague?.name}
                  seasonLabel={serieADayLabel}
                  roundLabel={rankingRoundLabel}
                  pendingGamesCount={pendingGamesCount}
                  days={liveDays}
                  loading={liveLoading && liveDays.length === 0}
                  error={liveError}
                  isSelectionIncomplete={!localLeagueId}
                  onRefresh={() => {
                    void refetchLive()
                  }}
                  selectionMessage="Seleziona una lega per visualizzare il calendario live."
                  emptyMessage="Nessuna giornata live disponibile per la lega selezionata."
                  loadingMessage="Caricamento calendario live..."
                  refreshLabel="Aggiorna live"
                  errorActionLabel="Riprova"
                  groupId={groupId}
                  leagueId={localLeagueId}
                  year={liveYear}
                />

                <LeagueRankingWidget
                  leagueName={selectedLiveLeague?.name}
                  seasonLabel={serieADayLabel}
                  roundLabel={rankingRoundLabel}
                  teamCount={rankingTeamCount}
                  rankedTeams={rankedTeams}
                  loading={liveLoading && rankedTeams.length === 0}
                  error={liveError}
                  isSelectionIncomplete={!localLeagueId}
                  onRefresh={() => {
                    void refetchLive()
                  }}
                  selectionMessage="Seleziona una lega per visualizzare la classifica live."
                  emptyMessage="La classifica live non è disponibile per la lega selezionata."
                  loadingMessage="Caricamento classifica live..."
                  refreshLabel="Aggiorna live"
                  errorActionLabel="Riprova"
                />
              </YStack>
            )}
          </YStack>
        )}
      </YStack>
    </ScreenContent>
  )
}
