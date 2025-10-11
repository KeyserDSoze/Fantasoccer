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
  StatPlayer,
  AuctionWin,
  AuctionType,
  AuctionKind,
  AuctionTypeLabels,
  AuctionKindLabels,
  ActualAuction,
  PlayerAssignedMessage,
  BidPlaced
} from 'types/auction'
import { Role } from 'types/group'
import { 
  Gavel, Users, TrendingUp, Pause, Play, XCircle, ChevronDown, 
  Clock, Lock, RotateCcw, Search, DollarSign, 
  AlertCircle, Volume2, VolumeX, Vibrate
} from '@tamagui/lucide-icons'
import { useRoute, RouteProp, useNavigation } from '@react-navigation/native'
import { RootStackParamList } from 'navigation'
import { useAuth } from 'providers/AuthProvider'
import { Team, TeamKey, TeamHelper } from 'types/team'
import { Entity } from 'types/default'
import { getPlayerKey } from 'utils'
import { 
  getRoleLabel, 
  getAuctionStatusLabel as getStatusLabel, 
  getAuctionStatusColor as getStatusColor,
  playBidSound as playBidSoundUtil
} from 'utils/auctionHelper'
import { 
  AuctioneerPanel, 
  PersonalBiddingPanel, 
  CurrentPlayerCard,
  TeamsPanel,
  CustomBidDialog,
  TeamDetailDialog,
  ChangeStatusDialog,
  SubstituteSelectionDialog,
  PlayerSearchDialog
} from 'components/auction'

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
  const navigation = useNavigation()

  // ============================================
  // STATE
  // ============================================
  const [connectionState, setConnectionState] = useState<'Disconnected' | 'Connecting' | 'Connected'>('Disconnected')
  const [connectionError, setConnectionError] = useState<string | null>(null)
  const reconnectAttemptsRef = useRef(0)
  const [connectionTrigger, setConnectionTrigger] = useState(0) // Used to trigger reconnection
  const [auctionState, setAuctionState] = useState<AuctionState | null>(null)
  const [currentPlayer, setCurrentPlayer] = useState<StatPlayer | null>(null)
  const [teams, setTeams] = useState<Entity<Team, TeamKey>[]>([])
  const [currentBid, setCurrentBid] = useState<number>(0)
  const [currentBidder, setCurrentBidder] = useState<string | null>(null)
  const [currentSubstitute, setCurrentSubstitute] = useState<string | null>(null)
  const [mySelectedSubstitute, setMySelectedSubstitute] = useState<string | null>(null) // Player to substitute for personal bids
  const [auctioneerSelectedSubstitute, setAuctioneerSelectedSubstitute] = useState<string | null>(null) // Player to substitute for auctioneer bids
  const [showSubstituteSelectionDialog, setShowSubstituteSelectionDialog] = useState(false)
  const [isSubstituteSelectionForAuctioneer, setIsSubstituteSelectionForAuctioneer] = useState(false)
  const [isLocked, setIsLocked] = useState(false)
  const [salesHistory, setSalesHistory] = useState<AuctionWin[]>([])
  const [isAuctioneer, setIsAuctioneer] = useState(false)
  const [myTeam, setMyTeam] = useState<Entity<Team, TeamKey> | null>(null)
  const [startingMoney, setStartingMoney] = useState<number>(1000) // League's starting budget
  const [myTeamBudget, setMyTeamBudget] = useState<number>(1000)
  const [timerSecondsLeft, setTimerSecondsLeft] = useState<number>(0)
  const [offerTimeoutSeconds, setOfferTimeoutSeconds] = useState<number>(10)
  const offerTimeoutSecondsRef = useRef<number>(10) // Ref to always have latest value
  const [timerRunning, setTimerRunning] = useState<boolean>(false)
  const [timerExpired, setTimerExpired] = useState<boolean>(false)
  const [firstOfferHappened, setFirstOfferHappened] = useState<boolean>(false)
  const timerIntervalRef = useRef<NodeJS.Timeout | null>(null)
  const [selectedRole, setSelectedRole] = useState<Role>(Role.GoalKeeper)
  const [selectedTeamForBid, setSelectedTeamForBid] = useState<string>('')
  const [showCustomBidDialog, setShowCustomBidDialog] = useState(false)
  const [isCustomBidFromAuctioneer, setIsCustomBidFromAuctioneer] = useState(false) // Track if custom bid is from auctioneer panel
  const [showPlayerSearchDialog, setShowPlayerSearchDialog] = useState(false)
  const [showTeamDetailDialog, setShowTeamDetailDialog] = useState(false)
  const [selectedTeamDetail, setSelectedTeamDetail] = useState<Entity<Team, TeamKey> | null>(null)
  const [showChangeStatusDialog, setShowChangeStatusDialog] = useState(false)
  const [changeStatusPlayer, setChangeStatusPlayer] = useState<{ playerKey: string; playerName: string; owner: string } | null>(null)
  const [playerSearchQuery, setPlayerSearchQuery] = useState('')
  const [selectedPlayerForForce, setSelectedPlayerForForce] = useState<string>('')
  const [loading, setLoading] = useState(false)
  const [tempBlockButtons, setTempBlockButtons] = useState(false)
  const [audioEnabled, setAudioEnabled] = useState(true)
  const [vibrationEnabled, setVibrationEnabled] = useState(true)
  const audioContextRef = useRef<AudioContext | null>(null)
  const connectionAttemptedRef = useRef<boolean>(false)
  const isConnectingRef = useRef<boolean>(false)

  // Initialize audio context
  useEffect(() => {
    if (Platform.OS === 'web') {
      audioContextRef.current = new (window.AudioContext || (window as any).webkitAudioContext)()
    }
    return () => {
      audioContextRef.current?.close()
    }
  }, [])

  const playBidSound = useCallback(() => {
    if (Platform.OS !== 'web') return
    playBidSoundUtil(audioContextRef.current, audioEnabled)
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

  // Helper: Check if auction is repair auction (requires substitution)
  const isRepairAuction = useCallback((): boolean => {
    return auctionState?.wrapper.king === 1 // 1 = Repair auction (based on AuctionKind enum)
  }, [auctionState])

  // Helper: Update auction state from API response
  const updateAuctionState = useCallback((state: AuctionState) => {
    console.log('📦 Updating auction state...')
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
    console.log('⏱️ Setting offer timeout to', auctionTimer, 'seconds')
    setOfferTimeoutSeconds(auctionTimer)
    offerTimeoutSecondsRef.current = auctionTimer // Keep ref in sync
    
    const myTeamEntity = state.teams.find(t => t.value.owner.toLowerCase() === user?.email?.toLowerCase())
    if (myTeamEntity) {
      setMyTeam(myTeamEntity)
      const budget = leagueStartingMoney - TeamHelper.getCost(myTeamEntity.value)
      setMyTeamBudget(budget)
    }
    
    if (state.wrapper.actual) {
      setCurrentPlayer(state.wrapper.actual.player)
      setCurrentBid(state.wrapper.actual.price)
      setCurrentBidder(state.wrapper.actual.owner)
      setCurrentSubstitute(state.wrapper.actual.substituted)
    }
    
    console.log('✅ Auction state updated successfully')
  }, [user?.email])

  // ============================================
  // TIMER LOGIC
  // ============================================
  const startTimer = useCallback(() => {
    console.log('⏱️ Starting timer...')
    const timeout = offerTimeoutSecondsRef.current // Use ref for latest value
    setTimerSecondsLeft(timeout)
    console.log('⏱️ Timer set to', timeout, 'seconds')
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
  }, []) // No dependencies - always uses latest ref value

  const resetTimer = useCallback(() => {
    if (timerIntervalRef.current) clearInterval(timerIntervalRef.current)
    const timeout = offerTimeoutSecondsRef.current // Use ref for latest value
    setTimerSecondsLeft(timeout)
    setTimerRunning(false)
    setTimerExpired(false)
    setFirstOfferHappened(false)
  }, [])

  useEffect(() => {
    return () => {
      if (timerIntervalRef.current) clearInterval(timerIntervalRef.current)
    }
  }, [])

  // Update selectedTeamDetail when teams change (after AuctionUpdated event)
  useEffect(() => {
    if (selectedTeamDetail && teams.length > 0) {
      const updatedTeam = teams.find(t => t.value.owner === selectedTeamDetail.value.owner)
      if (updatedTeam) {
        setSelectedTeamDetail(updatedTeam)
      }
    }
  }, [teams, selectedTeamDetail])

  // Reset substitute selection ONLY when player role changes (not when player doesn't exist)
  useEffect(() => {
    if (currentPlayer) {
      // Check if selected substitute has different role than current player
      // Keep substitute even if player doesn't exist in team (was just sold)
      if (mySelectedSubstitute && myTeam) {
        const player = myTeam.value.players.find(p => p.name === mySelectedSubstitute)
        // Only reset if player exists AND has different role
        if (player && player.role !== currentPlayer.role) {
          setMySelectedSubstitute(null)
        }
      }
      if (auctioneerSelectedSubstitute && selectedTeamForBid) {
        const team = teams.find(t => t.value.owner === selectedTeamForBid)
        const player = team?.value.players.find(p => p.name === auctioneerSelectedSubstitute)
        // Only reset if player exists AND has different role
        if (player && player.role !== currentPlayer.role) {
          setAuctioneerSelectedSubstitute(null)
        }
      }
    }
  }, [currentPlayer, mySelectedSubstitute, auctioneerSelectedSubstitute, myTeam, selectedTeamForBid, teams])

  // ============================================
  // BID LOGIC
  // ============================================
  const placeBid = useCallback(async (amount: number, isAuctioneerBid: boolean = false) => {
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
    // Determine which team is bidding based on context:
    // - If isAuctioneerBid=true, use selectedTeamForBid (auctioneer panel)
    // - Otherwise, use myTeam (personal panel)
    const biddingTeam = isAuctioneerBid && selectedTeamForBid
      ? teams.find(t => t.value.owner === selectedTeamForBid)
      : myTeam
      
    if (!biddingTeam) {
      toast.show('❌ Nessuna squadra selezionata', { variant: 'error' })
      return
    }
    
    // Check if repair auction requires a substitute player
    // Only require substitute if team has reached the role limit
    if (isRepairAuction() && currentPlayer.role !== undefined) {
      const limit = ROLE_LIMITS[currentPlayer.role] || 0
      const activePlayers = TeamHelper.getActivePlayers(biddingTeam.value)
      const currentCount = activePlayers.filter(p => p.role === currentPlayer.role).length
      
      // Only require substitute if team has reached or exceeded limit
      if (currentCount >= limit) {
        const selectedSubstitute = isAuctioneerBid ? auctioneerSelectedSubstitute : mySelectedSubstitute
        if (!selectedSubstitute) {
          toast.show('❌ Limite ruolo raggiunto - Seleziona un giocatore da sostituire', { variant: 'error' })
          return
        }
      }
    } else if (currentPlayer.role && hasReachedRoleLimit(currentPlayer.role)) {
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
      // Only pass bidderEmail if it's an auctioneer bid (for someone else's team)
      const bidderEmail = isAuctioneerBid ? biddingTeam.value.owner : undefined
      // Choose which substitute to send based on context:
      // - Personal bid (bidderEmail undefined) → use mySelectedSubstitute
      // - Auctioneer bid (bidderEmail specified) → use auctioneerSelectedSubstitute
      // Convert player name to playerKey using utility
      const selectedSubstituteName = isRepairAuction() 
        ? (isAuctioneerBid ? auctioneerSelectedSubstitute : mySelectedSubstitute) 
        : undefined
      
      const substitutedPlayerKey = selectedSubstituteName ? getPlayerKey(selectedSubstituteName) : undefined
      await auctionService.placeBid(auctionId, amount, bidderEmail, substitutedPlayerKey)
    } catch (err) {
      toast.show(`❌ ${err instanceof Error ? err.message : 'Errore sconosciuto'}`, { variant: 'error' })
      setTempBlockButtons(false)
    } finally {
      setLoading(false)
    }
  }, [auctionId, currentPlayer, isLocked, isAuctioneer, currentBid, currentBidder, user?.email, selectedTeamForBid, teams, myTeam, hasReachedRoleLimit, calculateBudgetRemaining, getRoleLabel, toast, isRepairAuction, mySelectedSubstitute, auctioneerSelectedSubstitute])

  const handleIncrementBid = useCallback((increment: number, isAuctioneerBid: boolean = false) => {
    placeBid(currentBid + increment, isAuctioneerBid)
  }, [currentBid, placeBid])

  const handleFixedBid = useCallback((amount: number, isAuctioneerBid: boolean = false) => {
    placeBid(amount, isAuctioneerBid)
  }, [placeBid])

  // Check if personal bidding buttons should be disabled (for myTeam)
  const arePersonalBidButtonsDisabled = useCallback((): { disabled: boolean; reason: string | null } => {
    if (!currentPlayer) return { disabled: true, reason: 'Nessun giocatore in asta' }
    if (tempBlockButtons) return { disabled: true, reason: null }
    if (isLocked && !isAuctioneer) return { disabled: true, reason: 'Asta in pausa' }
    if (timerExpired && !isAuctioneer) return { disabled: true, reason: 'Tempo scaduto - Solo il banditore può agire' }
    if (!isAuctioneer && currentBidder?.toLowerCase() === user?.email?.toLowerCase()) {
      return { disabled: true, reason: 'Sei già il miglior offerente' }
    }
    
    // Check role limits for myTeam
    if (myTeam && currentPlayer.role >= 0) {
      const limit = ROLE_LIMITS[currentPlayer.role] || 0
      const activePlayers = TeamHelper.getActivePlayers(myTeam.value)
      // If there's a selected substitute, exclude it from the count (it will be removed)
      let currentCount = activePlayers.filter(p => p.role === currentPlayer.role).length
      if (mySelectedSubstitute && isRepairAuction()) {
        const substitutePlayer = activePlayers.find(p => p.name === mySelectedSubstitute && p.role === currentPlayer.role)
        if (substitutePlayer) {
          currentCount -= 1 // Exclude the substitute from the count
        }
      }
      if (currentCount >= limit) {
        return { disabled: true, reason: `Limite ruolo raggiunto per ${getRoleLabel(currentPlayer.role)} (${currentCount}/${limit})` }
      }
    }
    
    return { disabled: false, reason: null }
  }, [currentPlayer, tempBlockButtons, isLocked, isAuctioneer, timerExpired, currentBidder, user?.email, myTeam, getRoleLabel, mySelectedSubstitute, isRepairAuction])

  // Check if auctioneer bidding buttons should be disabled (for selectedTeamForBid)
  const areAuctioneerBidButtonsDisabled = useCallback((): { disabled: boolean; reason: string | null } => {
    if (!currentPlayer) return { disabled: true, reason: 'Nessun giocatore in asta' }
    if (tempBlockButtons) return { disabled: true, reason: null }
    if (!selectedTeamForBid) return { disabled: true, reason: 'Seleziona una squadra per fare un\'offerta' }
    
    // Check if selectedTeamForBid is already the current bidder
    if (currentBidder && selectedTeamForBid.toLowerCase() === currentBidder.toLowerCase()) {
      return { disabled: true, reason: 'Questa squadra è già il miglior offerente' }
    }
    
    // Check role limits for selectedTeamForBid
    if (currentPlayer.role >= 0) {
      const selectedTeam = teams.find(t => t.value.owner === selectedTeamForBid)
      if (selectedTeam) {
        const limit = ROLE_LIMITS[currentPlayer.role] || 0
        const activePlayers = TeamHelper.getActivePlayers(selectedTeam.value)
        // If there's a selected substitute for auctioneer, exclude it from the count (it will be removed)
        let currentCount = activePlayers.filter(p => p.role === currentPlayer.role).length
        if (auctioneerSelectedSubstitute && isRepairAuction()) {
          const substitutePlayer = activePlayers.find(p => p.name === auctioneerSelectedSubstitute && p.role === currentPlayer.role)
          if (substitutePlayer) {
            currentCount -= 1 // Exclude the substitute from the count
          }
        }
        if (currentCount >= limit) {
          return { disabled: true, reason: `${selectedTeam.value.name}: Limite ruolo raggiunto per ${getRoleLabel(currentPlayer.role)} (${currentCount}/${limit})` }
        }
      }
    }
    
    return { disabled: false, reason: null }
  }, [currentPlayer, tempBlockButtons, selectedTeamForBid, teams, getRoleLabel, currentBidder, auctioneerSelectedSubstitute, isRepairAuction])

  // ============================================
  // SIGNALR CONNECTION
  // ============================================
  
  // Check if user authentication fails/times out
  useEffect(() => {
    // If we're still waiting for user after 5 seconds, redirect to login
    const authCheckTimeout = setTimeout(async () => {
      if (!user?.email) {
        setConnectionError('Autenticazione necessaria. Reindirizzamento alla pagina di login...')
        
        // Save return URL
        const returnUrl = `/auction/${auctionId}`
        
        setTimeout(async () => {
          const { returnUrlService } = await import('../services/returnUrlService')
          await returnUrlService.saveReturnUrl(returnUrl)
          
          navigation.navigate('Login' as any)
        }, 2000)
      }
    }, 5000) // Wait 5 seconds for auth to complete
    
    return () => clearTimeout(authCheckTimeout)
  }, [user?.email, auctionId, navigation])
  
  useEffect(() => {
    if (!user?.email) {
      console.log('⏸️ Connection useEffect skipped - no user email')
      return
    }
    
    // Reset refs at the start of each effect run to allow reconnection
    // This fixes hot reload issues where refs persist between module reloads
    const wasAttempted = connectionAttemptedRef.current
    const wasConnecting = isConnectingRef.current
    
    if (wasAttempted || wasConnecting) {
      console.log('🔄 Resetting connection refs for new attempt', {
        wasAttempted,
        wasConnecting
      })
      connectionAttemptedRef.current = false
      isConnectingRef.current = false
    }
    
    console.log('🚀 Connection useEffect starting...', { auctionId, userEmail: user.email })
    
    let isSubscribed = true
    
    setConnectionState('Connecting')
    
    const connectAndJoin = async () => {
      // Double-check to prevent race conditions
      if (isConnectingRef.current) {
        console.log('⏸️ Already connecting, skipping...')
        return
      }
      
      isConnectingRef.current = true
      connectionAttemptedRef.current = true
      
      try {
        // CRITICAL: Disconnect any existing connection before attempting new one
        // This fixes hot reload issues where old connection lingers
        if (auctionService.isConnected()) {
          console.log('🔌 Disconnecting existing connection before reconnecting...')
          await auctionService.disconnect()
          // Small delay to ensure cleanup
          await new Promise(resolve => setTimeout(resolve, 100))
        }
        
        // Setup event handlers BEFORE connecting
        auctionService.setEventHandlers({
          onConnected: (connectionId) => {
            console.log('✅ Connected to auction hub, connectionId:', connectionId)
            if (isSubscribed) {
              setConnectionState('Connected')
              reconnectAttemptsRef.current = 0
            }
          },
          onReconnecting: () => {
            console.log('🔄 SignalR reconnecting...')
            if (isSubscribed) {
              setConnectionState('Connecting')
              toast.show('🔄 Riconnessione in corso...', { variant: 'info', duration: 2000 })
            }
          },
          onReconnected: async (connectionId) => {
            console.log('✅ SignalR reconnected, connectionId:', connectionId)
            
            if (isSubscribed) {
              setConnectionState('Connected')
              toast.show('✅ Riconnesso - caricamento stato asta...', { variant: 'success', duration: 3000 })
             
              try {
                const auctionState = await auctionService.getAuction(auctionId)
                updateAuctionState(auctionState)
              } catch (error) {
                console.error('❌ Failed to reload state after reconnect:', error)
                toast.show('⚠️ Errore ricaricamento stato', { variant: 'error', duration: 3000 })
              }
            }
          },
          onDisconnected: (error) => {
            console.log('🔌 SignalR disconnected', error)
            if (isSubscribed) {
              setConnectionState('Disconnected')
              
              // Only show error if it was unexpected (not during normal navigation away)
              if (error) {
                const errorMsg = error.message || 'Unknown error'
                if (errorMsg.includes('401') || errorMsg.includes('Unauthorized')) {
                  setConnectionError('Sessione scaduta - ricarica la pagina')
                } else {
                  toast.show('⚠️ Connessione persa - riconnessione automatica in corso...', { variant: 'warning', duration: 3000 })
                }
              }
            }
          },
          onError: (error) => {
            if (isSubscribed) toast.show(`❌ ${error || 'Errore del server'}`, { variant: 'error', duration: 4000 })
          },
          onUserJoined: (email) => toast.show(`👋 Utente connesso: ${email}`, { variant: 'info', duration: 4000 }),
          onUserLeft: (email) => toast.show(`👋 L'utente ${email} si è disconnesso`, { variant: 'info', duration: 4000 }),
          onPlayerShown: (actualAuction: ActualAuction) => {
            if (isSubscribed) {
              // Check if role changed - if so, reset substitutes
              const roleChanged = currentPlayer && currentPlayer.role !== actualAuction.player.role
              console.log(currentPlayer, actualAuction, 'roleChanged:', roleChanged)
              setCurrentPlayer(actualAuction.player)
              setCurrentBid(0)
              setCurrentBidder(null)
              setCurrentSubstitute(null)
              
              // Reset selected substitutes only if role changed
              if (roleChanged) {
                setMySelectedSubstitute(null)
                setAuctioneerSelectedSubstitute(null)
              }
              
              // Reset player search dialog state
              setPlayerSearchQuery('')
              setSelectedPlayerForForce('')
              
              resetTimer()
            }
          },
          onPlayerAssigned: (data: PlayerAssignedMessage) => {
            if (isSubscribed) {
              playBidSound()
              vibrateOnBid()
              toast.show(`✅ Giocatore assegnato: ${data.playerAssignedInAuction?.playerName} a ${data.playerAssignedInAuction?.teamName} per €${data.playerAssignedInAuction?.price}. Ha preso ${data.playerAssignedInAuction?.teamPlayersCount} giocatori nella squadra.`, { variant: 'success', duration: 4000 })
              
              // Update the team that won the auction with the updated team data
              if (data.team) {
                setTeams(prevTeams => 
                  prevTeams.map(team => 
                    team.value.owner === data.team!.value.owner ? data.team! : team
                  )
                )
                
                // Update myTeam if I won the auction
                if (user?.email && data.team.value.owner.toLowerCase() === user.email.toLowerCase()) {
                  setMyTeam(data.team)
                  const budget = startingMoney - TeamHelper.getCost(data.team.value)
                  setMyTeamBudget(budget)
                }
              }
              
              // Reset substitute for the winner (player is no longer available - was removed/substituted)
              // Check if winner is myTeam or selectedTeamForBid
              // If I won the auction, reset my substitute
              if (user?.email && data.playerAssignedInAuction?.owner.toLowerCase() === user.email.toLowerCase()) {
                setMySelectedSubstitute(null)
              }
              // If auctioneer's selected team won, reset auctioneer substitute
              if (selectedTeamForBid && data.playerAssignedInAuction?.owner.toLowerCase() === selectedTeamForBid.toLowerCase()) {
                setAuctioneerSelectedSubstitute(null)
              }
              
              console.log('📦 onPlayerAssigned data:', data)
              setCurrentPlayer(null)
              setCurrentBid(0)
              setCurrentBidder(null)
              setCurrentSubstitute(null)
              // Don't reset selectedTeamForBid - auctioneer keeps working with same team
              resetTimer()
            }
          },
          onBidPlaced: (data: BidPlaced) => {
            if (isSubscribed) {
              setCurrentBid(data.amount)
              setCurrentBidder(data.bidderEmail)
              setCurrentSubstitute(data.substituted)
              // if (data.bidderEmail.toLowerCase() !== user?.email?.toLowerCase()) {
                playBidSound()
                vibrateOnBid()
              // }
              startTimer()
            }
          },
          onAuctionPaused: () => {
            toast.show('⏸️ Asta in pausa', { variant: 'info', duration: 4000 })
            if (isSubscribed) setIsLocked(true)
          },
          onAuctionResumed: () => {
            toast.show('▶️ Asta ripresa', { variant: 'info', duration: 4000 })
            if (isSubscribed) setIsLocked(false)
          },
          onAuctionClosed: () => {
            toast.show('🏁 Asta chiusa', { variant: 'info', duration: 4000 })
            if (isSubscribed) setConnectionError('L\'asta è stata chiusa')
          },
          onCurrentAuctionReset: () => {
            if (isSubscribed) {
              setCurrentBid(0)
              setCurrentBidder(null)
              setCurrentSubstitute(null)
              resetTimer()
              toast.show('🔄 Asta resettata - tutte le offerte cancellate', { variant: 'info', duration: 1000 })
            }
          },
          onTimerReset: () => {
            if (isSubscribed) {
              startTimer()
              toast.show('⏱️ Timer riavviato', { variant: 'info', duration: 1000 })
            }
          },
          onTeamUpdated: (team: Entity<Team, TeamKey>) => {
            if (isSubscribed) {
              setTeams(prevTeams => 
                prevTeams.map(t => t.key === team.key ? team : t)
              )
            }
          },
          onTimerUpdate: (seconds) => {
            if (isSubscribed) {
              console.log('⏱️ Timer updated to', seconds, 'seconds')
              setOfferTimeoutSeconds(seconds)
              offerTimeoutSecondsRef.current = seconds // Keep ref in sync
            }
          }
        })
        
        // Connect to SignalR hub (includes automatic join)
        console.log('🔌 Connecting to auction hub...')
        await auctionService.connect(auctionId, user.email)
        console.log('✅ Connected successfully')
        
        // Verify connection
        if (!isSubscribed) return
        
        if (!auctionService.isConnected()) {
          throw new Error('Connection not established after connect()')
        }
        
        // Join auction room
        console.log('📨 Joining auction room...')
        const joined = await auctionService.joinAuction(auctionId)
        if (!joined) {
          if (isSubscribed) {
            setConnectionError('Non autorizzato ad unirsi a questa asta')
            setConnectionState('Disconnected')
          }
          return
        }
        console.log('✅ Joined auction room successfully')
        
        // Request initial auction state from API
        console.log('📨 Requesting initial auction state...')
        const auctionState = await auctionService.getAuction(auctionId)
        updateAuctionState(auctionState)
        
        console.log('✅ Initial setup complete - waiting for state events...')
        
        // Mark connection as successful
        isConnectingRef.current = false
      } catch (err) {
        console.error('❌ Error connecting to auction:', err)
        isConnectingRef.current = false
        
        if (isSubscribed) {
          const errorMessage = err instanceof Error ? err.message : 'Unknown error'
          
          // Check if it's an authentication error
          if (errorMessage.includes('401') || errorMessage.includes('Unauthorized') || errorMessage.includes('authentication') || errorMessage.includes('refresh token') || errorMessage.includes('Failed to refresh')) {
            setConnectionError('Sessione scaduta. Reindirizzamento alla pagina di login...')
            
            // Save current location for redirect after login
            const returnUrl = `/auction/${auctionId}`
            
            // Redirect to login after a short delay to show the message
            setTimeout(async () => {
              // Save return URL to storage (cross-platform)
              const { returnUrlService } = await import('../services/returnUrlService')
              await returnUrlService.saveReturnUrl(returnUrl)
              
              // Clear token to force fresh login
              const { tokenStorageService } = await import('../services/tokenStorageService')
              await tokenStorageService.removeToken()
              
              // Navigate to login (will redirect back after successful login)
              navigation.navigate('Login' as any)
            }, 2000)
            
            // Don't retry on auth errors
          } else if (errorMessage.includes('Connection timeout')) {
            setConnectionError('Timeout di connessione. Verifica che il backend sia in esecuzione.')
            // Don't retry on timeout
          } else {
            setConnectionError(`Errore: ${errorMessage}`)
            
            // Only retry if not an auth error and we haven't exceeded max attempts
            if (reconnectAttemptsRef.current < 3) {
              toast.show(`⏰ Tentativo di riconnessione ${reconnectAttemptsRef.current + 1}/3 in 3 secondi...`, { variant: 'error' })
              setTimeout(() => {
                reconnectAttemptsRef.current++
                connectionAttemptedRef.current = false
                isConnectingRef.current = false
                setConnectionTrigger(prev => prev + 1) // Trigger reconnection
              }, 3000)
            } else {
              toast.show('❌ Raggiunto il numero massimo di tentativi di riconnessione', { variant: 'error' })
            }
          }
          
          setConnectionState('Disconnected')
        }
      }
    }
    connectAndJoin()
    return () => {
      // Cleanup: mark component as unsubscribed and notify server we're leaving
      isSubscribed = false
      connectionAttemptedRef.current = false
      isConnectingRef.current = false
      
      // Only notify server we're leaving the auction room
      // Don't call disconnect() - SignalR will automatically close the connection
      // when needed (tab closed, network lost, timeout)
      auctionService.leaveAuction(auctionId).catch(console.error)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [auctionId, user?.email, connectionTrigger])



  // ============================================
  // RENDER COMPONENTS
  // ============================================

  // Current Player Card Component
  const renderCurrentPlayer = () => {
    if (!currentPlayer) return null
    return (
      <CurrentPlayerCard
        currentPlayer={currentPlayer}
        currentBid={currentBid}
        currentBidder={currentBidder}
        currentSubstitute={currentSubstitute}
        teams={teams}
        timerRunning={timerRunning}
        timerExpired={timerExpired}
        timerSecondsLeft={timerSecondsLeft}
        realPlayers={auctionState?.realPlayers}
      />
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
      <PersonalBiddingPanel
        myTeam={myTeam}
        myTeamBudget={myTeamBudget}
        currentPlayer={currentPlayer}
        onIncrementBid={handleIncrementBid}
        onFixedBid={handleFixedBid}
        onCustomBid={() => {
          setIsCustomBidFromAuctioneer(false)
          setShowCustomBidDialog(true)
        }}
        isRepairAuction={isRepairAuction()}
        mySelectedSubstitute={mySelectedSubstitute}
        onSelectSubstitute={() => {
          setIsSubstituteSelectionForAuctioneer(false)
          setShowSubstituteSelectionDialog(true)
        }}
        arePersonalBidButtonsDisabled={arePersonalBidButtonsDisabled}
        currentBidder={currentBidder}
        userEmail={user?.email || null}
      />
    )
  }

  // Pulsantiera Banditore con Controlli Component
  const renderAuctioneerPanel = () => {
    if (!isAuctioneer) return null

    const auctioneerPanel = (
      <AuctioneerPanel
        currentPlayer={currentPlayer}
        currentBid={currentBid}
        currentBidder={currentBidder}
        currentSubstitute={currentSubstitute}
        teams={teams}
        selectedTeamForBid={selectedTeamForBid}
        onSelectedTeamForBidChange={setSelectedTeamForBid}
        calculateBudgetRemaining={calculateBudgetRemaining}
        auctioneerSelectedSubstitute={auctioneerSelectedSubstitute}
        isRepairAuction={isRepairAuction()}
        onSelectSubstitute={() => {
          setIsSubstituteSelectionForAuctioneer(true)
          setShowSubstituteSelectionDialog(true)
        }}
        areAuctioneerBidButtonsDisabled={areAuctioneerBidButtonsDisabled}
        onIncrementBid={handleIncrementBid}
        tempBlockButtons={tempBlockButtons}
        selectedRole={selectedRole}
        onSelectedRoleChange={setSelectedRole}
        loading={loading}
        onShowNextPlayer={async () => {
          setLoading(true)
          try {
            await auctionService.showNextPlayer(auctionId, selectedRole)
          } catch (err) {
            toast.show(`❌ ${err instanceof Error ? err.message : 'Errore sconosciuto'}`, { variant: 'error' })
          } finally {
            setLoading(false)
          }
        }}
        onSearchPlayer={() => setShowPlayerSearchDialog(true)}
        auctionState={auctionState}
        onAssignPlayer={async () => {
          setLoading(true)
          try {
            await auctionService.assignPlayerToTeam(auctionId)
            toast.show('✅ Giocatore aggiudicato!', { variant: 'success' })
          } catch (err) {
            toast.show(`❌ ${err instanceof Error ? err.message : 'Errore sconosciuto'}`, { variant: 'error' })
          } finally {
            setLoading(false)
          }
        }}
        auctionStatus={auctionState?.wrapper.status || AuctionStatus.NotStarted}
        offerTimeoutSeconds={offerTimeoutSeconds}
        onChangeTimer={async (seconds) => {
          setLoading(true)
          try {
            await auctionService.changeTimer(auctionId, seconds)
            toast.show(`⏱️ Timer aggiornato a ${seconds}s`, { variant: 'success' })
          } catch (err) {
            toast.show(`❌ ${err instanceof Error ? err.message : 'Errore sconosciuto'}`, { variant: 'error' })
          } finally {
            setLoading(false)
          }
        }}
        onPauseAuction={async () => {
          setLoading(true)
          try {
            await auctionService.pauseAuction(auctionId)
            toast.show('⏸️ Asta in pausa', { variant: 'info' })
          } catch (err) {
            toast.show(`❌ ${err instanceof Error ? err.message : 'Errore sconosciuto'}`, { variant: 'error' })
          } finally {
            setLoading(false)
          }
        }}
        onResumeAuction={async () => {
          setLoading(true)
          try {
            await auctionService.resumeAuction(auctionId)
            toast.show('▶️ Asta ripresa', { variant: 'success' })
          } catch (err) {
            toast.show(`❌ ${err instanceof Error ? err.message : 'Errore sconosciuto'}`, { variant: 'error' })
          } finally {
            setLoading(false)
          }
        }}
        onResetTimer={async () => {
          setLoading(true)
          try {
            await auctionService.resetTimer(auctionId)
            toast.show('⏱️ Timer riavviato per tutti', { variant: 'success' })
          } catch (err) {
            toast.show(`❌ ${err instanceof Error ? err.message : 'Errore sconosciuto'}`, { variant: 'error' })
          } finally {
            setLoading(false)
          }
        }}
        onCompleteReset={async () => {
          setLoading(true)
          try {
            await auctionService.completeReset(auctionId)
            toast.show('🔄 Asta resettata per tutti', { variant: 'success' })
          } catch (err) {
            toast.show(`❌ ${err instanceof Error ? err.message : 'Errore sconosciuto'}`, { variant: 'error' })
          } finally {
            setLoading(false)
          }
        }}
        onCloseAuction={async () => {
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
        }}
      />
    )

    // Se banditore ha una squadra, mostra doppia pulsantiera (layout responsive)
    if (myTeam) {
      const StackComponent = media.gtMd ? XStack : YStack
      return (
        <StackComponent gap="$3" alignItems="stretch">
          <YStack flex={media.gtMd ? 1 : undefined} width="100%">{renderPersonalBidding()}</YStack>
          <YStack flex={media.gtMd ? 1 : undefined} width="100%" marginTop={media.gtMd ? 0 : "$3"}>{auctioneerPanel}</YStack>
        </StackComponent>
      )
    }

    // Se banditore senza squadra, mostra solo pannello banditore
    return <YStack width="100%">{auctioneerPanel}</YStack>
  }

  // Teams Panel Component  
  const renderTeams = () => {
    return (
      <TeamsPanel
        teams={teams}
        user={user}
        startingMoney={startingMoney}
        calculateBudgetRemaining={calculateBudgetRemaining}
        onTeamClick={(team) => {
          setSelectedTeamDetail(team)
          setShowTeamDetailDialog(true)
        }}
      />
    )
  }

  // Timer Component
  // Custom Bid Dialog Component
  const renderCustomBidDialog = () => (
    <CustomBidDialog
      open={showCustomBidDialog}
      currentBid={currentBid}
      onOpenChange={setShowCustomBidDialog}
      onConfirm={(amount) => {
        placeBid(amount, isCustomBidFromAuctioneer)
        toast.show('✅ Offerta inviata!', { variant: 'success' })
      }}
    />
  )

  // Team Detail Dialog Component
  const renderTeamDetailDialog = () => (
    <TeamDetailDialog
      open={showTeamDetailDialog}
      team={selectedTeamDetail}
      user={user}
      startingMoney={startingMoney}
      calculateBudgetRemaining={calculateBudgetRemaining}
      isAuctioneer={isAuctioneer}
      onOpenChange={setShowTeamDetailDialog}
      onModifyPlayerStatus={(playerKey, playerName, owner) => {
        setChangeStatusPlayer({ playerKey, playerName, owner })
        setShowChangeStatusDialog(true)
      }}
      getPlayerKey={getPlayerKey}
    />
  )

  // Change Player Status Dialog Component
  const renderChangeStatusDialog = () => (
    <ChangeStatusDialog
      open={showChangeStatusDialog}
      playerKey={changeStatusPlayer?.playerKey || null}
      playerName={changeStatusPlayer?.playerName || null}
      owner={changeStatusPlayer?.owner || null}
      loading={loading}
      onOpenChange={setShowChangeStatusDialog}
      onConfirm={async (owner, playerKey, status, revenue) => {
        setLoading(true)
        try {
          await auctionService.changePlayerStatus(auctionId, owner, playerKey, status, revenue)
          setShowChangeStatusDialog(false)
          setChangeStatusPlayer(null)
          toast.show('✅ Stato giocatore aggiornato', { variant: 'success' })
        } catch (err) {
          toast.show(`❌ ${err instanceof Error ? err.message : 'Errore sconosciuto'}`, { variant: 'error' })
        } finally {
          setLoading(false)
        }
      }}
      onCancel={() => {
        setChangeStatusPlayer(null)
      }}
    />
  )

  // Substitute Selection Dialog Component
  const renderSubstituteSelectionDialog = () => {
    const team = isSubstituteSelectionForAuctioneer && selectedTeamForBid
      ? teams.find(t => t.value.owner === selectedTeamForBid)
      : myTeam
    
    const currentSelection = isSubstituteSelectionForAuctioneer ? auctioneerSelectedSubstitute : mySelectedSubstitute
    
    return (
      <SubstituteSelectionDialog
        open={showSubstituteSelectionDialog}
        currentPlayer={currentPlayer}
        team={team || null}
        currentSelection={currentSelection}
        realPlayers={auctionState?.realPlayers}
        onOpenChange={setShowSubstituteSelectionDialog}
        onSelectPlayer={(playerName) => {
          if (isSubstituteSelectionForAuctioneer) {
            setAuctioneerSelectedSubstitute(playerName)
          } else {
            setMySelectedSubstitute(playerName)
          }
        }}
        onRemoveSelection={() => {
          if (isSubstituteSelectionForAuctioneer) {
            setAuctioneerSelectedSubstitute(null)
          } else {
            setMySelectedSubstitute(null)
          }
        }}
      />
    )
  }

  // Player Search Dialog Component (Force Next Player)
  const renderPlayerSearchDialog = () => {
    if (!auctionState) return null
    
    return (
      <PlayerSearchDialog
        open={showPlayerSearchDialog}
        selectedRole={selectedRole}
        playerSearchQuery={playerSearchQuery}
        selectedPlayerForForce={selectedPlayerForForce}
        realPlayers={auctionState.realPlayers}
        loading={loading}
        teams={teams}
        onOpenChange={setShowPlayerSearchDialog}
        onSelectedRoleChange={setSelectedRole}
        onPlayerSearchQueryChange={setPlayerSearchQuery}
        onSelectedPlayerForForceChange={setSelectedPlayerForForce}
        onConfirm={async () => {
          if (!selectedPlayerForForce) return
          
          setLoading(true)
          try {
            await auctionService.showNextPlayer(auctionId, selectedRole, selectedPlayerForForce)
            setShowPlayerSearchDialog(false)
            setPlayerSearchQuery('')
            setSelectedPlayerForForce('')
          } catch (err) {
            toast.show(`❌ ${err instanceof Error ? err.message : 'Errore sconosciuto'}`, { variant: 'error' })
          } finally {
            setLoading(false)
          }
        }}
        onCancel={() => {
          setPlayerSearchQuery('')
          setSelectedPlayerForForce('')
        }}
      />
    )
  }

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
                <Text fontSize="$3" color="$blue11" fontWeight="600">Lista: {auctionTypeLabel}</Text>
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
                <Button onPress={() => { 
                  setConnectionError(null)
                  reconnectAttemptsRef.current = 0
                  connectionAttemptedRef.current = false
                  isConnectingRef.current = false
                  setConnectionTrigger(prev => prev + 1) // Trigger reconnection
                }} theme="blue" icon={RotateCcw}>Riprova Connessione</Button>
              </YStack>
            ) : (
              <>
                <Spinner size="large" color="$blue10" />
                <Text fontSize="$4" color="$color11">{connectionState === 'Connecting' ? 'Connessione all\'asta...' : 'Disconnesso'}</Text>
                {reconnectAttemptsRef.current > 0 && <Text fontSize="$3" color="$color10">Tentativo {reconnectAttemptsRef.current} di 3...</Text>}
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
        {renderCustomBidDialog()}
        {renderTeamDetailDialog()}
        {renderChangeStatusDialog()}
        {renderSubstituteSelectionDialog()}
        {renderPlayerSearchDialog()}
      </ScreenContent>
    </View>
  )
}
