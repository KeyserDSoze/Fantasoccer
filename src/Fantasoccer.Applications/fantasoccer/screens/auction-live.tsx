// Auction Live Screen - Real-time auction management and participation
import { useState, useEffect, useCallback } from 'react'
import { View, ScrollView, Platform } from 'react-native'
import { Button, Card, Input, Select, Separator, Text, XStack, YStack, useMedia, Spinner } from 'tamagui'
import { ScreenContent } from 'components/layout/ScreenContent'
import { auctionService } from 'services/auctionService'
import { 
  AuctionState, 
  AuctionStatus, 
  AuctionTeam,
  PlayerAssignedData,
  PlayerWithStats,
  StatPlayer,
  statPlayerToPlayerWithStats
} from 'types/auction'
import { Role } from 'types/group'
import { Gavel, Users, TrendingUp, Pause, Play, XCircle, ChevronDown } from '@tamagui/lucide-icons'
import { useRoute, RouteProp } from '@react-navigation/native'
import { RootStackParamList } from 'navigation'
import { useAuth } from 'providers/AuthProvider'

type AuctionLiveRouteProp = RouteProp<RootStackParamList, 'AuctionLive'>

export default function AuctionLiveScreen() {
  const route = useRoute<AuctionLiveRouteProp>()
  const { auctionId } = route.params
  const media = useMedia()
  const containerWidth = media.gtMd ? 960 : '100%'
  const { user } = useAuth()

  // Connection state
  const [connected, setConnected] = useState(false)
  const [connectionError, setConnectionError] = useState<string | null>(null)

  // Auction state
  const [auctionState, setAuctionState] = useState<AuctionState | null>(null)
  const [currentPlayer, setCurrentPlayer] = useState<PlayerWithStats | null>(null)
  const [teams, setTeams] = useState<AuctionTeam[]>([])

  // User state
  const [isAuctioneer, setIsAuctioneer] = useState(false)

  // Banditore controls
  const [selectedRole, setSelectedRole] = useState<Role>(Role.GoalKeeper)
  const [selectedTeamOwner, setSelectedTeamOwner] = useState<string>('')
  const [assignPrice, setAssignPrice] = useState<string>('')
  const [loading, setLoading] = useState(false)
  const [actionError, setActionError] = useState<string | null>(null)

  // Connect to SignalR on mount
  useEffect(() => {
    // Wait for user to be loaded
    if (!user?.email) {
      console.log('⏳ Waiting for user to be loaded...')
      return
    }

    let isSubscribed = true

    const connectAndJoin = async () => {
      try {
        console.log('👤 User loaded:', user.email)
        
        // Set user email in service for mapping
        auctionService.setUserEmail(user.email)

        // Set event handlers
        auctionService.setEventHandlers({
          onConnected: () => {
            console.log('Connected to auction hub')
            if (isSubscribed) setConnected(true)
          },
          onError: (error) => {
            console.error('SignalR error:', error)
            if (isSubscribed) setConnectionError(error || 'Errore di connessione')
          },
          onUserJoined: (email) => {
            console.log('User joined:', email)
          },
          onUserLeft: (email) => {
            console.log('User left:', email)
          },
          onPlayerShown: (player: StatPlayer) => {
            console.log('Player shown:', player)
            if (isSubscribed) {
              setCurrentPlayer(statPlayerToPlayerWithStats(player))
            }
          },
          onPlayerAssigned: (data: PlayerAssignedData) => {
            console.log('Player assigned:', data)
            if (isSubscribed) {
              setCurrentPlayer(null)
              setAssignPrice('')
              setSelectedTeamOwner('')
            }
          },
          onAuctionState: (state: AuctionState) => {
            console.log('Auction state received (mapped):', state)
            if (isSubscribed) {
              setAuctionState(state)
              setTeams(state.teams)
              setIsAuctioneer(state.isAuctioneer)
            }
          }
        })

        // Connect
        await auctionService.connect()

        // Join auction
        const joined = await auctionService.joinAuction(auctionId)
        
        if (!joined) {
          if (isSubscribed) setConnectionError('Non autorizzato ad unirsi a questa asta')
          return
        }

        // Load initial data
        await auctionService.getAuctionState(auctionId)
        await auctionService.getCurrentPlayer(auctionId)

      } catch (err) {
        console.error('Error connecting to auction:', err)
        if (isSubscribed) setConnectionError(`Errore: ${err instanceof Error ? err.message : 'Unknown error'}`)
      }
    }

    connectAndJoin()

    // Cleanup on unmount
    return () => {
      isSubscribed = false
      auctionService.leaveAuction(auctionId).catch(console.error)
      auctionService.disconnect().catch(console.error)
    }
  }, [auctionId, user?.email])

  // Banditore Actions
  const handleShowNextPlayer = useCallback(async () => {
    setLoading(true)
    setActionError(null)
    try {
      await auctionService.showNextPlayer(auctionId, selectedRole)
    } catch (err) {
      setActionError(`Errore: ${err instanceof Error ? err.message : 'Unknown error'}`)
    } finally {
      setLoading(false)
    }
  }, [auctionId, selectedRole])

  const handleAssignPlayer = useCallback(async () => {
    if (!selectedTeamOwner || !assignPrice) {
      setActionError('Seleziona una squadra e inserisci il prezzo')
      return
    }

    const price = parseInt(assignPrice, 10)
    if (isNaN(price) || price <= 0) {
      setActionError('Inserisci un prezzo valido')
      return
    }

    setLoading(true)
    setActionError(null)
    try {
      await auctionService.assignPlayerToTeam(auctionId, selectedTeamOwner, price)
      setSelectedTeamOwner('')
      setAssignPrice('')
    } catch (err) {
      setActionError(`Errore: ${err instanceof Error ? err.message : 'Unknown error'}`)
    } finally {
      setLoading(false)
    }
  }, [auctionId, selectedTeamOwner, assignPrice])

  const handlePauseAuction = useCallback(async () => {
    setLoading(true)
    setActionError(null)
    try {
      await auctionService.pauseAuction(auctionId)
    } catch (err) {
      setActionError(`Errore: ${err instanceof Error ? err.message : 'Unknown error'}`)
    } finally {
      setLoading(false)
    }
  }, [auctionId])

  const handleResumeAuction = useCallback(async () => {
    setLoading(true)
    setActionError(null)
    try {
      await auctionService.resumeAuction(auctionId)
    } catch (err) {
      setActionError(`Errore: ${err instanceof Error ? err.message : 'Unknown error'}`)
    } finally {
      setLoading(false)
    }
  }, [auctionId])

  const handleCloseAuction = useCallback(async () => {
    if (!confirm('Sei sicuro di voler chiudere l\'asta?')) return

    setLoading(true)
    setActionError(null)
    try {
      await auctionService.closeAuction(auctionId)
    } catch (err) {
      setActionError(`Errore: ${err instanceof Error ? err.message : 'Unknown error'}`)
    } finally {
      setLoading(false)
    }
  }, [auctionId])

  // Render loading state
  if (!connected) {
    return (
      <View style={{ flex: 1 }}>
        <ScreenContent path="screens/auction-live.tsx" title="Asta Live">
          <YStack padding="$4" gap="$4" alignItems="center" justifyContent="center" minHeight={400}>
            {connectionError ? (
              <YStack gap="$3" alignItems="center" maxWidth={600}>
                <Card padding="$4" backgroundColor="$red2" borderColor="$red8" borderWidth="$0.5">
                  <YStack gap="$2">
                    <Text color="$red11" fontSize="$5" fontWeight="700">Errore di Connessione</Text>
                    <Text color="$red11" fontSize="$4">{connectionError}</Text>
                    <Text color="$red10" fontSize="$3" marginTop="$2">
                      Possibili cause:
                    </Text>
                    <Text color="$red10" fontSize="$3">
                      • Il server non è raggiungibile
                    </Text>
                    <Text color="$red10" fontSize="$3">
                      • Problemi di autenticazione
                    </Text>
                    <Text color="$red10" fontSize="$3">
                      • Timeout di connessione
                    </Text>
                  </YStack>
                </Card>
                <Button
                  onPress={() => {
                    setConnectionError(null)
                    setConnected(false)
                    // Force re-run of useEffect by changing a dependency
                    window.location.reload()
                  }}
                  theme="blue"
                >
                  Riprova Connessione
                </Button>
              </YStack>
            ) : (
              <>
                <Spinner size="large" color="$blue10" />
                <Text fontSize="$4" color="$color11">Connessione all'asta...</Text>
              </>
            )}
          </YStack>
        </ScreenContent>
      </View>
    )
  }

  const getRoleLabel = (role: Role | string): string => {
    if (typeof role === 'string') {
      // Already a string from API
      return role
    }
    const labels: Record<Role, string> = {
      [Role.Undefined]: 'Indefinito',
      [Role.GoalKeeper]: 'Portiere',
      [Role.Defensor]: 'Difensore',
      [Role.Midfielder]: 'Centrocampista',
      [Role.Forward]: 'Attaccante'
    }
    return labels[role] || 'Sconosciuto'
  }

  const getStatusLabel = (status: AuctionStatus | string): string => {
    if (typeof status === 'string') {
      // Parse from string
      const num = parseInt(status, 10)
      if (!isNaN(num)) {
        status = num as AuctionStatus
      } else {
        return status
      }
    }
    const labels: Record<AuctionStatus, string> = {
      [AuctionStatus.NotStarted]: 'Non Iniziata',
      [AuctionStatus.Paused]: 'In Pausa',
      [AuctionStatus.InProgress]: 'In Corso',
      [AuctionStatus.Finished]: 'Terminata'
    }
    return labels[status] || 'Sconosciuto'
  }

  const getStatusColor = (status: AuctionStatus | string): string => {
    if (typeof status === 'string') {
      const num = parseInt(status, 10)
      if (!isNaN(num)) {
        status = num as AuctionStatus
      } else {
        return '$gray10'
      }
    }
    const colors: Record<AuctionStatus, string> = {
      [AuctionStatus.NotStarted]: '$gray10',
      [AuctionStatus.Paused]: '$orange10',
      [AuctionStatus.InProgress]: '$green10',
      [AuctionStatus.Finished]: '$blue10'
    }
    return colors[status] || '$gray10'
  }

  const parseStatus = (status: string | AuctionStatus): AuctionStatus => {
    if (typeof status === 'number') return status
    const num = parseInt(status, 10)
    return isNaN(num) ? AuctionStatus.NotStarted : (num as AuctionStatus)
  }

  return (
    <View style={{ flex: 1 }}>
      <ScreenContent path="screens/auction-live.tsx" title="Asta Live">
        <ScrollView>
          <YStack padding="$4" gap="$6" alignItems="center">
            <YStack width={containerWidth} gap="$4">
              
              {/* Header - Auction Status */}
              {auctionState && (
                <Card padding="$4" backgroundColor="$blue2" borderRadius="$4" borderWidth="$0.5" borderColor="$blue8">
                  <XStack gap="$3" alignItems="center" justifyContent="space-between" flexWrap="wrap">
                    <XStack gap="$2" alignItems="center">
                      <Gavel size={24} color="$blue11" />
                      <Text fontSize="$5" fontWeight="700" color="$blue11">
                        Asta {isAuctioneer ? '(Banditore)' : ''}
                      </Text>
                    </XStack>
                    <XStack gap="$2" alignItems="center">
                      <Text fontSize="$4" fontWeight="600" color={getStatusColor(auctionState.wrapper.status)}>
                        {getStatusLabel(auctionState.wrapper.status)}
                      </Text>
                    </XStack>
                  </XStack>
                </Card>
              )}

              {/* Action Error */}
              {actionError && (
                <Card padding="$3" backgroundColor="$red2" borderColor="$red8" borderWidth="$0.5">
                  <Text color="$red11" fontSize="$3">{actionError}</Text>
                </Card>
              )}

              {/* Current Player Display */}
              {currentPlayer && (
                <Card padding="$4" backgroundColor="$green2" borderRadius="$4" borderWidth="$0.5" borderColor="$green8">
                  <YStack gap="$3">
                    <XStack gap="$2" alignItems="center">
                      <Users size={20} color="$green11" />
                      <Text fontSize="$5" fontWeight="700" color="$green11">
                        Giocatore Attuale
                      </Text>
                    </XStack>
                    
                    <YStack gap="$2">
                      <Text fontSize="$6" fontWeight="700" color="$green11">
                        {currentPlayer.name}
                      </Text>
                      <XStack gap="$4" flexWrap="wrap">
                        <Text fontSize="$4" color="$green10">
                          {getRoleLabel(currentPlayer.role)}
                        </Text>
                        <Text fontSize="$4" color="$green10">
                          {currentPlayer.realTeam}
                        </Text>
                      </XStack>
                    </YStack>

                    {currentPlayer.stats && (
                      <Card padding="$3" backgroundColor="$color1" borderRadius="$3">
                        <XStack gap="$4" flexWrap="wrap">
                          <YStack gap="$1">
                            <Text fontSize="$2" color="$color10">Goal</Text>
                            <Text fontSize="$4" fontWeight="600" color="$color11">{currentPlayer.stats.goal}</Text>
                          </YStack>
                          <YStack gap="$1">
                            <Text fontSize="$2" color="$color10">Assist</Text>
                            <Text fontSize="$4" fontWeight="600" color="$color11">{currentPlayer.stats.assist}</Text>
                          </YStack>
                          <YStack gap="$1">
                            <Text fontSize="$2" color="$color10">Media Voto</Text>
                            <Text fontSize="$4" fontWeight="600" color="$color11">{currentPlayer.stats.average?.toFixed(2) || 'N/A'}</Text>
                          </YStack>
                          <YStack gap="$1">
                            <Text fontSize="$2" color="$color10">Media Fanta</Text>
                            <Text fontSize="$4" fontWeight="600" color="$color11">{currentPlayer.stats.fantaAverage?.toFixed(2) || 'N/A'}</Text>
                          </YStack>
                        </XStack>
                      </Card>
                    )}
                  </YStack>
                </Card>
              )}

              {/* Banditore Controls */}
              {isAuctioneer && auctionState && parseStatus(auctionState.wrapper.status) !== AuctionStatus.Finished && (
                <Card padding="$4" backgroundColor="$orange2" borderRadius="$4" borderWidth="$0.5" borderColor="$orange8">
                  <YStack gap="$4">
                    <XStack gap="$2" alignItems="center">
                      <Gavel size={20} color="$orange11" />
                      <Text fontSize="$5" fontWeight="700" color="$orange11">
                        Controlli Banditore
                      </Text>
                    </XStack>
                    <Text fontSize="$3" color="$orange10">
                      💡 Come banditore, puoi gestire l'asta e allo stesso tempo partecipare con la tua squadra.
                    </Text>

                    {/* Show Next Player */}
                    <YStack gap="$2">
                      <Text fontSize="$4" fontWeight="600" color="$orange11">
                        Mostra Prossimo Giocatore
                      </Text>
                      <XStack gap="$2" flexWrap="wrap">
                        <Select value={selectedRole.toString()} onValueChange={(val) => setSelectedRole(Number(val) as Role)}>
                          <Select.Trigger iconAfter={ChevronDown} flex={1} minWidth={200}>
                            <Select.Value placeholder="Seleziona Ruolo" />
                          </Select.Trigger>
                          <Select.Adapt when="sm" platform="touch">
                            <Select.Sheet modal dismissOnSnapToBottom>
                              <Select.Sheet.Frame>
                                <Select.Sheet.ScrollView>
                                  <Select.Adapt.Contents />
                                </Select.Sheet.ScrollView>
                              </Select.Sheet.Frame>
                              <Select.Sheet.Overlay />
                            </Select.Sheet>
                          </Select.Adapt>
                          <Select.Content zIndex={200000}>
                            <Select.ScrollUpButton />
                            <Select.Viewport>
                              {Object.values(Role).filter(v => typeof v === 'number').map((role, idx) => (
                                <Select.Item key={role} index={idx} value={role.toString()}>
                                  <Select.ItemText>{getRoleLabel(role as Role)}</Select.ItemText>
                                </Select.Item>
                              ))}
                            </Select.Viewport>
                            <Select.ScrollDownButton />
                          </Select.Content>
                        </Select>
                        <Button
                          icon={TrendingUp}
                          onPress={handleShowNextPlayer}
                          disabled={loading || parseStatus(auctionState.wrapper.status) === AuctionStatus.Paused}
                          theme="orange"
                          flex={1}
                        >
                          Mostra Prossimo
                        </Button>
                      </XStack>
                    </YStack>

                    <Separator />

                    {/* Assign Player */}
                    {currentPlayer && (
                      <YStack gap="$2">
                        <Text fontSize="$4" fontWeight="600" color="$orange11">
                          Assegna Giocatore
                        </Text>
                        <Select value={selectedTeamOwner} onValueChange={setSelectedTeamOwner}>
                          <Select.Trigger iconAfter={ChevronDown}>
                            <Select.Value placeholder="Seleziona Squadra" />
                          </Select.Trigger>
                          <Select.Adapt when="sm" platform="touch">
                            <Select.Sheet modal dismissOnSnapToBottom>
                              <Select.Sheet.Frame>
                                <Select.Sheet.ScrollView>
                                  <Select.Adapt.Contents />
                                </Select.Sheet.ScrollView>
                              </Select.Sheet.Frame>
                              <Select.Sheet.Overlay />
                            </Select.Sheet>
                          </Select.Adapt>
                          <Select.Content zIndex={200000}>
                            <Select.ScrollUpButton />
                            <Select.Viewport>
                              {teams.map((team, idx) => (
                                <Select.Item key={team.owner} index={idx} value={team.owner}>
                                  <Select.ItemText>{team.name} ({team.owner})</Select.ItemText>
                                </Select.Item>
                              ))}
                            </Select.Viewport>
                            <Select.ScrollDownButton />
                          </Select.Content>
                        </Select>
                        <Input
                          placeholder="Prezzo"
                          value={assignPrice}
                          onChangeText={setAssignPrice}
                          keyboardType="numeric"
                        />
                        <Button
                          icon={Gavel}
                          onPress={handleAssignPlayer}
                          disabled={loading || !selectedTeamOwner || !assignPrice || parseStatus(auctionState.wrapper.status) === AuctionStatus.Paused}
                          theme="green"
                        >
                          Assegna
                        </Button>
                      </YStack>
                    )}

                    <Separator />

                    {/* Auction Controls */}
                    <YStack gap="$2">
                      <Text fontSize="$4" fontWeight="600" color="$orange11">
                        Controlli Asta
                      </Text>
                      <XStack gap="$2" flexWrap="wrap">
                        {parseStatus(auctionState.wrapper.status) === AuctionStatus.InProgress ? (
                          <Button
                            icon={Pause}
                            onPress={handlePauseAuction}
                            disabled={loading}
                            theme="orange"
                            flex={1}
                          >
                            Pausa
                          </Button>
                        ) : parseStatus(auctionState.wrapper.status) === AuctionStatus.Paused ? (
                          <Button
                            icon={Play}
                            onPress={handleResumeAuction}
                            disabled={loading}
                            theme="green"
                            flex={1}
                          >
                            Riprendi
                          </Button>
                        ) : null}
                        <Button
                          icon={XCircle}
                          onPress={handleCloseAuction}
                          disabled={loading}
                          theme="red"
                          flex={1}
                        >
                          Chiudi Asta
                        </Button>
                      </XStack>
                    </YStack>
                  </YStack>
                </Card>
              )}

              {/* Teams List */}
              {teams.length > 0 && (
                <Card padding="$4" backgroundColor="$color2" borderRadius="$4" borderWidth="$0.5" borderColor="$borderColor">
                  <YStack gap="$4">
                    <XStack gap="$2" alignItems="center">
                      <Users size={20} color="$color11" />
                      <Text fontSize="$5" fontWeight="700" color="$color11">
                        Squadre ({teams.length})
                      </Text>
                    </XStack>

                    {teams.map((team) => {
                      const isMyTeam = user?.email && team?.owner?.toLowerCase() === user.email.toLowerCase()
                      const hasLowBudget = team.remainingBudget < 50
                      const hasLowSlots = team.remainingSlots < 5
                      
                      return (
                        <Card 
                          key={team.owner} 
                          padding="$3" 
                          backgroundColor={isMyTeam ? '$blue2' : '$color1'} 
                          borderRadius="$3"
                          borderWidth="$0.5"
                          borderColor={isMyTeam ? '$blue8' : '$borderColor'}
                        >
                          <YStack gap="$2">
                            <XStack gap="$2" justifyContent="space-between" alignItems="center" flexWrap="wrap">
                              <Text fontSize="$4" fontWeight="600" color="$color11">
                                {team.name}
                              </Text>
                              {isMyTeam && (
                                <Card padding="$2" paddingHorizontal="$3" backgroundColor="$blue8" borderRadius="$2">
                                  <Text fontSize="$2" color="$blue1" fontWeight="700">LA TUA SQUADRA</Text>
                                </Card>
                              )}
                            </XStack>
                            <Text fontSize="$3" color="$color10">{team.owner}</Text>
                            
                            <XStack gap="$4" flexWrap="wrap">
                              <YStack gap="$1">
                                <Text fontSize="$2" color="$color10">Speso</Text>
                                <Text fontSize="$3" fontWeight="600" color="$color11">{team.cost} €</Text>
                              </YStack>
                              <YStack gap="$1">
                                <Text fontSize="$2" color="$color10">Rimasto</Text>
                                <Text 
                                  fontSize="$3" 
                                  fontWeight="600" 
                                  color={hasLowBudget ? '$red11' : '$color11'}
                                >
                                  {team.remainingBudget} €
                                </Text>
                              </YStack>
                              <YStack gap="$1">
                                <Text fontSize="$2" color="$color10">Giocatori</Text>
                                <Text fontSize="$3" fontWeight="600" color="$color11">{team.playersCount}/25</Text>
                              </YStack>
                              <YStack gap="$1">
                                <Text fontSize="$2" color="$color10">Slot Rimasti</Text>
                                <Text 
                                  fontSize="$3" 
                                  fontWeight="600" 
                                  color={hasLowSlots ? '$orange11' : '$color11'}
                                >
                                  {team.remainingSlots}
                                </Text>
                              </YStack>
                            </XStack>

                            {team.players && team.players.length > 0 && (
                              <YStack gap="$1" marginTop="$2">
                                <Text fontSize="$3" fontWeight="600" color="$color10">Rosa:</Text>
                                <YStack gap="$0.5" maxHeight={200} overflow="scroll">
                                  {team.players.map((player) => (
                                    <XStack key={player.name} gap="$2" justifyContent="space-between">
                                      <Text fontSize="$2" color="$color11">
                                        {player.name} ({getRoleLabel(player.role)}) - {player.realTeam}
                                      </Text>
                                      <Text fontSize="$2" color="$color10" fontWeight="600">{player.price} €</Text>
                                    </XStack>
                                  ))}
                                </YStack>
                              </YStack>
                            )}

                            {/* Budget Warning for user's team */}
                            {isMyTeam && hasLowBudget && team.remainingSlots > 0 && (
                              <Card padding="$2" backgroundColor="$red2" borderRadius="$2" borderWidth="$0.5" borderColor="$red8" marginTop="$2">
                                <Text fontSize="$2" color="$red11">
                                  ⚠️ Attenzione: budget basso! Assicurati di avere almeno 1€ per ogni slot rimanente.
                                </Text>
                              </Card>
                            )}
                          </YStack>
                        </Card>
                      )
                    })}
                  </YStack>
                </Card>
              )}
            </YStack>
          </YStack>
        </ScrollView>
      </ScreenContent>
    </View>
  )
}
