// Auction Live Screen - Real-time auction management with bidding interface (Pulsantiera)
import { useState, useEffect, useCallback, useRef } from 'react'
import { View, Platform, Vibration } from 'react-native'
import { 
  Button, Card, Input, Select, Separator, Text, XStack, YStack, 
  useMedia, Spinner, Label, Switch, Dialog, ScrollView
} from 'tamagui'
import { useToastController } from '@tamagui/toast'
import { ScreenContent } from 'components/layout/ScreenContent'
import { auctionService } from 'services/auctionService'
import { 
  AuctionState, 
  AuctionStatus, 
  PlayerAssignedData,
  StatPlayer,
  AuctionWin,
  AuctionType,
  AuctionKind,
  AuctionTypeLabels,
  AuctionKindLabels
} from 'types/auction'
import { Role } from 'types/group'
import { 
  Gavel, Users, TrendingUp, Pause, Play, XCircle, ChevronDown, 
  Clock, Lock, RotateCcw, Search, DollarSign, 
  AlertCircle, Volume2, VolumeX, Vibrate
} from '@tamagui/lucide-icons'
import { useRoute, RouteProp } from '@react-navigation/native'
import { RootStackParamList } from 'navigation'
import { useAuth } from 'providers/AuthProvider'
import { Team, TeamKey, TeamHelper } from 'types/team'
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
  const { user } = useAuth()
  const toast = useToastController()

  // ============================================
  // STATE
  // ============================================
  const [connectionState, setConnectionState] = useState<'Disconnected' | 'Connecting' | 'Connected'>('Disconnected')
  const [connectionError, setConnectionError] = useState<string | null>(null)
  const [reconnectAttempts, setReconnectAttempts] = useState(0)
  const [auctionState, setAuctionState] = useState<AuctionState | null>(null)
  const [currentPlayer, setCurrentPlayer] = useState<StatPlayer | null>(null)
  const [teams, setTeams] = useState<Entity<Team, TeamKey>[]>([])
  const [currentBid, setCurrentBid] = useState<number>(0)
  const [currentBidder, setCurrentBidder] = useState<string | null>(null)
  const [isLocked, setIsLocked] = useState(false)
  const [salesHistory, setSalesHistory] = useState<AuctionWin[]>([])
  const [isAuctioneer, setIsAuctioneer] = useState(false)
  const [myTeam, setMyTeam] = useState<Entity<Team, TeamKey> | null>(null)
  const [startingMoney, setStartingMoney] = useState<number>(1000) // League's starting budget
  const [myTeamBudget, setMyTeamBudget] = useState<number>(1000)
  const [timerSecondsLeft, setTimerSecondsLeft] = useState<number>(0)
  const [timerRunning, setTimerRunning] = useState<boolean>(false)
  const [timerExpired, setTimerExpired] = useState<boolean>(false)
  const [firstOfferHappened, setFirstOfferHappened] = useState<boolean>(false)
  const [offerTimeoutSeconds, setOfferTimeoutSeconds] = useState<number>(10)
  const timerIntervalRef = useRef<NodeJS.Timeout | null>(null)
  const [selectedRole, setSelectedRole] = useState<Role>(Role.GoalKeeper)
  const [selectedTeamForBid, setSelectedTeamForBid] = useState<string>('')
  const [assignPrice, setAssignPrice] = useState<string>('')
  const [customBidAmount, setCustomBidAmount] = useState<string>('')
  const [showCustomBidDialog, setShowCustomBidDialog] = useState(false)
  const [showPlayerSearchDialog, setShowPlayerSearchDialog] = useState(false)
  const [showTeamDetailDialog, setShowTeamDetailDialog] = useState(false)
  const [selectedTeamDetail, setSelectedTeamDetail] = useState<Entity<Team, TeamKey> | null>(null)
  const [showChangeStatusDialog, setShowChangeStatusDialog] = useState(false)
  const [changeStatusPlayer, setChangeStatusPlayer] = useState<{ playerKey: string; playerName: string; owner: string } | null>(null)
  const [changeStatusType, setChangeStatusType] = useState<number>(0) // PlayerInTeamStatus enum
  const [changeStatusRevenue, setChangeStatusRevenue] = useState<string>('')
  const [playerSearchQuery, setPlayerSearchQuery] = useState('')
  const [loading, setLoading] = useState(false)
  const [tempBlockButtons, setTempBlockButtons] = useState(false)
  const [audioEnabled, setAudioEnabled] = useState(true)
  const [vibrationEnabled, setVibrationEnabled] = useState(true)
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

  const playBidSound = useCallback(() => {
    if (!audioEnabled || Platform.OS !== 'web' || !audioContextRef.current) return
    const audioContext = audioContextRef.current
    const now = audioContext.currentTime
    const oscillator1 = audioContext.createOscillator()
    const gainNode1 = audioContext.createGain()
    oscillator1.connect(gainNode1)
    gainNode1.connect(audioContext.destination)
    oscillator1.frequency.value = 800
    gainNode1.gain.setValueAtTime(0.3, now)
    gainNode1.gain.exponentialRampToValueAtTime(0.01, now + 0.1)
    oscillator1.start(now)
    oscillator1.stop(now + 0.1)
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

  const vibrateOnBid = useCallback(() => {
    if (!vibrationEnabled || Platform.OS === 'web') return
    Vibration.vibrate([120, 30, 90])
  }, [vibrationEnabled])

  const hasReachedRoleLimit = useCallback((role: Role): boolean => {
    if (!myTeam || !currentPlayer) return false
    const limit = ROLE_LIMITS[role] || 0
    // Count only active players (status === 0)
    const activePlayers = TeamHelper.getActivePlayers(myTeam.value)
    const currentCount = activePlayers.filter(p => p.role === role).length
    return currentCount >= limit
  }, [myTeam, currentPlayer])

  const calculateBudgetRemaining = useCallback((team: Entity<Team, TeamKey>): number => {
    // Budget = StartingMoney - Cost (where Cost includes revenue adjustments)
    const cost = TeamHelper.getCost(team.value)
    return startingMoney - cost
  }, [startingMoney])

  // Helper: Get player status label
  const getPlayerStatusLabel = (status: number): string => {
    const labels: Record<number, string> = {
      0: 'Attivo', // Active
      1: 'Venduto 1 girone', // SoldForOneHalf
      2: 'Venduto senza rimborso', // SoldWithNoReturnedPrice
      3: 'Venduto', // Sold
      4: 'Rimosso' // Removed
    }
    return labels[status] || 'Sconosciuto'
  }

  // Helper: Get player status color
  const getPlayerStatusColor = (status: number): string => {
    const colors: Record<number, string> = {
      0: '$green11', // Active
      1: '$orange11', // SoldForOneHalf
      2: '$red11', // SoldWithNoReturnedPrice
      3: '$blue11', // Sold
      4: '$gray11' // Removed
    }
    return colors[status] || '$gray11'
  }

  // ============================================
  // TIMER LOGIC
  // ============================================
  const startTimer = useCallback(() => {
    setTimerSecondsLeft(offerTimeoutSeconds)
    setTimerRunning(true)
    setTimerExpired(false)
    setFirstOfferHappened(true)
    if (timerIntervalRef.current) clearInterval(timerIntervalRef.current)
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

  const resetTimer = useCallback(() => {
    if (timerIntervalRef.current) clearInterval(timerIntervalRef.current)
    setTimerSecondsLeft(offerTimeoutSeconds)
    setTimerRunning(false)
    setTimerExpired(false)
    setFirstOfferHappened(false)
  }, [offerTimeoutSeconds])

  useEffect(() => {
    return () => {
      if (timerIntervalRef.current) clearInterval(timerIntervalRef.current)
    }
  }, [])

  // ============================================
  // BID LOGIC
  // ============================================
  const placeBid = useCallback(async (amount: number) => {
    if (!currentPlayer) {
      toast.show('❌ Nessun giocatore in asta', { variant: 'error' })
      return
    }
    if (isLocked && !isAuctioneer) {
      toast.show('❌ L\'asta è bloccata', { variant: 'error' })
      return
    }
    if (amount <= currentBid) {
      toast.show(`❌ L'offerta deve essere superiore a €${currentBid}`, { variant: 'error' })
      return
    }
    if (!isAuctioneer && currentBidder?.toLowerCase() === user?.email?.toLowerCase()) {
      toast.show('❌ Sei già il miglior offerente', { variant: 'error' })
      return
    }
    const biddingTeam = isAuctioneer && selectedTeamForBid 
      ? teams.find(t => t.value.owner === selectedTeamForBid)
      : myTeam
    if (!biddingTeam) {
      toast.show('❌ Nessuna squadra selezionata', { variant: 'error' })
      return
    }
    if (currentPlayer.role && hasReachedRoleLimit(currentPlayer.role)) {
      toast.show(`❌ Limite ruolo raggiunto per ${getRoleLabel(currentPlayer.role)}`, { variant: 'error' })
      return
    }
    const teamBudget = calculateBudgetRemaining(biddingTeam)
    const activePlayers = TeamHelper.getActivePlayers(biddingTeam.value)
    const slotsRemaining = 25 - activePlayers.length
    const minimumBudgetNeeded = slotsRemaining > 1 ? slotsRemaining - 1 : 0
    if (amount > teamBudget - minimumBudgetNeeded) {
      toast.show(`❌ Budget insufficiente (disponibile: €${teamBudget - minimumBudgetNeeded})`, { variant: 'error' })
      return
    }
    setTempBlockButtons(true)
    setTimeout(() => setTempBlockButtons(false), 500)
    setLoading(true)
    try {
      const bidderEmail = isAuctioneer ? biddingTeam.value.owner : undefined
      await auctionService.placeBid(auctionId, amount, bidderEmail)
      console.log(`✅ Bid placed: €${amount} by ${biddingTeam.value.name}`)
    } catch (err) {
      toast.show(`❌ ${err instanceof Error ? err.message : 'Errore sconosciuto'}`, { variant: 'error' })
      setTempBlockButtons(false)
    } finally {
      setLoading(false)
    }
  }, [auctionId, currentPlayer, isLocked, isAuctioneer, currentBid, currentBidder, user?.email, selectedTeamForBid, teams, myTeam, hasReachedRoleLimit, calculateBudgetRemaining, getRoleLabel, toast])

  const handleIncrementBid = useCallback((increment: number) => {
    placeBid(currentBid + increment)
  }, [currentBid, placeBid])

  const handleFixedBid = useCallback((amount: number) => {
    placeBid(amount)
  }, [placeBid])

  const areBidButtonsDisabled = useCallback((): boolean => {
    if (!currentPlayer) return true
    if (loading) return true
    if (tempBlockButtons) return true
    if (isLocked && !isAuctioneer) return true
    if (timerExpired && !isAuctioneer) return true
    if (!isAuctioneer && currentBidder?.toLowerCase() === user?.email?.toLowerCase()) return true
    if (isAuctioneer && !selectedTeamForBid) return true
    return false
  }, [currentPlayer, loading, tempBlockButtons, isLocked, isAuctioneer, timerExpired, currentBidder, user?.email, selectedTeamForBid])

  // ============================================
  // SIGNALR CONNECTION
  // ============================================
  useEffect(() => {
    if (!user?.email) {
      console.log('⏳ Waiting for user to be loaded...')
      return
    }
    
    let isSubscribed = true
    let connectionAttempted = false
    
    setConnectionState('Connecting')
    
    const connectAndJoin = async () => {
      // Prevent multiple connection attempts
      if (connectionAttempted) {
        return
      }
      connectionAttempted = true
      
      try {
        console.log('👤 User loaded:', user.email)
        auctionService.setUserEmail(user.email)
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
            if (isSubscribed) toast.show(`❌ ${error || 'Errore del server'}`, { variant: 'error', duration: 4000 })
          },
          onUserJoined: (email) => console.log('👋 User joined:', email),
          onUserLeft: (email) => console.log('👋 User left:', email),
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
          onBidPlaced: (data: { Amount: number; BidderEmail: string; BidderName: string; PlayerName: string }) => {
            console.log('💰 Bid placed:', data)
            if (isSubscribed) {
              setCurrentBid(data.Amount)
              setCurrentBidder(data.BidderEmail)
              if (data.BidderEmail.toLowerCase() !== user?.email?.toLowerCase()) {
                playBidSound()
                vibrateOnBid()
              }
              startTimer()
              auctionService.getAuctionState(auctionId).catch(console.error)
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
              
              // Update starting money from league settings (default to 1000 if not available)
              const leagueStartingMoney = state.league?.settings?.startingMoney ?? 1000
              setStartingMoney(leagueStartingMoney)
              
              // Sync timer with auction state
              const auctionTimer = state.wrapper.secondsPerAuction ?? 10
              setOfferTimeoutSeconds(auctionTimer)
              
              const myTeamEntity = state.teams.find(t => t.value.owner.toLowerCase() === user.email?.toLowerCase())
              if (myTeamEntity) {
                setMyTeam(myTeamEntity)
                setMyTeamBudget(calculateBudgetRemaining(myTeamEntity))
              }
              if (state.wrapper.actual) {
                setCurrentPlayer(state.wrapper.actual.player)
                setCurrentBid(state.wrapper.actual.price)
                setCurrentBidder(state.wrapper.actual.owner)
              }
            }
          },
          onAuctionUpdated: () => {
            console.log('🔄 Auction updated')
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
            if (isSubscribed) setConnectionError('L\'asta è stata chiusa')
          }
        })
        
        await auctionService.connect()
        
        // Wait a bit for connection to stabilize
        await new Promise(resolve => setTimeout(resolve, 100))
        
        // Verify connection state before joining
        if (!isSubscribed) return
        
        if (!auctionService.isConnected()) {
          throw new Error('Connection not established after connect()')
        }
        
        const joined = await auctionService.joinAuction(auctionId)
        if (!joined) {
          if (isSubscribed) {
            setConnectionError('Non autorizzato ad unirsi a questa asta')
            setConnectionState('Disconnected')
          }
          return
        }
        await auctionService.getAuctionState(auctionId)
        await auctionService.getCurrentPlayer(auctionId)
      } catch (err) {
        console.error('❌ Error connecting to auction:', err)
        if (isSubscribed) {
          setConnectionError(`Errore: ${err instanceof Error ? err.message : 'Unknown error'}`)
          setConnectionState('Disconnected')
          if (reconnectAttempts < 3) {
            setTimeout(() => {
              if (isSubscribed) {
                connectionAttempted = false
                setReconnectAttempts(prev => prev + 1)
              }
            }, 3000)
          }
        }
      }
    }
    connectAndJoin()
    return () => {
      isSubscribed = false
      auctionService.leaveAuction(auctionId).catch(console.error)
      auctionService.disconnect().catch(console.error)
    }
  }, [auctionId, user?.email, reconnectAttempts, playBidSound, vibrateOnBid, resetTimer, calculateBudgetRemaining, toast, startTimer])

  // ============================================
  // RENDER COMPONENTS
  // ============================================

  // Header Component
  const renderHeader = () => {
    if (!auctionState) return null
    
    const auctionTypeLabel = AuctionTypeLabels[auctionState.wrapper.type] || 'Sconosciuto'
    const auctionKindLabel = AuctionKindLabels[auctionState.wrapper.king] || 'Sconosciuto'
    
    return (
      <Card padding="$4" backgroundColor="$blue2" borderRadius="$4" borderWidth="$0.5" borderColor="$blue8">
        <YStack gap="$2">
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
          <XStack gap="$3" alignItems="center" justifyContent="space-between" flexWrap="wrap">
             <XStack gap="$2" alignItems="center">
              <Card padding="$2" paddingHorizontal="$3" backgroundColor="$blue1" borderRadius="$3">
                <Text fontSize="$3" color="$blue11" fontWeight="600">{auctionKindLabel}</Text>
              </Card>
              <Card padding="$2" paddingHorizontal="$3" backgroundColor="$blue1" borderRadius="$3">
                <Text fontSize="$3" color="$blue11" fontWeight="600">Lista giocatori: {auctionTypeLabel}</Text>
              </Card>
            </XStack>
             <XStack gap="$2" alignItems="center">
               {audioEnabled ? <Volume2 size={18} color="$color11" /> : <VolumeX size={18} color="$color10" />}
              <Text fontSize="$3" color="$color11">Audio</Text>
              <Switch size="$2" checked={audioEnabled} onCheckedChange={setAudioEnabled}>
                <Switch.Thumb animation="quick" />
              </Switch>
              {Platform.OS !== 'web' && (
              <XStack gap="$2" alignItems="center">
                <Vibrate size={18} color={vibrationEnabled ? '$color11' : '$color10'} />
                <Text fontSize="$3" color="$color11">Vibrazione</Text>
                <Switch size="$2" checked={vibrationEnabled} onCheckedChange={setVibrationEnabled}>
                  <Switch.Thumb animation="quick" />
                </Switch>
              </XStack>
            )}
            </XStack>
          </XStack>
        </YStack>
      </Card>
    )
  }

  // Current Player Card Component
  const renderCurrentPlayer = () => {
    if (!currentPlayer) return null
    return (
      <Card padding="$4" backgroundColor="$green2" borderRadius="$4" borderWidth="$0.5" borderColor="$green8">
        <YStack gap="$3">
          <XStack gap="$2" alignItems="center" justifyContent="space-between" flexWrap="wrap">
            <XStack gap="$2" alignItems="center">
              <Users size={20} color="$green11" />
              <Text fontSize="$5" fontWeight="700" color="$green11">Giocatore in Asta</Text>
            </XStack>
            <Text fontSize="$4" color="$green10">{getRoleLabel(currentPlayer.role)}</Text>
          </XStack>
          <YStack gap="$2">
            <Text fontSize="$7" fontWeight="700" color="$green11">{currentPlayer.name}</Text>
            <Text fontSize="$4" color="$green10">{currentPlayer.realTeam.name}</Text>
          </YStack>
          <Card padding="$3" backgroundColor="$color1" borderRadius="$3">
            <XStack gap="$4" justifyContent="space-between" alignItems="center" flexWrap="wrap">
              <YStack gap="$1">
                <Text fontSize="$2" color="$color10">Offerta Corrente</Text>
                <Text fontSize="$6" fontWeight="700" color="$color11">€{currentBid}</Text>
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
          <Card padding="$3" backgroundColor="$color1" borderRadius="$3">
            <XStack gap="$4" flexWrap="wrap" justifyContent="space-between">
              <YStack gap="$1">
                <Text fontSize="$2" color="$color10">Media Voto</Text>
                <Text fontSize="$4" fontWeight="600" color="$color11">{currentPlayer.average?.toFixed(2) || 'N/A'}</Text>
              </YStack>
              <YStack gap="$1">
                <Text fontSize="$2" color="$color10">Fantamedia</Text>
                <Text fontSize="$4" fontWeight="600" color="$color11">{currentPlayer.fantaAverage?.toFixed(2) || 'N/A'}</Text>
              </YStack>
              <YStack gap="$1">
                <Text fontSize="$2" color="$color10">Goal</Text>
                <Text fontSize="$4" fontWeight="600" color="$color11">{currentPlayer.goals || 0}</Text>
              </YStack>
              <YStack gap="$1">
                <Text fontSize="$2" color="$color10">Assist</Text>
                <Text fontSize="$4" fontWeight="600" color="$color11">{currentPlayer.assists || 0}</Text>
              </YStack>
            </XStack>
          </Card>
        </YStack>
      </Card>
    )
  }

  // Pulsantiera Partecipante Component
  const renderParticipantBidding = () => {
    if (isAuctioneer) return null
    return (
      <YStack gap="$2">
        {renderPersonalBidding()}
        {timerExpired && currentPlayer && (
          <Card padding="$2" backgroundColor="$orange2" borderColor="$orange8" borderWidth="$0.5">
            <Text fontSize="$3" color="$orange11">⏱️ Tempo scaduto - Solo il banditore può agire</Text>
          </Card>
        )}
      </YStack>
    )
  }

  // Helper: Pulsantiera Personale (usata sia da partecipanti che banditore con squadra)
  const renderPersonalBidding = () => {
    if (!myTeam) return null
    return (
      <Card padding="$4" backgroundColor="$blue2" borderRadius="$4" borderWidth="$0.5" borderColor="$blue8" opacity={tempBlockButtons ? 0.6 : 1}>
        <YStack gap="$3">
          <XStack gap="$2" alignItems="center">
            <DollarSign size={20} color="$blue11" />
            <Text fontSize="$5" fontWeight="700" color="$blue11">Mia Squadra</Text>
          </XStack>
          <Card padding="$2" paddingHorizontal="$3" backgroundColor="$blue1" borderRadius="$3">
            <Text fontSize="$3" color="$blue11">{myTeam.value.name} • Budget: €{myTeamBudget}</Text>
          </Card>
          <XStack gap="$2">
            <Button flex={1} size="$6" fontSize="$7" fontWeight="700" theme="blue" onPress={() => handleIncrementBid(1)} disabled={areBidButtonsDisabled()}>Incremento</Button>
            <Button flex={1} size="$6" fontSize="$7" fontWeight="700" theme="blue" onPress={() => handleIncrementBid(3)} disabled={areBidButtonsDisabled()}>+3</Button>
          </XStack>
          <XStack gap="$2">
            <Button flex={1} size="$6" fontSize="$7" fontWeight="700" theme="blue" onPress={() => handleIncrementBid(2)} disabled={areBidButtonsDisabled()}>+2</Button>
            <Button flex={1} size="$6" fontSize="$7" fontWeight="700" theme="blue" onPress={() => handleIncrementBid(5)} disabled={areBidButtonsDisabled()}>+5</Button>
          </XStack>
          <YStack gap="$2">
            <Text fontSize="$3" color="$blue10" fontWeight="600">Offerte Fisse</Text>
            <XStack gap="$2" flexWrap="wrap">
              {[50, 100, 150, 200].map(amount => (
                <Button key={amount} flex={1} minWidth={80} size="$3" fontSize="$4" theme="blue" variant="outlined" onPress={() => handleFixedBid(amount)} disabled={areBidButtonsDisabled()}>{amount}</Button>
              ))}
            </XStack>
          </YStack>
          <Button icon={DollarSign} theme="blue" variant="outlined" onPress={() => setShowCustomBidDialog(true)} disabled={areBidButtonsDisabled()}>Offerta Personalizzata</Button>
          {currentBidder?.toLowerCase() === user?.email?.toLowerCase() && (
            <Card padding="$2" backgroundColor="$green2" borderColor="$green8" borderWidth="$0.5">
              <Text fontSize="$3" color="$green11">✅ Sei il miglior offerente</Text>
            </Card>
          )}
        </YStack>
      </Card>
    )
  }

  // Pulsantiera Banditore con Controlli Component
  const renderAuctioneerPanel = () => {
    if (!isAuctioneer) return null

    const renderAuctioneerBidding = () => (
      <YStack gap="$3">
        <XStack gap="$2" alignItems="center">
          <Gavel size={20} color="$orange11" />
          <Text fontSize="$5" fontWeight="700" color="$orange11">Pulsantiera Banditore</Text>
        </XStack>
        
        {!currentPlayer && (
          <Card padding="$3" backgroundColor="$orange1" borderRadius="$3">
            <Text fontSize="$3" color="$orange11" textAlign="center">
              Nessun giocatore in asta - Seleziona il prossimo giocatore
            </Text>
          </Card>
        )}
        
        {currentPlayer && (
          <>
            <YStack gap="$2">
              <Label htmlFor="team-select" fontSize="$4" color="$orange11">Fai offerta per squadra:</Label>
              <Select id="team-select" value={selectedTeamForBid} onValueChange={setSelectedTeamForBid}>
                <Select.Trigger iconAfter={ChevronDown}><Select.Value placeholder="Seleziona Squadra" /></Select.Trigger>
                <Select.Content zIndex={200000}>
                  <Select.Viewport>
                    {teams.map((team, idx) => (
                      <Select.Item key={team.value.owner} index={idx} value={team.value.owner}>
                        <Select.ItemText>{team.value.name} (€{calculateBudgetRemaining(team)})</Select.ItemText>
                      </Select.Item>
                    ))}
                  </Select.Viewport>
                </Select.Content>
              </Select>
              {selectedTeamForBid && (
                <Card padding="$2" backgroundColor="$orange1" borderRadius="$2">
                  <Text fontSize="$3" color="$orange11">Selezionato: {teams.find(t => t.value.owner === selectedTeamForBid)?.value.name}</Text>
                </Card>
              )}
            </YStack>
            <Separator />
            <XStack gap="$2">
              <Button flex={1} size="$6" fontSize="$7" fontWeight="700" theme="orange" onPress={() => handleIncrementBid(1)} disabled={areBidButtonsDisabled()}>Incremento</Button>
              <Button flex={1} size="$6" fontSize="$7" fontWeight="700" theme="orange" onPress={() => handleIncrementBid(3)} disabled={areBidButtonsDisabled()}>+3</Button>
            </XStack>
            <YStack gap="$2">
              <Text fontSize="$3" color="$orange10" fontWeight="600">Offerte Fisse</Text>
              <XStack gap="$2" flexWrap="wrap">
                {[50, 100].map(amount => (
                  <Button key={amount} flex={1} minWidth={80} size="$3" fontSize="$4" theme="orange" variant="outlined" onPress={() => handleFixedBid(amount)} disabled={areBidButtonsDisabled()}>{amount}</Button>
                ))}
              </XStack>
            </YStack>
          </>
        )}
      </YStack>
    )

    const renderAuctioneerControls = () => (
      <YStack gap="$3">
        <Separator />
        <Text fontSize="$4" fontWeight="600" color="$orange11">Impostazioni Asta</Text>
        <XStack gap="$2" alignItems="center">
          <Clock size={18} color="$orange11" />
          <Text fontSize="$3" color="$orange11">Timer per offerta:</Text>
          <Select 
            value={offerTimeoutSeconds.toString()} 
            onValueChange={async (val) => {
              const newSeconds = Number(val)
              setLoading(true)
              try {
                await auctionService.changeTimer(auctionId, newSeconds)
                setOfferTimeoutSeconds(newSeconds)
                toast.show(`⏱️ Timer aggiornato a ${newSeconds}s`, { variant: 'success' })
              } catch (err) {
                toast.show(`❌ ${err instanceof Error ? err.message : 'Errore sconosciuto'}`, { variant: 'error' })
              } finally {
                setLoading(false)
              }
            }}
          >
            <Select.Trigger width={80}><Select.Value /></Select.Trigger>
            <Select.Content zIndex={200000}>
              <Select.Viewport>
                {[5, 10, 15, 20, 30, 45, 60].map((secs, idx) => (
                  <Select.Item key={secs} index={idx} value={secs.toString()}>
                    <Select.ItemText>{secs}s</Select.ItemText>
                  </Select.Item>
                ))}
              </Select.Viewport>
            </Select.Content>
          </Select>
        </XStack>
        <Separator />
        <Text fontSize="$4" fontWeight="600" color="$orange11">Gestione Giocatore</Text>
        <XStack gap="$2">
          <YStack flex={1}>
            <Select value={selectedRole.toString()} onValueChange={(val) => setSelectedRole(Number(val) as Role)}>
              <Select.Trigger iconAfter={ChevronDown}><Select.Value /></Select.Trigger>
              <Select.Content zIndex={200000}>
                <Select.Viewport>
                  {[Role.GoalKeeper, Role.Defensor, Role.Midfielder, Role.Forward].map((role, idx) => (
                    <Select.Item key={role} index={idx} value={role.toString()}>
                      <Select.ItemText>{getRoleLabel(role)}</Select.ItemText>
                    </Select.Item>
                  ))}
                </Select.Viewport>
              </Select.Content>
            </Select>
          </YStack>
          <Button icon={TrendingUp} theme="orange" flex={1} disabled={loading} onPress={async () => {
            setLoading(true)
            try {
              await auctionService.showNextPlayer(auctionId, selectedRole)
            } catch (err) {
              toast.show(`❌ ${err instanceof Error ? err.message : 'Errore sconosciuto'}`, { variant: 'error' })
            } finally {
              setLoading(false)
            }
          }}>Prossimo</Button>
        </XStack>
        <Button icon={Search} theme="orange" variant="outlined" onPress={() => setShowPlayerSearchDialog(true)}>Cerca Giocatore</Button>
        <Separator />
        {currentBidder && currentPlayer && (
          <>
            <Text fontSize="$4" fontWeight="600" color="$orange11">Aggiudica Giocatore</Text>
            <Card padding="$3" backgroundColor="$orange1">
              <Text fontSize="$3" color="$orange11">{currentPlayer.name} a {teams.find(t => t.value.owner === currentBidder)?.value.name} per €{currentBid}</Text>
            </Card>
            <Button icon={Gavel} theme="green" disabled={loading} onPress={async () => {
              setLoading(true)
              try {
                await auctionService.assignPlayerToTeam(auctionId, currentBidder, currentBid)
                toast.show('✅ Giocatore aggiudicato!', { variant: 'success' })
              } catch (err) {
                toast.show(`❌ ${err instanceof Error ? err.message : 'Errore sconosciuto'}`, { variant: 'error' })
              } finally {
                setLoading(false)
              }
            }}>Aggiudica</Button>
            <Separator />
          </>
        )}
        <Text fontSize="$4" fontWeight="600" color="$orange11">Controlli Asta</Text>
        <XStack gap="$2" flexWrap="wrap">
          {auctionState && auctionState.wrapper.status === AuctionStatus.InProgress ? (
            <Button icon={Pause} theme="orange" flex={1} disabled={loading} onPress={async () => {
              setLoading(true)
              try {
                await auctionService.pauseAuction(auctionId)
                toast.show('⏸️ Asta in pausa', { variant: 'info' })
              } catch (err) {
                toast.show(`❌ ${err instanceof Error ? err.message : 'Errore sconosciuto'}`, { variant: 'error' })
              } finally {
                setLoading(false)
              }
            }}>Pausa</Button>
          ) : (
            <Button icon={Play} theme="green" flex={1} disabled={loading} onPress={async () => {
              setLoading(true)
              try {
                await auctionService.resumeAuction(auctionId)
                toast.show('▶️ Asta ripresa', { variant: 'success' })
              } catch (err) {
                toast.show(`❌ ${err instanceof Error ? err.message : 'Errore sconosciuto'}`, { variant: 'error' })
              } finally {
                setLoading(false)
              }
            }}>Riprendi</Button>
          )}
          <Button icon={RotateCcw} theme="orange" variant="outlined" flex={1} onPress={() => {
            resetTimer()
            setCurrentBid(0)
            setCurrentBidder(null)
          }}>Reset Offerte</Button>
        </XStack>
        <Button icon={XCircle} theme="red" disabled={loading} onPress={async () => {
          if (Platform.OS === 'web' && !confirm('Sei sicuro di voler chiudere l\'asta?')) return
          setLoading(true)
          try {
            await auctionService.closeAuction(auctionId)
            toast.show('🏁 Asta chiusa', { variant: 'success' })
          } catch (err) {
            toast.show(`❌ ${err instanceof Error ? err.message : 'Errore sconosciuto'}`, { variant: 'error' })
          } finally {
            setLoading(false)
          }
        }}>Chiudi Asta</Button>
      </YStack>
    )

    const auctioneerCard = (
      <Card padding="$4" backgroundColor="$orange2" borderRadius="$4" borderWidth="$0.5" borderColor="$orange8" opacity={tempBlockButtons ? 0.6 : 1}>
        {renderAuctioneerBidding()}
        {renderAuctioneerControls()}
      </Card>
    )

    // Se banditore ha una squadra, mostra doppia pulsantiera (layout responsive)
    if (myTeam) {
      const StackComponent = media.gtMd ? XStack : YStack
      return (
        <StackComponent gap="$4" alignItems="flex-start">
          <YStack flex={1}>{renderPersonalBidding()}</YStack>
          <YStack flex={1}>{auctioneerCard}</YStack>
        </StackComponent>
      )
    }

    // Se banditore senza squadra, mostra solo pannello banditore
    return auctioneerCard
  }

  // Teams Panel Component  
  const renderTeams = () => {
    if (teams.length === 0) return null
    
    // Responsive grid: 5 teams per row on desktop, 2 on mobile
    // Width dinamica con massimo per mantenere 5 colonne su desktop
    const teamsPerRow = media.gtMd ? 5 : 2
    const gapSize = 12 // $3 = 12px
    const maxCardWidth = media.gtMd ? 240 : 180
    
    // Sort teams by budget (descending - from highest to lowest)
    const sortedTeams = [...teams].sort((a, b) => {
      const budgetA = calculateBudgetRemaining(a)
      const budgetB = calculateBudgetRemaining(b)
      return budgetB - budgetA // Descending order
    })
    
    return (
      <Card padding="$4" backgroundColor="$color2" borderRadius="$4" borderWidth="$0.5" borderColor="$borderColor">
        <YStack gap="$4">
          <XStack gap="$2" alignItems="center">
            <Users size={20} color="$color11" />
            <Text fontSize="$5" fontWeight="700" color="$color11">Squadre Partecipanti ({teams.length})</Text>
          </XStack>
          <XStack gap="$3" flexWrap="wrap" justifyContent="flex-start">
            {sortedTeams.map((team) => {
              const isMyTeam = user?.email && team?.value?.owner?.toLowerCase() === user.email.toLowerCase()
              const budget = calculateBudgetRemaining(team)
              const hasLowBudget = budget < 100
              
              // Count only active players (status === 0)
              const activePlayers = TeamHelper.getActivePlayers(team.value)
              const roleCount = {
                [Role.GoalKeeper]: activePlayers.filter(p => p.role === Role.GoalKeeper).length,
                [Role.Defensor]: activePlayers.filter(p => p.role === Role.Defensor).length,
                [Role.Midfielder]: activePlayers.filter(p => p.role === Role.Midfielder).length,
                [Role.Forward]: activePlayers.filter(p => p.role === Role.Forward).length,
              }
              return (
                <Card 
                  key={team.value.owner}
                  flex={1}
                  minWidth={media.gtMd ? 180 : 150}
                  maxWidth={maxCardWidth}
                  padding="$3" 
                  backgroundColor={isMyTeam ? '$blue2' : '$color1'} 
                  borderRadius="$3" 
                  borderWidth="$0.5" 
                  borderColor={isMyTeam ? '$blue8' : '$borderColor'}
                  pressStyle={{ scale: 0.98, opacity: 0.9 }}
                  cursor="pointer"
                  onPress={() => {
                    setSelectedTeamDetail(team)
                    setShowTeamDetailDialog(true)
                  }}
                >
                  <YStack gap="$2">
                    <XStack gap="$2" alignItems="center" justifyContent="space-between">
                      <Text fontSize="$4" fontWeight="700" color="$color11" numberOfLines={1} flex={1}>
                        {team.value.name}
                      </Text>
                      {isMyTeam && (
                        <Card padding="$1" paddingHorizontal="$2" backgroundColor="$blue8" borderRadius="$2">
                          <Text fontSize="$1" color="$blue1" fontWeight="700">TUA</Text>
                        </Card>
                      )}
                    </XStack>
                    <XStack gap="$2" flexWrap="wrap">
                      <YStack gap="$0.5" flex={1}>
                        <Text fontSize="$1" color="$color10">Budget</Text>
                        <Text fontSize="$3" fontWeight="600" color={hasLowBudget ? '$red11' : '$color11'}>€{budget}</Text>
                      </YStack>
                      <YStack gap="$0.5" flex={1}>
                        <Text fontSize="$1" color="$color10">Gioc.</Text>
                        <Text fontSize="$3" fontWeight="600" color="$color11">{activePlayers.length}/25</Text>
                      </YStack>
                    </XStack>
                    
                    {/* Budget Breakdown */}
                    <Card padding="$2" backgroundColor="$color3" borderRadius="$2">
                      <YStack gap="$1">
                        <Text fontSize="$1" color="$color10" fontWeight="600">Dettaglio Budget:</Text>
                        <XStack gap="$2" flexWrap="wrap" justifyContent="space-between">
                          <YStack gap="$0.5" flex={1} minWidth={80}>
                            <Text fontSize="$1" color="$green10">Budget Iniziale</Text>
                            <Text fontSize="$2" fontWeight="600" color="$green11">+€{startingMoney}</Text>
                          </YStack>
                          <YStack gap="$0.5" flex={1} minWidth={80}>
                            <Text fontSize="$1" color="$red10">Costo Totale</Text>
                            <Text fontSize="$2" fontWeight="600" color="$red11">-€{TeamHelper.getTotalCostForPlayers(team.value)}</Text>
                          </YStack>
                        </XStack>
                        <XStack gap="$2" flexWrap="wrap" justifyContent="space-between">
                          <YStack gap="$0.5" flex={1} minWidth={80}>
                            <Text fontSize="$1" color="$green10">Ricavi Vendite</Text>
                            <Text fontSize="$2" fontWeight="600" color="$green11">+€{TeamHelper.getRevenueMoney(team.value)}</Text>
                          </YStack>
                          <YStack gap="$0.5" flex={1} minWidth={80}>
                            <Text fontSize="$1" color="$green10">Da Classifica</Text>
                            <Text fontSize="$2" fontWeight="600" color="$green11">+€{team.value.moneyFromRank}</Text>
                          </YStack>
                        </XStack>
                        <XStack gap="$2" flexWrap="wrap" justifyContent="space-between">
                          <YStack gap="$0.5" flex={1} minWidth={80}>
                            <Text fontSize="$1" color="$green10">Infortunati/Ceduti</Text>
                            <Text fontSize="$2" fontWeight="600" color="$green11">+€{TeamHelper.getMoneyFromSoldWithOneHalfReturnedPrice(team.value)}</Text>
                          </YStack>
                        </XStack>
                      </YStack>
                    </Card>
                    
                    <Card padding="$2" backgroundColor="$color2" borderRadius="$2">
                      <XStack gap="$2" justifyContent="space-between">
                        <YStack gap="$0.5" alignItems="center">
                          <Text fontSize="$1" color="$color10">P</Text>
                          <Text fontSize="$2" fontWeight="600" color={roleCount[Role.GoalKeeper] >= ROLE_LIMITS[Role.GoalKeeper] ? '$red11' : '$color11'}>{roleCount[Role.GoalKeeper]}/{ROLE_LIMITS[Role.GoalKeeper]}</Text>
                        </YStack>
                        <YStack gap="$0.5" alignItems="center">
                          <Text fontSize="$1" color="$color10">D</Text>
                          <Text fontSize="$2" fontWeight="600" color={roleCount[Role.Defensor] >= ROLE_LIMITS[Role.Defensor] ? '$red11' : '$color11'}>{roleCount[Role.Defensor]}/{ROLE_LIMITS[Role.Defensor]}</Text>
                        </YStack>
                        <YStack gap="$0.5" alignItems="center">
                          <Text fontSize="$1" color="$color10">C</Text>
                          <Text fontSize="$2" fontWeight="600" color={roleCount[Role.Midfielder] >= ROLE_LIMITS[Role.Midfielder] ? '$red11' : '$color11'}>{roleCount[Role.Midfielder]}/{ROLE_LIMITS[Role.Midfielder]}</Text>
                        </YStack>
                        <YStack gap="$0.5" alignItems="center">
                          <Text fontSize="$1" color="$color10">A</Text>
                          <Text fontSize="$2" fontWeight="600" color={roleCount[Role.Forward] >= ROLE_LIMITS[Role.Forward] ? '$red11' : '$color11'}>{roleCount[Role.Forward]}/{ROLE_LIMITS[Role.Forward]}</Text>
                        </YStack>
                      </XStack>
                    </Card>
                  </YStack>
                </Card>
              )
            })}
          </XStack>
        </YStack>
      </Card>
    )
  }

  // Timer Component
  const renderTimer = () => {
    if (!timerRunning && !timerExpired) return null
    if (!currentPlayer) return null
    return (
      <YStack position="absolute" bottom="$4" right="$4" zIndex={1000}>
        <Card padding="$3" backgroundColor={timerExpired ? '$red8' : timerSecondsLeft <= 3 ? '$orange8' : '$blue8'} borderRadius="$6" shadowColor="$shadowColor" shadowOffset={{ width: 0, height: 2 }} shadowOpacity={0.25} shadowRadius={4}>
          <XStack gap="$2" alignItems="center">
            <Clock size={20} color="white" />
            <Text fontSize="$6" fontWeight="700" color="white">{timerExpired ? 'SCADUTO' : `${timerSecondsLeft}s`}</Text>
          </XStack>
        </Card>
      </YStack>
    )
  }

  // Custom Bid Dialog Component
  const renderCustomBidDialog = () => (
    <Dialog modal open={showCustomBidDialog} onOpenChange={setShowCustomBidDialog}>
      <Dialog.Portal>
        <Dialog.Overlay key="overlay" animation="quick" opacity={0.5} enterStyle={{ opacity: 0 }} exitStyle={{ opacity: 0 }} />
        <Dialog.Content bordered elevate key="content" animateOnly={['transform', 'opacity']} animation={['quick', { opacity: { overshootClamping: true } }]} enterStyle={{ x: 0, y: -20, opacity: 0, scale: 0.9 }} exitStyle={{ x: 0, y: 10, opacity: 0, scale: 0.95 }} gap="$4" width={400} maxWidth="90%">
          <Dialog.Title fontSize="$6" fontWeight="700">Offerta Personalizzata</Dialog.Title>
          <Dialog.Description>Inserisci l'importo della tua offerta (deve essere superiore a €{currentBid})</Dialog.Description>
          <YStack gap="$3">
            <Input placeholder="Importo (€)" value={customBidAmount} onChangeText={setCustomBidAmount} keyboardType="numeric" size="$5" />
            <XStack gap="$2" justifyContent="flex-end">
              <Dialog.Close asChild>
                <Button variant="outlined">Annulla</Button>
              </Dialog.Close>
              <Button theme="blue" onPress={() => {
                const amount = parseInt(customBidAmount, 10)
                if (!isNaN(amount) && amount > 0) {
                  placeBid(amount)
                  setCustomBidAmount('')
                  setShowCustomBidDialog(false)
                } else {
                  toast.show('❌ Inserisci un importo valido', { variant: 'error' })
                }
              }}>Conferma Offerta</Button>
            </XStack>
          </YStack>
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog>
  )

  // Team Detail Dialog Component
  const renderTeamDetailDialog = () => {
    if (!selectedTeamDetail) return null
    const team = selectedTeamDetail
    const budget = calculateBudgetRemaining(team)
    const spent = 1000 - budget
    const isMyTeam = user?.email && team?.value?.owner?.toLowerCase() === user.email.toLowerCase()
    
    // Count only active players
    const activePlayers = TeamHelper.getActivePlayers(team.value)
    
    // Sort players: orderBy status, thenBy role
    const sortedPlayers = [...team.value.players].sort((a, b) => {
      if (a.status !== b.status) return (a.status || 0) - (b.status || 0)
      return (a.role || 0) - (b.role || 0)
    })

    return (
      <Dialog modal open={showTeamDetailDialog} onOpenChange={setShowTeamDetailDialog}>
        <Dialog.Portal>
          <Dialog.Overlay key="overlay" animation="quick" opacity={0.5} enterStyle={{ opacity: 0 }} exitStyle={{ opacity: 0 }} />
          <Dialog.Content bordered elevate key="content" animateOnly={['transform', 'opacity']} animation={['quick', { opacity: { overshootClamping: true } }]} enterStyle={{ x: 0, y: -20, opacity: 0, scale: 0.9 }} exitStyle={{ x: 0, y: 10, opacity: 0, scale: 0.95 }} gap="$4" width={700} maxWidth="95%" maxHeight="90%">
            <Dialog.Title fontSize="$6" fontWeight="700">{team.value.name}</Dialog.Title>
            <Dialog.Description>Dettaglio squadra - {team.value.owner}</Dialog.Description>
            <YStack maxHeight={600} overflow="scroll">
              <YStack gap="$4" padding="$2">
                <Card padding="$3" backgroundColor={isMyTeam ? '$blue2' : '$color2'} borderRadius="$3">
                  <XStack gap="$4" flexWrap="wrap">
                    <YStack gap="$1">
                      <Text fontSize="$2" color="$color10">Speso</Text>
                      <Text fontSize="$5" fontWeight="700" color="$color11">€{spent}</Text>
                    </YStack>
                    <YStack gap="$1">
                      <Text fontSize="$2" color="$color10">Budget Rimasto</Text>
                      <Text fontSize="$5" fontWeight="700" color={budget < 100 ? '$red11' : '$color11'}>€{budget}</Text>
                    </YStack>
                    <YStack gap="$1">
                      <Text fontSize="$2" color="$color10">Giocatori</Text>
                      <Text fontSize="$5" fontWeight="700" color="$color11">{activePlayers.length}/25</Text>
                    </YStack>
                    <YStack gap="$1">
                      <Text fontSize="$2" color="$color10">Slot Rimasti</Text>
                      <Text fontSize="$5" fontWeight="700" color="$color11">{25 - activePlayers.length}</Text>
                    </YStack>
                  </XStack>
                </Card>
                <YStack gap="$2">
                  <Text fontSize="$4" fontWeight="700" color="$color11">Rosa ({sortedPlayers.length})</Text>
                  {sortedPlayers.length === 0 ? (
                    <Card padding="$3" backgroundColor="$color2">
                      <Text fontSize="$3" color="$color10" textAlign="center">Nessun giocatore ancora</Text>
                    </Card>
                  ) : (
                    <YStack gap="$2">
                      {sortedPlayers.map((player) => (
                        <Card key={player.name} padding="$3" backgroundColor="$color1" borderRadius="$3">
                          <XStack gap="$2" alignItems="center" justifyContent="space-between" flexWrap="wrap">
                            <YStack gap="$1" flex={1}>
                              <Text fontSize="$4" fontWeight="600" color="$color11">{player.name}</Text>
                              <XStack gap="$2" flexWrap="wrap">
                                <Text fontSize="$2" color="$color10">{getRoleLabel(player.role)} • {player.team.name}</Text>
                                <Text fontSize="$2" fontWeight="600" color={getPlayerStatusColor(player.status || 0)}>
                                  {getPlayerStatusLabel(player.status || 0)}
                                </Text>
                                {(player.status === 3 || player.status === 1) && player.revenue !== undefined && player.revenue !== 0 && (
                                  <Text fontSize="$2" fontWeight="600" color={player.revenue > 0 ? '$green11' : '$red11'}>
                                    ({player.revenue > 0 ? '+' : ''}{player.revenue}€)
                                  </Text>
                                )}
                              </XStack>
                            </YStack>
                            <XStack gap="$2" alignItems="center">
                              <Text fontSize="$4" fontWeight="700" color="$color11">€{player.price}</Text>
                              {isAuctioneer && (
                                <Button 
                                  size="$2" 
                                  theme="orange" 
                                  variant="outlined"
                                  onPress={() => {
                                    setChangeStatusPlayer({ playerKey: player.name, playerName: player.name, owner: team.value.owner })
                                    setChangeStatusType(player.status || 0)
                                    setChangeStatusRevenue(player.revenue?.toString() || '')
                                    setShowChangeStatusDialog(true)
                                  }}
                                >
                                  Modifica
                                </Button>
                              )}
                            </XStack>
                          </XStack>
                        </Card>
                      ))}
                    </YStack>
                  )}
                </YStack>
              </YStack>
            </YStack>
            <XStack justifyContent="flex-end">
              <Dialog.Close asChild>
                <Button>Chiudi</Button>
              </Dialog.Close>
            </XStack>
          </Dialog.Content>
        </Dialog.Portal>
      </Dialog>
    )
  }

  // Change Player Status Dialog Component
  const renderChangeStatusDialog = () => {
    if (!changeStatusPlayer) return null
    const needsRevenue = changeStatusType === 3 // Sold status
    
    return (
      <Dialog modal open={showChangeStatusDialog} onOpenChange={setShowChangeStatusDialog}>
        <Dialog.Portal>
          <Dialog.Overlay key="overlay" animation="quick" opacity={0.5} enterStyle={{ opacity: 0 }} exitStyle={{ opacity: 0 }} />
          <Dialog.Content bordered elevate key="content" animateOnly={['transform', 'opacity']} animation={['quick', { opacity: { overshootClamping: true } }]} enterStyle={{ x: 0, y: -20, opacity: 0, scale: 0.9 }} exitStyle={{ x: 0, y: 10, opacity: 0, scale: 0.95 }} gap="$4" width={500} maxWidth="95%">
            <Dialog.Title fontSize="$6" fontWeight="700">Cambia Stato Giocatore</Dialog.Title>
            <Dialog.Description>{changeStatusPlayer.playerName}</Dialog.Description>
            <YStack gap="$3">
              <YStack gap="$2">
                <Label htmlFor="status-select" fontSize="$4">Motivo cambio stato:</Label>
                <Select id="status-select" value={changeStatusType.toString()} onValueChange={(val) => setChangeStatusType(Number(val))}>
                  <Select.Trigger iconAfter={ChevronDown}><Select.Value /></Select.Trigger>
                  <Select.Content zIndex={200000}>
                    <Select.Viewport>
                      <Select.Item index={0} value="0"><Select.ItemText>Attivo (ripristina)</Select.ItemText></Select.Item>
                      <Select.Item index={1} value="1"><Select.ItemText>Errore di caricamento (rimosso)</Select.ItemText></Select.Item>
                      <Select.Item index={2} value="2"><Select.ItemText>Infortunato/Estero (1 girone)</Select.ItemText></Select.Item>
                      <Select.Item index={3} value="3"><Select.ItemText>Venduto ad altro giocatore</Select.ItemText></Select.Item>
                    </Select.Viewport>
                  </Select.Content>
                </Select>
              </YStack>
              {needsRevenue && (
                <YStack gap="$2">
                  <Label htmlFor="revenue-input" fontSize="$4">Importo vendita (+ guadagno / - perdita):</Label>
                  <Input 
                    id="revenue-input"
                    placeholder="Es: 50 o -30" 
                    value={changeStatusRevenue} 
                    onChangeText={setChangeStatusRevenue} 
                    keyboardType="numeric" 
                    size="$4" 
                  />
                  <Text fontSize="$2" color="$color10">Lascia vuoto o 0 se nessun importo da registrare</Text>
                </YStack>
              )}
              <Card padding="$3" backgroundColor="$blue2" borderRadius="$3">
                <YStack gap="$2">
                  <Text fontSize="$3" fontWeight="600" color="$blue11">Spiegazione stati:</Text>
                  <Text fontSize="$2" color="$blue11">• <Text fontWeight="600">Errore di caricamento:</Text> Giocatore rimosso definitivamente</Text>
                  <Text fontSize="$2" color="$blue11">• <Text fontWeight="600">Infortunato/Estero:</Text> Venduto per un girone (torna secondo girone)</Text>
                  <Text fontSize="$2" color="$blue11">• <Text fontWeight="600">Venduto:</Text> Venduto ad un altro giocatore (con/senza prezzo)</Text>
                </YStack>
              </Card>
              <XStack gap="$2" justifyContent="flex-end">
                <Dialog.Close asChild>
                  <Button variant="outlined" onPress={() => {
                    setChangeStatusPlayer(null)
                    setChangeStatusRevenue('')
                  }}>Annulla</Button>
                </Dialog.Close>
                <Button theme="orange" disabled={loading} onPress={async () => {
                  setLoading(true)
                  try {
                    let revenue: number | null = null
                    if (needsRevenue && changeStatusRevenue) {
                      const parsed = parseInt(changeStatusRevenue, 10)
                      if (!isNaN(parsed)) revenue = parsed
                    }
                    
                    await auctionService.changePlayerStatus(
                      auctionId, 
                      changeStatusPlayer.owner, 
                      changeStatusPlayer.playerKey, 
                      changeStatusType, 
                      revenue
                    )
                    
                    toast.show('✅ Stato giocatore aggiornato!', { variant: 'success' })
                    setShowChangeStatusDialog(false)
                    setChangeStatusPlayer(null)
                    setChangeStatusRevenue('')
                  } catch (err) {
                    toast.show(`❌ ${err instanceof Error ? err.message : 'Errore sconosciuto'}`, { variant: 'error' })
                  } finally {
                    setLoading(false)
                  }
                }}>Conferma</Button>
              </XStack>
            </YStack>
          </Dialog.Content>
        </Dialog.Portal>
      </Dialog>
    )
  }

  // ============================================
  // MAIN RENDER
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
                    <Text color="$red10" fontSize="$3" marginTop="$2">Possibili cause:</Text>
                    <Text color="$red10" fontSize="$3">• Il server non è raggiungibile</Text>
                    <Text color="$red10" fontSize="$3">• Problemi di autenticazione</Text>
                    <Text color="$red10" fontSize="$3">• Timeout di connessione</Text>
                  </YStack>
                </Card>
                <Button onPress={() => { setConnectionError(null); setReconnectAttempts(0) }} theme="blue" icon={RotateCcw}>Riprova Connessione</Button>
              </YStack>
            ) : (
              <>
                <Spinner size="large" color="$blue10" />
                <Text fontSize="$4" color="$color11">{connectionState === 'Connecting' ? 'Connessione all\'asta...' : 'Disconnesso'}</Text>
                {reconnectAttempts > 0 && <Text fontSize="$3" color="$color10">Tentativo {reconnectAttempts} di 3...</Text>}
              </>
            )}
          </YStack>
        </ScreenContent>
      </View>
    )
  }

  return (
    <View style={{ flex: 1 }}>
      <ScreenContent path="screens/auction-live.tsx" title="Asta Live">
        <ScrollView>
          <YStack padding="$4" gap="$6">
            <YStack width="100%" gap="$4">
              {renderHeader()}
              {renderCurrentPlayer()}
              {renderParticipantBidding()}
              {renderAuctioneerPanel()}
              {renderTeams()}
            </YStack>
          </YStack>
        </ScrollView>
        {renderTimer()}
        {renderCustomBidDialog()}
        {renderTeamDetailDialog()}
        {renderChangeStatusDialog()}
      </ScreenContent>
    </View>
  )
}
