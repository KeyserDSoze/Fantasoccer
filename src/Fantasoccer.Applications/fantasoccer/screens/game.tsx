import React, { useMemo, useEffect, useState } from 'react';
import { View } from 'react-native';
import { ScreenContent } from 'components/layout/ScreenContent';
import { YStack, XStack, Text, H3, Separator, Button, Card, useMedia } from 'tamagui';
import { useSerieACalendar } from 'hooks/useSerieACalendar';
import { useFieldZoom } from 'hooks/useFieldZoom';
import { SoccerField } from 'components/lineup';
import { type GameWrapper } from 'types/game';
import { type StatPlayer } from 'types/stat';
import gameService from 'services/gameService';
import { statPlayerService } from 'services/statPlayerService';
import { createStatPlayerMap } from 'utils/statPlayerCalculator';
import { useNavigation, useRoute } from '@react-navigation/native';
import { formatItalianDate } from 'utils/timezoneHelpers';
import type { RootStackParamList } from '../navigation';
import type { RouteProp } from '@react-navigation/native';
import { TeamLogo } from 'components/team/TeamLogo';
import { TeamBonuses } from 'components/icons/BonusIcons';
import { useAuth } from '../providers/AuthProvider';
import { useApp } from '../providers/AppProvider';
import { IdentityRole, hasRole } from '../types/identity';

export default function GameScreen() {
  const media = useMedia();
  const { calendar, isLive: calendarIsLive, isLoading, error } = useSerieACalendar({
    refreshInterval: 60000,
    enablePolling: true,
  });

  // Zoom controls
  const { zoomLevel, increaseZoom, decreaseZoom, resetZoom, minZoom, maxZoom } = useFieldZoom({
    isMobile: media.sm,
  });

  const route = useRoute<RouteProp<RootStackParamList, 'Game'>>();
  const { params } = route;

  // Estraggo i parametri dalla URL
  const routeGameId = params?.gameId ?? null;
  const routeLeagueId = params?.leagueId ?? null;
  const routeYear = params?.year ?? null;
  const routeGroupId = params?.groupId ?? null;
  const editMode = params?.editMode ?? 'view';
  const navigation = useNavigation();

  // Auth e SuperAdmin check
  const { user } = useAuth();
  const { currentGroup, superAdminMode } = useApp();

  // Verifica se l'utente è SuperAdmin nel gruppo corrente
  const isSuperAdmin = useMemo(() => {
    if (!user || !currentGroup || !routeGroupId) return false;
    const userInGroup = user.groups?.find((g) => g.id === routeGroupId);
    return userInGroup ? hasRole(userInGroup.role, IdentityRole.SuperAdmin) : false;
  }, [user, currentGroup, routeGroupId]);
  const effectiveEditMode =
    isSuperAdmin && superAdminMode && editMode === 'view' ? 'formation' : editMode;
  const asAdmin = isSuperAdmin && superAdminMode;

  // Stato per la modalità admin (permette di modificare entrambe le squadre)
  // null = nessuna modalità admin, 'home' = modifica squadra casa, 'away' = modifica squadra ospite
  const [adminEditingTeam, setAdminEditingTeam] = useState<'home' | 'away' | null>(null);
  // gameService result (live teams + extra info) — prefer this when available
  const [gameWrapper, setGameWrapper] = useState<GameWrapper | null>(null);
  const [gameLoading, setGameLoading] = useState(false);
  const [gameError, setGameError] = useState<string | null>(null);
  const [isRefreshing, setIsRefreshing] = useState(false); // Flag per evitare refresh multipli
  const [isEditingFormation, setIsEditingFormation] = useState(false);

  // StatPlayers map per calcoli trend/positiveness
  const [statPlayersMap, setStatPlayersMap] = useState<Map<string, StatPlayer>>(new Map());
  const [statPlayersLoading, setStatPlayersLoading] = useState(false);

  // Funzione per ricaricare i dati della partita
  const refreshGame = async (isAutoRefresh = false) => {
    if (!routeGameId || !routeGroupId || !routeLeagueId || !routeYear) return;
    if (isRefreshing) return; // Evita chiamate multiple simultanee

    setIsRefreshing(true);

    // Solo mostra loading se non è un auto-refresh
    if (!isAutoRefresh) {
      setGameLoading(true);
    }
    setGameError(null);

    try {
      const result = await gameService.getGame({
        groupId: routeGroupId,
        leagueId: routeLeagueId,
        year: parseInt(routeYear),
        gameId: routeGameId,
        editMode: effectiveEditMode,
        asAdmin,
      });
      setGameWrapper(result);
    } catch (err: any) {
      setGameError(err?.message ?? String(err));
    } finally {
      if (!isAutoRefresh) {
        setGameLoading(false);
      }
      setIsRefreshing(false);
    }
  };

  // Caricamento iniziale della partita
  useEffect(() => {
    if (!routeGameId || !routeGroupId || !routeLeagueId || !routeYear) {
      setGameWrapper(null);
      return;
    }

    let mounted = true;
    setGameLoading(true);
    setGameError(null);

    gameService
      .getGame({
        groupId: routeGroupId,
        leagueId: routeLeagueId,
        year: parseInt(routeYear),
        gameId: routeGameId,
        editMode: effectiveEditMode,
        asAdmin,
      })
      .then((result) => {
        if (mounted) setGameWrapper(result);
      })
      .catch((err) => {
        if (mounted) setGameError(err?.message ?? String(err));
      })
      .finally(() => {
        if (mounted) setGameLoading(false);
      });

    return () => {
      mounted = false;
    };
  }, [routeGameId, routeLeagueId, routeYear, routeGroupId, effectiveEditMode, asAdmin]);

  // Caricamento StatPlayers per calcoli trend/positiveness
  useEffect(() => {
    if (!routeYear) {
      return;
    }

    let mounted = true;
    setStatPlayersLoading(true);

    statPlayerService
      .getCurrentUserEnhancedWrapper({ year: parseInt(routeYear) })
      .then((wrapper) => {
        if (mounted && wrapper) {
          const map = createStatPlayerMap(wrapper.players);
          setStatPlayersMap(map);
        }
      })
      .catch((err) => {
        console.error('Error loading StatPlayers:', err);
      })
      .finally(() => {
        if (mounted) setStatPlayersLoading(false);
      });

    return () => {
      mounted = false;
    };
  }, [routeYear]);

  // Stato per controllare se ci sono partite live in questo momento
  const [isLiveActive, setIsLiveActive] = useState(false);

  // Stato per l'effetto lampeggiante del risultato
  const [isBlinking, setIsBlinking] = useState(false);

  // Effetto lampeggiante quando ci sono partite live
  useEffect(() => {
    if (isLiveActive) {
      setIsBlinking(false);
      return;
    }

    // Lampeggia ogni 600ms quando live
    const blinkInterval = setInterval(() => {
      setIsBlinking((prev) => !prev);
    }, 600);

    return () => {
      clearInterval(blinkInterval);
    };
  }, [isLiveActive]);

  // Job che controlla ogni 10 secondi se ci sono partite live
  useEffect(() => {
    if (!gameWrapper?.serieADay || !calendar) return;

    const checkLiveStatus = () => {
      if (calendarIsLive) {
        setIsLiveActive(true);
        return;
      }

      // Trova la giornata di Serie A corrispondente
      const targetDay = calendar.days?.find((day) => day.serieADay === gameWrapper.serieADay);

      if (!targetDay || !targetDay.games || targetDay.games.length === 0) {
        setIsLiveActive(false);
        return;
      }

      // Controlla se almeno una partita della giornata è live in questo momento
      const now = new Date();
      const TWO_HOURS_MS = 2 * 60 * 60 * 1000; // 2 ore in millisecondi

      const hasLiveGame = targetDay.games.some((game) => {
        const gameStart = game.date.getTime();
        const gameEnd = gameStart + TWO_HOURS_MS;
        const nowTime = now.getTime();
        return nowTime >= gameStart && nowTime <= gameEnd;
      });

      setIsLiveActive(hasLiveGame);
    };

    // Controlla subito
    checkLiveStatus();

    // Poi controlla ogni 10 secondi
    const liveCheckInterval = setInterval(checkLiveStatus, 10000);

    return () => {
      clearInterval(liveCheckInterval);
    };
  }, [gameWrapper?.serieADay, calendar, calendarIsLive]);

  // Polling automatico ogni 10 secondi SOLO se ci sono partite live E non si sta modificando
  useEffect(() => {
    if (!routeGameId || !routeGroupId || !routeLeagueId || !routeYear) {
      return;
    }

    // ⚠️ IMPORTANTE: Avvia il polling SOLO quando ci sono partite live E non si sta modificando in modalità admin
    if (
      (!isLiveActive && !gameWrapper?.isLiveFormationWindow) ||
      adminEditingTeam !== null ||
      isEditingFormation
    ) {
      return;
    }

    const pollingInterval = setInterval(() => {
      refreshGame(true); // true = è un auto-refresh, non mostra loading
    }, 10000); // 10 secondi

    // Cleanup: ferma il polling quando il componente viene smontato o cambiano le dipendenze
    return () => {
      clearInterval(pollingInterval);
    };
  }, [
    routeGameId,
    routeLeagueId,
    routeYear,
    routeGroupId,
    isLiveActive,
    gameWrapper?.isLiveFormationWindow,
    adminEditingTeam,
    isEditingFormation,
    editMode,
  ]);

  // Compute played score and bonuses from gameService wrapper if available
  const title = gameWrapper ? `${gameWrapper.game?.home} vs ${gameWrapper.game?.away}` : 'Partita';
  const playedScore = gameWrapper?.game?.result
    ? `${gameWrapper.game.result.homeGoals}-${gameWrapper.game.result.awayGoals}`
    : null;

  // Trova la data della partita dal calendario di Serie A usando serieADay
  const gameDate = useMemo(() => {
    if (!gameWrapper?.serieADay || !calendar) return null;

    // Cerca la giornata nel calendario che corrisponde a serieADay
    const targetDay = calendar.days?.find((day) => day.serieADay === gameWrapper.serieADay);

    if (!targetDay || !targetDay.games || targetDay.games.length === 0) return null;

    // Prende la prima partita in ordine di tempo di quella giornata
    const sortedGames = [...targetDay.games].sort((a, b) => a.date.getTime() - b.date.getTime());
    return sortedGames[0]?.date || null;
  }, [gameWrapper?.serieADay, calendar]);

  const homeHasAGoodDefense = gameWrapper?.game?.result?.home?.defensiveBonus;
  const homeHasAGoodPeople = gameWrapper?.game?.result?.home?.goodPeople;
  const awayHasAGoodDefense = gameWrapper?.game?.result?.away?.defensiveBonus;
  const awayHasAGoodPeople = gameWrapper?.game?.result?.away?.goodPeople;

  // Calcola autorete solo se: entrambe < 66 punti E differenza >= 6
  // E mostra SOLO per la squadra che ha meno punti
  const homePoints = gameWrapper?.game?.result?.home?.value || 0;
  const awayPoints = gameWrapper?.game?.result?.away?.value || 0;
  const pointsDifference = Math.abs(homePoints - awayPoints);
  const bothUnder66 = homePoints < 66 && awayPoints < 66;
  const shouldShowOwnGoal = bothUnder66 && pointsDifference >= 6;

  const homeHasDoneOwnGoal =
    shouldShowOwnGoal && homePoints < awayPoints && gameWrapper?.game?.result?.home?.ownGoal;
  const awayHasDoneOwnGoal =
    shouldShowOwnGoal && awayPoints < homePoints && gameWrapper?.game?.result?.away?.ownGoal;

  // Calcola se almeno una squadra ha bonus per determinare l'altezza minima
  const hasAnyBonus =
    homeHasAGoodDefense ||
    homeHasAGoodPeople ||
    homeHasDoneOwnGoal ||
    awayHasAGoodDefense ||
    awayHasAGoodPeople ||
    awayHasDoneOwnGoal;
  const bonusMinHeight = hasAnyBonus ? 32 : 8; // Se nessuna ha bonus, spazio minimo

  return (
    <View style={{ flex: 1 }}>
      <ScreenContent path="screens/game.tsx" title={title}>
        <YStack gap="$4" width="100%">
          <Card padding="$3" backgroundColor="$color2">
            {isLoading ? (
              <Text>Caricamento...</Text>
            ) : error ? (
              <Text color="$red10">{error}</Text>
            ) : !gameWrapper ? (
              <Text>Nessuna partita disponibile</Text>
            ) : (
              <YStack gap="$2">
                {!playedScore && (
                  <XStack justifyContent="center" alignItems="center" gap="$5" padding="$3">
                    <YStack alignItems="center" gap="$2" flex={1}>
                      <TeamLogo
                        groupId={routeGroupId || ''}
                        year={routeYear || ''}
                        ownerEmail={gameWrapper.game?.homeOwner || ''}
                        size={media.gtSm ? 60 : 52}
                      />
                      <Text fontSize="$4" fontWeight="700" textAlign="center">
                        {gameWrapper.game?.home}
                      </Text>
                    </YStack>
                    <YStack alignItems="center" gap="$1">
                      <H3>VS</H3>
                      <Text fontSize="$2" color="$color10">
                        Giornata {gameWrapper.serieADay}
                      </Text>
                      {gameDate && (
                        <Text fontSize="$2" color="$color10">
                          {formatItalianDate(gameDate)}
                        </Text>
                      )}
                    </YStack>
                    <YStack alignItems="center" gap="$2" flex={1}>
                      <TeamLogo
                        groupId={routeGroupId || ''}
                        year={routeYear || ''}
                        ownerEmail={gameWrapper.game?.awayOwner || ''}
                        size={media.gtSm ? 60 : 52}
                      />
                      <Text fontSize="$4" fontWeight="700" textAlign="center">
                        {gameWrapper.game?.away}
                      </Text>
                    </YStack>
                  </XStack>
                )}
                {playedScore && (
                  <YStack gap="$3">
                    {/* Layout responsive: mobile vs desktop */}
                    {media.gtSm ? (
                      // Desktop Layout - tutto su una linea
                      <XStack
                        alignItems="center"
                        justifyContent="space-between"
                        gap="$3"
                        padding="$2">
                        {/* Scudetto Home */}
                        <TeamLogo
                          groupId={routeGroupId || ''}
                          year={routeYear || ''}
                          ownerEmail={gameWrapper.game?.homeOwner || ''}
                          size={60}
                        />

                        {/* Nome squadra home */}
                        <Text fontSize="$5" fontWeight="600" textAlign="center" minWidth={140}>
                          {gameWrapper.game?.home}
                        </Text>

                        {/* Punteggio home */}
                        <Text
                          fontSize="$5"
                          fontWeight="bold"
                          minWidth={70}
                          textAlign="center"
                          color={isLiveActive && isBlinking ? '$green10' : '$color12'}>
                          {gameWrapper.game?.result?.home?.value || 0} pt
                        </Text>

                        {/* Bonus home */}
                        <View style={{ minWidth: 100, alignItems: 'center' }}>
                          <TeamBonuses
                            hasGoodDefense={homeHasAGoodDefense}
                            hasGoodPeople={homeHasAGoodPeople}
                            hasOwnGoal={homeHasDoneOwnGoal}
                            iconSize={24}
                          />
                        </View>

                        {/* Goal e data centrale */}
                        <YStack alignItems="center" gap="$1" minWidth={120}>
                          <Text
                            fontSize="$7"
                            fontWeight="bold"
                            color={isLiveActive && isBlinking ? '$red10' : '$color12'}
                            style={{
                              transform:
                                isLiveActive && isBlinking ? [{ scale: 1.1 }] : [{ scale: 1.0 }],
                              transition: 'all 0.3s ease',
                            }}>
                            {gameWrapper.game?.result?.homeGoals} -{' '}
                            {gameWrapper.game?.result?.awayGoals}
                          </Text>
                          {gameDate && (
                            <Text fontSize="$3" color="$color11" textAlign="center">
                              {formatItalianDate(gameDate)}
                            </Text>
                          )}
                        </YStack>

                        {/* Bonus away */}
                        <View style={{ minWidth: 100, alignItems: 'center' }}>
                          <TeamBonuses
                            hasGoodDefense={awayHasAGoodDefense}
                            hasGoodPeople={awayHasAGoodPeople}
                            hasOwnGoal={awayHasDoneOwnGoal}
                            iconSize={24}
                          />
                        </View>

                        {/* Punteggio away */}
                        <Text
                          fontSize="$5"
                          fontWeight="bold"
                          minWidth={70}
                          textAlign="center"
                          color={isLiveActive && isBlinking ? '$green10' : '$color12'}>
                          {gameWrapper.game?.result?.away?.value || 0} pt
                        </Text>

                        {/* Nome squadra away */}
                        <Text fontSize="$5" fontWeight="600" textAlign="center" minWidth={140}>
                          {gameWrapper.game?.away}
                        </Text>

                        {/* Scudetto Away */}
                        <TeamLogo
                          groupId={routeGroupId || ''}
                          year={routeYear || ''}
                          ownerEmail={gameWrapper.game?.awayOwner || ''}
                          size={60}
                        />
                      </XStack>
                    ) : (
                      // Mobile Layout - layout esistente (verticale) ma con elementi più grandi
                      <XStack justifyContent="space-between" alignItems="center" gap="$4">
                        {/* Squadra di casa */}
                        <YStack alignItems="center" gap="$2" flex={1}>
                          <TeamLogo
                            groupId={routeGroupId || ''}
                            year={routeYear || ''}
                            ownerEmail={gameWrapper.game?.homeOwner || ''}
                            size={70}
                          />
                          <Text fontSize="$3" fontWeight="600" textAlign="center" numberOfLines={2}>
                            {gameWrapper.game?.home}
                          </Text>
                          <YStack alignItems="center" gap="$2">
                            <Text
                              fontSize="$6"
                              fontWeight="bold"
                              color={isLiveActive && isBlinking ? '$green10' : '$color12'}>
                              {gameWrapper.game?.result?.home?.value || 0} pt
                            </Text>
                            <View style={{ minHeight: bonusMinHeight, justifyContent: 'center' }}>
                              <TeamBonuses
                                hasGoodDefense={homeHasAGoodDefense}
                                hasGoodPeople={homeHasAGoodPeople}
                                hasOwnGoal={homeHasDoneOwnGoal}
                                iconSize={28}
                              />
                            </View>
                          </YStack>
                        </YStack>

                        {/* VS e punteggio goals */}
                        <YStack alignItems="center" gap="$1">
                          <Text
                            fontSize="$7"
                            fontWeight="bold"
                            color={isLiveActive && isBlinking ? '$red10' : '$color12'}
                            style={{
                              transform:
                                isLiveActive && isBlinking ? [{ scale: 1.1 }] : [{ scale: 1.0 }],
                              transition: 'all 0.3s ease',
                            }}>
                            {gameWrapper.game?.result?.homeGoals} -{' '}
                            {gameWrapper.game?.result?.awayGoals}
                          </Text>
                          <Text fontSize="$2" color="$color11">
                            {gameDate && <Text>{formatItalianDate(gameDate)}</Text>}
                          </Text>
                        </YStack>

                        {/* Squadra ospite */}
                        <YStack alignItems="center" gap="$2" flex={1}>
                          <TeamLogo
                            groupId={routeGroupId || ''}
                            year={routeYear || ''}
                            ownerEmail={gameWrapper.game?.awayOwner || ''}
                            size={70}
                          />
                          <Text fontSize="$3" fontWeight="600" textAlign="center" numberOfLines={2}>
                            {gameWrapper.game?.away}
                          </Text>
                          <YStack alignItems="center" gap="$2">
                            <Text
                              fontSize="$6"
                              fontWeight="bold"
                              color={isLiveActive && isBlinking ? '$green10' : '$color12'}>
                              {gameWrapper.game?.result?.away?.value || 0} pt
                            </Text>
                            <View style={{ minHeight: bonusMinHeight, justifyContent: 'center' }}>
                              <TeamBonuses
                                hasGoodDefense={awayHasAGoodDefense}
                                hasGoodPeople={awayHasAGoodPeople}
                                hasOwnGoal={awayHasDoneOwnGoal}
                                iconSize={28}
                              />
                            </View>
                          </YStack>
                        </YStack>
                      </XStack>
                    )}
                  </YStack>
                )}
              </YStack>
            )}
          </Card>

          {gameWrapper && (
            <Card padding="$3" backgroundColor="$color2">
              {gameLoading ? (
                <Text>Caricamento formazione partita...</Text>
              ) : gameWrapper ? (
                <SoccerField
                  gameWrapper={gameWrapper}
                  groupId={routeGroupId ?? undefined}
                  leagueId={routeLeagueId ?? undefined}
                  year={routeYear ? parseInt(routeYear) : undefined}
                  gameId={routeGameId ?? undefined}
                  editMode={effectiveEditMode}
                  adminControls={
                    isSuperAdmin && superAdminMode && gameWrapper.canEdit ? (
                      <Card
                        padding="$3"
                        backgroundColor="$purple2"
                        borderWidth={2}
                        borderColor="$purple8">
                        <YStack gap="$3">
                          <XStack gap="$3" alignItems="center">
                            <YStack gap="$1" flex={1}>
                              <Text fontSize="$4" fontWeight="700" color="$purple11">
                                Modalità Super Admin
                              </Text>
                              <Text fontSize="$2" color="$purple10">
                                {adminEditingTeam === null
                                  ? '✗ Disattivata - Seleziona una squadra da modificare'
                                  : adminEditingTeam === 'home'
                                    ? `✓ Modifica squadra CASA: ${gameWrapper.game?.home}`
                                    : `✓ Modifica squadra OSPITE: ${gameWrapper.game?.away}`}
                              </Text>
                            </YStack>
                          </XStack>
                          <XStack gap="$3" flexWrap="wrap">
                            <Button
                              flex={1}
                              minWidth={150}
                              size="$3"
                              backgroundColor={
                                adminEditingTeam === 'home' ? '$purple10' : '$purple4'
                              }
                              color={adminEditingTeam === 'home' ? '$white1' : '$purple11'}
                              borderWidth={2}
                              borderColor={adminEditingTeam === 'home' ? '$purple11' : '$purple6'}
                              onPress={() =>
                                setAdminEditingTeam(adminEditingTeam === 'home' ? null : 'home')
                              }
                              disabled={isEditingFormation}
                              opacity={isEditingFormation ? 0.6 : 1}>
                              {adminEditingTeam === 'home' ? '✓ ' : ''}Modifica{' '}
                              {gameWrapper.game?.home}
                            </Button>
                            <Button
                              flex={1}
                              minWidth={150}
                              size="$3"
                              backgroundColor={
                                adminEditingTeam === 'away' ? '$purple10' : '$purple4'
                              }
                              color={adminEditingTeam === 'away' ? '$white1' : '$purple11'}
                              borderWidth={2}
                              borderColor={adminEditingTeam === 'away' ? '$purple11' : '$purple6'}
                              onPress={() =>
                                setAdminEditingTeam(adminEditingTeam === 'away' ? null : 'away')
                              }
                              disabled={isEditingFormation}
                              opacity={isEditingFormation ? 0.6 : 1}>
                              {adminEditingTeam === 'away' ? '✓ ' : ''}Modifica{' '}
                              {gameWrapper.game?.away}
                            </Button>
                          </XStack>
                        </YStack>
                      </Card>
                    ) : null
                  }
                  onRefresh={refreshGame}
                  onEditingChange={setIsEditingFormation}
                  customZoom={zoomLevel}
                  zoomControls={{
                    zoomLevel,
                    onIncrease: increaseZoom,
                    onDecrease: decreaseZoom,
                    onReset: resetZoom,
                    minZoom,
                    maxZoom,
                  }}
                  statPlayersMap={statPlayersMap}
                  adminEditingTeam={adminEditingTeam}
                />
              ) : (
                <Text>Nessun dato formazione disponibile</Text>
              )}
            </Card>
          )}
        </YStack>
      </ScreenContent>
    </View>
  );
}
