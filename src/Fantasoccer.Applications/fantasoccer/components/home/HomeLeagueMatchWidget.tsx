import { useEffect, useMemo, useState } from 'react';
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
  const { currentGroup, selectedLeagueId, selectedYear, getAvailableLeagues, getAvailableYears } =
    useApp();
  const { user } = useAuth();
  const { width } = useWindowDimensions();
  const isDesktop = width >= 900;
  const [localLeagueId, setLocalLeagueId] = useState<string | null>(selectedLeagueId);
  const [localYear, setLocalYear] = useState<number | null>(selectedYear);

  useEffect(() => {
    setLocalLeagueId(selectedLeagueId);
  }, [selectedLeagueId]);
  useEffect(() => {
    setLocalYear(selectedYear);
  }, [selectedYear]);

  const leagues = getAvailableLeagues();
  const leagueOptions = useMemo(
    () => leagues.map((league) => ({ id: league.id, name: league.name, isMain: league.isMain })),
    [leagues]
  );
  const availableYears = useMemo(
    () => (localLeagueId ? getAvailableYears(localLeagueId).sort((a, b) => b - a) : []),
    [getAvailableYears, localLeagueId]
  );

  useEffect(() => {
    if (localLeagueId && !leagues.some((league) => league.id === localLeagueId)) {
      setLocalLeagueId(leagues[0]?.id ?? null);
    }
    return undefined;
  }, [leagues, localLeagueId]);

  useEffect(() => {
    if (availableYears.length > 0 && (localYear == null || !availableYears.includes(localYear))) {
      setLocalYear(availableYears[0]);
    }
    return undefined;
  }, [availableYears, localYear]);

  const leagueKey = useMemo(() => {
    if (!currentGroup || !localLeagueId || localYear == null) return null;
    return RankHelper.createLeagueKey(currentGroup.id, localLeagueId, localYear);
  }, [currentGroup, localLeagueId, localYear]);
  const { days, loading, error, refetch } = useCalendar(leagueKey, { refreshInterval: 60000 });
  const { liveDay, isLive } = useSerieACalendar({
    refreshInterval: 60000,
    enablePolling: true,
    pollOnlyWhenLive: false,
  });

  const {
    loading: rankLoading,
    error: rankError,
    refetch: refetchRank,
    availableRounds,
    getEnhancedRoundRanking,
  } = useRankByComponents(currentGroup?.id ?? null, localLeagueId, localYear, {
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
        selectedLeagueId={localLeagueId}
        onSelectLeague={setLocalLeagueId}
        years={availableYears}
        selectedYear={localYear}
        onSelectYear={setLocalYear}
        formatYearLabel={formatSeasonFromYear}
        title="La tua ultima partita"
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
            flexDirection={isDesktop ? 'row' : 'column'}
          >
            <YStack flex={1} minWidth={0} width={isDesktop ? '50%' : '100%'}>
              <LeagueCalendarWidget
                leagueName={leagues.find((league) => league.id === localLeagueId)?.name}
                seasonLabel={localYear == null ? null : formatSeasonFromYear(localYear)}
                days={[{ ...selectedDay.day, games: selectedDay.games }]}
                groupId={currentGroup.id}
                leagueId={localLeagueId}
                year={localYear}
                editMode="view"
              />
            </YStack>
            <YStack flex={1} minWidth={0} width={isDesktop ? '50%' : '100%'}>
              <LeagueRankingWidget
                leagueName={leagues.find((league) => league.id === localLeagueId)?.name}
                seasonLabel={localYear == null ? null : formatSeasonFromYear(localYear)}
                roundLabel={rankingRoundId === '@' ? 'Classifica generale' : rankingRoundId}
                rankedTeams={rankedTeams}
                loading={rankLoading}
                error={rankError}
                groupId={currentGroup.id}
                year={localYear}
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
