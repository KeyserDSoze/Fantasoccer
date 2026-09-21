import { useMemo } from 'react';
import { AlertTriangle, RefreshCw } from '@tamagui/lucide-icons-2';
import { Button, Card, Spinner, Text, XStack, YStack } from 'tamagui';
import { useWindowDimensions } from 'react-native';

import { LeagueSelectorsWidget } from 'components/LeagueSelectorsWidget';
import { LeagueCalendarWidget } from 'components/calendar/LeagueCalendarWidget';
import { LeagueRankingWidget } from 'components/rank/LeagueRankingWidget';
import { useCalendar } from 'hooks/useCalendar';
import { useRankByComponents } from 'hooks/useRank';
import { useSerieACalendar } from 'hooks/useSerieACalendar';
import { useApp } from 'providers/AppProvider';
import { useAuth } from 'providers/AuthProvider';
import { RankHelper } from 'types/rank';
import { formatSeasonFromYear } from 'utils/season';

export function HomeLeagueMatchWidget() {
  const {
    currentGroup,
    selectedLeagueId,
    selectedYear,
    setSelectedLeague,
    setSelectedYear,
    getAvailableLeagues,
    getAvailableYears,
  } = useApp();
  const { user } = useAuth();
  const { width } = useWindowDimensions();
  const isDesktop = width >= 900;
  const leagues = getAvailableLeagues();
  const leagueOptions = useMemo(
    () => leagues.map((league) => ({ id: league.id, name: league.name, isMain: league.isMain })),
    [leagues]
  );
  const availableYears = useMemo(
    () => (selectedLeagueId ? getAvailableYears(selectedLeagueId).sort((a, b) => b - a) : []),
    [getAvailableYears, selectedLeagueId]
  );

  const leagueKey = useMemo(() => {
    if (!currentGroup || !selectedLeagueId || selectedYear == null) return null;
    return RankHelper.createLeagueKey(currentGroup.id, selectedLeagueId, selectedYear);
  }, [currentGroup, selectedLeagueId, selectedYear]);
  const { liveDay, isLive } = useSerieACalendar({
    refreshInterval: 15000,
    enablePolling: true,
    pollOnlyWhenLive: false,
  });
  const { days, loading, error, refetch } = useCalendar(leagueKey, {
    refreshInterval: isLive ? 15000 : 60000,
  });

  const {
    loading: rankLoading,
    error: rankError,
    refetch: refetchRank,
    availableRounds,
    getEnhancedRoundRanking,
  } = useRankByComponents(currentGroup?.id ?? null, selectedLeagueId, selectedYear, {
    refreshInterval: 60000,
  });

  const selectedDay = useMemo(() => {
    const ownDays = days
      .filter((day) => day.games.length > 0)
      .map((day) => ({ day, games: day.games }));

    if (ownDays.length === 0) return null;
    const live =
      isLive && liveDay && ownDays.find(({ day }) => day.serieADay === liveDay.serieADay);
    if (live) return { ...live, isLive: true };

    const previous = ownDays
      .filter(({ games }) => games.some((game) => Boolean(game.result)))
      .sort((left, right) => right.day.serieADay - left.day.serieADay)[0];
    return previous ? { ...previous, isLive: false } : null;
  }, [days, isLive, liveDay]);

  const rankingRoundId = availableRounds.includes('@') ? '@' : (availableRounds[0] ?? null);
  const rankedTeams = rankingRoundId ? getEnhancedRoundRanking(rankingRoundId) : [];

  if (!currentGroup || !user) return null;

  return (
    <YStack gap="$3" width="100%">
      <LeagueSelectorsWidget
        leagues={leagueOptions}
        selectedLeagueId={selectedLeagueId}
        onSelectLeague={(leagueId) => void setSelectedLeague(leagueId)}
        years={availableYears}
        selectedYear={selectedYear}
        onSelectYear={(year) => void setSelectedYear(year)}
        formatYearLabel={formatSeasonFromYear}
        title={isLive ? 'Partita live' : 'La tua ultima partita'}
        leagueHint="Scegli la lega"
        yearHint="Scegli la stagione"
        showRoundSection={false}
        defaultExpanded={leagueOptions.length > 1 || availableYears.length > 1}
      />

      {loading && !selectedDay ? (
        <Card padding="$4" backgroundColor="$color2">
          <XStack justifyContent="center" alignItems="center" gap="$3">
            <Spinner size="small" color="$blue10" />
            <Text color="$color10">Caricamento ultima partita...</Text>
          </XStack>
        </Card>
      ) : error ? (
        <Card padding="$4" backgroundColor="$red2">
          <YStack alignItems="center" gap="$3">
            <AlertTriangle color="$red10" />
            <Text color="$red11" textAlign="center">
              {error}
            </Text>
            <Button icon={RefreshCw} variant="outlined" onPress={() => void refetch()}>
              Riprova
            </Button>
          </YStack>
        </Card>
      ) : selectedDay ? (
        <YStack gap="$3">
          <Text color={selectedDay.isLive ? '$red10' : '$color10'} fontWeight="700">
            {selectedDay.isLive ? 'Partita live' : 'Ultima partita giocata'} · Giornata{' '}
            {selectedDay.day.serieADay}
          </Text>
          <XStack
            gap="$3"
            width="100%"
            alignItems="flex-start"
            flexDirection={isDesktop ? 'row' : 'column'}>
            <YStack flex={1} minWidth={0} width={isDesktop ? '50%' : '100%'}>
              <LeagueCalendarWidget
                leagueName={leagues.find((league) => league.id === selectedLeagueId)?.name}
                seasonLabel={selectedYear == null ? null : formatSeasonFromYear(selectedYear)}
                days={[{ ...selectedDay.day, games: selectedDay.games }]}
                groupId={currentGroup.id}
                leagueId={selectedLeagueId}
                year={selectedYear}
                editMode="view"
              />
            </YStack>
            <YStack flex={1} minWidth={0} width={isDesktop ? '50%' : '100%'}>
              <LeagueRankingWidget
                leagueName={leagues.find((league) => league.id === selectedLeagueId)?.name}
                seasonLabel={selectedYear == null ? null : formatSeasonFromYear(selectedYear)}
                roundLabel={rankingRoundId === '@' ? 'Classifica generale' : rankingRoundId}
                rankedTeams={rankedTeams}
                loading={rankLoading}
                error={rankError}
                groupId={currentGroup.id}
                year={selectedYear}
                onRefresh={() => void refetchRank()}
              />
            </YStack>
          </XStack>
        </YStack>
      ) : (
        <Card padding="$4" backgroundColor="$color2">
          <Text color="$color10" textAlign="center">
            Non ci sono ancora partite giocate per i filtri selezionati.
          </Text>
        </Card>
      )}
    </YStack>
  );
}
