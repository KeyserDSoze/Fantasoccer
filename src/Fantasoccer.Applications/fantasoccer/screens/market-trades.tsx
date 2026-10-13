import { useState, useEffect, useMemo, useCallback } from 'react'
import { useRoute, type RouteProp } from '@react-navigation/native'
import { View } from 'react-native'
import {
  YStack,
  XStack,
  Text,
  Card,
  Button,
  Separator,
  Spinner,
  H3,
  H5,
  ScrollView,
  Select,
  Adapt,
  Sheet,
} from 'tamagui'
import {
  TrendingUp,
  Users,
  AlertTriangle,
  CheckCircle,
  XCircle,
  ThumbsUp,
  ThumbsDown,
  ArrowRightLeft,
  Clock,
  DollarSign,
  Filter,
  Calendar,
} from '@tamagui/lucide-icons-2'
import { ScreenContent } from 'components/layout/ScreenContent'
import { useApp } from 'providers/AppProvider'
import { useAuth } from 'providers/AuthProvider'
import { teamService } from 'services/teamService'
import { marketService } from 'services/marketService'
import { Team } from 'types/team'
import { MarketType, Role } from 'types/group'
import { Market, MarketStatus, MarketStatusHelper, MarketHelper } from 'types/market'
import { formatSeasonFromYear } from 'utils/season'
import type { RootStackParamList } from 'navigation'
import { useMainLeagueMarketActivity } from 'providers/MarketActivityProvider'

// Helper to get role label
const getRoleLabel = (role: Role): string => {
  switch (role) {
    case Role.GoalKeeper: return 'P'
    case Role.Defensor: return 'D'
    case Role.Midfielder: return 'C'
    case Role.Forward: return 'A'
    default: return '?'
  }
}

// Helper to get role color
const getRoleColor = (role: Role): string => {
  switch (role) {
    case Role.GoalKeeper: return '$orange10'
    case Role.Defensor: return '$blue10'
    case Role.Midfielder: return '$green10'
    case Role.Forward: return '$red10'
    default: return '$gray10'
  }
}

// Helper to get status color
const getStatusColor = (status: MarketStatus): string => {
  switch (status) {
    case MarketStatus.Pending: return '$blue10'
    case MarketStatus.Approved: return '$green10'
    case MarketStatus.Denied: return '$red10'
    case MarketStatus.Cancelled: return '$gray10'
    default: return '$orange10'
  }
}

// Helper to get status background color
const getStatusBgColor = (status: MarketStatus): string => {
  switch (status) {
    case MarketStatus.Pending: return '$blue2'
    case MarketStatus.Approved: return '$green2'
    case MarketStatus.Denied: return '$red2'
    case MarketStatus.Cancelled: return '$gray2'
    default: return '$orange2'
  }
}

export default function MarketTradesScreen() {
  const route = useRoute<RouteProp<RootStackParamList, 'MarketTrades'>>()
  const { currentGroup, selectedLeagueId: appSelectedLeagueId, selectedYear: appSelectedYear, setCurrentGroup } = useApp()
  const selectedLeagueId = route.params?.leagueId ?? appSelectedLeagueId
  const routeYear = route.params?.year
  const parsedRouteYear = typeof routeYear === 'string' ? Number(routeYear) : routeYear
  const selectedYear = parsedRouteYear != null && Number.isFinite(parsedRouteYear) ? parsedRouteYear : appSelectedYear
  const { user } = useAuth()
  const marketActivity = useMainLeagueMarketActivity()
  
  const [markets, setMarkets] = useState<Market[]>([])
  const [teams, setTeams] = useState<Team[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [selectedMarket, setSelectedMarket] = useState<Market | null>(null)
  const [isVoting, setIsVoting] = useState(false)
  const [statusFilter, setStatusFilter] = useState<string>('all')

  useEffect(() => {
    const requestedGroupId = route.params?.groupId
    if (requestedGroupId && requestedGroupId !== currentGroup?.id) {
      void setCurrentGroup(requestedGroupId)
    }
  }, [route.params?.groupId, currentGroup?.id, setCurrentGroup])

  // Get latest year from group baskets
  const latestYear = useMemo(() => {
    if (!currentGroup) return null
    const allYears = currentGroup.baskets
      ?.flatMap((basket: any) => basket.years?.map((y: any) => y.year) ?? [])
      .filter((year: any, index: number, self: any[]) => self.indexOf(year) === index) ?? []
    return allYears.length > 0 ? Math.max(...allYears) : null
  }, [currentGroup])

  // Check if current year is the latest
  const isCurrentYearLatest = selectedYear === latestYear
  const canVote = isCurrentYearLatest && selectedYear != null && selectedLeagueId != null
  const marketNeedsLeagueVote = useMemo(() => {
    const league = currentGroup?.leagues?.find((item: any) => item.id === selectedLeagueId)
    const annualLeague = league?.years?.find((item: any) => item.year === selectedYear)
    return annualLeague?.settings?.market === MarketType.WithVote
  }, [currentGroup, selectedLeagueId, selectedYear])

  // Calculate league size (number of teams)
  const leagueSize = useMemo(() => {
    return teams.length
  }, [teams])

  // Calculate quorum
  const quorum = useMemo(() => {
    return MarketHelper.calculateQuorum(leagueSize)
  }, [leagueSize])

  const isEmailForTeam = useCallback((team: Team, email: string | null | undefined): boolean => {
    if (!email) return false
    const normalizedEmail = email.toLowerCase()
    return team.owner.toLowerCase() === normalizedEmail ||
      team.additionalOwners?.some(owner => owner.toLowerCase() === normalizedEmail) === true
  }, [])

  const myOwner = useMemo(() => {
    return teams.find(team => isEmailForTeam(team, user?.email))?.owner ?? user?.email ?? ''
  }, [teams, user?.email, isEmailForTeam])

  // Load data
  useEffect(() => {
    loadData()
  }, [currentGroup, selectedLeagueId, selectedYear, user?.email])

  const loadData = async () => {
    if (!currentGroup || !selectedLeagueId || !selectedYear || !user?.email) {
      setError('Configurazione incompleta')
      setIsLoading(false)
      return
    }

    setIsLoading(true)
    setError(null)

    try {
      const league = currentGroup.leagues?.find((l: any) => l.id === selectedLeagueId)
      if (!league || !league.basketsId || league.basketsId.length === 0) {
        setError('Lega non valida')
        setMarkets([])
        setTeams([])
        setIsLoading(false)
        return
      }

      const basketId = league.basketsId[0]
      const basket = currentGroup.baskets?.find((b: any) => b.id === basketId)
      if (!basket) {
        setError('Basket non trovato')
        setMarkets([])
        setTeams([])
        setIsLoading(false)
        return
      }

      const yearlyBasket = basket.years?.find((y: any) => y.year === selectedYear)
      if (!yearlyBasket || !yearlyBasket.teams || yearlyBasket.teams.length === 0) {
        setError('Nessuna squadra trovata per questo anno')
        setMarkets([])
        setTeams([])
        setIsLoading(false)
        return
      }

      // Load teams
      const owners = yearlyBasket.teams.map((t: any) => t.owner)
      const fetchedTeams = await teamService.getTeamsByBasket(
        currentGroup.id,
        selectedYear,
        basketId,
        owners
      )
      setTeams(fetchedTeams)

      // Load markets
      const fetchedMarkets = await marketService.getMarketsByLeague(
        currentGroup.id,
        selectedYear,
        selectedLeagueId
      )
      setMarkets(fetchedMarkets)

    } catch (err: any) {
      console.error('Error loading data:', err)
      setError(err?.message ?? 'Errore nel caricamento dei dati')
    } finally {
      setIsLoading(false)
    }
  }

  // Filter and sort markets
  const filteredMarkets = useMemo(() => {
    let filtered = markets

    // Filter by status
    if (statusFilter !== 'all') {
      if (statusFilter === 'pending') {
        filtered = MarketHelper.getActiveMarkets(filtered)
      } else if (statusFilter === 'completed') {
        filtered = MarketHelper.getCompletedMarkets(filtered)
      } else {
        const status = parseInt(statusFilter) as MarketStatus
        filtered = MarketHelper.filterByStatus(filtered, status)
      }
    }

    // Sort: pending first (newest), then completed (newest)
    const pending = MarketHelper.getActiveMarkets(filtered)
    const completed = MarketHelper.getCompletedMarkets(filtered)

    return [
      ...MarketHelper.sortByNewest(pending),
      ...MarketHelper.sortByNewest(completed),
    ]
  }, [markets, statusFilter])

  // Handle vote
  const handleVote = async (market: Market, voteType: 'approve' | 'deny') => {
    if (!currentGroup || !selectedLeagueId || !selectedYear || !user?.email) {
      return
    }

    setIsVoting(true)
    setError(null)

    try {
      let success = false
      if (voteType === 'approve') {
        success = await marketService.approveMarket(
          currentGroup.id,
          selectedYear,
          selectedLeagueId,
          market.id
        )
      } else {
        success = await marketService.denyMarket(
          currentGroup.id,
          selectedYear,
          selectedLeagueId,
          market.id
        )
      }

      if (success) {
        // Reload markets
        await loadData()
        await marketActivity.refresh()
        setSelectedMarket(null)
      } else {
        setError('Errore durante la votazione')
      }
    } catch (err: any) {
      console.error('Error voting:', err)
      setError(err?.message ?? 'Errore durante la votazione')
    } finally {
      setIsVoting(false)
    }
  }

  const handleSellerDecision = async (market: Market, decision: 'accept' | 'reject') => {
    if (!currentGroup || !selectedLeagueId || !selectedYear) return

    setIsVoting(true)
    setError(null)
    try {
      const success = decision === 'accept'
        ? await marketService.acceptMarket(currentGroup.id, selectedYear, selectedLeagueId, market.id)
        : await marketService.rejectMarket(currentGroup.id, selectedYear, selectedLeagueId, market.id)
      if (success) {
        await loadData()
        await marketActivity.refresh()
        setSelectedMarket(null)
      } else {
        setError(decision === 'accept'
          ? "Impossibile accettare la proposta"
          : "Impossibile rifiutare la proposta")
      }
    } catch (err: any) {
      console.error('Error responding to market proposal:', err)
      setError(err?.message ?? 'Errore durante la risposta alla proposta')
    } finally {
      setIsVoting(false)
    }
  }

  // Get team name by owner
  const getTeamNameByOwner = (owner: string): string => {
    const team = teams.find(t => t.owner.toLowerCase() === owner.toLowerCase())
    return team?.name ?? owner
  }

  // Format currency
  const formatCurrency = (amount: number) => {
    return new Intl.NumberFormat('it-IT', {
      style: 'currency',
      currency: 'EUR',
      minimumFractionDigits: 0,
      maximumFractionDigits: 0,
    }).format(amount)
  }

  // Format date
  const formatDate = (date: Date) => {
    return new Intl.DateTimeFormat('it-IT', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    }).format(date)
  }

  // Render market card
  const renderMarketCard = (market: Market) => {
    const isPending = MarketStatusHelper.isPending(market.status)
    const userVote = MarketHelper.getUserVote(market, myOwner)
    const isUserInvolved = MarketHelper.isUserInvolved(market, myOwner)
    const needsUserVote = isPending && market.sellerAccepted && marketNeedsLeagueVote && !isUserInvolved && !userVote
    const hasReachedApprovalQuorum = MarketHelper.hasReachedApprovalQuorum(market, leagueSize)
    const hasReachedDenialQuorum = MarketHelper.hasReachedDenialQuorum(market, leagueSize)

    return (
      <Card
        key={market.id}
        padding="$4"
        backgroundColor={getStatusBgColor(market.status)}
        borderWidth={1}
        borderColor={isPending ? '$borderColor' : getStatusColor(market.status)}
        pressStyle={{ scale: 0.98 }}
        onPress={() => setSelectedMarket(market)}
        cursor="pointer"
      >
        <YStack gap="$3">
          {/* Header */}
          <XStack justifyContent="space-between" alignItems="center">
            <XStack gap="$2" alignItems="center" flex={1}>
              <ArrowRightLeft size={20} color={getStatusColor(market.status)} />
              <YStack flex={1}>
                <Text fontSize="$5" fontWeight="600" color="$color12">
                  {getTeamNameByOwner(market.buyer)} ⇄ {getTeamNameByOwner(market.seller)}
                </Text>
                <Text fontSize="$2" color="$color10">
                  {formatDate(market.creationTime)}
                </Text>
              </YStack>
            </XStack>
            <XStack gap="$2" alignItems="center">
              {isPending && (
                <XStack
                  gap="$1"
                  alignItems="center"
                  backgroundColor="$red3"
                  paddingHorizontal="$2"
                  paddingVertical="$1"
                  borderRadius="$3"
                  aria-label={needsUserVote ? 'Devi votare questo scambio' : 'Scambio attivo'}>
                  <YStack width={16} height={16} borderRadius={8} backgroundColor="$red10" alignItems="center" justifyContent="center">
                    <Text color="white" fontSize={11} fontWeight="900" lineHeight={13}>!</Text>
                  </YStack>
                  <Text color="$red11" fontSize="$1" fontWeight="800">
                    {needsUserVote ? 'Vota' : 'Attivo'}
                  </Text>
                </XStack>
              )}
              {isPending && (
                <Clock size={16} color="$blue10" />
              )}
              <Text
                fontSize="$3"
                fontWeight="600"
                color={getStatusColor(market.status)}
              >
                {MarketStatusHelper.asLabel(market.status)}
              </Text>
            </XStack>
          </XStack>

          {/* Players summary */}
          <XStack gap="$3" justifyContent="space-between" flexWrap="wrap">
            <YStack gap="$1" flex={1} minWidth={150}>
              <Text fontSize="$2" color="$color10" fontWeight="500">
                {getTeamNameByOwner(market.buyer)} cede:
              </Text>
              <Text fontSize="$3" color="$color12">
                {market.buyerPlayers.length} giocatori
              </Text>
              {market.moneyFromBuyer > 0 && (
                <Text fontSize="$2" color="$green10">
                  + {formatCurrency(market.moneyFromBuyer)}
                </Text>
              )}
            </YStack>

            <YStack gap="$1" flex={1} minWidth={150}>
              <Text fontSize="$2" color="$color10" fontWeight="500">
                {getTeamNameByOwner(market.seller)} cede:
              </Text>
              <Text fontSize="$3" color="$color12">
                {market.sellerPlayers.length} giocatori
              </Text>
              {market.moneyFromSeller > 0 && (
                <Text fontSize="$2" color="$green10">
                  + {formatCurrency(market.moneyFromSeller)}
                </Text>
              )}
            </YStack>
          </XStack>

          {/* Seller acceptance / voting info for pending markets */}
          {isPending && !market.sellerAccepted && (
            <>
              <Separator />
              <Text fontSize="$3" color="$orange10" fontWeight="600">
                In attesa dell&apos;accettazione del venditore
              </Text>
            </>
          )}

          {isPending && market.sellerAccepted && marketNeedsLeagueVote && (
            <>
              <Separator />
              <XStack justifyContent="space-between" alignItems="center" gap="$3" flexWrap="wrap">
                <XStack gap="$4">
                  <XStack gap="$1" alignItems="center">
                    <ThumbsUp size={16} color="$green10" />
                    <Text fontSize="$3" color="$green10" fontWeight="600">
                      {market.approvers.length}
                    </Text>
                    {hasReachedApprovalQuorum && (
                      <CheckCircle size={14} color="$green10" />
                    )}
                  </XStack>
                  <XStack gap="$1" alignItems="center">
                    <ThumbsDown size={16} color="$red10" />
                    <Text fontSize="$3" color="$red10" fontWeight="600">
                      {market.deniers.length}
                    </Text>
                    {hasReachedDenialQuorum && (
                      <XCircle size={14} color="$red10" />
                    )}
                  </XStack>
                </XStack>
                
                <Text fontSize="$2" color="$color10">
                  Quorum: {quorum}/{leagueSize}
                </Text>
              </XStack>

              {/* User vote indicator */}
              {userVote && !isUserInvolved && (
                <XStack gap="$2" alignItems="center">
                  {userVote === 'approve' ? (
                    <>
                      <CheckCircle size={16} color="$green10" />
                      <Text fontSize="$2" color="$green10">
                        Hai votato a favore
                      </Text>
                    </>
                  ) : (
                    <>
                      <XCircle size={16} color="$red10" />
                      <Text fontSize="$2" color="$red10">
                        Hai votato contro
                      </Text>
                    </>
                  )}
                </XStack>
              )}

              {/* User involved indicator */}
              {isUserInvolved && (
                <XStack gap="$2" alignItems="center">
                  <Users size={16} color="$blue10" />
                  <Text fontSize="$2" color="$blue10" fontWeight="500">
                    Sei coinvolto in questo scambio
                  </Text>
                </XStack>
              )}
            </>
          )}
        </YStack>
      </Card>
    )
  }

  return (
    <View style={{ flex: 1 }}>
      <ScreenContent path="screens/market-trades.tsx" title="Scambi di Mercato">
        <YStack gap="$4" width="100%" maxWidth={1200} alignSelf="center" paddingHorizontal="$4">
          {/* Header */}
          <YStack gap="$2" alignItems="center">
            <H3 color="$green10" fontWeight="700" textAlign="center">
              <TrendingUp size={28} /> Scambi di Mercato
            </H3>
            <Text color="$color11" textAlign="center" fontSize="$3">
              Vota gli scambi proposti e controlla lo storico
            </Text>
          </YStack>

          <Separator />

          {/* Year validation warning */}
          {!canVote && !isLoading && (
            <Card backgroundColor="$orange2" padding="$4" borderColor="$orange8" borderWidth={1}>
              <XStack gap="$3" alignItems="center">
                <AlertTriangle size={24} color="$orange10" />
                <YStack flex={1}>
                  <Text color="$orange11" fontWeight="600">
                    Votazione non disponibile
                  </Text>
                  <Text color="$orange10" fontSize="$2" marginTop="$1">
                    {!isCurrentYearLatest && selectedYear 
                      ? `Anno selezionato: ${formatSeasonFromYear(selectedYear)}. La votazione è disponibile solo per l'anno corrente (${latestYear ? formatSeasonFromYear(latestYear) : 'N/A'}).`
                      : 'Seleziona un gruppo e una lega dalla barra di navigazione.'}
                  </Text>
                </YStack>
              </XStack>
            </Card>
          )}

          {/* Loading */}
          {isLoading && (
            <YStack alignItems="center" padding="$6" gap="$3">
              <Spinner size="large" color="$green10" />
              <Text color="$color11">Caricamento scambi...</Text>
            </YStack>
          )}

          {/* Error */}
          {error && !isLoading && (
            <Card backgroundColor="$red2" padding="$4" borderColor="$red8" borderWidth={1}>
              <Text color="$red11" textAlign="center">
                ❌ {error}
              </Text>
            </Card>
          )}

          {/* Filters and stats */}
          {!isLoading && (
            <>
              <Card padding="$4" backgroundColor="$color2" borderWidth={1} borderColor="$borderColor">
                <YStack gap="$3">
                  <XStack justifyContent="space-between" alignItems="center" gap="$3" flexWrap="wrap">
                    <Text fontSize="$4" fontWeight="600" color="$color12">
                      Filtri
                    </Text>
                    <XStack gap="$3" alignItems="center" flexWrap="wrap">
                      <Text fontSize="$3" color="$color10">
                        Totale: {filteredMarkets.length}
                      </Text>
                      {canVote && marketNeedsLeagueVote && (
                        <Text fontSize="$3" color="$blue10" fontWeight="500">
                          Quorum: {quorum}/{leagueSize} squadre
                        </Text>
                      )}
                    </XStack>
                  </XStack>

                  <Select value={statusFilter} onValueChange={setStatusFilter}>
                    <Select.Trigger width="100%" iconAfter={Filter}>
                      <Select.Value placeholder="Filtra per stato" />
                    </Select.Trigger>

                    <Adapt when="sm" platform="touch">
                      <Sheet
                        modal
                        dismissOnSnapToBottom
                        transitionConfig={{
                          type: 'spring',
                          damping: 20,
                          mass: 1.2,
                          stiffness: 250,
                        }}
                      >
                        <Sheet.Frame>
                          <Sheet.ScrollView>
                            <Adapt.Contents />
                          </Sheet.ScrollView>
                        </Sheet.Frame>
                        <Sheet.Overlay
                          transition="lazy"
                          enterStyle={{ opacity: 0 }}
                          exitStyle={{ opacity: 0 }}
                        />
                      </Sheet>
                    </Adapt>

                    <Select.Content>
                      <Select.ScrollUpButton />
                      <Select.Viewport minWidth={200}>
                        <Select.Item index={0} value="all">
                          <Select.ItemText>Tutti</Select.ItemText>
                        </Select.Item>
                        <Select.Item index={1} value="pending">
                          <Select.ItemText>In attesa</Select.ItemText>
                        </Select.Item>
                        <Select.Item index={2} value="completed">
                          <Select.ItemText>Completati</Select.ItemText>
                        </Select.Item>
                        <Select.Item index={3} value={MarketStatus.Approved.toString()}>
                          <Select.ItemText>Approvati</Select.ItemText>
                        </Select.Item>
                        <Select.Item index={4} value={MarketStatus.Denied.toString()}>
                          <Select.ItemText>Rifiutati</Select.ItemText>
                        </Select.Item>
                        <Select.Item index={5} value={MarketStatus.Cancelled.toString()}>
                          <Select.ItemText>Annullati</Select.ItemText>
                        </Select.Item>
                      </Select.Viewport>
                      <Select.ScrollDownButton />
                    </Select.Content>
                  </Select>
                </YStack>
              </Card>

              {/* Markets list */}
              <ScrollView maxHeight={800}>
                <YStack gap="$3">
                  {filteredMarkets.length === 0 ? (
                    <Card padding="$6" backgroundColor="$color2" borderWidth={1} borderColor="$borderColor">
                      <YStack alignItems="center" gap="$2">
                        <Calendar size={48} color="$color8" />
                        <Text fontSize="$4" color="$color10" textAlign="center">
                          Nessuno scambio trovato
                        </Text>
                        <Text fontSize="$2" color="$color9" textAlign="center">
                          {statusFilter !== 'all' 
                            ? 'Prova a cambiare il filtro'
                            : 'Non ci sono scambi in questa lega'}
                        </Text>
                      </YStack>
                    </Card>
                  ) : (
                    filteredMarkets.map(renderMarketCard)
                  )}
                </YStack>
              </ScrollView>
            </>
          )}

          {/* Info */}
          {!isLoading && canVote && (
            <Card backgroundColor="$blue2" padding="$3" marginTop="$2">
              <Text fontSize="$2" color="$blue11" textAlign="center">
                Il venditore deve prima accettare ogni proposta. {marketNeedsLeagueVote
                  ? `Dopo l'accettazione, servono almeno ${quorum} voti favorevoli su ${leagueSize} squadre (50% + 1).`
                  : 'Se il venditore accetta, lo scambio viene eseguito subito.'}
              </Text>
            </Card>
          )}
        </YStack>
      </ScreenContent>

      {/* Market detail sheet */}
      {selectedMarket && (
        <Sheet
          modal
          open={!!selectedMarket}
          onOpenChange={(open: boolean) => !open && setSelectedMarket(null)}
          snapPoints={[90]}
          snapPointsMode="percent"
          dismissOnSnapToBottom
        >
          <Sheet.Overlay transition="lazy" enterStyle={{ opacity: 0 }} exitStyle={{ opacity: 0 }} />
          <Sheet.Frame padding="$4" backgroundColor="$background">
            <Sheet.Handle />
            <ScrollView>
              <YStack gap="$4" paddingVertical="$4">
                {/* Header */}
                <YStack gap="$2">
                  <H5 color="$color12" fontWeight="700">
                    Dettaglio Scambio
                  </H5>
                  <XStack gap="$2" alignItems="center">
                    <Text fontSize="$2" color="$color10">
                      {formatDate(selectedMarket.creationTime)}
                    </Text>
                    <Text fontSize="$2" color="$color10">•</Text>
                    <Text
                      fontSize="$3"
                      fontWeight="600"
                      color={getStatusColor(selectedMarket.status)}
                    >
                      {MarketStatusHelper.asLabel(selectedMarket.status)}
                    </Text>
                  </XStack>
                </YStack>

                <Separator />

                {/* Teams */}
                <XStack gap="$4" flexWrap="wrap">
                  {/* Buyer */}
                  <YStack flex={1} minWidth={250} gap="$2">
                    <Text fontSize="$4" fontWeight="600" color="$blue10">
                      {getTeamNameByOwner(selectedMarket.buyer)}
                    </Text>
                    <Text fontSize="$2" color="$color10">
                      Cede:
                    </Text>
                    <YStack gap="$1">
                      {selectedMarket.buyerPlayers.map((player) => (
                        <XStack key={player.name} gap="$2" alignItems="center">
                          <Text fontSize="$2" color={getRoleColor(player.role)} fontWeight="600">
                            {getRoleLabel(player.role)}
                          </Text>
                          <Text fontSize="$3" color="$color12">
                            {player.name}
                          </Text>
                          <Text fontSize="$2" color="$color10">
                            ({formatCurrency(player.price)})
                          </Text>
                        </XStack>
                      ))}
                      {selectedMarket.moneyFromBuyer > 0 && (
                        <XStack gap="$2" alignItems="center">
                          <DollarSign size={14} color="$green10" />
                          <Text fontSize="$3" color="$green10" fontWeight="600">
                            {formatCurrency(selectedMarket.moneyFromBuyer)}
                          </Text>
                        </XStack>
                      )}
                    </YStack>
                  </YStack>

                  {/* Arrow */}
                  <YStack justifyContent="center" alignItems="center">
                    <ArrowRightLeft size={32} color="$color10" />
                  </YStack>

                  {/* Seller */}
                  <YStack flex={1} minWidth={250} gap="$2">
                    <Text fontSize="$4" fontWeight="600" color="$orange10">
                      {getTeamNameByOwner(selectedMarket.seller)}
                    </Text>
                    <Text fontSize="$2" color="$color10">
                      Cede:
                    </Text>
                    <YStack gap="$1">
                      {selectedMarket.sellerPlayers.map((player) => (
                        <XStack key={player.name} gap="$2" alignItems="center">
                          <Text fontSize="$2" color={getRoleColor(player.role)} fontWeight="600">
                            {getRoleLabel(player.role)}
                          </Text>
                          <Text fontSize="$3" color="$color12">
                            {player.name}
                          </Text>
                          <Text fontSize="$2" color="$color10">
                            ({formatCurrency(player.price)})
                          </Text>
                        </XStack>
                      ))}
                      {selectedMarket.moneyFromSeller > 0 && (
                        <XStack gap="$2" alignItems="center">
                          <DollarSign size={14} color="$green10" />
                          <Text fontSize="$3" color="$green10" fontWeight="600">
                            {formatCurrency(selectedMarket.moneyFromSeller)}
                          </Text>
                        </XStack>
                      )}
                    </YStack>
                  </YStack>
                </XStack>

                <Separator />

                {/* Seller acceptance status or voting status */}
                {MarketStatusHelper.isPending(selectedMarket.status) && (
                  <>
                    {!selectedMarket.sellerAccepted ? (
                      <Card padding="$4" backgroundColor="$orange2" borderWidth={1} borderColor="$orange8">
                        <YStack gap="$2">
                          <Text fontSize="$4" fontWeight="600" color="$orange11">
                            In attesa dell&apos;accettazione del venditore
                          </Text>
                          {selectedMarket.seller.toLowerCase() === myOwner.toLowerCase() ? (
                            <Text fontSize="$3" color="$orange10">
                              La proposta non modifica le squadre finché non scegli di accettarla.
                            </Text>
                          ) : (
                            <Text fontSize="$3" color="$orange10">
                              Lo scambio non può essere eseguito senza l&apos;accettazione esplicita del venditore.
                            </Text>
                          )}
                        </YStack>
                      </Card>
                    ) : marketNeedsLeagueVote ? (
                    <YStack gap="$3">
                      <Text fontSize="$4" fontWeight="600" color="$color12">
                        Stato Votazione
                      </Text>
                      
                      <Card padding="$3" backgroundColor="$green2" borderWidth={1} borderColor="$green8">
                        <YStack gap="$2">
                          <XStack justifyContent="space-between" alignItems="center">
                            <XStack gap="$2" alignItems="center">
                              <ThumbsUp size={20} color="$green10" />
                              <Text fontSize="$4" fontWeight="600" color="$green11">
                                Favorevoli
                              </Text>
                            </XStack>
                            <Text fontSize="$5" fontWeight="700" color="$green11">
                              {selectedMarket.approvers.length}/{quorum}
                            </Text>
                          </XStack>
                          {selectedMarket.approvers.length > 0 && (
                            <YStack gap="$1" marginTop="$1">
                              {selectedMarket.approvers.map((approver) => (
                                <Text key={approver} fontSize="$2" color="$green10">
                                  • {getTeamNameByOwner(approver)}
                                </Text>
                              ))}
                            </YStack>
                          )}
                        </YStack>
                      </Card>

                      <Card padding="$3" backgroundColor="$red2" borderWidth={1} borderColor="$red8">
                        <YStack gap="$2">
                          <XStack justifyContent="space-between" alignItems="center">
                            <XStack gap="$2" alignItems="center">
                              <ThumbsDown size={20} color="$red10" />
                              <Text fontSize="$4" fontWeight="600" color="$red11">
                                Contrari
                              </Text>
                            </XStack>
                            <Text fontSize="$5" fontWeight="700" color="$red11">
                              {selectedMarket.deniers.length}/{quorum}
                            </Text>
                          </XStack>
                          {selectedMarket.deniers.length > 0 && (
                            <YStack gap="$1" marginTop="$1">
                              {selectedMarket.deniers.map((denier) => (
                                <Text key={denier} fontSize="$2" color="$red10">
                                  • {getTeamNameByOwner(denier)}
                                </Text>
                              ))}
                            </YStack>
                          )}
                        </YStack>
                      </Card>

                      {MarketHelper.hasReachedApprovalQuorum(selectedMarket, leagueSize) && (
                        <Card backgroundColor="$green2" padding="$3" borderColor="$green8" borderWidth={1}>
                          <XStack gap="$2" alignItems="center">
                            <CheckCircle size={20} color="$green10" />
                            <Text color="$green11" fontWeight="600">
                              Quorum raggiunto! Lo scambio sarà approvato.
                            </Text>
                          </XStack>
                        </Card>
                      )}

                      {MarketHelper.hasReachedDenialQuorum(selectedMarket, leagueSize) && (
                        <Card backgroundColor="$red2" padding="$3" borderColor="$red8" borderWidth={1}>
                          <XStack gap="$2" alignItems="center">
                            <XCircle size={20} color="$red10" />
                            <Text color="$red11" fontWeight="600">
                              Quorum raggiunto! Lo scambio sarà rifiutato.
                            </Text>
                          </XStack>
                        </Card>
                      )}
                    </YStack>
                    ) : (
                      <Card padding="$4" backgroundColor="$green2" borderWidth={1} borderColor="$green8">
                        <Text fontSize="$3" color="$green11">
                          Proposta accettata dal venditore. Lo scambio è stato eseguito.
                        </Text>
                      </Card>
                    )}

                    <Separator />

                    {canVote && !selectedMarket.sellerAccepted &&
                      selectedMarket.seller.toLowerCase() === myOwner.toLowerCase() && (
                      <YStack gap="$2">
                        <Text fontSize="$4" fontWeight="600" color="$color12">
                          La tua risposta
                        </Text>
                        <XStack gap="$3" flexWrap="wrap">
                          <Button
                            flex={1}
                            minWidth={150}
                            size="$4"
                            backgroundColor="$green9"
                            color="white"
                            onPress={() => handleSellerDecision(selectedMarket, 'accept')}
                            disabled={isVoting}
                            opacity={isVoting ? 0.5 : 1}
                          >
                            {isVoting ? <Spinner color="white" /> : <Text fontSize="$4" fontWeight="600" color="white">Accetta proposta</Text>}
                          </Button>
                          <Button
                            flex={1}
                            minWidth={150}
                            size="$4"
                            backgroundColor="$red9"
                            color="white"
                            onPress={() => handleSellerDecision(selectedMarket, 'reject')}
                            disabled={isVoting}
                            opacity={isVoting ? 0.5 : 1}
                          >
                            {isVoting ? <Spinner color="white" /> : <Text fontSize="$4" fontWeight="600" color="white">Rifiuta proposta</Text>}
                          </Button>
                        </XStack>
                      </YStack>
                    )}

                    {/* Voting buttons */}
                    {canVote && marketNeedsLeagueVote && selectedMarket.sellerAccepted &&
                      !MarketHelper.isUserInvolved(selectedMarket, myOwner) && (
                      <YStack gap="$2">
                        <Text fontSize="$4" fontWeight="600" color="$color12">
                          Il tuo voto
                        </Text>
                        
                        {MarketHelper.getUserVote(selectedMarket, myOwner) && (
                          <Text fontSize="$2" color="$color10" marginBottom="$2">
                            Hai già votato, ma puoi cambiare idea
                          </Text>
                        )}

                        <XStack gap="$3" flexWrap="wrap">
                          <Button
                            flex={1}
                            minWidth={150}
                            size="$4"
                            backgroundColor="$green9"
                            color="white"
                            onPress={() => handleVote(selectedMarket, 'approve')}
                            disabled={isVoting}
                            opacity={isVoting ? 0.5 : 1}
                          >
                            {isVoting ? (
                              <Spinner color="white" />
                            ) : (
                              <XStack gap="$2" alignItems="center">
                                <ThumbsUp size={20} />
                                <Text fontSize="$4" fontWeight="600" color="white">
                                  Approva
                                </Text>
                              </XStack>
                            )}
                          </Button>

                          <Button
                            flex={1}
                            minWidth={150}
                            size="$4"
                            backgroundColor="$red9"
                            color="white"
                            onPress={() => handleVote(selectedMarket, 'deny')}
                            disabled={isVoting}
                            opacity={isVoting ? 0.5 : 1}
                          >
                            {isVoting ? (
                              <Spinner color="white" />
                            ) : (
                              <XStack gap="$2" alignItems="center">
                                <ThumbsDown size={20} />
                                <Text fontSize="$4" fontWeight="600" color="white">
                                  Rifiuta
                                </Text>
                              </XStack>
                            )}
                          </Button>
                        </XStack>
                      </YStack>
                    )}
                  </>
                )}

                {/* Completed status */}
                {MarketStatusHelper.isCompleted(selectedMarket.status) && (
                  <Card 
                    padding="$4" 
                    backgroundColor={getStatusBgColor(selectedMarket.status)}
                    borderWidth={1}
                    borderColor={getStatusColor(selectedMarket.status)}
                  >
                    <YStack gap="$2" alignItems="center">
                      {MarketStatusHelper.isSuccessful(selectedMarket.status) ? (
                        <>
                          <CheckCircle size={48} color="$green10" />
                          <Text fontSize="$5" fontWeight="600" color="$green11">
                            Scambio Completato
                          </Text>
                        </>
                      ) : (
                        <>
                          <XCircle size={48} color={getStatusColor(selectedMarket.status)} />
                          <Text fontSize="$5" fontWeight="600" color={getStatusColor(selectedMarket.status)}>
                            {MarketStatusHelper.asLabel(selectedMarket.status)}
                          </Text>
                        </>
                      )}
                      <Text fontSize="$3" color="$color10" textAlign="center" marginTop="$2">
                        Voti favorevoli: {selectedMarket.approvers.length} • 
                        Voti contrari: {selectedMarket.deniers.length}
                      </Text>
                    </YStack>
                  </Card>
                )}

                {/* Close button */}
                <Button
                  size="$4"
                  variant="outlined"
                  onPress={() => setSelectedMarket(null)}
                  marginTop="$2"
                >
                  Chiudi
                </Button>
              </YStack>
            </ScrollView>
          </Sheet.Frame>
        </Sheet>
      )}
    </View>
  )
}
