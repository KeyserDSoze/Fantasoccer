import { useMemo, useState } from 'react';
import { AlertTriangle, RefreshCw } from '@tamagui/lucide-icons-2';
import { Button, Card, Text, XStack, YStack } from 'tamagui';

import { LeagueSelectorsWidget } from 'components/LeagueSelectorsWidget';
import { LeagueCalendarWidget } from 'components/calendar/LeagueCalendarWidget';
import { LeagueRankingWidget } from 'components/rank/LeagueRankingWidget';
import { ResponsiveWidgetPair } from 'components/layout/ResponsiveWidgetPair';
import { SplashScreen } from 'components/layout/SplashScreen';
import { useCalendar } from 'hooks/useCalendar';
import { useRankByComponents } from 'hooks/useRank';
import { useLiveGroup } from 'hooks/useLiveGroup';
import { useApp } from 'providers/AppProvider';
import { useAuth } from 'providers/AuthProvider';
import { RankHelper, enhanceRankWithTeamPositions } from 'types/rank';
import { homeMatchDisplay } from 'utils/homeMatchDisplay';
import { formatSeasonFromYear } from 'utils/season';
import { goalCreditsEnabled } from 'utils/rankMoney';
import { GroupHelper } from 'types/group';

export function HomeLeagueMatchWidget() {
  const { currentGroup, selectedLeagueId, selectedYear } = useApp();

  // Reset only when the navigation context changes, not when using the widget's local filters.
  return <HomeLeagueMatchContent key={`${currentGroup?.id}/${selectedLeagueId}/${selectedYear}`} />;
}

function HomeLeagueMatchContent() {
  const {
    currentGroup,
    selectedLeagueId: globalLeagueId,
    selectedYear: globalYear,
    getAvailableLeagues,
    getAvailableYears,
  } = useApp();
  const [selectedLeagueId, setSelectedLeague] = useState<string | null>(globalLeagueId);
  const [localYear, setSelectedYear] = useState<number | null>(globalYear);
  const { user } = useAuth();
  const leagues = getAvailableLeagues();
  const leagueOptions = useMemo(
    () => leagues.map((league) => ({ id: league.id, name: league.name, isMain: league.isMain })),
    [leagues]
  );
  const availableYears = useMemo(
    () => (selectedLeagueId ? getAvailableYears(selectedLeagueId).sort((a, b) => b - a) : []),
    [getAvailableYears, selectedLeagueId]
  );

  const selectedYear =
    localYear != null && availableYears.includes(localYear)
      ? localYear
      : (availableYears[0] ?? null);

  const leagueKey = useMemo(() => {
    if (!currentGroup || !selectedLeagueId || selectedYear == null) return null;
    return RankHelper.createLeagueKey(currentGroup.id, selectedLeagueId, selectedYear);
  }, [currentGroup, selectedLeagueId, selectedYear]);
  const { liveGroup, loading: liveLoading, error: liveError, refetch: refetchLive } = useLiveGroup(selectedYear == null ? null : currentGroup?.id ?? null, {
    year: selectedYear ?? undefined,
    refreshInterval: 15000,
  });
  const { days, loading, error, refetch } = useCalendar(leagueKey, {
    refreshInterval: 60000,
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

  const display = useMemo(() => homeMatchDisplay(days, liveGroup, selectedLeagueId, selectedYear), [days, liveGroup, selectedLeagueId, selectedYear]);
  const rounds = display.isLive ? Object.keys(display.rank?.rounds ?? {}) : availableRounds;
  const rankingRoundId = rounds.includes('@') ? '@' : (rounds[0] ?? null);
  const rankedTeams = display.isLive
    ? rankingRoundId && display.rank ? enhanceRankWithTeamPositions(display.rank)[rankingRoundId] ?? [] : []
    : rankingRoundId ? getEnhancedRoundRanking(rankingRoundId) : [];
  const displayError = display.isLive ? liveError : error;

  if (!currentGroup || !user) return null;

  return (
    <YStack gap="$4" width="100%" minWidth={0} flexShrink={0}>
      <LeagueSelectorsWidget
        leagues={leagueOptions}
        selectedLeagueId={selectedLeagueId}
        onSelectLeague={setSelectedLeague}
        years={availableYears}
        selectedYear={selectedYear}
        onSelectYear={setSelectedYear}
        formatYearLabel={formatSeasonFromYear}
        title={display.isLive ? 'Live della tua lega' : 'La tua ultima giornata'}
        leagueHint="Scegli la lega"
        yearHint="Scegli la stagione"
        showRoundSection={false}
        defaultExpanded={leagueOptions.length > 1 || availableYears.length > 1}
      />

      {(loading || liveLoading) && display.days.length === 0 ? (
        <Card padding="$4" backgroundColor="$color2">
          <SplashScreen compact message="Caricamento della giornata…" steps={[
            { id: 'calendar', label: 'Calendario della lega', status: loading ? 'pending' : error ? 'error' : 'done' },
            { id: 'live', label: 'Risultati e classifica Live', status: liveLoading ? 'pending' : liveError ? 'error' : 'done' },
          ]} />
        </Card>
      ) : displayError && display.days.length === 0 ? (
        <Card padding="$4" backgroundColor="$red2">
          <YStack alignItems="center" gap="$3">
            <AlertTriangle color="$red10" />
            <Text color="$red11" textAlign="center">
              {displayError}
            </Text>
            <Button icon={RefreshCw} variant="outlined" onPress={() => { void refetch(); void refetchLive() }}>
              Riprova
            </Button>
          </YStack>
        </Card>
      ) : display.days.length > 0 ? (
        <YStack gap="$3" flexShrink={0}>
          <Text color={display.isLive ? '$red10' : '$color10'} fontWeight="700">
            {display.isLive ? 'Live della lega' : 'Ultima giornata giocata'} · Giornata{' '}
            {display.serieADay}
          </Text>
          <ResponsiveWidgetPair testID="home-league-widgets">
            <LeagueCalendarWidget
              leagueName={leagues.find((league) => league.id === selectedLeagueId)?.name}
              seasonLabel={selectedYear == null ? null : formatSeasonFromYear(selectedYear)}
              days={display.days}
              groupId={currentGroup.id}
              leagueId={selectedLeagueId}
              year={selectedYear}
              editMode={display.isLive ? 'live' : 'view'}
              pulseResults={display.pulseResults}
            />
            <LeagueRankingWidget
              leagueName={leagues.find((league) => league.id === selectedLeagueId)?.name}
              seasonLabel={selectedYear == null ? null : formatSeasonFromYear(selectedYear)}
              roundLabel={rankingRoundId === '@' ? 'Classifica generale' : rankingRoundId}
              rankedTeams={rankedTeams}
              loading={display.isLive ? liveLoading : rankLoading}
              error={display.isLive ? liveError : rankError}
              groupId={currentGroup.id}
              year={selectedYear}
              showGoalCredits={goalCreditsEnabled(
                leagues.find((league) => league.id === selectedLeagueId)?.isMain,
                selectedLeagueId && selectedYear != null && currentGroup ? GroupHelper.getAnnualLeague(currentGroup, selectedLeagueId, selectedYear)?.settings : null
              )}
              onRetry={() => { if (display.isLive) void refetchLive(); else void refetchRank() }}
            />
          </ResponsiveWidgetPair>
        </YStack>
      ) : (
        <Card padding="$4" backgroundColor="$color2">
          <Text color="$color10" textAlign="center">
            {display.isLive ? 'Live in preparazione: i dati compariranno automaticamente al prossimo aggiornamento.' : 'Non ci sono ancora partite giocate per i filtri selezionati.'}
          </Text>
        </Card>
      )}
    </YStack>
  );
}
