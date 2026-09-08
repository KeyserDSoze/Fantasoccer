// Auction Live Screen - Real-time auction management with bidding interface (Pulsantiera)
import { useState, useEffect, useCallback, useRef } from 'react'
import { View, Platform, Vibration } from 'react-native'
import { 
  Button, Card, Input, Select, Separator, Text, XStack, YStack, 
  useMedia, Spinner, Label, Dialog, ScrollView, Accordion, Square
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
  AlertCircle, History, UserX
} from '@tamagui/lucide-icons-2'
import { useRoute, RouteProp, useNavigation } from '@react-navigation/native'
import { RootStackParamList } from 'navigation'
import { useAuth } from 'providers/AuthProvider'
import { Team, TeamKey, TeamHelper, PlayerInTeamStatus, PlayerStatusHelper } from 'types/team'
import { Entity } from 'types/default'
import { getPlayerKey } from 'utils'

import { 
  getRoleLabel, 
  getAuctionStatusLabel as getStatusLabel, 
  getAuctionStatusColor as getStatusColor,
  playAuctionSound,
  AuctionSoundType
} from 'utils/auctionHelper'
import { 
  AuctioneerPanel, 
  PersonalBiddingPanel, 
  CurrentPlayerCard,
  TeamsPanel,
  CustomBidDialog,
  TeamDetailDialog,
  RemainingPlayersPanel,
  DiscardedPlayersPanel,
  ChangeStatusDialog,
  SubstituteSelectionDialog,
  PlayerSearchDialog,
  FloatingPlayerWidget
} from 'components/auction'
import { FloatingEmoji } from 'components/auction/FloatingEmoji'
import { ToggleChip } from 'components/ToggleChip'

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
  const teamsRef = useRef<Entity<Team, TeamKey>[]>([])
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
  const timerIntervalRef = useRef<ReturnType<typeof setInterval> | null>(null)
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
  const [emojiSoundEnabled, setEmojiSoundEnabled] = useState(true)
  const emojiSoundEnabledRef = useRef<boolean>(true)
  const audioContextRef = useRef<AudioContext | null>(null)
  const connectionAttemptedRef = useRef<boolean>(false)
  const isConnectingRef = useRef<boolean>(false)
  
  // UI visibility toggles
  const [showAuctioneerPanel, setShowAuctioneerPanel] = useState(true)
  const [showMyTeamPanel, setShowMyTeamPanel] = useState(true)
  const [showTeamsPanel, setShowTeamsPanel] = useState(true)
  const [showPlayerStats, setShowPlayerStats] = useState(true)
  const [showTeamDetails, setShowTeamDetails] = useState(true)
  const [showMyTeamStats, setShowMyTeamStats] = useState(true)
  const [showPurchaseHistory, setShowPurchaseHistory] = useState(true)
  const [showInactivePlayers, setShowInactivePlayers] = useState(true)
  const [showEmojis, setShowEmojis] = useState(true)
  const showEmojisRef = useRef<boolean>(true)

  // Stats and chance data (use state instead of ref to trigger re-renders)
  const [previousYearStatsMap, setPreviousYearStatsMap] = useState<Map<string, StatPlayer>>(new Map())
  const [chancedPlayersMap, setChancedPlayersMap] = useState<Map<string, any>>(new Map())
  const [connectedUsers, setConnectedUsers] = useState<string[]>([])
  const [selectedUserForTransfer, setSelectedUserForTransfer] = useState<string | null>(null)
  const [refreshKey, setRefreshKey] = useState(0) // Per forzare refresh delle liste giocatori
  
  // Emoji reactions
  interface ActiveEmoji {
    id: string
    emoji: string
    sender: string
    timestamp: number
  }
  const [activeEmojis, setActiveEmojis] = useState<ActiveEmoji[]>([])

  // Initialize audio context
  useEffect(() => {
    if (Platform.OS === 'web') {
      audioContextRef.current = new (window.AudioContext || (window as any).webkitAudioContext)()
    }
    return () => {
      audioContextRef.current?.close()
    }
  }, [])

  // Sync emojiSoundEnabled ref
  useEffect(() => {
    emojiSoundEnabledRef.current = emojiSoundEnabled
  }, [emojiSoundEnabled])

  // Sync showEmojis ref
  useEffect(() => {
    showEmojisRef.current = showEmojis
  }, [showEmojis])

  const playSound = useCallback((soundType: AuctionSoundType) => {
    if (Platform.OS !== 'web') return
    playAuctionSound(audioContextRef.current, audioEnabled, soundType)
  }, [audioEnabled])

  const playEmojiSound = useCallback((frequency: number) => {
    if (Platform.OS !== 'web' || !showEmojisRef.current || !emojiSoundEnabledRef.current || !audioContextRef.current) return
    
    const ctx = audioContextRef.current
    const now = ctx.currentTime
    
    // Oscillatore principale
    const osc1 = ctx.createOscillator()
    const gain1 = ctx.createGain()
    osc1.type = 'sine'
    osc1.frequency.setValueAtTime(frequency, now)
    osc1.connect(gain1)
    
    // Oscillatore armonico (quinta superiore per ricchezza)
    const osc2 = ctx.createOscillator()
    const gain2 = ctx.createGain()
    osc2.type = 'sine'
    osc2.frequency.setValueAtTime(frequency * 1.5, now)
    osc2.connect(gain2)
    
    // Filtro passa-basso per warmth
    const filter = ctx.createBiquadFilter()
    filter.type = 'lowpass'
    filter.frequency.setValueAtTime(frequency * 6, now)
    filter.Q.setValueAtTime(3, now)
    
    // Connessioni
    gain1.connect(filter)
    gain2.connect(filter)
    filter.connect(ctx.destination)
    
    // Envelope ADSR più secco e veloce
    gain1.gain.setValueAtTime(0, now)
    gain1.gain.linearRampToValueAtTime(0.25, now + 0.05) // Attack veloce
    gain1.gain.linearRampToValueAtTime(0.15, now + 0.12) // Decay rapido
    gain1.gain.setValueAtTime(0.15, now + 0.25) // Sustain breve
    gain1.gain.exponentialRampToValueAtTime(0.01, now + 0.45) // Release veloce
    
    // Envelope per armonico (più soft)
    gain2.gain.setValueAtTime(0, now)
    gain2.gain.linearRampToValueAtTime(0.1, now + 0.05)
    gain2.gain.linearRampToValueAtTime(0.06, now + 0.12)
    gain2.gain.setValueAtTime(0.06, now + 0.25)
    gain2.gain.exponentialRampToValueAtTime(0.01, now + 0.45)
    
    // Sweep filtro più rapido
    filter.frequency.setValueAtTime(frequency * 6, now)
    filter.frequency.exponentialRampToValueAtTime(frequency * 2.5, now + 0.3)
    
    // Start/Stop
    osc1.start(now)
    osc2.start(now)
    osc1.stop(now + 0.5)
    osc2.stop(now + 0.5)
  }, [])

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
    setAuctionState(state)
    setTeams(state.teams)
    teamsRef.current = state.teams // Keep ref in sync
    setIsAuctioneer(state.isAuctioneer)
    setSalesHistory(state.wrapper.winnings)
    setIsLocked(state.wrapper.status === AuctionStatus.Paused)
    
    // Sync selected role with wrapper's current role
    setSelectedRole(state.wrapper.currentRole)
    
    // Update starting money from league settings (default to 1000 if not available)
    const leagueStartingMoney = state.league?.settings?.startingMoney ?? 1000
    setStartingMoney(leagueStartingMoney)
    
    // Sync timer with auction state
    const auctionTimer = state.wrapper.secondsPerAuction ?? 10
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
    
  }, [user?.email])

  // ============================================
  // TIMER LOGIC
  // ============================================
  const startTimer = useCallback(() => {
    const timeout = offerTimeoutSecondsRef.current // Use ref for latest value
    setTimerSecondsLeft(timeout)
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
          // Play gong sound when timer expires
          playSound(AuctionSoundType.TimerExpired)
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

  // Send emoji reaction
  const sendEmoji = useCallback(async (emoji: string) => {
    if (!auctionId) return
    try {
      await auctionService.sendEmoji(auctionId, emoji)
    } catch (error) {
      console.error('Error sending emoji:', error)
    }
  }, [auctionId])

  // Transfer auctioneer role to another user
  const handleTransferAuctioneer = useCallback(async (newAuctioneerEmail: string) => {
    if (!auctionId) return
    try {
      await auctionService.transferAuctioneerRole(auctionId, newAuctioneerEmail)
      // Success toast will be shown when AuctioneerChanged event is received
    } catch (error) {
      console.error('Error transferring auctioneer role:', error)
      toast.show('❌ Errore nel trasferimento del ruolo', { type: 'error' })
    }
  }, [auctionId, toast])

  // Handle role change from auctioneer panel
  const handleRoleChange = useCallback(async (newRole: Role) => {
    if (!auctionId) return
    setSelectedRole(newRole)
    if (isAuctioneer) {
      try {
        await auctionService.setCurrentRole(auctionId, newRole)
      } catch (error) {
        console.error('Error setting current role:', error)
      }
    }
  }, [auctionId, isAuctioneer])

  // Remove completed emoji animation
  const removeEmoji = useCallback((id: string) => {
    setActiveEmojis(prev => prev.filter(e => e.id !== id))
  }, [])

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
          onUserJoined: (email) => {
            if (isSubscribed) {
              setConnectedUsers(prev => [...new Set([...prev, email])])
              toast.show(`👋 Utente connesso: ${email}`, { variant: 'info', duration: 4000 })
            }
          },
          onUserLeft: (email) => {
            if (isSubscribed) {
              setConnectedUsers(prev => prev.filter(e => e !== email))
              toast.show(`👋 L'utente ${email} si è disconnesso`, { variant: 'info', duration: 4000 })
            }
          },
          onEmojiReceived: (data: { emoji: string; sender: string }) => {
            if (isSubscribed) {
              // Only add emoji if showEmojis is enabled
              if (showEmojisRef.current) {
                // Get fresh teams state from ref
                setActiveEmojis(prev => {
                  // Find sender name (team name if player, email otherwise)
                  const team = teamsRef.current.find(t => t.value.owner.toLowerCase() === data.sender.toLowerCase())
                  const senderName = team?.value.name || data.sender.split('@')[0]
                  
                  console.log('Emoji received:', { sender: data.sender, team: team?.value.name, senderName })
                  
                  // Add emoji to active emojis
                  const emojiId = `${Date.now()}-${Math.random()}`
                  return [...prev, { id: emojiId, emoji: data.emoji, sender: senderName, timestamp: Date.now() }]
                })
              }
              
              // Play sound based on emoji (respects showEmojis and emojiSoundEnabled via ref)
              const emojiSounds: Record<string, number> = {
                '🔥': 600, '😂': 400, '💰': 700, '👍': 500,
                '❤️': 650, '😱': 550, '🤔': 450, '🎉': 750
              }
              const frequency = emojiSounds[data.emoji] || 500
              playEmojiSound(frequency)
            }
          },
          onAuctioneerChanged: (data: { oldAuctioneer: string; newAuctioneer: string }) => {
            if (isSubscribed) {
              console.log('🎯 Auctioneer changed:', data)
              // Update isAuctioneer state if current user is the new auctioneer
              const isNewAuctioneer = data.newAuctioneer.toLowerCase() === user?.email?.toLowerCase()
              setIsAuctioneer(isNewAuctioneer)
              
              // Show success toast with the new auctioneer name
              const newAuctioneerName = teams.find(t => t.value.owner.toLowerCase() === data.newAuctioneer.toLowerCase())?.value.name || data.newAuctioneer
              toast.show(`✅ Ruolo trasferito a: ${newAuctioneerName}`, { type: 'success' })
            }
          },
          onCurrentRoleChanged: (data: { currentRole: number }) => {
            if (isSubscribed) {
              console.log('🎯 Current role changed:', data)
              const newRole = data.currentRole as Role
              setSelectedRole(newRole)
              
              // Reset selected substitute if it has a different role
              if (mySelectedSubstitute && myTeam) {
                const player = myTeam.value.players.find(p => p.name === mySelectedSubstitute)
                if (player && player.role !== newRole) {
                  setMySelectedSubstitute(null)
                }
              }
              if (auctioneerSelectedSubstitute && selectedTeamForBid) {
                const team = teams.find(t => t.value.owner === selectedTeamForBid)
                if (team) {
                  const player = team.value.players.find(p => p.name === auctioneerSelectedSubstitute)
                  if (player && player.role !== newRole) {
                    setAuctioneerSelectedSubstitute(null)
                  }
                }
              }
            }
          },
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
              
              // Forza refresh liste giocatori
              setRefreshKey(prev => prev + 1)
              
              resetTimer()
            }
          },
          onPlayerAssigned: (data: PlayerAssignedMessage) => {
            if (isSubscribed) {
              // Determina se ho vinto io l'asta
              const iWon = user?.email && data.playerAssignedInAuction?.owner.toLowerCase() === user.email.toLowerCase()
              playSound(iWon ? AuctionSoundType.MyWin : AuctionSoundType.OtherWin)
              vibrateOnBid()
              toast.show(`✅ Giocatore assegnato: ${data.playerAssignedInAuction?.playerName} a ${data.playerAssignedInAuction?.teamName} per €${data.playerAssignedInAuction?.price}. Ha preso ${data.playerAssignedInAuction?.teamPlayersCount} giocatori nella squadra.`, { variant: 'success', duration: 4000 })
              
              // Update the team that won the auction with the updated team data
              if (data.team) {
                setTeams(prevTeams => {
                  const updated = prevTeams.map(team => 
                    team.value.owner === data.team!.value.owner ? data.team! : team
                  )
                  teamsRef.current = updated // Keep ref in sync
                  return updated
                })
                
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
              
              // Forza refresh liste giocatori
              setRefreshKey(prev => prev + 1)
              resetTimer()
            }
          },
          onBidPlaced: (data: BidPlaced) => {
            if (isSubscribed) {
              setCurrentBid(data.amount)
              setCurrentBidder(data.bidderEmail)
              setCurrentSubstitute(data.substituted)
              // Determina se sono io a fare l'offerta
              const isMyBid = user?.email && data.bidderEmail.toLowerCase() === user.email.toLowerCase()
              playSound(isMyBid ? AuctionSoundType.MyBid : AuctionSoundType.OtherBid)
              vibrateOnBid()
              startTimer()
            }
          },
          onAuctionPaused: () => {
            toast.show('⏸️ Asta in pausa', { variant: 'info', duration: 4000 })
            if (isSubscribed) {
              setIsLocked(true)
              setAuctionState(prev => prev ? { ...prev, wrapper: { ...prev.wrapper, status: AuctionStatus.Paused } } : prev)
            }
          },
          onAuctionResumed: () => {
            toast.show('▶️ Asta ripresa', { variant: 'info', duration: 4000 })
            if (isSubscribed) {
              setIsLocked(false)
              setAuctionState(prev => prev ? { ...prev, wrapper: { ...prev.wrapper, status: AuctionStatus.InProgress } } : prev)
            }
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
          onCurrentAuctionClosed: () => {
            if (isSubscribed) {
              setCurrentPlayer(null)
              setCurrentBid(0)
              setCurrentBidder(null)
              setCurrentSubstitute(null)
              resetTimer()
              toast.show('📞 Asta aperta a chiamata - Puoi ora selezionare un giocatore scartato', { variant: 'info', duration: 3000 })
            }
          },
          onTimerReset: () => {
            if (isSubscribed) {
              startTimer()
              toast.show('⏱️ Timer riavviato', { variant: 'info', duration: 1000 })
            }
          },
          onTeamUpdated: (team: Entity<Team, TeamKey>) => {
            console.log('📦 Team updated from server:', team.value.name)
            if (isSubscribed) {
              // Update teams array
              setTeams(prevTeams => {
                const updated = prevTeams.map(t => t.value.owner === team.value.owner ? team : t)
                teamsRef.current = updated // Keep ref in sync
                return updated
              })
              
              // Update myTeam if it's my team
              if (user?.email && team.value.owner.toLowerCase() === user.email.toLowerCase()) {
                setMyTeam(team)
                const budget = startingMoney - TeamHelper.getCost(team.value)
                setMyTeamBudget(budget)
              }
              
              // Update selectedTeamDetail if it's the team being viewed in the dialog
              if (selectedTeamDetail && team.value.owner === selectedTeamDetail.value.owner) {
                setSelectedTeamDetail(team)
              }
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
        
        // Get list of connected users
        if (isSubscribed) {
          const connectedUsersList = await auctionService.getConnectedUsers(auctionId)
          console.log('👥 Connected users:', connectedUsersList)
          setConnectedUsers(connectedUsersList)
        }
        
        // Request initial auction state from API
        console.log('📨 Requesting initial auction state...')
        const auctionState = await auctionService.getAuction(auctionId)
        updateAuctionState(auctionState)
        
        console.log('✅ Initial setup complete - loading additional data...')
        
        // Load previous year stats for comparison (after we have auctionState)
        if (auctionState?.league?.year) {
          const previousYear = auctionState.league.year - 1
          console.log(`📊 Loading stats for previous year (${previousYear})...`)
          
          try {
            const { statPlayerService } = await import('../services/statPlayerService')
            const previousYearWrapper = await statPlayerService.getCurrentUserEnhancedWrapper({ year: previousYear })
            
            if (previousYearWrapper?.players && previousYearWrapper.players.length > 0) {
              const map = new Map<string, StatPlayer>()
              previousYearWrapper.players.forEach(player => {
                const key = getPlayerKey(player.name)
                map.set(key, player)
              })
              setPreviousYearStatsMap(map)
              console.log(`📊 Loaded ${map.size} players from previous year (${previousYear})`)
            }
          } catch (error) {
            console.error('❌ Error loading previous year stats:', error)
          }
        }
        
        // Load Serie A calendar and chance data (ALWAYS use current fantacalcio year, not auction year!)
        // Auction might be for a past year, but we always want current Serie A data
        try {
          // Get current fantacalcio year (not from auction, but from actual date)
          const { getActualFantacalcioYear } = await import('../utils/timezoneHelpers')
          const currentFantacalcioYear = getActualFantacalcioYear()
          console.log(`🎲 Loading chance data for current fantacalcio year (${currentFantacalcioYear})...`)
          
          // Get current/next Serie A day for the current fantacalcio year
          const { serieAService } = await import('../services/serieAService')
          const calendarData = await serieAService.getEnhancedCalendarByYear(currentFantacalcioYear)
          const currentSerieADay = calendarData?.liveSerieADay || calendarData?.nextDay?.serieADay || 0
          
          if (currentSerieADay > 0) {
            console.log(`📅 Live/Next Serie A day: ${currentSerieADay}`)
            
            // Try to load chance data for current day (using current fantacalcio year)
            const { chanceService } = await import('../services/chanceService')
            let chancedPlayersWrapper = await chanceService.getEnhancedWrapper({ year: currentFantacalcioYear, day: currentSerieADay })
            let effectiveDay = currentSerieADay
            
            // If no data for current day, try previous day
            if ((!chancedPlayersWrapper?.players || chancedPlayersWrapper.players.length === 0) && currentSerieADay > 1) {
              const previousDay = currentSerieADay - 1
              console.log(`🎲 No data for day ${currentSerieADay}, trying previous day ${previousDay}...`)
              chancedPlayersWrapper = await chanceService.getEnhancedWrapper({ year: currentFantacalcioYear, day: previousDay })
              effectiveDay = previousDay
            }
            
            // Populate chanced players map
            if (chancedPlayersWrapper?.players && chancedPlayersWrapper.players.length > 0) {
              const map = new Map()
              chancedPlayersWrapper.players.forEach(player => {
                const key = getPlayerKey(player.name)
                map.set(key, player)
              })
              setChancedPlayersMap(map)
              console.log(`🎲 Loaded ${map.size} chanced players for day ${effectiveDay} (year ${currentFantacalcioYear})`)
            } else {
              console.log(`🎲 No chanced players available for day ${effectiveDay}`)
            }
          } else {
            console.log('📅 No live/next Serie A day available')
          }
        } catch (error) {
          console.error('❌ Error loading chance data:', error)
        }
        
        console.log('✅ All data loaded - waiting for state events...')
        
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
    // Get previous year stats for current player
    const previousYearStats = currentPlayer 
      ? previousYearStatsMap.get(getPlayerKey(currentPlayer.name))
      : undefined
    // Get chance and real game info for current player
    const chancedPlayer = currentPlayer
      ? chancedPlayersMap.get(getPlayerKey(currentPlayer.name))
      : undefined
    return (
      <CurrentPlayerCard
        currentPlayer={currentPlayer}
        previousYearStats={previousYearStats}
        chancedPlayer={chancedPlayer}
        currentBid={currentBid}
        currentBidder={currentBidder}
        currentSubstitute={currentSubstitute}
        teams={teams}
        timerRunning={timerRunning}
        timerExpired={timerExpired}
        timerSecondsLeft={timerSecondsLeft}
        realPlayers={auctionState?.realPlayers}
        showStats={showPlayerStats}
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
        sendEmoji={sendEmoji}
        isRepairAuction={isRepairAuction()}
        mySelectedSubstitute={mySelectedSubstitute}
        onSelectSubstitute={() => {
          setIsSubstituteSelectionForAuctioneer(false)
          setShowSubstituteSelectionDialog(true)
        }}
        arePersonalBidButtonsDisabled={arePersonalBidButtonsDisabled}
        currentBidder={currentBidder}
        userEmail={user?.email || null}
        chancedPlayersMap={chancedPlayersMap}
        showStats={showMyTeamStats}
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
        onSelectedRoleChange={handleRoleChange}
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
        onFinalizeAuction={async () => {
          setLoading(true)
          try {
            await auctionService.finalizeAuction(auctionId)
            toast.show('✅ Asta finalizzata e tutte le squadre salvate con successo!', { variant: 'success', duration: 5000 })
          } catch (err) {
            toast.show(`❌ ${err instanceof Error ? err.message : 'Errore sconosciuto'}`, { variant: 'error' })
          } finally {
            setLoading(false)
          }
        }}
        onReopenAuction={async () => {
          setLoading(true)
          try {
            await auctionService.reopenAuction(auctionId)
            toast.show('✅ Asta riaperta con successo!', { variant: 'success' })
          } catch (err) {
            toast.show(`❌ ${err instanceof Error ? err.message : 'Errore sconosciuto'}`, { variant: 'error' })
          } finally {
            setLoading(false)
          }
        }}
        onCloseCurrentAuction={async () => {
          setLoading(true)
          try {
            await auctionService.closeCurrentAuction(auctionId)
            toast.show('✅ Asta corrente chiusa - Ora puoi chiamare un giocatore scartato', { variant: 'success' })
          } catch (err) {
            toast.show(`❌ ${err instanceof Error ? err.message : 'Errore sconosciuto'}`, { variant: 'error' })
          } finally {
            setLoading(false)
          }
        }}
      />
    )

    // Se banditore ha una squadra, mostra doppia pulsantiera (layout responsive)
    if (myTeam && showMyTeamPanel && showAuctioneerPanel) {
      const StackComponent = media.gtMd ? XStack : YStack
      return (
        <StackComponent gap="$3" alignItems="stretch">
          <YStack flex={media.gtMd ? 1 : undefined} width="100%">{renderPersonalBidding()}</YStack>
          <YStack flex={media.gtMd ? 1 : undefined} width="100%" marginTop={media.gtMd ? 0 : "$3"}>{auctioneerPanel}</YStack>
        </StackComponent>
      )
    }

    // Se banditore senza squadra, mostra solo pannello banditore
    if(showAuctioneerPanel)
      return <YStack width="100%">{auctioneerPanel}</YStack>
    if(showMyTeamPanel)
      return <YStack width="100%">{renderPersonalBidding()}</YStack>
    return null
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
        chancedPlayersMap={chancedPlayersMap}
        showTeamDetails={showTeamDetails}
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
        selectedRole={selectedRole}
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

  // Purchase History Component
  const renderPurchaseHistory = () => {
    if (!showPurchaseHistory || !auctionState) return null
    
    // Group purchases by team
    const purchasesByTeam = salesHistory.reduce((acc, purchase) => {
      if (!purchase.owner) return acc
      if (!acc[purchase.owner]) {
        acc[purchase.owner] = []
      }
      acc[purchase.owner].push(purchase)
      return acc
    }, {} as Record<string, AuctionWin[]>)
    
    // Sort teams by total spent
    const sortedTeams = Object.entries(purchasesByTeam).sort((a, b) => {
      const totalA = a[1].reduce((sum, p) => sum + p.price, 0)
      const totalB = b[1].reduce((sum, p) => sum + p.price, 0)
      return totalB - totalA
    })
    
    return (
      <Accordion overflow="hidden" type="multiple" defaultValue={['purchases']}>
        <Accordion.Item value="purchases">
          <Accordion.Trigger
            flexDirection="row"
            justifyContent="space-between"
            padding="$4"
            backgroundColor="$green2"
            borderRadius="$4"
            borderWidth="$0.5"
            borderColor="$green8"
          >
            {({ open }: { open: boolean }) => (
              <>
                <XStack gap="$2" alignItems="center" flex={1}>
                  <History size={24} color="$green11" />
                  <Text fontSize="$5" fontWeight="700" color="$green11">
                    Storico Acquisti ({salesHistory.length})
                  </Text>
                  {salesHistory.length > 0 && (
                    <Card padding="$1.5" paddingHorizontal="$2.5" backgroundColor="$green1" borderRadius="$2">
                      <Text fontSize="$3" color="$green11" fontWeight="600">
                        Totale speso: €{salesHistory.reduce((sum, p) => sum + p.price, 0)}
                      </Text>
                    </Card>
                  )}
                </XStack>
                <Square transition="quick" rotate={open ? '180deg' : '0deg'}>
                  <ChevronDown size={20} color="$green11" />
                </Square>
              </>
            )}
          </Accordion.Trigger>
          
          <Accordion.HeightAnimator transition="quick">
            <Accordion.Content
              padding="$4"
              paddingTop="$2"
              backgroundColor="$green2"
              borderBottomLeftRadius="$4"
              borderBottomRightRadius="$4"
            >
              {salesHistory.length === 0 ? (
                <Card padding="$4" backgroundColor="$green1" borderRadius="$3">
                  <Text color="$green11" textAlign="center">Nessun acquisto ancora effettuato</Text>
                </Card>
              ) : (
                <YStack gap="$3">
                  {sortedTeams.map(([owner, purchases]) => {
                    const team = teams.find(t => t.value.owner === owner)
                    const teamName = team?.value.name || owner
                    const totalSpent = purchases.reduce((sum, p) => sum + p.price, 0)
                    
                    return (
                      <Card key={owner} padding="$3" backgroundColor="$green1" borderRadius="$3">
                        <YStack gap="$2">
                          <XStack justifyContent="space-between" alignItems="center">
                            <Text fontSize="$4" fontWeight="700" color="$green11">
                              {teamName}
                            </Text>
                            <Card padding="$1.5" paddingHorizontal="$2.5" backgroundColor="$green3" borderRadius="$2">
                              <Text fontSize="$3" color="$green11" fontWeight="600">
                                €{totalSpent} ({purchases.length} giocatori)
                              </Text>
                            </Card>
                          </XStack>
                          <Separator marginVertical="$1" />
                          <YStack gap="$1.5">
                            {purchases.map((purchase, idx) => {
                              // Find player name from realPlayers
                              const player = auctionState.realPlayers.find(
                                p => getPlayerKey(p.name) === purchase.playerKey
                              )
                              const playerName = player?.name || purchase.playerKey
                              
                              return (
                                <XStack
                                  key={`${purchase.playerKey}-${idx}`}
                                  justifyContent="space-between"
                                  alignItems="center"
                                  padding="$2"
                                  backgroundColor="$green3"
                                  borderRadius="$2"
                                >
                                  <Text fontSize="$3" color="$green12">
                                    {playerName}
                                  </Text>
                                  <Text fontSize="$3" color="$green11" fontWeight="600">
                                    €{purchase.price}
                                  </Text>
                                </XStack>
                              )
                            })}
                          </YStack>
                        </YStack>
                      </Card>
                    )
                  })}
                </YStack>
              )}
            </Accordion.Content>
          </Accordion.HeightAnimator>
        </Accordion.Item>
      </Accordion>
    )
  }

  // Inactive Players Component (Sold/Removed players)
  const renderInactivePlayers = () => {
    if (!showInactivePlayers || !auctionState) return null
    
    // Collect all inactive players from all teams
    const inactivePlayers: Array<{
      player: any
      teamName: string
      owner: string
    }> = []
    
    teams.forEach(team => {
      team.value.players.forEach(player => {
        if (player.status !== PlayerInTeamStatus.Active) {
          inactivePlayers.push({
            player,
            teamName: team.value.name,
            owner: team.value.owner
          })
        }
      })
    })
    
    // Group by team
    const inactiveByTeam = inactivePlayers.reduce((acc, item) => {
      if (!acc[item.owner]) {
        acc[item.owner] = {
          teamName: item.teamName,
          players: []
        }
      }
      acc[item.owner].players.push(item.player)
      return acc
    }, {} as Record<string, { teamName: string; players: any[] }>)
    
    const totalInactive = inactivePlayers.length
    
    return (
      <Accordion overflow="hidden" type="multiple">
        <Accordion.Item value="inactive">
          <Accordion.Trigger
            flexDirection="row"
            justifyContent="space-between"
            padding="$4"
            backgroundColor="$red2"
            borderRadius="$4"
            borderWidth="$0.5"
            borderColor="$red8"
          >
            {({ open }: { open: boolean }) => (
              <>
                <XStack gap="$2" alignItems="center" flex={1}>
                  <UserX size={24} color="$red11" />
                  <Text fontSize="$5" fontWeight="700" color="$red11">
                    Giocatori Cacciati/Venduti ({totalInactive})
                  </Text>
                </XStack>
                <Square transition="quick" rotate={open ? '180deg' : '0deg'}>
                  <ChevronDown size={20} color="$red11" />
                </Square>
              </>
            )}
          </Accordion.Trigger>
          
          <Accordion.HeightAnimator transition="quick">
            <Accordion.Content
              padding="$4"
              paddingTop="$2"
              backgroundColor="$red2"
              borderBottomLeftRadius="$4"
              borderBottomRightRadius="$4"
            >
              {totalInactive === 0 ? (
                <Card padding="$4" backgroundColor="$red1" borderRadius="$3">
                  <Text color="$red11" textAlign="center">Nessun giocatore cacciato o venduto</Text>
                </Card>
              ) : (
                <YStack gap="$3">
                  {Object.entries(inactiveByTeam).map(([owner, data]) => (
                    <Card key={owner} padding="$3" backgroundColor="$red1" borderRadius="$3">
                      <YStack gap="$2">
                        <XStack justifyContent="space-between" alignItems="center">
                          <Text fontSize="$4" fontWeight="700" color="$red11">
                            {data.teamName}
                          </Text>
                          <Card padding="$1.5" paddingHorizontal="$2.5" backgroundColor="$red3" borderRadius="$2">
                            <Text fontSize="$3" color="$red11" fontWeight="600">
                              {data.players.length} giocatori
                            </Text>
                          </Card>
                        </XStack>
                        <Separator marginVertical="$1" />
                        <YStack gap="$1.5">
                          {data.players.map((player, idx) => (
                            <XStack
                              key={`${player.key}-${idx}`}
                              justifyContent="space-between"
                              alignItems="center"
                              padding="$2"
                              backgroundColor="$red3"
                              borderRadius="$2"
                            >
                              <YStack flex={1}>
                                <Text fontSize="$3" color="$red12" fontWeight="600">
                                  {player.name}
                                </Text>
                                <Text fontSize="$2" color="$red10">
                                  {PlayerStatusHelper.asLabel(player.status)}
                                </Text>
                              </YStack>
                              <XStack gap="$2" alignItems="center">
                                <Text fontSize="$3" color="$red11" fontWeight="600">
                                  Pagato: €{player.price}
                                </Text>
                                {player.revenue != null && player.revenue > 0 && (
                                  <Text fontSize="$3" color="$green11" fontWeight="600">
                                    Venduto: €{player.revenue}
                                  </Text>
                                )}
                              </XStack>
                            </XStack>
                          ))}
                        </YStack>
                      </YStack>
                    </Card>
                  ))}
                </YStack>
              )}
            </Accordion.Content>
          </Accordion.HeightAnimator>
        </Accordion.Item>
      </Accordion>
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
        onSelectedRoleChange={handleRoleChange}
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
      <Accordion overflow="hidden" type="multiple" defaultValue={['header']}>
        <Accordion.Item value="header">
          <Accordion.Trigger 
            flexDirection="row" 
            justifyContent="space-between"
            padding="$4"
            backgroundColor="$blue2"
            borderRadius="$4"
            borderWidth="$0.5"
            borderColor="$blue8"
          >
            {({ open }: { open: boolean }) => (
              <>
                <XStack gap="$2" alignItems="center" flex={1}>
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
                  <Card padding="$1.5" paddingHorizontal="$2.5" backgroundColor="$orange2" borderRadius="$3" borderWidth="$0.5" borderColor="$orange8">
                    <Text fontSize="$3" fontWeight="700" color="$orange11">
                      🎯 {getRoleLabel(selectedRole)}
                    </Text>
                  </Card>
                  <Square transition="quick" rotate={open ? '180deg' : '0deg'}>
                    <ChevronDown size={20} color="$blue11" />
                  </Square>
                </XStack>
              </>
            )}
          </Accordion.Trigger>
          
          <Accordion.HeightAnimator transition="quick">
            <Accordion.Content 
              padding="$4" 
              paddingTop="$2"
              backgroundColor="$blue2"
              borderBottomLeftRadius="$4"
              borderBottomRightRadius="$4"
            >
              <YStack gap="$2">
                <XStack gap="$3" alignItems="center" justifyContent="space-between" flexWrap="wrap">
                  <XStack gap="$2" alignItems="center">
                    <Card padding="$2" paddingHorizontal="$3" backgroundColor="$blue1" borderRadius="$3">
                      <Text fontSize="$3" color="$blue11" fontWeight="600">{auctionKindLabel}</Text>
                    </Card>
                    <Card padding="$2" paddingHorizontal="$3" backgroundColor="$blue1" borderRadius="$3">
                      <Text fontSize="$3" color="$blue11" fontWeight="600">Lista: {auctionTypeLabel}</Text>
                    </Card>
                  </XStack>
                  <XStack gap="$2" alignItems="center" flexWrap="wrap">
                    <ToggleChip label="Audio" accent="green" checked={audioEnabled} onCheckedChange={setAudioEnabled} />
                    {Platform.OS !== 'web' && (
                      <ToggleChip label="Vibrazione" accent="green" checked={vibrationEnabled} onCheckedChange={setVibrationEnabled} />
                    )}
                  </XStack>
                </XStack>
                
                {/* UI Visibility Toggles */}
                <Card padding="$2" backgroundColor="$blue1" borderRadius="$3">
                  <YStack gap="$2.5">
                    <Text fontSize="$2" color="$blue11" fontWeight="600">Mostra/Nascondi Sezioni:</Text>
                    <XStack gap="$2" flexWrap="wrap">
                      {myTeam && (
                        <ToggleChip label="Pulsantiera" checked={showMyTeamPanel} onCheckedChange={setShowMyTeamPanel} />
                      )}
                      {isAuctioneer && (
                        <ToggleChip label="Pulsantiera Banditore" checked={showAuctioneerPanel} onCheckedChange={setShowAuctioneerPanel} />
                      )}
                      <ToggleChip label="Statistiche Giocatore" checked={showPlayerStats} onCheckedChange={setShowPlayerStats} />
                      {myTeam && (
                        <ToggleChip label="Statistiche Mia Squadra" checked={showMyTeamStats} onCheckedChange={setShowMyTeamStats} />
                      )}
                      <ToggleChip label="Squadre Partecipanti" checked={showTeamsPanel} onCheckedChange={setShowTeamsPanel} />
                      <ToggleChip label="Dettagli Squadre" checked={showTeamDetails} onCheckedChange={setShowTeamDetails} />
                      <ToggleChip label="Storico Acquisti" checked={showPurchaseHistory} onCheckedChange={setShowPurchaseHistory} />
                      <ToggleChip label="Giocatori Cacciati/Venduti" checked={showInactivePlayers} onCheckedChange={setShowInactivePlayers} />
                      <ToggleChip label="Reazioni Emoji" checked={showEmojis} onCheckedChange={setShowEmojis} />
                      <ToggleChip label="Audio Emoji" checked={emojiSoundEnabled} onCheckedChange={setEmojiSoundEnabled} />
                    </XStack>
                  </YStack>
                </Card>
                
                {/* Connected Users */}
                <Card padding="$2" backgroundColor="$blue1" borderRadius="$3">
                  <YStack gap="$2">
                    <Text fontSize="$2" color="$blue11" fontWeight="600">Utenti Connessi ({connectedUsers.length}):</Text>
                    {connectedUsers.length === 0 ? (
                      <Text fontSize="$2" color="$blue10">Nessun utente connesso</Text>
                    ) : (
                      <XStack gap="$2" flexWrap="wrap">
                        {connectedUsers.map(email => {
                          const isThisAuctioneer = email === auctionState.userEmail && auctionState.isAuctioneer
                          const team = teams.find(t => t.value.owner.toLowerCase() === email.toLowerCase())
                          const displayName = team?.value.name || email
                          
                          // Show transfer button if:
                          // 1. Current user IS auctioneer
                          // 2. Target user is NOT current user
                          // 3. This user is currently selected
                          // Backend will validate if target user is actually admin/superadmin
                          const canTransferToThisUser = isAuctioneer && email.toLowerCase() !== user?.email?.toLowerCase()
                          const isSelected = selectedUserForTransfer === email
                          
                          return (
                            <Card 
                              key={email} 
                              padding="$1.5" 
                              paddingHorizontal="$2" 
                              backgroundColor={isSelected ? "$blue4" : "$blue2"}
                              borderRadius="$2" 
                              borderWidth="$0.5" 
                              borderColor={isSelected ? "$blue9" : "$blue7"}
                              accessibilityLabel={email}
                              cursor={canTransferToThisUser ? "pointer" : "default"}
                              onPress={canTransferToThisUser ? () => setSelectedUserForTransfer(isSelected ? null : email) : undefined}
                            >
                              <YStack gap="$1">
                                <XStack gap="$1.5" alignItems="center">
                                  <Text fontSize="$2" color="$blue11" fontWeight="500">{displayName}</Text>
                                  {isThisAuctioneer && (
                                    <Card padding="$0.5" paddingHorizontal="$1.5" backgroundColor="$orange9" borderRadius="$2">
                                      <Text fontSize="$1" color="white" fontWeight="600">Banditore</Text>
                                    </Card>
                                  )}
                                  {team && (
                                    <Card padding="$0.5" paddingHorizontal="$1.5" backgroundColor="$green9" borderRadius="$2">
                                      <Text fontSize="$1" color="white" fontWeight="600">Giocatore</Text>
                                    </Card>
                                  )}
                                  {!isThisAuctioneer && !team && (
                                    <Card padding="$0.5" paddingHorizontal="$1.5" backgroundColor="$gray9" borderRadius="$2">
                                      <Text fontSize="$1" color="white" fontWeight="600">Ospite</Text>
                                    </Card>
                                  )}
                                </XStack>
                                {canTransferToThisUser && isSelected && (
                                  <Button 
                                    size="$2" 
                                    theme="orange" 
                                    onPress={(e) => {
                                      e.stopPropagation()
                                      handleTransferAuctioneer(email)
                                      setSelectedUserForTransfer(null)
                                    }}
                                    fontSize="$1"
                                  >
                                    🎯 Trasferisci Ruolo
                                  </Button>
                                )}
                              </YStack>
                            </Card>
                          )
                        })}
                      </XStack>
                    )}
                  </YStack>
                </Card>
              </YStack>
            </Accordion.Content>
          </Accordion.HeightAnimator>
        </Accordion.Item>
      </Accordion>
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
              {/* Layout responsivo basato su ruolo e device */}
              {!isAuctioneer && showMyTeamPanel ? (
                /* Caso 1: Giocatore normale (NON banditore) */
                <>
                  {media.gtMd ? (
                    <XStack gap="$4" alignItems="stretch">
                      <YStack flex={1}>{renderCurrentPlayer()}</YStack>
                      <YStack flex={1}>{renderParticipantBidding()}</YStack>
                    </XStack>
                  ) : (
                    <YStack gap="$4">
                      {renderCurrentPlayer()}
                      {renderParticipantBidding()}
                    </YStack>
                  )}
                </>
              ) : isAuctioneer && !myTeam ? (
                /* Caso 2: SOLO banditore (senza team) */
                <>
                  {media.gtMd ? (
                    <XStack gap="$4" alignItems="stretch">
                      <YStack flex={1}>{renderCurrentPlayer()}</YStack>
                      <YStack flex={1}>{renderAuctioneerPanel()}</YStack>
                    </XStack>
                  ) : (
                    <YStack gap="$4">
                      {renderCurrentPlayer()}
                      {renderAuctioneerPanel()}
                    </YStack>
                  )}
                </>
              ) : (
                /* Caso 3: Banditore con team (già gestito nel renderAuctioneerPanel) */
                <>
                  {renderCurrentPlayer()}
                  {renderAuctioneerPanel()}
                </>
              )}
              <RemainingPlayersPanel
                key={`remaining-${refreshKey}`}
                auctionState={auctionState}
                teams={teams}
                selectedRole={selectedRole}
                currentPlayer={currentPlayer}
                isAuctioneer={isAuctioneer}
                onForceShowPlayer={async (playerKey: string) => {
                  setLoading(true)
                  try {
                    await auctionService.showNextPlayer(auctionId, selectedRole, playerKey)
                  } catch (err) {
                    toast.show(`❌ ${err instanceof Error ? err.message : 'Errore sconosciuto'}`, { variant: 'error' })
                  } finally {
                    setLoading(false)
                  }
                }}
              />
              <DiscardedPlayersPanel
                key={`discarded-${refreshKey}`}
                auctionState={auctionState}
                teams={teams}
                selectedRole={selectedRole}
                currentPlayer={currentPlayer}
                isAuctioneer={isAuctioneer}
                onForceShowPlayer={async (playerKey: string) => {
                  setLoading(true)
                  try {
                    await auctionService.showNextPlayer(auctionId, selectedRole, playerKey)
                  } catch (err) {
                    toast.show(`❌ ${err instanceof Error ? err.message : 'Errore sconosciuto'}`, { variant: 'error' })
                  } finally {
                    setLoading(false)
                  }
                }}
              />
              {showTeamsPanel && renderTeams()}
              {renderPurchaseHistory()}
              {renderInactivePlayers()}
            </YStack>
          </YStack>
        </ScrollView>
        {renderCustomBidDialog()}
        {renderTeamDetailDialog()}
        {renderChangeStatusDialog()}
        {renderSubstituteSelectionDialog()}
        {renderPlayerSearchDialog()}
      </ScreenContent>
      
      {/* Floating player widget - rendered OUTSIDE ScreenContent to be truly fixed */}
      {currentPlayer && myTeam && (
        <FloatingPlayerWidget
          currentPlayer={currentPlayer}
          currentBid={currentBid}
          currentBidder={currentBidder}
          teams={teams}
          timerSecondsLeft={timerSecondsLeft}
          onIncrementBid={() => handleIncrementBid(1, false)}
          disabled={arePersonalBidButtonsDisabled().disabled}
        />
      )}
      
      {/* Floating emojis - rendered outside to animate freely */}
      {showEmojis && activeEmojis.map((emojiData) => (
        <FloatingEmoji
          key={emojiData.id}
          id={emojiData.id}
          emoji={emojiData.emoji}
          sender={emojiData.sender}
          onComplete={() => removeEmoji(emojiData.id)}
        />
      ))}
    </View>
  )
}
