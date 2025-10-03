import React, { useEffect, useState } from 'react'
import { View } from 'react-native'
import { ScreenContent } from 'components/layout/ScreenContent'
import { YStack, XStack, Text, H3, Separator, Card, Spinner, Button } from 'tamagui'
import { Users, DollarSign, TrendingUp, Award, ChevronDown, ChevronUp, User } from '@tamagui/lucide-icons'
import { useApp } from 'providers/AppProvider'
import { teamService } from 'services/teamService'
import type { Team, Player } from 'types/team'
import { TeamLogo } from 'components/team/TeamLogo'
import { Role } from 'types/group'

export default function TeamsScreen() {
  const { currentGroup, selectedLeagueId, selectedYear } = useApp()
  const [teams, setTeams] = useState<Team[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [expandedTeamIndex, setExpandedTeamIndex] = useState<number | null>(null)

  useEffect(() => {
    loadTeams()
  }, [currentGroup, selectedLeagueId, selectedYear])

  const loadTeams = async () => {
    if (!currentGroup || !selectedLeagueId || !selectedYear) {
      setError('Configurazione lega non completa')
      setIsLoading(false)
      return
    }

    setIsLoading(true)
    setError(null)

    try {
      // Trova la lega selezionata
      const league = currentGroup.leagues?.find(l => l.id === selectedLeagueId)
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
      const yearlyBasket = basket.years?.find(y => y.year === selectedYear)
      if (!yearlyBasket || !yearlyBasket.teams || yearlyBasket.teams.length === 0) {
        setError('Nessuna squadra trovata per questo anno')
        setTeams([])
        setIsLoading(false)
        return
      }

      // Estrai gli owner (email) dai team
      const owners = yearlyBasket.teams.map(t => t.owner)

      // Recupera tutti i team usando il teamService
      const fetchedTeams = await teamService.getTeamsByBasket(
        currentGroup.id,
        selectedYear,
        basketId,
        owners
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

  const getTotalCost = (team: Team) => {
    return team.players.reduce((sum, player) => sum + (player.price || 0), 0)
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
      
      // Debug: log per vedere i ruoli dei giocatori
      if (role === Role.GoalKeeper) {
        console.log('Portiere trovato:', player.name, 'role:', role)
      }
      
      if (!groups[role]) {
        groups[role] = []
      }
      groups[role].push(player)
    })

    // Debug: log per vedere quanti portieri abbiamo
    console.log('Portieri totali:', groups[Role.GoalKeeper].length)

    // Ordina i giocatori per prezzo decrescente in ogni ruolo
    Object.keys(groups).forEach(roleKey => {
      const role = parseInt(roleKey) as Role
      groups[role].sort((a, b) => (b.price || 0) - (a.price || 0))
    })

    return groups
  }

  const toggleTeamExpansion = (index: number) => {
    setExpandedTeamIndex(expandedTeamIndex === index ? null : index)
  }

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
                const playersByRole = groupPlayersByRole(team.players)

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
                          year={String(selectedYear || '')}
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
                        <YStack gap="$2" minWidth={150}>
                          <XStack gap="$2" alignItems="center">
                            <DollarSign size={18} color="$green10" />
                            <Text fontSize="$4" fontWeight="600" color="$color12">
                              {formatCurrency(getTotalCost(team))}
                            </Text>
                          </XStack>
                          <XStack gap="$2" alignItems="center">
                            <Users size={16} color="$blue10" />
                            <Text fontSize="$3" color="$color11">
                              {team.players.length} giocatori
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
                          <Text fontSize="$5" fontWeight="600" color="$color12">
                            Rosa completa
                          </Text>

                          {/* Giocatori raggruppati per ruolo */}
                          {[Role.GoalKeeper, Role.Defensor, Role.Midfielder, Role.Forward].map(role => {
                            const players = playersByRole[role]
                            if (!players || players.length === 0) return null

                            return (
                              <YStack key={role} gap="$2">
                                <Text fontSize="$4" fontWeight="600" color={getRoleColor(role)}>
                                  {getRoleLabel(role)} ({players.length})
                                </Text>
                                <YStack gap="$2" paddingLeft="$3">
                                  {players.map((player, pIndex) => (
                                    <XStack
                                      key={`${player.name}-${pIndex}`}
                                      gap="$3"
                                      alignItems="center"
                                      padding="$2"
                                      backgroundColor="$color1"
                                      borderRadius="$2"
                                    >
                                      <User size={16} color="$color11" />
                                      <Text flex={1} fontSize="$3" color="$color12">
                                        {player.name}
                                      </Text>
                                      <Text fontSize="$3" color="$color11">
                                        {player.team.abbreviation}
                                      </Text>
                                      <Text fontSize="$3" fontWeight="600" color="$green10" minWidth={70} textAlign="right">
                                        {formatCurrency(player.price || 0)}
                                      </Text>
                                    </XStack>
                                  ))}
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
          {!isLoading && currentGroup && selectedLeagueId && (
            <Card backgroundColor="$blue2" padding="$3" marginTop="$4">
              <Text fontSize="$2" color="$blue11" textAlign="center">
                💡 Gruppo: {currentGroup.name || 'Sconosciuto'} • Lega: {currentGroup.leagues?.find(l => l.id === selectedLeagueId)?.name || 'N/A'} • Anno: {selectedYear || 'N/A'}
              </Text>
            </Card>
          )}
        </YStack>
      </ScreenContent>
    </View>
  )
}
