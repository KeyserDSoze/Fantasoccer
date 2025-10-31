import { useState, useEffect, useMemo, useCallback } from 'react'
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
  Input,
  ScrollView,
} from 'tamagui'
import {
  DollarSign,
  Users,
  AlertTriangle,
  ArrowRightLeft,
  Plus,
  Minus,
  CheckCircle,
  XCircle,
} from '@tamagui/lucide-icons'
import { ScreenContent } from 'components/layout/ScreenContent'
import { useApp } from 'providers/AppProvider'
import { useAuth } from 'providers/AuthProvider'
import { teamService } from 'services/teamService'
import { marketService } from 'services/marketService'
import { EnhancedTeam, Player } from 'types/team'
import { Role } from 'types/group'
import { MarketStatus } from 'types/market'
import { formatSeasonFromYear } from 'utils/season'

// Helper to get role label
const getRoleLabel = (role: Role): string => {
  switch (role) {
    case Role.GoalKeeper: return 'Portiere'
    case Role.Defensor: return 'Difensore'
    case Role.Midfielder: return 'Centrocampista'
    case Role.Forward: return 'Attaccante'
    default: return 'Altro'
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

export default function MarketScreen() {
  const { currentGroup, selectedLeagueId, selectedYear } = useApp()
  const { user } = useAuth()
  
  const [teams, setTeams] = useState<EnhancedTeam[]>([])
  const [myTeam, setMyTeam] = useState<EnhancedTeam | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  
  // Trade state
  const [selectedTargetTeam, setSelectedTargetTeam] = useState<EnhancedTeam | null>(null)
  const [mySelectedPlayers, setMySelectedPlayers] = useState<Player[]>([])
  const [targetSelectedPlayers, setTargetSelectedPlayers] = useState<Player[]>([])
  const [myMoneyOffer, setMyMoneyOffer] = useState<number>(0)
  const [targetMoneyOffer, setTargetMoneyOffer] = useState<number>(0)
  const [isCreatingTrade, setIsCreatingTrade] = useState(false)
  const [tradeSuccess, setTradeSuccess] = useState(false)

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
  const canCreateTrade = isCurrentYearLatest && selectedYear != null && selectedLeagueId != null

  // Get league settings
  const leagueSettings = useMemo(() => {
    if (!currentGroup || !selectedLeagueId || !selectedYear) return null
    
    const league = currentGroup.leagues?.find((l: any) => l.id === selectedLeagueId)
    if (!league) return null
    
    const annualLeague = league.years?.find((y: any) => y.year === selectedYear)
    return annualLeague?.settings ?? null
  }, [currentGroup, selectedLeagueId, selectedYear])

  // Calculate team budget: startingMoney - totalCost + moneyFromRank
  const getTeamBudget = useCallback((team: EnhancedTeam): number => {
    if (!leagueSettings) return 0
    return leagueSettings.startingMoney - team.totalCost + team.moneyFromRank
  }, [leagueSettings])

  // Get available budget for my team (considering current money offer)
  const getMyAvailableBudget = useMemo(() => {
    if (!myTeam) return 0
    return getTeamBudget(myTeam) - myMoneyOffer
  }, [myTeam, getTeamBudget, myMoneyOffer])

  // Get available budget for target team (considering current money offer)
  const getTargetAvailableBudget = useMemo(() => {
    if (!selectedTargetTeam) return 0
    return getTeamBudget(selectedTargetTeam) - targetMoneyOffer
  }, [selectedTargetTeam, getTeamBudget, targetMoneyOffer])

  // Load teams
  useEffect(() => {
    loadTeams()
  }, [currentGroup, selectedLeagueId, selectedYear, user?.email])

  const loadTeams = async () => {
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
        setTeams([])
        setMyTeam(null)
        setIsLoading(false)
        return
      }

      const basketId = league.basketsId[0]
      const basket = currentGroup.baskets?.find((b: any) => b.id === basketId)
      if (!basket) {
        setError('Basket non trovato')
        setTeams([])
        setMyTeam(null)
        setIsLoading(false)
        return
      }

      const yearlyBasket = basket.years?.find((y: any) => y.year === selectedYear)
      if (!yearlyBasket || !yearlyBasket.teams || yearlyBasket.teams.length === 0) {
        setError('Nessuna squadra trovata per questo anno')
        setTeams([])
        setMyTeam(null)
        setIsLoading(false)
        return
      }

      // Get league settings for moneyFromRank calculation
      const annualLeague = league.years?.find((y: any) => y.year === selectedYear)
      const settings = annualLeague?.settings

      const owners = yearlyBasket.teams.map((t: any) => t.owner)
      const fetchedTeams = await teamService.getEnhancedTeamsByBasket(
        currentGroup.id,
        selectedYear,
        basketId,
        owners,
        selectedLeagueId,
        settings
      )

      setTeams(fetchedTeams)
      
      // Find my team
      const myTeamData = fetchedTeams.find(t => 
        t.owner.toLowerCase() === user.email.toLowerCase()
      )
      setMyTeam(myTeamData ?? null)

    } catch (err: any) {
      console.error('Error loading teams:', err)
      setError(err?.message ?? 'Errore nel caricamento delle squadre')
    } finally {
      setIsLoading(false)
    }
  }

  // Filter active players only - EnhancedTeam already provides activePlayers
  const getActivePlayers = (team: EnhancedTeam): Player[] => {
    return team.activePlayers
  }

  // Group players by role
  const groupPlayersByRole = (players: Player[]): Record<Role, Player[]> => {
    const groups: Record<Role, Player[]> = {
      [Role.GoalKeeper]: [],
      [Role.Defensor]: [],
      [Role.Midfielder]: [],
      [Role.Forward]: [],
      [Role.Undefined]: [],
    }

    players.forEach(player => {
      const role = player.role ?? Role.Undefined
      if (!groups[role]) groups[role] = []
      groups[role].push(player)
    })

    return groups
  }

  // Calculate role balance
  const getRoleBalance = useMemo(() => {
    const myRoles = groupPlayersByRole(mySelectedPlayers)
    const targetRoles = groupPlayersByRole(targetSelectedPlayers)

    const balance: Record<Role, number> = {
      [Role.GoalKeeper]: myRoles[Role.GoalKeeper].length - targetRoles[Role.GoalKeeper].length,
      [Role.Defensor]: myRoles[Role.Defensor].length - targetRoles[Role.Defensor].length,
      [Role.Midfielder]: myRoles[Role.Midfielder].length - targetRoles[Role.Midfielder].length,
      [Role.Forward]: myRoles[Role.Forward].length - targetRoles[Role.Forward].length,
      [Role.Undefined]: 0,
    }

    return balance
  }, [mySelectedPlayers, targetSelectedPlayers])

  // Check if trade is balanced (all roles must be 0)
  const isTradeBalanced = useMemo(() => {
    return Object.values(getRoleBalance).every(balance => balance === 0)
  }, [getRoleBalance])

  // Calculate total values
  const myTotalValue = useMemo(() => {
    const playersValue = mySelectedPlayers.reduce((sum, p) => sum + (p.price || 0), 0)
    return playersValue + myMoneyOffer
  }, [mySelectedPlayers, myMoneyOffer])

  const targetTotalValue = useMemo(() => {
    const playersValue = targetSelectedPlayers.reduce((sum, p) => sum + (p.price || 0), 0)
    return playersValue + targetMoneyOffer
  }, [targetSelectedPlayers, targetMoneyOffer])

  // Toggle player selection
  const togglePlayerSelection = (player: Player, isMyTeam: boolean) => {
    if (isMyTeam) {
      setMySelectedPlayers(prev => 
        prev.find(p => p.name === player.name)
          ? prev.filter(p => p.name !== player.name)
          : [...prev, player]
      )
    } else {
      setTargetSelectedPlayers(prev => 
        prev.find(p => p.name === player.name)
          ? prev.filter(p => p.name !== player.name)
          : [...prev, player]
      )
    }
  }

  // Create trade
  const handleCreateTrade = async () => {
    if (!currentGroup || !selectedLeagueId || !selectedYear || !user?.email || !selectedTargetTeam || !myTeam) {
      return
    }

    if (!isTradeBalanced) {
      setError('Lo scambio deve avere lo stesso numero di giocatori per ruolo')
      return
    }

    if (mySelectedPlayers.length === 0 || targetSelectedPlayers.length === 0) {
      setError('Seleziona almeno un giocatore per squadra')
      return
    }

    // Validate budgets
    if (myMoneyOffer > getTeamBudget(myTeam)) {
      setError(`Non hai abbastanza budget. Disponibile: ${formatCurrency(getTeamBudget(myTeam))}`)
      return
    }

    if (targetMoneyOffer > getTeamBudget(selectedTargetTeam)) {
      setError(`${selectedTargetTeam.name} non ha abbastanza budget. Disponibile: ${formatCurrency(getTeamBudget(selectedTargetTeam))}`)
      return
    }

    if (getMyAvailableBudget < 0) {
      setError('Il tuo budget non può essere negativo dopo lo scambio')
      return
    }

    if (getTargetAvailableBudget < 0) {
      setError(`Il budget di ${selectedTargetTeam.name} non può essere negativo dopo lo scambio`)
      return
    }

    setIsCreatingTrade(true)
    setError(null)

    try {
      const market = {
        id: `${Date.now()}-${user.email}`, // Generate unique ID
        buyer: user.email,
        seller: selectedTargetTeam.owner,
        buyerPlayers: mySelectedPlayers,
        sellerPlayers: targetSelectedPlayers,
        moneyFromBuyer: myMoneyOffer,
        moneyFromSeller: targetMoneyOffer,
        approvers: [],
        deniers: [],
        status: MarketStatus.Pending,
        creationTime: new Date(),
      }

      const success = await marketService.updateMarket(
        currentGroup.id,
        selectedYear,
        selectedLeagueId,
        market
      )

      if (success) {
        setTradeSuccess(true)
        // Reset form
        setSelectedTargetTeam(null)
        setMySelectedPlayers([])
        setTargetSelectedPlayers([])
        setMyMoneyOffer(0)
        setTargetMoneyOffer(0)
        
        setTimeout(() => setTradeSuccess(false), 3000)
      } else {
        setError('Errore nella creazione dello scambio')
      }
    } catch (err: any) {
      console.error('Error creating trade:', err)
      setError(err?.message ?? 'Errore nella creazione dello scambio')
    } finally {
      setIsCreatingTrade(false)
    }
  }

  const formatCurrency = (amount: number) => {
    return new Intl.NumberFormat('it-IT', {
      style: 'currency',
      currency: 'EUR',
      minimumFractionDigits: 0,
      maximumFractionDigits: 0,
    }).format(amount)
  }

  return (
    <View style={{ flex: 1 }}>
      <ScreenContent path="screens/market.tsx" title="Mercato">
        <YStack gap="$4" width="100%" maxWidth={1200} alignSelf="center" paddingHorizontal="$4">
          {/* Header */}
          <YStack gap="$2" alignItems="center">
            <H3 color="$green10" fontWeight="700" textAlign="center">
              <DollarSign size={28} /> Crea Nuovo Scambio
            </H3>
            <Text color="$color11" textAlign="center" fontSize="$3">
              Proponi uno scambio di giocatori con un'altra squadra
            </Text>
          </YStack>

          <Separator />

          {/* Year validation warning */}
          {!canCreateTrade && !isLoading && (
            <Card backgroundColor="$orange2" padding="$4" borderColor="$orange8" borderWidth={1}>
              <XStack gap="$3" alignItems="center">
                <AlertTriangle size={24} color="$orange10" />
                <YStack flex={1}>
                  <Text color="$orange11" fontWeight="600">
                    Mercato chiuso per questo anno
                  </Text>
                  <Text color="$orange10" fontSize="$2" marginTop="$1">
                    {!isCurrentYearLatest && selectedYear 
                      ? `Anno selezionato: ${formatSeasonFromYear(selectedYear)}. Il mercato è disponibile solo per l'anno corrente (${latestYear ? formatSeasonFromYear(latestYear) : 'N/A'}).`
                      : 'Seleziona un gruppo e una lega dalla barra di navigazione per accedere al mercato.'}
                  </Text>
                </YStack>
              </XStack>
            </Card>
          )}

          {/* Loading */}
          {isLoading && (
            <YStack alignItems="center" padding="$6" gap="$3">
              <Spinner size="large" color="$green10" />
              <Text color="$color11">Caricamento squadre...</Text>
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

          {/* Success message */}
          {tradeSuccess && (
            <Card backgroundColor="$green2" padding="$4" borderColor="$green8" borderWidth={1}>
              <XStack gap="$3" alignItems="center">
                <CheckCircle size={24} color="$green10" />
                <Text color="$green11" fontWeight="600">
                  Scambio creato con successo! In attesa di approvazione.
                </Text>
              </XStack>
            </Card>
          )}

          {/* Main content */}
          {!isLoading && canCreateTrade && myTeam && (
            <YStack gap="$4">
              {/* My Team Info */}
              <Card backgroundColor="$blue2" padding="$4" borderColor="$blue8" borderWidth={1}>
                <Text fontSize="$5" fontWeight="600" color="$blue11">
                  La mia squadra: {myTeam.name}
                </Text>
                <XStack gap="$4" marginTop="$2" flexWrap="wrap">
                  <YStack>
                    <Text fontSize="$2" color="$blue10">Budget disponibile</Text>
                    <Text fontSize="$4" fontWeight="600" color="$blue11">
                      💰 {formatCurrency(getTeamBudget(myTeam))}
                    </Text>
                  </YStack>
                  <YStack>
                    <Text fontSize="$2" color="$blue10">Valore rosa</Text>
                    <Text fontSize="$4" fontWeight="600" color="$blue11">
                      📊 {formatCurrency(myTeam.totalCost)}
                    </Text>
                  </YStack>
                  <YStack>
                    <Text fontSize="$2" color="$blue10">Premi classifica</Text>
                    <Text fontSize="$4" fontWeight="600" color="$blue11">
                      🏆 {formatCurrency(myTeam.moneyFromRank)}
                    </Text>
                  </YStack>
                </XStack>
              </Card>

              {/* Select target team */}
              <Card padding="$4" backgroundColor="$color2" borderWidth={1} borderColor="$borderColor">
                <Text fontSize="$5" fontWeight="600" color="$color12" marginBottom="$3">
                  1️⃣ Seleziona squadra con cui scambiare
                </Text>
                
                <YStack gap="$2">
                  {teams
                    .filter(t => t.owner.toLowerCase() !== user?.email?.toLowerCase())
                    .map((team) => (
                      <Button
                        key={team.owner}
                        variant="outlined"
                        backgroundColor={selectedTargetTeam?.owner === team.owner ? '$green2' : 'transparent'}
                        borderColor={selectedTargetTeam?.owner === team.owner ? '$green8' : '$borderColor'}
                        onPress={() => {
                          setSelectedTargetTeam(team)
                          setTargetSelectedPlayers([])
                        }}
                        justifyContent="flex-start"
                        paddingHorizontal="$4"
                        paddingVertical="$3"
                        height="auto"
                      >
                        <XStack gap="$3" alignItems="flex-start" flex={1} width="100%">
                          <Users size={20} color={selectedTargetTeam?.owner === team.owner ? '$green10' : '$color11'} style={{ marginTop: 2 }} />
                          <YStack flex={1} alignItems="flex-start" gap="$1">
                            <Text fontSize="$4" fontWeight="500" color="$color12">
                              {team.name}
                            </Text>
                            <Text fontSize="$2" color="$color10">
                              {team.owner}
                            </Text>
                            <Text fontSize="$2" color="$color11">
                              💰 Budget: {formatCurrency(getTeamBudget(team))}
                            </Text>
                          </YStack>
                        </XStack>
                      </Button>
                    ))}
                </YStack>
              </Card>

              {/* Player selection */}
              {selectedTargetTeam && (
                <>
                  <Card padding="$4" backgroundColor="$color2" borderWidth={1} borderColor="$borderColor">
                    <Text fontSize="$5" fontWeight="600" color="$color12" marginBottom="$3">
                      2️⃣ Seleziona giocatori da scambiare
                    </Text>

                    <XStack gap="$4" flexWrap="wrap">
                      {/* My players */}
                      <YStack flex={1} minWidth={300} gap="$3">
                        <Text fontSize="$4" fontWeight="600" color="$blue10">
                          I miei giocatori
                        </Text>
                        <ScrollView maxHeight={400}>
                          <YStack gap="$2">
                            {getActivePlayers(myTeam).map((player) => {
                              const isSelected = mySelectedPlayers.find(p => p.name === player.name) != null
                              return (
                                <Button
                                  key={player.name}
                                  variant="outlined"
                                  size="$3"
                                  backgroundColor={isSelected ? '$blue2' : 'transparent'}
                                  borderColor={isSelected ? '$blue8' : '$borderColor'}
                                  onPress={() => togglePlayerSelection(player, true)}
                                  justifyContent="flex-start"
                                  paddingHorizontal="$3"
                                >
                                  <XStack gap="$2" alignItems="center" flex={1}>
                                    {isSelected ? (
                                      <CheckCircle size={16} color="$blue10" />
                                    ) : (
                                      <XCircle size={16} color="$color10" />
                                    )}
                                    <YStack flex={1} alignItems="flex-start">
                                      <Text fontSize="$3" fontWeight="500" color="$color12">
                                        {player.name}
                                      </Text>
                                      <XStack gap="$2">
                                        <Text fontSize="$2" color={getRoleColor(player.role)}>
                                          {getRoleLabel(player.role)}
                                        </Text>
                                        <Text fontSize="$2" color="$color10">
                                          • {formatCurrency(player.price)}
                                        </Text>
                                      </XStack>
                                    </YStack>
                                  </XStack>
                                </Button>
                              )
                            })}
                          </YStack>
                        </ScrollView>
                      </YStack>

                      {/* Arrow */}
                      <YStack justifyContent="center" alignItems="center" paddingVertical="$10">
                        <ArrowRightLeft size={32} color="$color10" />
                      </YStack>

                      {/* Target players */}
                      <YStack flex={1} minWidth={300} gap="$3">
                        <Text fontSize="$4" fontWeight="600" color="$orange10">
                          Giocatori di {selectedTargetTeam.name}
                        </Text>
                        <ScrollView maxHeight={400}>
                          <YStack gap="$2">
                            {getActivePlayers(selectedTargetTeam).map((player) => {
                              const isSelected = targetSelectedPlayers.find(p => p.name === player.name) != null
                              return (
                                <Button
                                  key={player.name}
                                  variant="outlined"
                                  size="$3"
                                  backgroundColor={isSelected ? '$orange2' : 'transparent'}
                                  borderColor={isSelected ? '$orange8' : '$borderColor'}
                                  onPress={() => togglePlayerSelection(player, false)}
                                  justifyContent="flex-start"
                                  paddingHorizontal="$3"
                                >
                                  <XStack gap="$2" alignItems="center" flex={1}>
                                    {isSelected ? (
                                      <CheckCircle size={16} color="$orange10" />
                                    ) : (
                                      <XCircle size={16} color="$color10" />
                                    )}
                                    <YStack flex={1} alignItems="flex-start">
                                      <Text fontSize="$3" fontWeight="500" color="$color12">
                                        {player.name}
                                      </Text>
                                      <XStack gap="$2">
                                        <Text fontSize="$2" color={getRoleColor(player.role)}>
                                          {getRoleLabel(player.role)}
                                        </Text>
                                        <Text fontSize="$2" color="$color10">
                                          • {formatCurrency(player.price)}
                                        </Text>
                                      </XStack>
                                    </YStack>
                                  </XStack>
                                </Button>
                              )
                            })}
                          </YStack>
                        </ScrollView>
                      </YStack>
                    </XStack>
                  </Card>

                  {/* Role balance check */}
                  {(mySelectedPlayers.length > 0 || targetSelectedPlayers.length > 0) && (
                    <Card 
                      padding="$4" 
                      backgroundColor={isTradeBalanced ? '$green2' : '$orange2'} 
                      borderWidth={1} 
                      borderColor={isTradeBalanced ? '$green8' : '$orange8'}
                    >
                      <Text fontSize="$4" fontWeight="600" color={isTradeBalanced ? '$green11' : '$orange11'} marginBottom="$2">
                        Bilanciamento ruoli
                      </Text>
                      <YStack gap="$1">
                        {Object.entries(getRoleBalance).map(([role, balance]) => {
                          const roleNum = parseInt(role) as Role
                          if (roleNum === Role.Undefined) return null
                          return (
                            <XStack key={role} gap="$2" alignItems="center">
                              <Text fontSize="$3" color={getRoleColor(roleNum)} fontWeight="500" minWidth={120}>
                                {getRoleLabel(roleNum)}:
                              </Text>
                              <Text 
                                fontSize="$3" 
                                color={balance === 0 ? '$green10' : '$orange10'}
                                fontWeight="600"
                              >
                                {balance === 0 ? '✓ Bilanciato' : `${balance > 0 ? '+' : ''}${balance} (${Math.abs(balance)} ${balance > 0 ? 'in più da te' : 'in più da loro'})`}
                              </Text>
                            </XStack>
                          )
                        })}
                      </YStack>
                    </Card>
                  )}

                  {/* Money offers */}
                  <Card padding="$4" backgroundColor="$color2" borderWidth={1} borderColor="$borderColor">
                    <Text fontSize="$5" fontWeight="600" color="$color12" marginBottom="$3">
                      3️⃣ Aggiungi denaro (opzionale)
                    </Text>

                    <XStack gap="$4" flexWrap="wrap">
                      {/* My money offer */}
                      <YStack flex={1} minWidth={250} gap="$2">
                        <Text fontSize="$3" color="$blue10" fontWeight="500">
                          Denaro che offro
                        </Text>
                        <XStack gap="$2" alignItems="center">
                          <Button
                            size="$3"
                            circular
                            onPress={() => setMyMoneyOffer(Math.max(0, myMoneyOffer - 10))}
                            backgroundColor="$blue4"
                            disabled={myMoneyOffer <= 0}
                            opacity={myMoneyOffer <= 0 ? 0.5 : 1}
                          >
                            <Minus size={16} />
                          </Button>
                          <Input
                            flex={1}
                            value={myMoneyOffer.toString()}
                            onChangeText={(text) => {
                              const num = parseInt(text) || 0
                              const maxBudget = myTeam ? getTeamBudget(myTeam) : 0
                              setMyMoneyOffer(Math.max(0, Math.min(maxBudget, num)))
                            }}
                            keyboardType="numeric"
                            textAlign="center"
                          />
                          <Button
                            size="$3"
                            circular
                            onPress={() => {
                              const maxBudget = myTeam ? getTeamBudget(myTeam) : 0
                              setMyMoneyOffer(Math.min(maxBudget, myMoneyOffer + 10))
                            }}
                            backgroundColor="$blue4"
                            disabled={myTeam && myMoneyOffer >= getTeamBudget(myTeam)}
                            opacity={myTeam && myMoneyOffer >= getTeamBudget(myTeam) ? 0.5 : 1}
                          >
                            <Plus size={16} />
                          </Button>
                        </XStack>
                        <YStack gap="$1">
                          <Text fontSize="$2" color="$color10" textAlign="center">
                            {formatCurrency(myMoneyOffer)}
                          </Text>
                          <Text 
                            fontSize="$2" 
                            color={getMyAvailableBudget < 0 ? '$red10' : '$blue10'} 
                            textAlign="center"
                            fontWeight="500"
                          >
                            Disponibile: {formatCurrency(getMyAvailableBudget)}
                          </Text>
                        </YStack>
                      </YStack>

                      {/* Target money offer */}
                      <YStack flex={1} minWidth={250} gap="$2">
                        <Text fontSize="$3" color="$orange10" fontWeight="500">
                          Denaro richiesto a {selectedTargetTeam.name}
                        </Text>
                        <XStack gap="$2" alignItems="center">
                          <Button
                            size="$3"
                            circular
                            onPress={() => setTargetMoneyOffer(Math.max(0, targetMoneyOffer - 10))}
                            backgroundColor="$orange4"
                            disabled={targetMoneyOffer <= 0}
                            opacity={targetMoneyOffer <= 0 ? 0.5 : 1}
                          >
                            <Minus size={16} />
                          </Button>
                          <Input
                            flex={1}
                            value={targetMoneyOffer.toString()}
                            onChangeText={(text) => {
                              const num = parseInt(text) || 0
                              const maxBudget = selectedTargetTeam ? getTeamBudget(selectedTargetTeam) : 0
                              setTargetMoneyOffer(Math.max(0, Math.min(maxBudget, num)))
                            }}
                            keyboardType="numeric"
                            textAlign="center"
                          />
                          <Button
                            size="$3"
                            circular
                            onPress={() => {
                              const maxBudget = selectedTargetTeam ? getTeamBudget(selectedTargetTeam) : 0
                              setTargetMoneyOffer(Math.min(maxBudget, targetMoneyOffer + 10))
                            }}
                            backgroundColor="$orange4"
                            disabled={selectedTargetTeam && targetMoneyOffer >= getTeamBudget(selectedTargetTeam)}
                            opacity={selectedTargetTeam && targetMoneyOffer >= getTeamBudget(selectedTargetTeam) ? 0.5 : 1}
                          >
                            <Plus size={16} />
                          </Button>
                        </XStack>
                        <YStack gap="$1">
                          <Text fontSize="$2" color="$color10" textAlign="center">
                            {formatCurrency(targetMoneyOffer)}
                          </Text>
                          <Text 
                            fontSize="$2" 
                            color={getTargetAvailableBudget < 0 ? '$red10' : '$orange10'} 
                            textAlign="center"
                            fontWeight="500"
                          >
                            Disponibile: {formatCurrency(getTargetAvailableBudget)}
                          </Text>
                        </YStack>
                      </YStack>
                    </XStack>
                  </Card>

                  {/* Trade summary */}
                  <Card padding="$4" backgroundColor="$purple2" borderWidth={1} borderColor="$purple8">
                    <Text fontSize="$5" fontWeight="600" color="$purple11" marginBottom="$3">
                      Riepilogo scambio
                    </Text>
                    <XStack gap="$4" flexWrap="wrap" justifyContent="space-around">
                      <YStack gap="$1" alignItems="center">
                        <Text fontSize="$3" color="$color10">Tu dai</Text>
                        <Text fontSize="$6" fontWeight="700" color="$blue10">
                          {formatCurrency(myTotalValue)}
                        </Text>
                        <Text fontSize="$2" color="$color10">
                          {mySelectedPlayers.length} giocatori + {formatCurrency(myMoneyOffer)}
                        </Text>
                      </YStack>

                      <YStack justifyContent="center">
                        <ArrowRightLeft size={32} color="$purple10" />
                      </YStack>

                      <YStack gap="$1" alignItems="center">
                        <Text fontSize="$3" color="$color10">Tu ricevi</Text>
                        <Text fontSize="$6" fontWeight="700" color="$orange10">
                          {formatCurrency(targetTotalValue)}
                        </Text>
                        <Text fontSize="$2" color="$color10">
                          {targetSelectedPlayers.length} giocatori + {formatCurrency(targetMoneyOffer)}
                        </Text>
                      </YStack>
                    </XStack>
                  </Card>

                  {/* Create trade button */}
                  <Button
                    size="$5"
                    backgroundColor="$green9"
                    color="white"
                    fontWeight="700"
                    disabled={!isTradeBalanced || mySelectedPlayers.length === 0 || isCreatingTrade}
                    opacity={!isTradeBalanced || mySelectedPlayers.length === 0 ? 0.5 : 1}
                    onPress={handleCreateTrade}
                    marginTop="$2"
                  >
                    {isCreatingTrade ? (
                      <Spinner color="white" />
                    ) : (
                      <XStack gap="$2" alignItems="center">
                        <CheckCircle size={24} />
                        <Text fontSize="$5" fontWeight="700" color="white">
                          Crea Scambio
                        </Text>
                      </XStack>
                    )}
                  </Button>
                </>
              )}
            </YStack>
          )}

          {/* Info */}
          {!isLoading && canCreateTrade && (
            <Card backgroundColor="$blue2" padding="$3" marginTop="$2">
              <Text fontSize="$2" color="$blue11" textAlign="center">
                💡 Gli scambi devono avere lo stesso numero di giocatori per ogni ruolo. Lo scambio sarà in attesa di approvazione dagli altri membri della lega.
              </Text>
            </Card>
          )}
        </YStack>
      </ScreenContent>
    </View>
  )
}
