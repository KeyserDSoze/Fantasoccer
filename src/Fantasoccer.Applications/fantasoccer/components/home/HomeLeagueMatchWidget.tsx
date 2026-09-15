import { useEffect, useMemo, useState } from 'react';
import { AlertTriangle, RefreshCw } from '@tamagui/lucide-icons-2';
import { Button, Card, Spinner, Text, XStack, YStack } from 'tamagui';

import { LeagueSelectorsWidget } from 'components/LeagueSelectorsWidget';
import { LeagueCalendarWidget } from 'components/calendar/LeagueCalendarWidget';
import { useCalendar } from 'hooks/useCalendar';
import { useSerieACalendar } from 'hooks/useSerieACalendar';
import { useApp } from 'providers/AppProvider';
import { useAuth } from 'providers/AuthProvider';
import { RankHelper } from 'types/rank';
import { formatSeasonFromYear } from 'utils/season';

export function HomeLeagueMatchWidget() {
  const { currentGroup, selectedLeagueId, selectedYear, getAvailableLeagues, getAvailableYears } =
    useApp();
  const { user } = useAuth();
  const [localLeagueId, setLocalLeagueId] = useState<string | null>(selectedLeagueId);
  const [localYear, setLocalYear] = useState<number | null>(selectedYear);

  useEffect(() => {
    const timeout = setTimeout(() => setLocalLeagueId(selectedLeagueId), 0);
    return () => clearTimeout(timeout);
  }, [selectedLeagueId]);
  useEffect(() => {
    const timeout = setTimeout(() => setLocalYear(selectedYear), 0);
    return () => clearTimeout(timeout);
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
      const timeout = setTimeout(() => setLocalLeagueId(leagues[0]?.id ?? null), 0);
      return () => clearTimeout(timeout);
    }
    return undefined;
  }, [leagues, localLeagueId]);

  useEffect(() => {
    if (availableYears.length > 0 && (localYear == null || !availableYears.includes(localYear))) {
      const timeout = setTimeout(() => setLocalYear(availableYears[0]), 0);
      return () => clearTimeout(timeout);
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

  const userOwners = (() => {
    if (!currentGroup || !localLeagueId || localYear == null || !user?.email)
      return new Set<string>();
    const league = currentGroup.leagues.find((candidate) => candidate.id === localLeagueId);
    const basketIds = new Set(league?.basketsId ?? []);
    const email = user.email.toLowerCase();
    return new Set(
      currentGroup.baskets
        .filter((basket) => basketIds.has(basket.id))
        .flatMap((basket) => basket.years.filter((year) => year.year === localYear))
        .flatMap((year) => year.teams)
        .filter(
          (team) =>
            team.owner.toLowerCase() === email ||
            team.additionalOwners?.some((owner) => owner.toLowerCase() === email)
        )
        .map((team) => team.owner.toLowerCase())
    );
  })();

  const selectedDay = useMemo(() => {
    const ownDays = days
      .map((day) => ({
        day,
        games: day.games.filter(
          (game) =>
            userOwners.has(game.homeOwner.toLowerCase()) ||
            userOwners.has(game.awayOwner.toLowerCase())
        ),
      }))
      .filter(({ games }) => games.length > 0);

    if (ownDays.length === 0) return null;
    const live = isLive && liveDay && ownDays.find(({ day }) => day.serieADay === liveDay.serieADay);
    if (live) return { ...live, isLive: true };

    const previous = ownDays
      .filter(({ games }) => games.some((game) => Boolean(game.result)))
      .sort((left, right) => right.day.serieADay - left.day.serieADay)[0];
    return previous ? { ...previous, isLive: false } : null;
  }, [days, isLive, liveDay, userOwners]);

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
        <YStack gap="$2">
          <Text color={selectedDay.isLive ? '$red10' : '$color10'} fontWeight="700">
            {selectedDay.isLive ? 'Partita live' : 'Ultima partita giocata'} · Giornata{' '}
            {selectedDay.day.serieADay}
          </Text>
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
