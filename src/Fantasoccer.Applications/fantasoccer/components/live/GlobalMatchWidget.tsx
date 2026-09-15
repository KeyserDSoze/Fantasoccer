import { useEffect, useState } from 'react';
import { Platform, ScrollView } from 'react-native';
import { useNavigation, useNavigationState } from '@react-navigation/native';
import {
  CalendarClock,
  ChevronDown,
  ChevronUp,
  ExternalLink,
  RadioTower,
  Trophy,
  X,
} from '@tamagui/lucide-icons-2';
import { Button, Card, Dialog, Spinner, Text, XStack, YStack, useMedia } from 'tamagui';

import { useLiveGroup } from 'hooks/useLiveGroup';
import { useSerieACalendar } from 'hooks/useSerieACalendar';
import type { NavigationProp } from 'navigation';
import { useApp } from 'providers/AppProvider';
import { useAuth } from 'providers/AuthProvider';
import { settingsService, subscribeToUserSettings } from 'services/settingsService';
import type { CalendarGame } from 'types/calendar';
import type { League } from 'types/group';
import type { UserSettings } from 'types/settings';
import { DEFAULT_USER_SETTINGS } from 'types/settings';
import { getActualFantacalcioYear } from 'utils/timezoneHelpers';

type UserMatch = {
  league: League;
  game: CalendarGame;
  serieADay: number;
};

export function GlobalMatchWidget() {
  const { isAuthenticated, isLoading, user } = useAuth();
  const activeRouteName = useNavigationState((state) => state.routes[state.index]?.name);

  if (
    isLoading ||
    !isAuthenticated ||
    !user ||
    activeRouteName === 'Live' ||
    activeRouteName === 'Game'
  ) {
    return null;
  }

  return <AuthenticatedGlobalMatchWidget key={user.email} />;
}

function AuthenticatedGlobalMatchWidget() {
  const [settings, setSettings] = useState<UserSettings | null>(null);
  const [dismissedForSession, setDismissedForSession] = useState(false);

  useEffect(() => {
    let mounted = true;
    settingsService
      .getUserSettings()
      .then((value) => {
        if (mounted) setSettings(value);
      })
      .catch(() => {
        if (mounted) setSettings(DEFAULT_USER_SETTINGS);
      });
    const unsubscribe = subscribeToUserSettings((value) => {
      if (mounted) setSettings(value);
    });
    return () => {
      mounted = false;
      unsubscribe();
    };
  }, []);

  if (!settings?.liveWidgetEnabled || dismissedForSession) return null;

  return (
    <GlobalMatchWidgetContent
      settings={settings}
      onDismissForSession={() => setDismissedForSession(true)}
    />
  );
}

function GlobalMatchWidgetContent({
  settings,
  onDismissForSession,
}: {
  settings: UserSettings;
  onDismissForSession: () => void;
}) {
  const media = useMedia();
  const isMobile = Boolean(media.sm);
  const navigation = useNavigation<NavigationProp>();
  const { user } = useAuth();
  const { currentGroup, setSelectedLeague } = useApp();
  const [expanded, setExpanded] = useState(false);
  const [dismissDialogOpen, setDismissDialogOpen] = useState(false);
  const [disabling, setDisabling] = useState(false);
  const [disableError, setDisableError] = useState(false);
  const [now, setNow] = useState<number | null>(null);
  const year = getActualFantacalcioYear();
  const groupId = currentGroup?.id ?? null;

  const { liveDay, nextDay } = useSerieACalendar({
    refreshInterval: 60000,
    enablePolling: true,
    pollOnlyWhenLive: false,
  });
  const isLiveDay = Boolean(liveDay);
  const { enhancedLiveGroup, loading } = useLiveGroup(groupId, {
    refreshInterval: isLiveDay ? 15000 : 60000,
  });

  useEffect(() => {
    if (isLiveDay) return;
    const timeout = setTimeout(() => setNow(Date.now()), 0);
    const interval = setInterval(() => setNow(Date.now()), 1000);
    return () => {
      clearTimeout(timeout);
      clearInterval(interval);
    };
  }, [isLiveDay]);

  const matches = (() => {
    if (!currentGroup || !user?.email || !enhancedLiveGroup) return [];
    const targetDay = liveDay?.serieADay ?? nextDay?.serieADay;
    if (!targetDay) return [];
    const userEmail = user.email.toLowerCase();
    const result: UserMatch[] = [];

    for (const liveLeague of enhancedLiveGroup.leaguesWithRounds) {
      const league = currentGroup.leagues.find((candidate) => candidate.id === liveLeague.id);
      if (!league || !league.years.some((annual) => annual.year === year)) continue;
      const basketIds = new Set(league.basketsId);
      const owners = new Set(
        currentGroup.baskets
          .filter((basket) => basketIds.has(basket.id))
          .flatMap((basket) => basket.years.filter((item) => item.year === year))
          .flatMap((item) => item.teams)
          .filter(
            (team) =>
              team.owner.toLowerCase() === userEmail ||
              team.additionalOwners?.some((owner) => owner.toLowerCase() === userEmail)
          )
          .map((team) => team.owner.toLowerCase())
      );

      for (const round of liveLeague.roundsList) {
        if (round.day.serieADay !== targetDay) continue;
        for (const game of round.day.games) {
          if (
            owners.has(game.homeOwner.toLowerCase()) ||
            owners.has(game.awayOwner.toLowerCase())
          ) {
            result.push({ league, game, serieADay: round.day.serieADay });
          }
        }
      }
    }

    return result.sort((left, right) => Number(right.league.isMain) - Number(left.league.isMain));
  })();

  const mainMatch = matches.find((match) => match.league.isMain) ?? matches[0] ?? null;
  const currentLeagues =
    currentGroup?.leagues.filter((league) => league.years.some((annual) => annual.year === year)) ??
    [];
  const mainLeague = currentLeagues.find((league) => league.isMain) ?? currentLeagues[0];
  const nextDates =
    nextDay?.games
      .filter((game) => !game.delayed && game.date)
      .map((game) => game.date.getTime()) ?? [];
  const nextStart = nextDates.length > 0 ? Math.min(...nextDates) : null;
  const countdown = (() => {
    if (!nextStart || now === null) return null;
    const remaining = Math.max(0, nextStart - now);
    const days = Math.floor(remaining / 86400000);
    const hours = Math.floor((remaining % 86400000) / 3600000);
    const minutes = Math.floor((remaining % 3600000) / 60000);
    const seconds = Math.floor((remaining % 60000) / 1000);
    return `${days > 0 ? `${days}g ` : ''}${hours.toString().padStart(2, '0')}:${minutes
      .toString()
      .padStart(2, '0')}:${seconds.toString().padStart(2, '0')}`;
  })();

  const navigateToLeagueLive = async (leagueId: string) => {
    await setSelectedLeague(leagueId);
    setExpanded(false);
    navigation.navigate('Live');
  };

  const navigateToGame = async (match: UserMatch) => {
    await setSelectedLeague(match.league.id);
    setExpanded(false);
    navigation.navigate('Game', {
      groupId,
      leagueId: match.league.id,
      year: year.toString(),
      gameId: match.game.id,
      editMode: 'live',
    });
  };

  const navigateToFormation = () => {
    if (!mainLeague) return;
    navigation.navigate('Edit', { leagueId: mainLeague.id, year });
  };

  const disablePermanently = async () => {
    setDisabling(true);
    setDisableError(false);
    try {
      const updated = await settingsService.updateUserSettings({
        ...settings,
        liveWidgetEnabled: false,
      });
      if (updated) setDismissDialogOpen(false);
      else setDisableError(true);
    } catch {
      setDisableError(true);
    } finally {
      setDisabling(false);
    }
  };

  if (!currentGroup || !user || loading || (isLiveDay && matches.length === 0)) return null;
  if (!isLiveDay && (!nextDay || !countdown)) return null;

  const floatingStyle = Platform.OS === 'web' ? ({ position: 'fixed' } as const) : undefined;

  return (
    <>
      <Card
        position="absolute"
        bottom={isMobile ? 0 : 24}
        left={isMobile ? 0 : undefined}
        right={isMobile ? 0 : 24}
        width={isMobile ? '100%' : 380}
        maxHeight={isMobile ? 540 : 500}
        zIndex={10000}
        backgroundColor="$color1"
        borderWidth={1}
        borderColor={isLiveDay ? '$red8' : '$blue8'}
        borderRadius={isMobile ? 0 : '$5'}
        borderTopLeftRadius="$5"
        borderTopRightRadius="$5"
        elevation={12}
        shadowColor="#000"
        shadowOpacity={0.35}
        shadowRadius={18}
        shadowOffset={{ width: 0, height: 8 }}
        overflow="hidden"
        style={floatingStyle}>
        {isLiveDay && mainMatch ? (
          <YStack>
            <XStack
              padding="$3"
              gap="$2"
              alignItems="center"
              backgroundColor={expanded ? '$color2' : '$red2'}>
              <Button
                chromeless
                flex={1}
                height="auto"
                padding={0}
                justifyContent="flex-start"
                onPress={() => setExpanded((value) => !value)}>
                <XStack flex={1} alignItems="center" gap="$3" width="100%">
                  <YStack
                    width={42}
                    height={42}
                    borderRadius={21}
                    backgroundColor="$red9"
                    alignItems="center"
                    justifyContent="center">
                    <RadioTower size={21} color="white" />
                  </YStack>
                  <YStack flex={1} minWidth={0} gap={2}>
                    <XStack alignItems="center" gap="$2">
                      <Text color="$red10" fontSize="$2" fontWeight="800" letterSpacing={1}>
                        LIVE
                      </Text>
                      <Text color="$color10" fontSize="$2" numberOfLines={1}>
                        {mainMatch.league.name}
                      </Text>
                    </XStack>
                    <XStack alignItems="center" gap="$2">
                      <Text
                        color="$color12"
                        fontWeight="700"
                        numberOfLines={1}
                        flex={1}
                        textAlign="right">
                        {mainMatch.game.home}
                      </Text>
                      <Text color="$color12" fontSize="$5" fontWeight="900">
                        {mainMatch.game.result?.homeGoals ?? 0} -{' '}
                        {mainMatch.game.result?.awayGoals ?? 0}
                      </Text>
                      <Text color="$color12" fontWeight="700" numberOfLines={1} flex={1}>
                        {mainMatch.game.away}
                      </Text>
                    </XStack>
                  </YStack>
                  {expanded ? <ChevronDown color="$color10" /> : <ChevronUp color="$color10" />}
                </XStack>
              </Button>
              <Button
                circular
                chromeless
                size="$2"
                icon={X}
                aria-label="Chiudi widget partita"
                onPress={() => {
                  setDisableError(false);
                  setDismissDialogOpen(true);
                }}
              />
            </XStack>

            {expanded && (
              <ScrollView style={{ maxHeight: isMobile ? 430 : 390 }}>
                <YStack padding="$3" gap="$3">
                  <XStack justifyContent="space-between" alignItems="center">
                    <Text color="$color12" fontSize="$5" fontWeight="800">
                      Le tue partite
                    </Text>
                    <Text color="$color9" fontSize="$2">
                      Giornata {liveDay?.serieADay}
                    </Text>
                  </XStack>
                  {matches.map((match) => (
                    <Card
                      key={`${match.league.id}-${match.game.id}`}
                      padding="$3"
                      gap="$3"
                      backgroundColor="$color2"
                      borderWidth={1}
                      borderColor={match.league.isMain ? '$red7' : '$borderColor'}>
                      <XStack alignItems="center" justifyContent="space-between" gap="$2">
                        <XStack alignItems="center" gap="$2" flex={1}>
                          <Trophy size={16} color={match.league.isMain ? '$yellow10' : '$color9'} />
                          <Text color="$color11" fontWeight="700" numberOfLines={1}>
                            {match.league.name}
                          </Text>
                        </XStack>
                        {match.league.isMain && (
                          <Text
                            backgroundColor="$yellow4"
                            color="$yellow11"
                            paddingHorizontal="$2"
                            borderRadius="$2"
                            fontSize="$1"
                            fontWeight="800">
                            MAIN
                          </Text>
                        )}
                      </XStack>
                      <Button
                        chromeless
                        height="auto"
                        padding="$2"
                        onPress={() => navigateToGame(match)}>
                        <XStack width="100%" alignItems="center" gap="$2">
                          <Text
                            flex={1}
                            color="$color12"
                            fontWeight="700"
                            textAlign="right"
                            numberOfLines={1}>
                            {match.game.home}
                          </Text>
                          <YStack alignItems="center" minWidth={72}>
                            <Text color="$red10" fontSize="$6" fontWeight="900">
                              {match.game.result?.homeGoals ?? 0} -{' '}
                              {match.game.result?.awayGoals ?? 0}
                            </Text>
                            <Text color="$color9" fontSize="$1">
                              {match.game.result?.home.value ?? 0} -{' '}
                              {match.game.result?.away.value ?? 0} pt
                            </Text>
                          </YStack>
                          <Text flex={1} color="$color12" fontWeight="700" numberOfLines={1}>
                            {match.game.away}
                          </Text>
                        </XStack>
                      </Button>
                      <XStack gap="$2">
                        <Button
                          flex={1}
                          size="$2"
                          icon={RadioTower}
                          variant="outlined"
                          onPress={() => navigateToLeagueLive(match.league.id)}>
                          Live campionato
                        </Button>
                        <Button
                          flex={1}
                          size="$2"
                          iconAfter={ExternalLink}
                          onPress={() => navigateToGame(match)}>
                          Partita
                        </Button>
                      </XStack>
                    </Card>
                  ))}
                </YStack>
              </ScrollView>
            )}
          </YStack>
        ) : (
          <XStack padding="$3" gap="$3" alignItems="center" backgroundColor="$blue2">
            <Button
              chromeless
              flex={1}
              height="auto"
              padding={0}
              justifyContent="flex-start"
              onPress={navigateToFormation}>
              <XStack flex={1} alignItems="center" gap="$3" width="100%">
                <YStack
                  width={42}
                  height={42}
                  borderRadius={21}
                  backgroundColor="$blue9"
                  alignItems="center"
                  justifyContent="center">
                  <CalendarClock size={21} color="white" />
                </YStack>
                <YStack flex={1} gap={1}>
                  <Text color="$color10" fontSize="$2">
                    Prossima giornata {nextDay?.serieADay}
                  </Text>
                  <Text color="$color12" fontSize="$6" fontWeight="900" letterSpacing={1}>
                    {countdown}
                  </Text>
                  <Text color="$blue10" fontSize="$2" fontWeight="700">
                    Tocca per schierare la formazione
                  </Text>
                </YStack>
              </XStack>
            </Button>
            <Button
              circular
              chromeless
              size="$2"
              icon={X}
              aria-label="Chiudi widget formazione"
              onPress={() => {
                setDisableError(false);
                setDismissDialogOpen(true);
              }}
            />
          </XStack>
        )}
      </Card>

      <Dialog modal open={dismissDialogOpen} onOpenChange={setDismissDialogOpen}>
        <Dialog.Portal>
          <Dialog.Overlay
            key="live-widget-overlay"
            backgroundColor="rgba(0,0,0,0.65)"
            enterStyle={{ opacity: 0 }}
            exitStyle={{ opacity: 0 }}
          />
          <Dialog.Content
            key="live-widget-content"
            bordered
            elevate
            width="90%"
            maxWidth={430}
            padding="$4"
            gap="$4">
            <Dialog.Title>Chiudere il widget partita?</Dialog.Title>
            <Dialog.Description color="$color10">
              Puoi nasconderlo solo per questo momento oppure disattivarlo per sempre. Per riaprirlo
              dopo la disattivazione dovrai andare nelle Impostazioni e riattivarlo.
            </Dialog.Description>
            {disableError && (
              <Text color="$red10">Non è stato possibile salvare la preferenza. Riprova.</Text>
            )}
            <YStack gap="$2">
              <Button
                onPress={() => {
                  setDismissDialogOpen(false);
                  onDismissForSession();
                }}>
                Chiudi solo per ora
              </Button>
              <Button
                theme="red"
                disabled={disabling}
                icon={disabling ? Spinner : X}
                onPress={disablePermanently}>
                {disabling ? 'Disattivazione...' : 'Disattiva per sempre'}
              </Button>
              <Button chromeless onPress={() => setDismissDialogOpen(false)}>
                Annulla
              </Button>
            </YStack>
          </Dialog.Content>
        </Dialog.Portal>
      </Dialog>
    </>
  );
}
