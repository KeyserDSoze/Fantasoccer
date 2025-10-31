import React, { useEffect, useState } from 'react'
import { View } from 'react-native'
import { ScreenContent } from 'components/layout/ScreenContent'
import { YStack, XStack, Text, H3, Separator, Card, Spinner, Button } from 'tamagui'
import { Users, DollarSign, TrendingUp, Award, ChevronDown, ChevronUp, User } from '@tamagui/lucide-icons'
import { useApp } from 'providers/AppProvider'
import { teamService } from 'services/teamService'
import type { EnhancedTeam, Player } from 'types/team'
import { PlayerInTeamStatus } from 'types/team'
import { TeamLogo } from 'components/team/TeamLogo'
import { Role } from 'types/group'
import { LeagueSelectorsWidget } from 'components/LeagueSelectorsWidget'
import type { LeagueOption } from 'components/LeagueSelectorsWidget'
import { formatSeasonFromYear } from 'utils/season'

export default function TeamsScreen() {
  const { currentGroup, selectedLeagueId, selectedYear } = useApp()
  const [teams, setTeams] = useState<EnhancedTeam[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [expandedTeamIndex, setExpandedTeamIndex] = useState<number | null>(null)
  
  // Local state for selectors (independent from global context)
  const [localLeagueId, setLocalLeagueId] = useState<string | null>(selectedLeagueId)
  const [localYear, setLocalYear] = useState<number | null>(selectedYear)

  // Sync local state with global state when global changes
  useEffect(() => {
    setLocalLeagueId(selectedLeagueId ?? null)
  }, [selectedLeagueId])

  useEffect(() => {
    setLocalYear(selectedYear ?? null)
  }, [selectedYear])

  // Load teams when local selections change
  useEffect(() => {
    loadTeams()
  }, [currentGroup, localLeagueId, localYear])

  const loadTeams = async () => {
    if (!currentGroup || !localLeagueId || !localYear) {
      setError('Configurazione lega non completa')
      setIsLoading(false)
      return
    }

    setIsLoading(true)
    setError(null)

    try {
      // Trova la lega selezionata
      const league = currentGroup.leagues?.find(l => l.id === localLeagueId)
      if (!league || !league.basketsId || league.basketsId.length === 0) {
        setError('Lega non valida o senza basket associato')
        setTeams([])
        setIsLoading(false)
        return
      }

      // Prendi il primo basketId della lega (o potresti avere logica per selezionare il basket corretto)
      const basketId = league.basketsId[0]
      
      // Trova il basket nel gruppo
      const basket = currentGroup.baskets?.find(b => b.id === basketId)
      if (!basket) {
        setError('Basket non trovato')
        setTeams([])
        setIsLoading(false)
        return
      }

      // Trova l'anno nel basket
      const yearlyBasket = basket.years?.find(y => y.year === localYear)
      if (!yearlyBasket || !yearlyBasket.teams || yearlyBasket.teams.length === 0) {
        setError('Nessuna squadra trovata per questo anno')
        setTeams([])
        setIsLoading(false)
        return
      }

      // Estrai gli owner (email) dai team
      const owners = yearlyBasket.teams.map(t => t.owner)

      // Get league settings for moneyFromRank calculation
      const annualLeague = league.years?.find((y: any) => y.year === localYear)
      const settings = annualLeague?.settings

      // Recupera tutti i team enhanced usando il teamService
      const fetchedTeams = await teamService.getEnhancedTeamsByBasket(
        currentGroup.id,
        localYear,
        basketId,
        owners,
        localLeagueId,
        settings
      )

      setTeams(fetchedTeams)
    } catch (err: any) {
      console.error('Error loading teams:', err)
      setError(err?.message ?? 'Errore nel caricamento delle squadre')
    } finally {
      setIsLoading(false)
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

  const getTotalCost = (team: EnhancedTeam) => {
    // Usa i valori precalcolati dell'EnhancedTeam
    return team.totalCost
  }

  const getBudget = (team: EnhancedTeam) => {
    // Budget = starting money - netCost + revenue + rank money
    // Per ora usiamo semplicemente il netCost che include i ricavi
    return team.cost // netCost è già il costo netto (spese - ricavi)
  }

  const getActivePlayers = (team: EnhancedTeam) => {
    return team.activePlayers
  }

  const groupPlayersByRole = (players: Player[]) => {
    const groups: Record<Role, Player[]> = {
      [Role.GoalKeeper]: [],
      [Role.Defensor]: [],
      [Role.Midfielder]: [],
      [Role.Forward]: [],
      [Role.Undefined]: [],
    }

    players.forEach(player => {
      const role = player.role ?? Role.Undefined
      if (!groups[role]) {
        groups[role] = []
      }
      groups[role].push(player)
    })

    // Ordina i giocatori per prezzo decrescente in ogni ruolo
    Object.keys(groups).forEach(roleKey => {
      const role = parseInt(roleKey) as Role
      groups[role].sort((a, b) => (b.price || 0) - (a.price || 0))
    })

    return groups
  }

  const getRoleLabel = (role: Role): string => {
    switch (role) {
      case Role.GoalKeeper: return '🥅 Portieri'
      case Role.Defensor: return '🛡️ Difensori'
      case Role.Midfielder: return '⚽ Centrocampisti'
      case Role.Forward: return '🎯 Attaccanti'
      default: return '❓ Altro'
    }
  }

  const getRoleColor = (role: Role): string => {
    switch (role) {
      case Role.GoalKeeper: return '$orange10'
      case Role.Defensor: return '$blue10'
      case Role.Midfielder: return '$green10'
      case Role.Forward: return '$red10'
      default: return '$gray10'
    }
  }

  const toggleTeamExpansion = (index: number) => {
    setExpandedTeamIndex(expandedTeamIndex === index ? null : index)
  }

  const getPlayerStatusLabel = (status: PlayerInTeamStatus): string => {
    switch (status) {
      case PlayerInTeamStatus.Active:
        return 'In squadra'
      case PlayerInTeamStatus.SoldForOneHalf:
        return 'Scartato a gennaio (infortunato)'
      case PlayerInTeamStatus.SoldWithNoReturnedPrice:
        return 'Scartato'
      case PlayerInTeamStatus.Sold:
        return 'Venduto'
      case PlayerInTeamStatus.Removed:
        return 'Rimosso dall\'amministratore'
      default:
        return 'Stato sconosciuto'
    }
  }

  const getPlayerStatusColor = (status: PlayerInTeamStatus): string => {
    switch (status) {
      case PlayerInTeamStatus.Active:
        return '$green10'
      case PlayerInTeamStatus.SoldForOneHalf:
        return '$orange10'
      case PlayerInTeamStatus.SoldWithNoReturnedPrice:
        return '$red10'
      case PlayerInTeamStatus.Sold:
        return '$blue10'
      case PlayerInTeamStatus.Removed:
        return '$gray10'
      default:
        return '$gray10'
    }
  }

  const isPlayerActive = (status: PlayerInTeamStatus): boolean => {
    return status === PlayerInTeamStatus.Active
  }

  // Prepare league options
  const leagueOptions: LeagueOption[] = (currentGroup?.leagues ?? []).map(league => ({
    id: league.id,
    name: league.name,
    isMain: league.isMain
  }))

  // Prepare year options from group baskets
  const availableYears = currentGroup?.baskets
    ?.flatMap(basket => basket.years?.map(y => y.year) ?? [])
    .filter((year, index, self) => self.indexOf(year) === index)
    .sort((a, b) => b - a) ?? []

  const shouldRenderSelectors = leagueOptions.length > 0 && availableYears.length > 0

  return (
    <View style={{ flex: 1 }}>
      <ScreenContent path="screens/teams.tsx" title="Squadre">
        <YStack gap="$4" width="100%" maxWidth={1200} alignSelf="center" paddingHorizontal="$4">
          {/* Header */}
          <YStack gap="$2" alignItems="center">
            <H3 color="$blue10" fontWeight="700" textAlign="center">
              <Users size={28} /> Squadre della Lega
            </H3>
            <Text color="$color11" textAlign="center" fontSize="$3">
              Tutte le squadre partecipanti e il loro valore
            </Text>
          </YStack>

          <Separator />

          {/* League Selectors */}
          {shouldRenderSelectors && (
            <LeagueSelectorsWidget
              leagues={leagueOptions}
              selectedLeagueId={localLeagueId}
              onSelectLeague={(leagueId) => {
                setLocalLeagueId(leagueId)
              }}
              years={availableYears}
              selectedYear={localYear}
              onSelectYear={(year) => {
                setLocalYear(year)
              }}
              formatYearLabel={formatSeasonFromYear}
              emptyLeagueLabel={currentGroup?.leagues?.find(l => l.id === selectedLeagueId)?.name ?? 'Nessuna lega'}
              showRoundSection={false}
            />
          )}

          {/* Loading State */}
          {isLoading && (
            <YStack alignItems="center" padding="$6" gap="$3">
              <Spinner size="large" color="$blue10" />
              <Text color="$color11">Caricamento squadre...</Text>
            </YStack>
          )}

          {/* Error State */}
          {error && !isLoading && (
            <Card backgroundColor="$red2" padding="$4" borderColor="$red8" borderWidth={1}>
              <Text color="$red11" textAlign="center">
                ❌ {error}
              </Text>
            </Card>
          )}

          {/* Teams List */}
          {!isLoading && !error && teams.length > 0 && (
            <YStack gap="$3">
              {teams.map((team, index) => {
                const isExpanded = expandedTeamIndex === index

                return (
                  <Card
                    key={`${team.owner}-${index}`}
                    padding="$4"
                    backgroundColor="$color2"
                    borderWidth={1}
                    borderColor={isExpanded ? '$blue8' : '$borderColor'}
                  >
                    {/* Team Header - Clickable */}
                    <YStack
                      onPress={() => toggleTeamExpansion(index)}
                      pressStyle={{ opacity: 0.7 }}
                    >
                      <XStack
                        gap="$4"
                        alignItems="center"
                        flexWrap="wrap"
                      >
                        {/* Team Logo */}
                        <TeamLogo
                          groupId={currentGroup?.id || ''}
                          year={String(localYear || '')}
                          ownerEmail={team.owner}
                          size={60}
                        />

                        {/* Team Info */}
                        <YStack flex={1} gap="$2" minWidth={200}>
                          <Text fontSize="$6" fontWeight="700" color="$color12">
                            {team.name}
                          </Text>
                          <Text fontSize="$3" color="$color11">
                            👤 {team.owner}
                          </Text>
                          {team.additionalOwners && team.additionalOwners.length > 0 && (
                            <Text fontSize="$2" color="$color10">
                              Co-proprietari: {team.additionalOwners.join(', ')}
                            </Text>
                          )}
                        </YStack>

                        {/* Team Stats */}
                        <YStack gap="$2" minWidth={180}>
                          <XStack gap="$2" alignItems="center">
                            <DollarSign size={18} color="$green10" />
                            <Text fontSize="$4" fontWeight="600" color="$color12">
                              Valore: {formatCurrency(getTotalCost(team))}
                            </Text>
                          </XStack>
                          <XStack gap="$2" alignItems="center">
                            <TrendingUp size={16} color="$blue10" />
                            <Text fontSize="$3" color="$color11">
                              Budget: {formatCurrency(getBudget(team))}
                            </Text>
                          </XStack>
                          <XStack gap="$2" alignItems="center">
                            <Users size={16} color="$purple10" />
                            <Text fontSize="$3" color="$color11">
                              {team.activePlayers.length}/{team.players.length} giocatori
                            </Text>
                          </XStack>
                          {team.revenueMoney > 0 && (
                            <XStack gap="$2" alignItems="center">
                              <TrendingUp size={16} color="$orange10" />
                              <Text fontSize="$3" color="$color11">
                                Ricavi: {formatCurrency(team.revenueMoney)}
                              </Text>
                            </XStack>
                          )}
                          {team.moneyFromRank > 0 && (
                            <XStack gap="$2" alignItems="center">
                              <Award size={16} color="$yellow10" />
                              <Text fontSize="$3" color="$color11">
                                Premi: {formatCurrency(team.moneyFromRank)}
                              </Text>
                            </XStack>
                          )}
                        </YStack>

                        {/* Expand Icon */}
                        <YStack justifyContent="center" alignItems="center" minWidth={40}>
                          {isExpanded ? (
                            <ChevronUp size={24} color="$blue10" />
                          ) : (
                            <ChevronDown size={24} color="$color11" />
                          )}
                        </YStack>
                      </XStack>
                    </YStack>

                    {/* Players List - Expanded */}
                    {isExpanded && (
                      <>
                        <Separator marginVertical="$3" />
                        <YStack gap="$4">
                          <XStack justifyContent="space-between" alignItems="center">
                            <Text fontSize="$5" fontWeight="600" color="$color12">
                              Rosa completa
                            </Text>
                            <XStack gap="$4">
                              <Text fontSize="$3" color="$color11">
                                💰 Costo netto: {formatCurrency(team.netCost)}
                              </Text>
                              <Text fontSize="$3" color="$color11">
                                📊 Valore: {formatCurrency(team.totalCost)}
                              </Text>
                              <Text fontSize="$3" color="$color11">
                                👥 {team.activePlayers.length} attivi
                              </Text>
                            </XStack>
                          </XStack>

                          {/* Giocatori raggruppati per ruolo usando i dati precalcolati */}
                          {[Role.GoalKeeper, Role.Defensor, Role.Midfielder, Role.Forward].map(role => {
                            const playersByRole = groupPlayersByRole(team.players)
                            const players = playersByRole[role] || []
                            if (players.length === 0) return null

                            const activePlayers = players.filter((p: Player) => isPlayerActive(p.status))

                            return (
                              <YStack key={role} gap="$2">
                                <Text fontSize="$4" fontWeight="600" color={getRoleColor(role)}>
                                  {getRoleLabel(role)} ({activePlayers.length}/{players.length})
                                </Text>
                                <YStack gap="$2" paddingLeft="$3">
                                  {players.map((player: Player, pIndex: number) => {
                                    const isActive = isPlayerActive(player.status)
                                    return (
                                      <XStack
                                        key={`${player.name}-${pIndex}`}
                                        gap="$3"
                                        alignItems="center"
                                        padding="$2"
                                        backgroundColor="$color1"
                                        borderRadius="$2"
                                        opacity={isActive ? 1 : 0.5}
                                      >
                                        <User size={16} color="$color11" />
                                        <YStack flex={1} gap="$1">
                                          <Text fontSize="$3" color="$color12">
                                            {player.name}
                                          </Text>
                                          {!isActive && (
                                            <Text fontSize="$2" color={getPlayerStatusColor(player.status)} fontStyle="italic">
                                              {getPlayerStatusLabel(player.status)}
                                            </Text>
                                          )}
                                        </YStack>
                                        <Text fontSize="$3" color="$color11">
                                          {player.team.abbreviation}
                                        </Text>
                                        <Text fontSize="$3" fontWeight="600" color="$green10" minWidth={70} textAlign="right">
                                          {formatCurrency(player.price || 0)}
                                        </Text>
                                      </XStack>
                                    )
                                  })}
                                </YStack>
                              </YStack>
                            )
                          })}
                        </YStack>
                      </>
                    )}
                  </Card>
                )
              })}
            </YStack>
          )}

          {/* Empty State */}
          {!isLoading && !error && teams.length === 0 && (
            <Card backgroundColor="$color3" padding="$6" alignItems="center">
              <Users size={48} color="$color10" />
              <Text fontSize="$5" color="$color11" marginTop="$3" textAlign="center">
                Nessuna squadra trovata
              </Text>
              <Text fontSize="$3" color="$color10" marginTop="$2" textAlign="center">
                Seleziona una lega valida per vedere le squadre partecipanti
              </Text>
            </Card>
          )}

          {/* Info Card */}
          {!isLoading && currentGroup && localLeagueId && (
            <Card backgroundColor="$blue2" padding="$3" marginTop="$4">
              <Text fontSize="$2" color="$blue11" textAlign="center">
                💡 Gruppo: {currentGroup.name || 'Sconosciuto'} • Lega: {currentGroup.leagues?.find(l => l.id === localLeagueId)?.name || 'N/A'} • Anno: {localYear || 'N/A'}
              </Text>
            </Card>
          )}
        </YStack>
      </ScreenContent>
    </View>
  )
}
