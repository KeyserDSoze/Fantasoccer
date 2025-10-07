// Auction Live Screen - Real-time auction management with bidding interface (Pulsantiera)
import { useState, useEffect, useCallback, useRef } from 'react'
import { View, ScrollView, Platform, Vibration } from 'react-native'
import { 
  Button, Card, Input, Select, Separator, Text, XStack, YStack, 
  useMedia, Spinner, Sheet, Label, Switch, Dialog
} from 'tamagui'
import { ScreenContent } from 'components/layout/ScreenContent'
import { auctionService } from 'services/auctionService'
import { 
  AuctionState, 
  AuctionStatus, 
  PlayerAssignedData,
  StatPlayer,
  AuctionWin
} from 'types/auction'
import { Role } from 'types/group'
import { 
  Gavel, Users, TrendingUp, Pause, Play, XCircle, ChevronDown, 
  Clock, Lock, Unlock, RotateCcw, Search, DollarSign, 
  AlertCircle, Volume2, VolumeX, Vibrate
} from '@tamagui/lucide-icons'
import { useRoute, RouteProp } from '@react-navigation/native'
import { RootStackParamList } from 'navigation'
import { useAuth } from 'providers/AuthProvider'
import { Team, TeamKey } from 'types/team'
import { Entity } from 'types/default'

type AuctionLiveRouteProp = RouteProp<RootStackParamList, 'AuctionLive'>

// Limiti ruolo per squadra
const ROLE_LIMITS: Record<Role, number> = {
  [Role.Undefined]: 0,
  [Role.GoalKeeper]: 3,
  [Role.Defensor]: 8,
  [Role.Midfielder]: 8,
  [Role.Forward]: 6
}

export default function AuctionLiveScreen() {
  const route = useRoute<AuctionLiveRouteProp>()
  const { auctionId } = route.params
  const media = useMedia()
  const containerWidth = media.gtMd ? 1200 : '100%'
  const { user } = useAuth()

  // ============================================
  // CONNECTION STATE
  // ============================================
  const [connectionState, setConnectionState] = useState<'Disconnected' | 'Connecting' | 'Connected'>('Disconnected')
  const [connectionError, setConnectionError] = useState<string | null>(null)
  const [reconnectAttempts, setReconnectAttempts] = useState(0)

  // ============================================
  // AUCTION STATE
  // ============================================
  const [auctionState, setAuctionState] = useState<AuctionState | null>(null)
  const [currentPlayer, setCurrentPlayer] = useState<StatPlayer | null>(null)
  const [teams, setTeams] = useState<Entity<Team, TeamKey>[]>([])
  const [currentBid, setCurrentBid] = useState<number>(0)
  const [currentBidder, setCurrentBidder] = useState<string | null>(null)
  const [isLocked, setIsLocked] = useState(false)
  const [salesHistory, setSalesHistory] = useState<AuctionWin[]>([])

  // ============================================
  // USER STATE
  // ============================================
  const [isAuctioneer, setIsAuctioneer] = useState(false)
  const [myTeam, setMyTeam] = useState<Entity<Team, TeamKey> | null>(null)
  const [myTeamBudget, setMyTeamBudget] = useState<number>(1000)

  // ============================================
  // TIMER STATE
  // ============================================
  const [timerSecondsLeft, setTimerSecondsLeft] = useState<number>(0)
  const [timerRunning, setTimerRunning] = useState<boolean>(false)
  const [timerExpired, setTimerExpired] = useState<boolean>(false)
  const [firstOfferHappened, setFirstOfferHappened] = useState<boolean>(false)
  const [offerTimeoutSeconds, setOfferTimeoutSeconds] = useState<number>(10)
  const timerIntervalRef = useRef<NodeJS.Timeout | null>(null)

  // ============================================
  // BANDITORE STATE
  // ============================================
  const [selectedRole, setSelectedRole] = useState<Role>(Role.GoalKeeper)
  const [selectedTeamForBid, setSelectedTeamForBid] = useState<string>('')
  const [assignPrice, setAssignPrice] = useState<string>('')
  const [customBidAmount, setCustomBidAmount] = useState<string>('')
  const [showCustomBidDialog, setShowCustomBidDialog] = useState(false)
  const [showPlayerSearchDialog, setShowPlayerSearchDialog] = useState(false)
  const [playerSearchQuery, setPlayerSearchQuery] = useState('')

  // ============================================
  // UI STATE
  // ============================================
  const [loading, setLoading] = useState(false)
  const [actionError, setActionError] = useState<string | null>(null)
  const [tempBlockButtons, setTempBlockButtons] = useState(false)
  const [audioEnabled, setAudioEnabled] = useState(true)
  const [vibrationEnabled, setVibrationEnabled] = useState(true)

  // ============================================
  // AUDIO CONTEXT
  // ============================================
  const audioContextRef = useRef<AudioContext | null>(null)

  // Initialize audio context
  useEffect(() => {
    if (Platform.OS === 'web') {
      audioContextRef.current = new (window.AudioContext || (window as any).webkitAudioContext)()
    }
    return () => {
      audioContextRef.current?.close()
    }
  }, [])

  // ============================================
  // HELPER FUNCTIONS
  // ============================================

  const getRoleLabel = (role: Role): string => {
    const labels: Record<Role, string> = {
      [Role.Undefined]: 'Indefinito',
      [Role.GoalKeeper]: 'Portiere',
      [Role.Defensor]: 'Difensore',
      [Role.Midfielder]: 'Centrocampista',
      [Role.Forward]: 'Attaccante'
    }
    return labels[role] || 'Sconosciuto'
  }

  const getStatusLabel = (status: AuctionStatus): string => {
    const labels: Record<AuctionStatus, string> = {
      [AuctionStatus.NotStarted]: 'Non Iniziata',
      [AuctionStatus.Paused]: 'In Pausa',
      [AuctionStatus.InProgress]: 'In Corso',
      [AuctionStatus.Finished]: 'Terminata'
    }
    return labels[status] || 'Sconosciuto'
  }

  const getStatusColor = (status: AuctionStatus): string => {
    const colors: Record<AuctionStatus, string> = {
      [AuctionStatus.NotStarted]: '$gray10',
      [AuctionStatus.Paused]: '$orange10',
      [AuctionStatus.InProgress]: '$green10',
      [AuctionStatus.Finished]: '$blue10'
    }
    return colors[status] || '$gray10'
  }

  // Play bid sound (double synthesized note)
  const playBidSound = useCallback(() => {
    if (!audioEnabled || Platform.OS !== 'web' || !audioContextRef.current) return

    const audioContext = audioContextRef.current
    const now = audioContext.currentTime

    // First note
    const oscillator1 = audioContext.createOscillator()
    const gainNode1 = audioContext.createGain()
    oscillator1.connect(gainNode1)
    gainNode1.connect(audioContext.destination)
    oscillator1.frequency.value = 800
    gainNode1.gain.setValueAtTime(0.3, now)
    gainNode1.gain.exponentialRampToValueAtTime(0.01, now + 0.1)
    oscillator1.start(now)
    oscillator1.stop(now + 0.1)

    // Second note
    const oscillator2 = audioContext.createOscillator()
    const gainNode2 = audioContext.createGain()
    oscillator2.connect(gainNode2)
    gainNode2.connect(audioContext.destination)
    oscillator2.frequency.value = 1000
    gainNode2.gain.setValueAtTime(0.3, now + 0.1)
    gainNode2.gain.exponentialRampToValueAtTime(0.01, now + 0.2)
    oscillator2.start(now + 0.1)
    oscillator2.stop(now + 0.2)
  }, [audioEnabled])

  // Vibrate on mobile
  const vibrateOnBid = useCallback(() => {
    if (!vibrationEnabled || Platform.OS === 'web') return
    Vibration.vibrate([120, 30, 90])
  }, [vibrationEnabled])

  // Check if user's team has reached role limit
  const hasReachedRoleLimit = useCallback((role: Role): boolean => {
    if (!myTeam || !currentPlayer) return false
    const limit = ROLE_LIMITS[role] || 0
    const currentCount = myTeam.value.players.filter(p => p.role === role).length
    return currentCount >= limit
  }, [myTeam, currentPlayer])

  // Calculate team budget remaining
  const calculateBudgetRemaining = useCallback((team: Entity<Team, TeamKey>): number => {
    const spent = team.value.players.reduce((sum, player) => sum + player.price, 0)
    return 1000 - spent
  }, [])

  // ============================================
  // TIMER LOGIC
  // ============================================

  // Start timer
  const startTimer = useCallback(() => {
    setTimerSecondsLeft(offerTimeoutSeconds)
    setTimerRunning(true)
    setTimerExpired(false)
    setFirstOfferHappened(true)

    if (timerIntervalRef.current) {
      clearInterval(timerIntervalRef.current)
    }

    timerIntervalRef.current = setInterval(() => {
      setTimerSecondsLeft(prev => {
        if (prev <= 1) {
          clearInterval(timerIntervalRef.current!)
          setTimerRunning(false)
          setTimerExpired(true)
          return 0
        }
        return prev - 1
      })
    }, 1000)
  }, [offerTimeoutSeconds])

  // Reset timer
  const resetTimer = useCallback(() => {
    if (timerIntervalRef.current) {
      clearInterval(timerIntervalRef.current)
    }
    setTimerSecondsLeft(offerTimeoutSeconds)
    setTimerRunning(false)
    setTimerExpired(false)
    setFirstOfferHappened(false)
  }, [offerTimeoutSeconds])

  // Cleanup timer on unmount
  useEffect(() => {
    return () => {
      if (timerIntervalRef.current) {
        clearInterval(timerIntervalRef.current)
      }
    }
  }, [])

  // ============================================
  // BID LOGIC
  // ============================================

  /**
   * Place a bid (for participants or banditore)
   */
  const placeBid = useCallback(async (amount: number) => {
    // Validations
    if (!currentPlayer) {
      setActionError('Nessun giocatore in asta')
      return
    }

    if (isLocked && !isAuctioneer) {
      setActionError('L\'asta è bloccata')
      return
    }

    if (amount <= currentBid) {
      setActionError(`L'offerta deve essere superiore a €${currentBid}`)
      return
    }

    // Check if I'm already the highest bidder (for participants)
    if (!isAuctioneer && currentBidder?.toLowerCase() === user?.email?.toLowerCase()) {
      setActionError('Sei già il miglior offerente')
      return
    }

    // For banditore bidding on behalf of a team
    const biddingTeam = isAuctioneer && selectedTeamForBid 
      ? teams.find(t => t.value.owner === selectedTeamForBid)
      : myTeam

    if (!biddingTeam) {
      setActionError('Nessuna squadra selezionata')
      return
    }

    // Check role limits
    if (currentPlayer.role && hasReachedRoleLimit(currentPlayer.role)) {
      setActionError(`Limite ruolo raggiunto per ${getRoleLabel(currentPlayer.role)}`)
      return
    }

    // Check budget
    const teamBudget = calculateBudgetRemaining(biddingTeam)
    const slotsRemaining = 25 - biddingTeam.value.players.length
    const minimumBudgetNeeded = slotsRemaining > 1 ? slotsRemaining - 1 : 0 // Reserve 1€ per slot

    if (amount > teamBudget - minimumBudgetNeeded) {
      setActionError(`Budget insufficiente (disponibile: €${teamBudget - minimumBudgetNeeded})`)
      return
    }

    // Temporary block buttons for 500ms to prevent double clicks
    setTempBlockButtons(true)
    setTimeout(() => setTempBlockButtons(false), 500)

    setLoading(true)
    setActionError(null)

    try {
      // TODO: Call SignalR method to place bid
      // await auctionService.placeBid(auctionId, amount, biddingTeam.value.name, biddingTeam.value.owner)
      
      // Temporary: Update local state
      setCurrentBid(amount)
      setCurrentBidder(biddingTeam.value.owner)
      
      // Play sound and vibrate
      playBidSound()
      vibrateOnBid()

      // Start or restart timer
      if (!firstOfferHappened) {
        startTimer()
      } else {
        // Reset timer to full duration
        startTimer()
      }

      console.log(`✅ Bid placed: €${amount} by ${biddingTeam.value.name}`)
    } catch (err) {
      setActionError(`Errore: ${err instanceof Error ? err.message : 'Unknown error'}`)
    } finally {
      setLoading(false)
    }
  }, [
    currentPlayer, 
    isLocked, 
    isAuctioneer, 
    currentBid, 
    currentBidder, 
    user?.email, 
    selectedTeamForBid, 
    teams, 
    myTeam, 
    hasReachedRoleLimit, 
    calculateBudgetRemaining, 
    playBidSound, 
    vibrateOnBid, 
    firstOfferHappened, 
    startTimer,
    getRoleLabel
  ])

  /**
   * Handle increment bid (add to current bid)
   */
  const handleIncrementBid = useCallback((increment: number) => {
    const newAmount = currentBid + increment
    placeBid(newAmount)
  }, [currentBid, placeBid])

  /**
   * Handle fixed bid (set specific amount)
   */
  const handleFixedBid = useCallback((amount: number) => {
    placeBid(amount)
  }, [placeBid])

  /**
   * Check if bid buttons should be disabled
   */
  const areBidButtonsDisabled = useCallback((): boolean => {
    if (!currentPlayer) return true
    if (loading) return true
    if (tempBlockButtons) return true
    if (isLocked && !isAuctioneer) return true
    if (timerExpired && !isAuctioneer) return true
    if (!isAuctioneer && currentBidder?.toLowerCase() === user?.email?.toLowerCase()) return true
    if (isAuctioneer && !selectedTeamForBid) return true
    return false
  }, [
    currentPlayer, 
    loading, 
    tempBlockButtons, 
    isLocked, 
    isAuctioneer, 
    timerExpired, 
    currentBidder, 
    user?.email, 
    selectedTeamForBid
  ])

  // ============================================
  // SIGNALR CONNECTION & EVENTS
  // ============================================

  useEffect(() => {
    // Wait for user to be loaded
    if (!user?.email) {
      console.log('⏳ Waiting for user to be loaded...')
      return
    }

    let isSubscribed = true
    setConnectionState('Connecting')

    const connectAndJoin = async () => {
      try {
        console.log('👤 User loaded:', user.email)
        
        // Set user email in service for mapping
        auctionService.setUserEmail(user.email)

        // Set event handlers
        auctionService.setEventHandlers({
          onConnected: () => {
            console.log('✅ Connected to auction hub')
            if (isSubscribed) {
              setConnectionState('Connected')
              setReconnectAttempts(0)
            }
          },
          onError: (error) => {
            console.error('❌ SignalR error:', error)
            if (isSubscribed) {
              setConnectionError(error || 'Errore di connessione')
              setConnectionState('Disconnected')
            }
          },
          onUserJoined: (email) => {
            console.log('👋 User joined:', email)
          },
          onUserLeft: (email) => {
            console.log('👋 User left:', email)
          },
          onPlayerShown: (player: StatPlayer) => {
            console.log('🎯 Player shown:', player)
            if (isSubscribed) {
              setCurrentPlayer(player)
              setCurrentBid(0)
              setCurrentBidder(null)
              resetTimer()
            }
          },
          onPlayerAssigned: (data: PlayerAssignedData) => {
            console.log('✅ Player assigned:', data)
            if (isSubscribed) {
              playBidSound()
              vibrateOnBid()
              setCurrentPlayer(null)
              setCurrentBid(0)
              setCurrentBidder(null)
              setAssignPrice('')
              setSelectedTeamForBid('')
              resetTimer()
            }
          },
          onAuctionState: (state: AuctionState) => {
            console.log('📦 Auction state received:', state)
            if (isSubscribed) {
              setAuctionState(state)
              setTeams(state.teams)
              setIsAuctioneer(state.isAuctioneer)
              setSalesHistory(state.wrapper.winnings)
              setIsLocked(state.wrapper.status === AuctionStatus.Paused)

              // Find my team
              const myTeamEntity = state.teams.find(
                t => t.value.owner.toLowerCase() === user.email?.toLowerCase()
              )
              if (myTeamEntity) {
                setMyTeam(myTeamEntity)
                setMyTeamBudget(calculateBudgetRemaining(myTeamEntity))
              }

              // Set current player if exists
              if (state.wrapper.actual) {
                setCurrentPlayer(state.wrapper.actual.player)
                setCurrentBid(state.wrapper.actual.price)
                setCurrentBidder(state.wrapper.actual.owner)
              }
            }
          },
          onAuctionUpdated: () => {
            console.log('🔄 Auction updated')
            // Refresh auction state
            auctionService.getAuctionState(auctionId).catch(console.error)
          },
          onAuctionPaused: () => {
            console.log('⏸️ Auction paused')
            if (isSubscribed) setIsLocked(true)
          },
          onAuctionResumed: () => {
            console.log('▶️ Auction resumed')
            if (isSubscribed) setIsLocked(false)
          },
          onAuctionClosed: () => {
            console.log('🏁 Auction closed')
            if (isSubscribed) {
              setConnectionError('L\'asta è stata chiusa')
            }
          }
        })

        // Connect
        await auctionService.connect()

        // Join auction
        const joined = await auctionService.joinAuction(auctionId)
        
        if (!joined) {
          if (isSubscribed) {
            setConnectionError('Non autorizzato ad unirsi a questa asta')
            setConnectionState('Disconnected')
          }
          return
        }

        // Load initial data
        await auctionService.getAuctionState(auctionId)
        await auctionService.getCurrentPlayer(auctionId)

      } catch (err) {
        console.error('❌ Error connecting to auction:', err)
        if (isSubscribed) {
          setConnectionError(`Errore: ${err instanceof Error ? err.message : 'Unknown error'}`)
          setConnectionState('Disconnected')
          
          // Retry connection
          if (reconnectAttempts < 3) {
            setTimeout(() => {
              setReconnectAttempts(prev => prev + 1)
            }, 3000)
          }
        }
      }
    }

    connectAndJoin()

    // Cleanup on unmount
    return () => {
      isSubscribed = false
      auctionService.leaveAuction(auctionId).catch(console.error)
      auctionService.disconnect().catch(console.error)
    }
  }, [auctionId, user?.email, reconnectAttempts, playBidSound, vibrateOnBid, resetTimer, calculateBudgetRemaining])

  // ============================================
  // RENDER: LOADING STATE
  // ============================================

  if (connectionState !== 'Connected') {
    return (
      <View style={{ flex: 1 }}>
        <ScreenContent path="screens/auction-live.tsx" title="Asta Live">
          <YStack padding="$4" gap="$4" alignItems="center" justifyContent="center" minHeight={400}>
            {connectionError ? (
              <YStack gap="$3" alignItems="center" maxWidth={600}>
                <Card padding="$4" backgroundColor="$red2" borderColor="$red8" borderWidth="$0.5">
                  <YStack gap="$2">
                    <XStack gap="$2" alignItems="center">
                      <AlertCircle size={24} color="$red11" />
                      <Text color="$red11" fontSize="$5" fontWeight="700">Errore di Connessione</Text>
                    </XStack>
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
                    setReconnectAttempts(0)
                  }}
                  theme="blue"
                  icon={RotateCcw}
                >
                  Riprova Connessione
                </Button>
              </YStack>
            ) : (
              <>
                <Spinner size="large" color="$blue10" />
                <Text fontSize="$4" color="$color11">
                  {connectionState === 'Connecting' ? 'Connessione all\'asta...' : 'Disconnesso'}
                </Text>
                {reconnectAttempts > 0 && (
                  <Text fontSize="$3" color="$color10">
                    Tentativo {reconnectAttempts} di 3...
                  </Text>
                )}
              </>
            )}
          </YStack>
        </ScreenContent>
      </View>
    )
  }

  // ============================================
  // RENDER: MAIN SCREEN (placeholder for now)
  // ============================================

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
                        Asta {isAuctioneer ? '(Banditore)' : ''} - {auctionState.leagueName || 'Lega'}
                      </Text>
                    </XStack>
                    <XStack gap="$2" alignItems="center">
                      <Text fontSize="$4" fontWeight="600" color={getStatusColor(auctionState.wrapper.status)}>
                        {getStatusLabel(auctionState.wrapper.status)}
                      </Text>
                      {isLocked && <Lock size={20} color="$orange10" />}
                    </XStack>
                  </XStack>
                </Card>
              )}

              {/* Action Error */}
              {actionError && (
                <Card padding="$3" backgroundColor="$red2" borderColor="$red8" borderWidth="$0.5">
                  <XStack gap="$2" alignItems="center">
                    <AlertCircle size={16} color="$red11" />
                    <Text color="$red11" fontSize="$3">{actionError}</Text>
                  </XStack>
                </Card>
              )}

              {/* Settings Panel - Audio & Vibration */}
              <Card padding="$3" backgroundColor="$color2" borderRadius="$4">
                <XStack gap="$4" alignItems="center" justifyContent="space-between" flexWrap="wrap">
                  <XStack gap="$3" alignItems="center">
                    <XStack gap="$2" alignItems="center">
                      {audioEnabled ? (
                        <Volume2 size={18} color="$color11" />
                      ) : (
                        <VolumeX size={18} color="$color10" />
                      )}
                      <Text fontSize="$3" color="$color11">Audio</Text>
                      <Switch 
                        size="$2" 
                        checked={audioEnabled} 
                        onCheckedChange={setAudioEnabled}
                      >
                        <Switch.Thumb animation="quick" />
                      </Switch>
                    </XStack>
                    
                    {Platform.OS !== 'web' && (
                      <XStack gap="$2" alignItems="center">
                        <Vibrate size={18} color={vibrationEnabled ? '$color11' : '$color10'} />
                        <Text fontSize="$3" color="$color11">Vibrazione</Text>
                        <Switch 
                          size="$2" 
                          checked={vibrationEnabled} 
                          onCheckedChange={setVibrationEnabled}
                        >
                          <Switch.Thumb animation="quick" />
                        </Switch>
                      </XStack>
                    )}
                  </XStack>

                  {isAuctioneer && (
                    <XStack gap="$2" alignItems="center">
                      <Clock size={18} color="$color11" />
                      <Text fontSize="$3" color="$color11">Timer:</Text>
                      <Select 
                        value={offerTimeoutSeconds.toString()} 
                        onValueChange={(val) => setOfferTimeoutSeconds(Number(val))}
                      >
                        <Select.Trigger width={80}>
                          <Select.Value />
                        </Select.Trigger>
                        <Select.Content zIndex={200000}>
                          <Select.Viewport>
                            {[5, 10, 15, 20, 30].map((secs, idx) => (
                              <Select.Item key={secs} index={idx} value={secs.toString()}>
                                <Select.ItemText>{secs}s</Select.ItemText>
                              </Select.Item>
                            ))}
                          </Select.Viewport>
                        </Select.Content>
                      </Select>
                    </XStack>
                  )}
                </XStack>
              </Card>

              {/* Current Player Card */}
              {currentPlayer && (
                <Card 
                  padding="$4" 
                  backgroundColor="$green2" 
                  borderRadius="$4" 
                  borderWidth="$0.5" 
                  borderColor="$green8"
                >
                  <YStack gap="$3">
                    <XStack gap="$2" alignItems="center" justifyContent="space-between" flexWrap="wrap">
                      <XStack gap="$2" alignItems="center">
                        <Users size={20} color="$green11" />
                        <Text fontSize="$5" fontWeight="700" color="$green11">
                          Giocatore in Asta
                        </Text>
                      </XStack>
                      <Text fontSize="$4" color="$green10">
                        {getRoleLabel(currentPlayer.role)}
                      </Text>
                    </XStack>
                    
                    <YStack gap="$2">
                      <Text fontSize="$7" fontWeight="700" color="$green11">
                        {currentPlayer.name}
                      </Text>
                      <Text fontSize="$4" color="$green10">
                        {currentPlayer.realTeam.name}
                      </Text>
                    </YStack>

                    {/* Current Bid Info */}
                    <Card padding="$3" backgroundColor="$color1" borderRadius="$3">
                      <XStack gap="$4" justifyContent="space-between" alignItems="center" flexWrap="wrap">
                        <YStack gap="$1">
                          <Text fontSize="$2" color="$color10">Offerta Corrente</Text>
                          <Text fontSize="$6" fontWeight="700" color="$color11">
                            €{currentBid}
                          </Text>
                        </YStack>
                        {currentBidder && (
                          <YStack gap="$1" alignItems="flex-end">
                            <Text fontSize="$2" color="$color10">Offerente</Text>
                            <Text fontSize="$4" fontWeight="600" color="$color11">
                              {teams.find(t => t.value.owner === currentBidder)?.value.name || currentBidder}
                            </Text>
                          </YStack>
                        )}
                      </XStack>
                    </Card>

                    {/* Player Stats */}
                    <Card padding="$3" backgroundColor="$color1" borderRadius="$3">
                      <XStack gap="$4" flexWrap="wrap" justifyContent="space-between">
                        <YStack gap="$1">
                          <Text fontSize="$2" color="$color10">Media Voto</Text>
                          <Text fontSize="$4" fontWeight="600" color="$color11">
                            {currentPlayer.average?.toFixed(2) || 'N/A'}
                          </Text>
                        </YStack>
                        <YStack gap="$1">
                          <Text fontSize="$2" color="$color10">Fantamedia</Text>
                          <Text fontSize="$4" fontWeight="600" color="$color11">
                            {currentPlayer.fantaAverage?.toFixed(2) || 'N/A'}
                          </Text>
                        </YStack>
                        <YStack gap="$1">
                          <Text fontSize="$2" color="$color10">Goal</Text>
                          <Text fontSize="$4" fontWeight="600" color="$color11">
                            {currentPlayer.goals || 0}
                          </Text>
                        </YStack>
                        <YStack gap="$1">
                          <Text fontSize="$2" color="$color10">Assist</Text>
                          <Text fontSize="$4" fontWeight="600" color="$color11">
                            {currentPlayer.assists || 0}
                          </Text>
                        </YStack>
                      </XStack>
                    </Card>
                  </YStack>
                </Card>
              )}

              {/* PULSANTIERA - Bidding Buttons */}
              {!isAuctioneer && currentPlayer && myTeam && (
                <Card 
                  padding="$4" 
                  backgroundColor="$blue2" 
                  borderRadius="$4" 
                  borderWidth="$0.5" 
                  borderColor="$blue8"
                  opacity={tempBlockButtons ? 0.6 : 1}
                >
                  <YStack gap="$3">
                    <XStack gap="$2" alignItems="center">
                      <DollarSign size={20} color="$blue11" />
                      <Text fontSize="$5" fontWeight="700" color="$blue11">
                        Pulsantiera Offerte
                      </Text>
                    </XStack>

                    {/* Info Banner */}
                    <Card padding="$2" paddingHorizontal="$3" backgroundColor="$blue1" borderRadius="$3">
                      <Text fontSize="$3" color="$blue11">
                        {myTeam.value.name} • Budget: €{myTeamBudget}
                      </Text>
                    </Card>

                    {/* Sezione 1: Incrementi Rapidi (Grandi) */}
                    <XStack gap="$2">
                      <Button
                        flex={1}
                        size="$6"
                        fontSize="$7"
                        fontWeight="700"
                        theme="blue"
                        onPress={() => handleIncrementBid(1)}
                        disabled={areBidButtonsDisabled()}
                      >
                        Incremento
                      </Button>
                      <Button
                        flex={1}
                        size="$6"
                        fontSize="$7"
                        fontWeight="700"
                        theme="blue"
                        onPress={() => handleIncrementBid(3)}
                        disabled={areBidButtonsDisabled()}
                      >
                        +3
                      </Button>
                    </XStack>

                    {/* Sezione 2: Incrementi Medi (Griglia) */}
                    <YStack gap="$2">
                      <XStack gap="$2" flexWrap="wrap">
                        {[2, 4, 5, 6, 7, 8].map(inc => (
                          <Button
                            key={inc}
                            flex={1}
                            minWidth={80}
                            size="$3"
                            fontSize="$4"
                            theme="blue"
                            onPress={() => handleIncrementBid(inc)}
                            disabled={areBidButtonsDisabled()}
                          >
                            +{inc}
                          </Button>
                        ))}
                      </XStack>
                      <XStack gap="$2" flexWrap="wrap">
                        {[10, 15, 20, 25, 50, 100].map(inc => (
                          <Button
                            key={inc}
                            flex={1}
                            minWidth={80}
                            size="$3"
                            fontSize="$4"
                            theme="blue"
                            onPress={() => handleIncrementBid(inc)}
                            disabled={areBidButtonsDisabled()}
                          >
                            +{inc}
                          </Button>
                        ))}
                      </XStack>
                    </YStack>

                    {/* Sezione 3: Offerte Fisse */}
                    <YStack gap="$2">
                      <Text fontSize="$3" color="$blue10" fontWeight="600">Offerte Fisse</Text>
                      <XStack gap="$2" flexWrap="wrap">
                        {[50, 100, 150, 200, 250, 300].map(amount => (
                          <Button
                            key={amount}
                            flex={1}
                            minWidth={80}
                            size="$3"
                            fontSize="$4"
                            theme="blue"
                            variant="outlined"
                            onPress={() => handleFixedBid(amount)}
                            disabled={areBidButtonsDisabled()}
                          >
                            {amount}
                          </Button>
                        ))}
                      </XStack>
                    </YStack>

                    {/* Sezione 4: Offerta Personalizzata */}
                    <Button
                      icon={DollarSign}
                      theme="blue"
                      variant="outlined"
                      onPress={() => setShowCustomBidDialog(true)}
                      disabled={areBidButtonsDisabled()}
                    >
                      Offerta Personalizzata
                    </Button>

                    {/* Warnings */}
                    {timerExpired && !isAuctioneer && (
                      <Card padding="$2" backgroundColor="$orange2" borderColor="$orange8" borderWidth="$0.5">
                        <Text fontSize="$3" color="$orange11">
                          ⏱️ Tempo scaduto - Solo il banditore può agire
                        </Text>
                      </Card>
                    )}
                    {currentBidder?.toLowerCase() === user?.email?.toLowerCase() && (
                      <Card padding="$2" backgroundColor="$green2" borderColor="$green8" borderWidth="$0.5">
                        <Text fontSize="$3" color="$green11">
                          ✅ Sei il miglior offerente
                        </Text>
                      </Card>
                    )}
                  </YStack>
                </Card>
              )}

              {/* PULSANTIERA BANDITORE */}
              {isAuctioneer && currentPlayer && (
                <Card 
                  padding="$4" 
                  backgroundColor="$orange2" 
                  borderRadius="$4" 
                  borderWidth="$0.5" 
                  borderColor="$orange8"
                  opacity={tempBlockButtons ? 0.6 : 1}
                >
                  <YStack gap="$3">
                    <XStack gap="$2" alignItems="center">
                      <Gavel size={20} color="$orange11" />
                      <Text fontSize="$5" fontWeight="700" color="$orange11">
                        Pulsantiera Banditore
                      </Text>
                    </XStack>

                    {/* Selezione Squadra per Offerte */}
                    <YStack gap="$2">
                      <Label htmlFor="team-select" fontSize="$4" color="$orange11">
                        Fai offerta per squadra:
                      </Label>
                      <Select 
                        id="team-select"
                        value={selectedTeamForBid} 
                        onValueChange={setSelectedTeamForBid}
                      >
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
                              <Select.Item key={team.value.owner} index={idx} value={team.value.owner}>
                                <Select.ItemText>
                                  {team.value.name} (€{calculateBudgetRemaining(team)})
                                </Select.ItemText>
                              </Select.Item>
                            ))}
                          </Select.Viewport>
                          <Select.ScrollDownButton />
                        </Select.Content>
                      </Select>
                      {selectedTeamForBid && (
                        <Card padding="$2" backgroundColor="$orange1" borderRadius="$2">
                          <Text fontSize="$3" color="$orange11">
                            Selezionato: {teams.find(t => t.value.owner === selectedTeamForBid)?.value.name}
                          </Text>
                        </Card>
                      )}
                    </YStack>

                    <Separator />

                    {/* Incrementi Rapidi */}
                    <XStack gap="$2">
                      <Button
                        flex={1}
                        size="$6"
                        fontSize="$7"
                        fontWeight="700"
                        theme="orange"
                        onPress={() => handleIncrementBid(1)}
                        disabled={areBidButtonsDisabled()}
                      >
                        Incremento
                      </Button>
                      <Button
                        flex={1}
                        size="$6"
                        fontSize="$7"
                        fontWeight="700"
                        theme="orange"
                        onPress={() => handleIncrementBid(3)}
                        disabled={areBidButtonsDisabled()}
                      >
                        +3
                      </Button>
                    </XStack>

                    {/* Incrementi Medi */}
                    <YStack gap="$2">
                      <XStack gap="$2" flexWrap="wrap">
                        {[2, 4, 5, 6, 7, 8].map(inc => (
                          <Button
                            key={inc}
                            flex={1}
                            minWidth={80}
                            size="$3"
                            fontSize="$4"
                            theme="orange"
                            onPress={() => handleIncrementBid(inc)}
                            disabled={areBidButtonsDisabled()}
                          >
                            +{inc}
                          </Button>
                        ))}
                      </XStack>
                      <XStack gap="$2" flexWrap="wrap">
                        {[10, 15, 20, 25, 50, 100].map(inc => (
                          <Button
                            key={inc}
                            flex={1}
                            minWidth={80}
                            size="$3"
                            fontSize="$4"
                            theme="orange"
                            onPress={() => handleIncrementBid(inc)}
                            disabled={areBidButtonsDisabled()}
                          >
                            +{inc}
                          </Button>
                        ))}
                      </XStack>
                    </YStack>

                    {/* Offerte Fisse */}
                    <YStack gap="$2">
                      <Text fontSize="$3" color="$orange10" fontWeight="600">Offerte Fisse</Text>
                      <XStack gap="$2" flexWrap="wrap">
                        {[50, 100, 150, 200, 250, 300].map(amount => (
                          <Button
                            key={amount}
                            flex={1}
                            minWidth={80}
                            size="$3"
                            fontSize="$4"
                            theme="orange"
                            variant="outlined"
                            onPress={() => handleFixedBid(amount)}
                            disabled={areBidButtonsDisabled()}
                          >
                            {amount}
                          </Button>
                        ))}
                      </XStack>
                    </YStack>

                    {/* Offerta Personalizzata */}
                    <Button
                      icon={DollarSign}
                      theme="orange"
                      variant="outlined"
                      onPress={() => setShowCustomBidDialog(true)}
                      disabled={areBidButtonsDisabled()}
                    >
                      Offerta Personalizzata
                    </Button>
                  </YStack>
                </Card>
              )}

              {/* PANNELLO SQUADRE */}
              {teams.length > 0 && (
                <Card padding="$4" backgroundColor="$color2" borderRadius="$4" borderWidth="$0.5" borderColor="$borderColor">
                  <YStack gap="$4">
                    <XStack gap="$2" alignItems="center">
                      <Users size={20} color="$color11" />
                      <Text fontSize="$5" fontWeight="700" color="$color11">
                        Squadre Partecipanti ({teams.length})
                      </Text>
                    </XStack>

                    {teams.map((team) => {
                      const isMyTeam = user?.email && team?.value?.owner?.toLowerCase() === user.email.toLowerCase()
                      const budget = calculateBudgetRemaining(team)
                      const spent = 1000 - budget
                      const hasLowBudget = budget < 100
                      const slotsRemaining = 25 - team.value.players.length

                      // Count players by role
                      const roleCount = {
                        [Role.GoalKeeper]: team.value.players.filter(p => p.role === Role.GoalKeeper).length,
                        [Role.Defensor]: team.value.players.filter(p => p.role === Role.Defensor).length,
                        [Role.Midfielder]: team.value.players.filter(p => p.role === Role.Midfielder).length,
                        [Role.Forward]: team.value.players.filter(p => p.role === Role.Forward).length,
                      }

                      return (
                        <Card
                          key={team.value.owner}
                          padding="$3"
                          backgroundColor={isMyTeam ? '$blue2' : '$color1'}
                          borderRadius="$3"
                          borderWidth="$0.5"
                          borderColor={isMyTeam ? '$blue8' : '$borderColor'}
                        >
                          <YStack gap="$2">
                            <XStack gap="$2" justifyContent="space-between" alignItems="center" flexWrap="wrap">
                              <Text fontSize="$5" fontWeight="700" color="$color11">
                                {team.value.name}
                              </Text>
                              {isMyTeam && (
                                <Card padding="$2" paddingHorizontal="$3" backgroundColor="$blue8" borderRadius="$2">
                                  <Text fontSize="$2" color="$blue1" fontWeight="700">TUA SQUADRA</Text>
                                </Card>
                              )}
                            </XStack>
                            <Text fontSize="$3" color="$color10">{team.value.owner}</Text>
                            
                            {/* Stats Grid */}
                            <XStack gap="$3" flexWrap="wrap">
                              <YStack gap="$1">
                                <Text fontSize="$2" color="$color10">Speso</Text>
                                <Text fontSize="$4" fontWeight="600" color="$color11">€{spent}</Text>
                              </YStack>
                              <YStack gap="$1">
                                <Text fontSize="$2" color="$color10">Budget</Text>
                                <Text 
                                  fontSize="$4" 
                                  fontWeight="600" 
                                  color={hasLowBudget ? '$red11' : '$color11'}
                                >
                                  €{budget}
                                </Text>
                              </YStack>
                              <YStack gap="$1">
                                <Text fontSize="$2" color="$color10">Giocatori</Text>
                                <Text fontSize="$4" fontWeight="600" color="$color11">{team.value.players.length}/25</Text>
                              </YStack>
                              <YStack gap="$1">
                                <Text fontSize="$2" color="$color10">Slot</Text>
                                <Text fontSize="$4" fontWeight="600" color="$color11">{slotsRemaining}</Text>
                              </YStack>
                            </XStack>

                            {/* Role Limits */}
                            <Card padding="$2" backgroundColor="$color2" borderRadius="$2">
                              <XStack gap="$3" flexWrap="wrap">
                                <YStack gap="$0.5">
                                  <Text fontSize="$2" color="$color10">P</Text>
                                  <Text fontSize="$3" fontWeight="600" color={roleCount[Role.GoalKeeper] >= ROLE_LIMITS[Role.GoalKeeper] ? '$red11' : '$color11'}>
                                    {roleCount[Role.GoalKeeper]}/{ROLE_LIMITS[Role.GoalKeeper]}
                                  </Text>
                                </YStack>
                                <YStack gap="$0.5">
                                  <Text fontSize="$2" color="$color10">D</Text>
                                  <Text fontSize="$3" fontWeight="600" color={roleCount[Role.Defensor] >= ROLE_LIMITS[Role.Defensor] ? '$red11' : '$color11'}>
                                    {roleCount[Role.Defensor]}/{ROLE_LIMITS[Role.Defensor]}
                                  </Text>
                                </YStack>
                                <YStack gap="$0.5">
                                  <Text fontSize="$2" color="$color10">C</Text>
                                  <Text fontSize="$3" fontWeight="600" color={roleCount[Role.Midfielder] >= ROLE_LIMITS[Role.Midfielder] ? '$red11' : '$color11'}>
                                    {roleCount[Role.Midfielder]}/{ROLE_LIMITS[Role.Midfielder]}
                                  </Text>
                                </YStack>
                                <YStack gap="$0.5">
                                  <Text fontSize="$2" color="$color10">A</Text>
                                  <Text fontSize="$3" fontWeight="600" color={roleCount[Role.Forward] >= ROLE_LIMITS[Role.Forward] ? '$red11' : '$color11'}>
                                    {roleCount[Role.Forward]}/{ROLE_LIMITS[Role.Forward]}
                                  </Text>
                                </YStack>
                              </XStack>
                            </Card>

                            {/* Players List (Collapsed) */}
                            {team.value.players.length > 0 && (
                              <YStack gap="$1" marginTop="$2">
                                <Text fontSize="$3" fontWeight="600" color="$color10">Rosa ({team.value.players.length}):</Text>
                                <YStack maxHeight={150} overflow="scroll">
                                  <YStack gap="$0.5">
                                    {team.value.players.map((player) => (
                                      <XStack key={player.name} gap="$2" justifyContent="space-between">
                                        <Text fontSize="$2" color="$color11" flex={1}>
                                          {player.name} ({getRoleLabel(player.role)}) - {player.team.name}
                                        </Text>
                                        <Text fontSize="$2" color="$color10" fontWeight="600">€{player.price}</Text>
                                      </XStack>
                                    ))}
                                  </YStack>
                                </YStack>
                              </YStack>
                            )}
                          </YStack>
                        </Card>
                      )
                    })}
                  </YStack>
                </Card>
              )}

              {/* CONTROLLI BANDITORE - Gestione Asta */}
              {isAuctioneer && auctionState && (
                <Card padding="$4" backgroundColor="$purple2" borderRadius="$4" borderWidth="$0.5" borderColor="$purple8">
                  <YStack gap="$4">
                    <XStack gap="$2" alignItems="center">
                      <Gavel size={20} color="$purple11" />
                      <Text fontSize="$5" fontWeight="700" color="$purple11">
                        Controlli Asta
                      </Text>
                    </XStack>

                    {/* Gestione Giocatore */}
                    <YStack gap="$2">
                      <Text fontSize="$4" fontWeight="600" color="$purple11">
                        Gestione Giocatore
                      </Text>
                      
                      <XStack gap="$2">
                        <YStack flex={1}>
                          <Select value={selectedRole.toString()} onValueChange={(val) => setSelectedRole(Number(val) as Role)}>
                            <Select.Trigger iconAfter={ChevronDown}>
                              <Select.Value placeholder="Ruolo" />
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
                                {[Role.GoalKeeper, Role.Defensor, Role.Midfielder, Role.Forward].map((role, idx) => (
                                  <Select.Item key={role} index={idx} value={role.toString()}>
                                    <Select.ItemText>{getRoleLabel(role)}</Select.ItemText>
                                  </Select.Item>
                                ))}
                              </Select.Viewport>
                              <Select.ScrollDownButton />
                            </Select.Content>
                          </Select>
                        </YStack>
                        
                        <Button
                          icon={TrendingUp}
                          theme="purple"
                          flex={1}
                          disabled={loading}
                          onPress={async () => {
                            setLoading(true)
                            try {
                              await auctionService.showNextPlayer(auctionId, selectedRole)
                            } catch (err) {
                              setActionError(`Errore: ${err instanceof Error ? err.message : 'Unknown'}`)
                            } finally {
                              setLoading(false)
                            }
                          }}
                        >
                          Prossimo
                        </Button>
                      </XStack>

                      <Button
                        icon={Search}
                        theme="purple"
                        variant="outlined"
                        onPress={() => setShowPlayerSearchDialog(true)}
                      >
                        Cerca Giocatore
                      </Button>
                    </YStack>

                    <Separator />

                    {/* Aggiudica Giocatore */}
                    {currentPlayer && currentBidder && (
                      <YStack gap="$2">
                        <Text fontSize="$4" fontWeight="600" color="$purple11">
                          Aggiudica Giocatore
                        </Text>
                        <Card padding="$3" backgroundColor="$purple1">
                          <Text fontSize="$3" color="$purple11">
                            {currentPlayer.name} a {teams.find(t => t.value.owner === currentBidder)?.value.name} per €{currentBid}
                          </Text>
                        </Card>
                        <Button
                          icon={Gavel}
                          theme="green"
                          disabled={loading}
                          onPress={async () => {
                            setLoading(true)
                            try {
                              await auctionService.assignPlayerToTeam(auctionId, currentBidder, currentBid)
                            } catch (err) {
                              setActionError(`Errore: ${err instanceof Error ? err.message : 'Unknown'}`)
                            } finally {
                              setLoading(false)
                            }
                          }}
                        >
                          Aggiudica
                        </Button>
                      </YStack>
                    )}

                    <Separator />

                    {/* Controlli Timer e Asta */}
                    <YStack gap="$2">
                      <Text fontSize="$4" fontWeight="600" color="$purple11">
                        Controlli Timer e Stato
                      </Text>
                      <XStack gap="$2" flexWrap="wrap">
                        {auctionState.wrapper.status === AuctionStatus.InProgress ? (
                          <Button
                            icon={Pause}
                            theme="orange"
                            flex={1}
                            disabled={loading}
                            onPress={async () => {
                              setLoading(true)
                              try {
                                await auctionService.pauseAuction(auctionId)
                              } catch (err) {
                                setActionError(`Errore: ${err instanceof Error ? err.message : 'Unknown'}`)
                              } finally {
                                setLoading(false)
                              }
                            }}
                          >
                            Pausa
                          </Button>
                        ) : (
                          <Button
                            icon={Play}
                            theme="green"
                            flex={1}
                            disabled={loading}
                            onPress={async () => {
                              setLoading(true)
                              try {
                                await auctionService.resumeAuction(auctionId)
                              } catch (err) {
                                setActionError(`Errore: ${err instanceof Error ? err.message : 'Unknown'}`)
                              } finally {
                                setLoading(false)
                              }
                            }}
                          >
                            Riprendi
                          </Button>
                        )}
                        
                        <Button
                          icon={RotateCcw}
                          theme="purple"
                          variant="outlined"
                          flex={1}
                          onPress={() => {
                            resetTimer()
                            setCurrentBid(0)
                            setCurrentBidder(null)
                          }}
                        >
                          Reset Offerte
                        </Button>
                      </XStack>

                      <Button
                        icon={XCircle}
                        theme="red"
                        disabled={loading}
                        onPress={async () => {
                          if (Platform.OS === 'web' && !confirm('Sei sicuro di voler chiudere l\'asta?')) return
                          setLoading(true)
                          try {
                            await auctionService.closeAuction(auctionId)
                          } catch (err) {
                            setActionError(`Errore: ${err instanceof Error ? err.message : 'Unknown'}`)
                          } finally {
                            setLoading(false)
                          }
                        }}
                      >
                        Chiudi Asta
                      </Button>
                    </YStack>
                  </YStack>
                </Card>
              )}

            </YStack>
          </YStack>
        </ScrollView>

        {/* TIMER FISSO (Bottom Right) */}
        {(timerRunning || timerExpired) && currentPlayer && (
          <YStack 
            position="absolute" 
            bottom="$4" 
            right="$4" 
            zIndex={1000}
          >
            <Card 
              padding="$3" 
              backgroundColor={timerExpired ? '$red8' : timerSecondsLeft <= 3 ? '$orange8' : '$blue8'}
              borderRadius="$6"
              shadowColor="$shadowColor"
              shadowOffset={{ width: 0, height: 2 }}
              shadowOpacity={0.25}
              shadowRadius={4}
            >
              <XStack gap="$2" alignItems="center">
                <Clock size={20} color="white" />
                <Text fontSize="$6" fontWeight="700" color="white">
                  {timerExpired ? 'SCADUTO' : `${timerSecondsLeft}s`}
                </Text>
              </XStack>
            </Card>
          </YStack>
        )}

        {/* Dialog: Offerta Personalizzata */}
        <Dialog modal open={showCustomBidDialog} onOpenChange={setShowCustomBidDialog}>
          <Dialog.Portal>
            <Dialog.Overlay
              key="overlay"
              animation="quick"
              opacity={0.5}
              enterStyle={{ opacity: 0 }}
              exitStyle={{ opacity: 0 }}
            />
            <Dialog.Content
              bordered
              elevate
              key="content"
              animateOnly={['transform', 'opacity']}
              animation={[
                'quick',
                {
                  opacity: {
                    overshootClamping: true,
                  },
                },
              ]}
              enterStyle={{ x: 0, y: -20, opacity: 0, scale: 0.9 }}
              exitStyle={{ x: 0, y: 10, opacity: 0, scale: 0.95 }}
              gap="$4"
              width={400}
              maxWidth="90%"
            >
              <Dialog.Title fontSize="$6" fontWeight="700">Offerta Personalizzata</Dialog.Title>
              <Dialog.Description>
                Inserisci l'importo della tua offerta (deve essere superiore a €{currentBid})
              </Dialog.Description>
              
              <YStack gap="$3">
                <Input
                  placeholder="Importo (€)"
                  value={customBidAmount}
                  onChangeText={setCustomBidAmount}
                  keyboardType="numeric"
                  size="$5"
                />
                
                <XStack gap="$2" justifyContent="flex-end">
                  <Dialog.Close asChild>
                    <Button variant="outlined">
                      Annulla
                    </Button>
                  </Dialog.Close>
                  <Button
                    theme="blue"
                    onPress={() => {
                      const amount = parseInt(customBidAmount, 10)
                      if (!isNaN(amount) && amount > 0) {
                        placeBid(amount)
                        setCustomBidAmount('')
                        setShowCustomBidDialog(false)
                      } else {
                        setActionError('Inserisci un importo valido')
                      }
                    }}
                  >
                    Conferma Offerta
                  </Button>
                </XStack>
              </YStack>
            </Dialog.Content>
          </Dialog.Portal>
        </Dialog>

      </ScreenContent>
    </View>
  )
}
