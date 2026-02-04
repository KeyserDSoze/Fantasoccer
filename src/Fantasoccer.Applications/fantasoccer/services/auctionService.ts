// Auction Service - SignalR real-time connection and HTTP API
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr'
import { buildApiUrl, getCommonHeaders, ApiError } from '../configurations/apiConfig'
import { tokenStorageService } from './tokenStorageService'
import type { Role } from '../types/group'
import type { 
  AuctionType,
  AuctionKind,
  AuctionState,
  AuctionHubWrapperRaw,
  ActualAuctionRaw,
  PlayerAssignedMessageRaw,
  StatPlayer,
  TeamEntityRaw,
  ActualAuction,
  PlayerAssignedMessage,
  BidPlaced,
  BidPlacedRaw
} from '../types/auction'
import type { StatPlayerRaw } from '../types/stat'
import { mapRawStatPlayerToStatPlayer } from '../types/stat'
import type { Entity } from '../types/default'
import type { Team, TeamKey } from '../types/team'
import {
  mapRawActualAuction,
  mapRawAuctionHubWrapperToAuctionState,
  mapRawBidPlaced,
  mapRawPlayerAssignedMessage,
  mapRawTeamEntity
} from '../types/auction'

// ============================================
// TYPES & INTERFACES
// ============================================

/**
 * SignalR event handlers
 * These are called when the server sends events to the client
 */
export interface AuctionEventHandlers {
  // Connection lifecycle
  onConnected?: (connectionId: string) => void
  onReconnecting?: () => void
  onReconnected?: (connectionId: string) => void
  onDisconnected?: (error?: Error) => void
  
  // Server events
  onError?: (message: string) => void
  onUserJoined?: (email: string) => void
  onUserLeft?: (email: string) => void
  
  // Auction events - UPDATED
  onPlayerShown?: (actualAuction: ActualAuction) => void  // Now receives full ActualAuction
  onPlayerAssigned?: (data: PlayerAssignedMessage) => void  // Now receives PlayerAssignedMessageInAuction
  onBidPlaced?: (data: BidPlaced) => void
  onTeamUpdated?: (team: Entity<Team, TeamKey>) => void  // NEW - Single team update
  onTimerUpdate?: (seconds: number) => void  // NEW - Timer value changed
  
  // Auction control events
  onAuctionPaused?: () => void
  onAuctionResumed?: () => void
  onAuctionClosed?: () => void
  onCurrentAuctionReset?: () => void
  onCurrentAuctionClosed?: () => void
  onTimerReset?: () => void
}

/**
 * Connection state for tracking SignalR status
 */
export type ConnectionStatus = 
  | 'Disconnected' 
  | 'Connecting' 
  | 'Connected' 
  | 'Reconnecting' 
  | 'Failed'

// ============================================
// AUCTION SERVICE CLASS
// ============================================

export class AuctionService {
  private hubConnection: HubConnection | null = null
  private eventHandlers: AuctionEventHandlers = {}
  private userEmail: string | null = null
  private isInitialized = false
  private currentAuctionId: string | null = null

  // ============================================
  // HTTP API METHODS
  // ============================================

  /**
   * Create a new auction via HTTP
   */
  async createAuction(auctionType: AuctionType, auctionKind: AuctionKind): Promise<string> {
    const token = await tokenStorageService.ensureValidToken()
    if (!token) {
      throw new ApiError('No authentication token available', 401)
    }

    const url = buildApiUrl('/Auction/CreateAuction', {
      auctionType: auctionType.toString(),
      auctionKind: auctionKind.toString()
    })

    const response = await fetch(url, {
      method: 'GET',
      headers: {
        ...getCommonHeaders(),
        'Authorization': `Bearer ${token}`
      },
    })

    if (!response.ok) {
      const errorText = await response.text()
      throw new ApiError(
        `Create auction failed: ${response.status} ${response.statusText}`,
        response.status,
        errorText
      )
    }

    const auctionId = await response.text()
    return auctionId.replace(/^"|"$/g, '')
  }

  /**
   * Get auction state via HTTP (called once at startup)
   * This replaces the old SendAuctionStateAsync SignalR method
   */
  async getAuction(auctionId: string): Promise<AuctionState> {
    const token = await tokenStorageService.ensureValidToken()
    if (!token) {
      throw new ApiError('No authentication token available', 401)
    }

    const url = buildApiUrl('/Auction/GetAuction', {
      auctionId: auctionId
    })

    console.log('📨 HTTP GET /Auction/GetAuction:', auctionId)

    const response = await fetch(url, {
      method: 'GET',
      headers: {
        ...getCommonHeaders(),
        'Authorization': `Bearer ${token}`
      },
    })

    if (!response.ok) {
      const errorText = await response.text()
      throw new ApiError(
        `Get auction failed: ${response.status} ${response.statusText}`,
        response.status,
        errorText
      )
    }

    const wrapperRaw: AuctionHubWrapperRaw = await response.json()
    console.log('✅ Auction data received:', wrapperRaw)

    // Map to AuctionState
    const mapped = mapRawAuctionHubWrapperToAuctionState(wrapperRaw, this.userEmail)
    console.log('✅ Auction state mapped:', mapped)

    return mapped
  }

  // ============================================
  // CONNECTION LIFECYCLE
  // ============================================

  /**
   * Initialize and connect to SignalR hub
   * This should be called AFTER getting initial state via HTTP
   */
  async connect(auctionId: string, userEmail: string): Promise<void> {
    console.log('🔌 AuctionService.connect() called', { auctionId, userEmail, currentState: this.getConnectionState() })

    // Store context
    this.currentAuctionId = auctionId
    this.userEmail = userEmail

    // If already connected to the same auction, just return
    if (this.hubConnection?.state === 'Connected' && this.isInitialized) {
      console.log('✅ Already connected to auction hub')
      return
    }

    // If there's an existing connection in any state, clean it up first
    if (this.hubConnection) {
      console.log('⚠️ Cleaning up existing connection before reconnecting')
      await this.cleanup()
    }

    // Build hub URL
    const apiConfig = await import('../configurations/apiConfig').then(m => m.getApiConfig())
    const hubUrl = `${apiConfig.baseUrl}/auctionhub`
    
    console.log('🔌 Building SignalR connection to:', hubUrl)

    // Create connection
    this.hubConnection = new HubConnectionBuilder()
      .withUrl(hubUrl, {
        accessTokenFactory: async () => {
          // This is called on EVERY connection attempt (initial + reconnections)
          // Use ensureValidToken to avoid unnecessary refreshes
          console.log('🔑 accessTokenFactory called - ensuring valid token...')
          const token = await tokenStorageService.ensureValidToken()
          if (!token) {
            console.error('❌ Token not available in accessTokenFactory')
            throw new Error('Authentication token not available')
          }
          return token
        },
      })
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (retryContext) => {
          // Stop retrying after 60 seconds total
          if (retryContext.elapsedMilliseconds > 60000) {
            console.log('❌ Reconnection timeout exceeded (60s)')
            return null
          }
          // Exponential backoff: 0s, 2s, 4s, 8s, 16s, 30s
          const delay = retryContext.previousRetryCount === 0 
            ? 0 
            : Math.min(2000 * Math.pow(2, retryContext.previousRetryCount - 1), 30000)
          console.log(`🔄 Reconnection attempt ${retryContext.previousRetryCount + 1}, delay: ${delay}ms`)
          return delay
        }
      })
      .configureLogging(LogLevel.Information)
      .build()

    // Setup lifecycle handlers
    this.setupConnectionLifecycle()

    // Register event handlers for auction events
    this.registerEventHandlers()

    // Start connection
    try {
      console.log('🚀 Starting SignalR connection...')
      await this.hubConnection.start()
      console.log('✅ SignalR connected, connectionId:', this.hubConnection.connectionId)
      
      this.isInitialized = true

      // Notify connected
      this.eventHandlers.onConnected?.(this.hubConnection.connectionId || '')
      
    } catch (error) {
      console.error('❌ Failed to start SignalR connection:', error)
      
      // Check for auth errors
      const errorMsg = error instanceof Error ? error.message : String(error)
      if (errorMsg.includes('401') || errorMsg.includes('Unauthorized')) {
        throw new Error('Sessione scaduta - effettua nuovamente il login')
      }
      
      // Cleanup on failure
      await this.cleanup()
      throw error
    }
  }

  /**
   * Setup connection lifecycle handlers (onclose, onreconnecting, onreconnected)
   * These are managed by SignalR automatically and should NOT be called manually
   */
  private setupConnectionLifecycle(): void {
    if (!this.hubConnection) return

    // Connection closed (network loss, server shutdown, manual disconnect, etc.)
    this.hubConnection.onclose((error) => {
      console.log('🔌 SignalR connection closed', error)
      this.isInitialized = false
      
      // Notify component
      this.eventHandlers.onDisconnected?.(error instanceof Error ? error : undefined)
      
      // Check for auth errors
      if (error) {
        const errorMsg = error.message || error.toString()
        if (errorMsg.includes('401') || errorMsg.includes('Unauthorized')) {
          console.error('🔐 Connection closed due to authentication error')
        }
      }
    })

    // Automatic reconnection started
    this.hubConnection.onreconnecting((error) => {
      console.log('🔄 SignalR reconnecting...', error)
      this.eventHandlers.onReconnecting?.()
    })

    // Automatic reconnection succeeded
    this.hubConnection.onreconnected(async (connectionId) => {
      console.log('✅ SignalR reconnected, new connectionId:', connectionId)
      
      // CRITICAL: Re-register event handlers
      console.log('🔄 Re-registering event handlers after reconnection...')
      this.registerEventHandlers()
      
      // CRITICAL: Rejoin the auction room
      if (this.currentAuctionId) {
        console.log('🔄 Rejoining auction after reconnection...')
        try {
          await this.joinAuction(this.currentAuctionId)
          console.log('✅ Rejoined auction successfully')
        } catch (error) {
          console.error('❌ Failed to rejoin auction after reconnection:', error)
        }
      }
      
      // Notify component
      this.eventHandlers.onReconnected?.(connectionId || '')
    })
  }

  /**
   * Cleanup connection (used internally, not exposed)
   */
  private async cleanup(): Promise<void> {
    if (!this.hubConnection) return

    console.log('🧹 Cleaning up SignalR connection...')
    
    try {
      const state = this.hubConnection.state
      
      // Only stop if in a stoppable state
      if (state === 'Connected' || state === 'Connecting' || state === 'Reconnecting') {
        await this.hubConnection.stop()
      }
      
    } catch (error) {
      console.warn('⚠️ Error during cleanup (non-critical):', error)
    } finally {
      this.hubConnection = null
      this.isInitialized = false
      console.log('✅ Cleanup complete')
    }
  }

  /**
   * Disconnect from hub (should only be called when leaving the app entirely)
   */
  async disconnect(): Promise<void> {
    console.log('🔌 Explicit disconnect requested')
    this.currentAuctionId = null
    await this.cleanup()
  }

  /**
   * Get current connection state
   */
  getConnectionState(): ConnectionStatus {
    if (!this.hubConnection) return 'Disconnected'
    
    const state = this.hubConnection.state
    switch (state) {
      case 'Connected': return 'Connected'
      case 'Connecting': return 'Connecting'
      case 'Reconnecting': return 'Reconnecting'
      case 'Disconnected': return 'Disconnected'
      default: return 'Failed'
    }
  }

  /**
   * Check if currently connected
   */
  isConnected(): boolean {
    return this.hubConnection?.state === 'Connected'
  }

  // ============================================
  // EVENT HANDLERS REGISTRATION
  // ============================================

  /**
   * Set event handlers BEFORE connecting
   */
  setEventHandlers(handlers: AuctionEventHandlers): void {
    this.eventHandlers = { ...handlers }
  }

  /**
   * Register all SignalR event handlers
   * This is called once on connect and again after each automatic reconnection
   */
  private registerEventHandlers(): void {
    if (!this.hubConnection) {
      console.warn('⚠️ Cannot register handlers: no hub connection')
      return
    }

    console.log('📋 Registering SignalR event handlers...')

    // Remove all existing handlers to prevent duplicates
    this.hubConnection.off('Connected')
    this.hubConnection.off('Error')
    this.hubConnection.off('UserJoined')
    this.hubConnection.off('UserLeft')
    this.hubConnection.off('PlayerShown')
    this.hubConnection.off('PlayerAssigned')
    this.hubConnection.off('BidPlaced')
    this.hubConnection.off('TeamUpdated')
    this.hubConnection.off('TimerUpdate')
    this.hubConnection.off('AuctionPaused')
    this.hubConnection.off('AuctionResumed')
    this.hubConnection.off('AuctionClosed')
    this.hubConnection.off('CurrentAuctionReset')
    this.hubConnection.off('TimerReset')

    // Register fresh handlers
    this.hubConnection.on('Connected', (message: string) => {
      console.log('📨 Hub.Connected:', message)
    })

    this.hubConnection.on('Error', (message: string) => {
      console.error('📨 Hub.Error:', message)
      this.eventHandlers.onError?.(message)
    })

    this.hubConnection.on('UserJoined', (email: string) => {
      console.log('📨 Hub.UserJoined:', email)
      this.eventHandlers.onUserJoined?.(email)
    })

    this.hubConnection.on('UserLeft', (email: string) => {
      console.log('📨 Hub.UserLeft:', email)
      this.eventHandlers.onUserLeft?.(email)
    })

    // PlayerShown - Now receives full ActualAuction object
    this.hubConnection.on('PlayerShown', (actualAuctionRaw: ActualAuctionRaw) => {
      console.log('📨 Hub.PlayerShown (ActualAuction):', actualAuctionRaw)
      try {
        // Map and call handler
        this.eventHandlers.onPlayerShown?.(mapRawActualAuction(actualAuctionRaw))
      } catch (error) {
        console.error('❌ Error processing PlayerShown:', error)
      }
    })

    // PlayerAssigned - Now receives PlayerAssignedMessageInAuction
    this.hubConnection.on('PlayerAssigned', (dataRaw: PlayerAssignedMessageRaw) => {
      console.log('📨 Hub.PlayerAssigned (PlayerAssignedMessageInAuction):', dataRaw)
      try {
        // Map and call handler
        this.eventHandlers.onPlayerAssigned?.(mapRawPlayerAssignedMessage(dataRaw))
      } catch (error) {
        console.error('❌ Error processing PlayerAssigned:', error)
      }
    })

    this.hubConnection.on('BidPlaced', (dataRaw: BidPlacedRaw) => {
      console.log('📨 Hub.BidPlaced:', dataRaw)
      try {
        // Map and call handler
        this.eventHandlers.onBidPlaced?.(mapRawBidPlaced(dataRaw))
      } catch (error) {
        console.error('❌ Error processing BidPlaced:', error)
      }
    })

    // NEW: TeamUpdated - Single team update
    this.hubConnection.on('TeamUpdated', (teamRaw: TeamEntityRaw) => {
      console.log('📨 Hub.TeamUpdated:', teamRaw)
      try {
        const mapped = mapRawTeamEntity(teamRaw)
        this.eventHandlers.onTeamUpdated?.(mapped)
      } catch (error) {
        console.error('❌ Error mapping TeamUpdated:', error)
      }
    })

    // NEW: TimerUpdate - Timer value changed
    this.hubConnection.on('TimerUpdate', (seconds: number) => {
      console.log('📨 Hub.TimerUpdate:', seconds)
      this.eventHandlers.onTimerUpdate?.(seconds)
    })

    this.hubConnection.on('AuctionPaused', () => {
      console.log('📨 Hub.AuctionPaused')
      this.eventHandlers.onAuctionPaused?.()
    })

    this.hubConnection.on('AuctionResumed', () => {
      console.log('📨 Hub.AuctionResumed')
      this.eventHandlers.onAuctionResumed?.()
    })

    this.hubConnection.on('AuctionClosed', () => {
      console.log('📨 Hub.AuctionClosed')
      this.eventHandlers.onAuctionClosed?.()
    })

    this.hubConnection.on('CurrentAuctionReset', () => {
      console.log('📨 Hub.CurrentAuctionReset')
      this.eventHandlers.onCurrentAuctionReset?.()
    })

    this.hubConnection.on('CurrentAuctionClosed', () => {
      console.log('📨 Hub.CurrentAuctionClosed')
      this.eventHandlers.onCurrentAuctionClosed?.()
    })

    this.hubConnection.on('TimerReset', () => {
      console.log('📨 Hub.TimerReset')
      this.eventHandlers.onTimerReset?.()
    })

    console.log('✅ Event handlers registered successfully')
  }

  // ============================================
  // SIGNALR HUB METHODS (INVOKE)
  // ============================================

  /**
   * Ensure we're connected before invoking methods
   */
  private ensureConnected(): void {
    if (!this.hubConnection) {
      throw new Error('Not connected to SignalR hub')
    }
    if (this.hubConnection.state !== 'Connected') {
      throw new Error(`Cannot invoke method: connection state is ${this.hubConnection.state}`)
    }
  }

  /**
   * Join auction room
   * This must be called after connection to receive auction events
   */
  async joinAuction(auctionId: string): Promise<boolean> {
    this.ensureConnected()
    
    console.log('📨 Invoking JoinAuctionAsync:', auctionId)
    
    try {
      const result = await this.hubConnection!.invoke<boolean>('JoinAuctionAsync', auctionId)
      console.log('✅ JoinAuctionAsync result:', result)
      return result
    } catch (error) {
      console.error('❌ JoinAuctionAsync failed:', error)
      throw this.handleInvokeError(error, 'join auction')
    }
  }

  /**
   * Leave auction room
   * This is called when navigating away from the auction screen
   */
  async leaveAuction(auctionId: string): Promise<void> {
    // Only leave if we're actually connected
    if (!this.hubConnection || this.hubConnection.state !== 'Connected') {
      console.log('⚠️ Cannot leave auction: not connected')
      return
    }
    
    console.log('📨 Invoking LeaveAuctionAsync:', auctionId)
    
    try {
      await this.hubConnection.invoke('LeaveAuctionAsync', auctionId)
      console.log('✅ Left auction room')
    } catch (error) {
      // Non-critical error, just log it
      console.warn('⚠️ LeaveAuctionAsync failed (non-critical):', error)
    }
  }

  /**
   * Get connected users (participants) in auction
   */
  async getConnectedUsers(auctionId: string): Promise<string[]> {
    this.ensureConnected()
    
    console.log('📨 Invoking GetConnectedUsersAsync:', auctionId)
    
    try {
      const result = await this.hubConnection!.invoke<string[]>('GetConnectedUsersAsync', auctionId)
      console.log('✅ GetConnectedUsersAsync result:', result)
      return result || []
    } catch (error) {
      console.error('❌ GetConnectedUsersAsync failed:', error)
      return []
    }
  }

  /**
   * Place a bid on the current player
   */
  async placeBid(
    auctionId: string, 
    amount: number, 
    bidderEmail?: string, 
    substituted?: string
  ): Promise<void> {
    this.ensureConnected()
    
    console.log('📨 Invoking PlaceBidAsync:', { auctionId, amount, bidderEmail, substituted })
    
    try {
      await this.hubConnection!.invoke('PlaceBidAsync', auctionId, amount, bidderEmail, substituted)
      console.log('✅ PlaceBidAsync invoked successfully')
    } catch (error) {
      console.error('❌ PlaceBidAsync failed:', error)
      throw this.handleInvokeError(error, 'place bid')
    }
  }

  // ============================================
  // AUCTIONEER METHODS
  // ============================================

  /**
   * Show next player for the given role
   */
  async showNextPlayer(auctionId: string, role: Role, forcedPlayerKey?: string | null): Promise<void> {
    this.ensureConnected()

    console.log('📨 Invoking ShowNextPlayerAsync:', { auctionId, role, forcedPlayerKey })

    try {
      await this.hubConnection!.invoke('ShowNextPlayerAsync', auctionId, role, forcedPlayerKey)
      console.log('✅ ShowNextPlayerAsync invoked')
    } catch (error) {
      console.error('❌ ShowNextPlayerAsync failed:', error)
      throw this.handleInvokeError(error, 'show next player')
    }
  }

  /**
   * Assign current player to the highest bidder's team
   */
  async assignPlayerToTeam(auctionId: string): Promise<void> {
    this.ensureConnected()
    
    console.log('📨 Invoking AssignPlayerToTeamAsync:', auctionId)
    
    try {
      await this.hubConnection!.invoke('AssignPlayerToTeamAsync', auctionId)
      console.log('✅ AssignPlayerToTeamAsync invoked')
    } catch (error) {
      console.error('❌ AssignPlayerToTeamAsync failed:', error)
      throw this.handleInvokeError(error, 'assign player')
    }
  }

  /**
   * Pause the auction
   */
  async pauseAuction(auctionId: string): Promise<void> {
    this.ensureConnected()
    
    console.log('📨 Invoking PauseAuctionAsync:', auctionId)
    
    try {
      await this.hubConnection!.invoke('PauseAuctionAsync', auctionId)
      console.log('✅ PauseAuctionAsync invoked')
    } catch (error) {
      console.error('❌ PauseAuctionAsync failed:', error)
      throw this.handleInvokeError(error, 'pause auction')
    }
  }

  /**
   * Resume the auction
   */
  async resumeAuction(auctionId: string): Promise<void> {
    this.ensureConnected()
    
    console.log('📨 Invoking ResumeAuctionAsync:', auctionId)
    
    try {
      await this.hubConnection!.invoke('ResumeAuctionAsync', auctionId)
      console.log('✅ ResumeAuctionAsync invoked')
    } catch (error) {
      console.error('❌ ResumeAuctionAsync failed:', error)
      throw this.handleInvokeError(error, 'resume auction')
    }
  }

  /**
   * Close the auction permanently
   */
  async closeAuction(auctionId: string): Promise<void> {
    this.ensureConnected()
    
    console.log('📨 Invoking CloseAuctionAsync:', auctionId)
    
    try {
      await this.hubConnection!.invoke('CloseAuctionAsync', auctionId)
      console.log('✅ CloseAuctionAsync invoked')
    } catch (error) {
      console.error('❌ CloseAuctionAsync failed:', error)
      throw this.handleInvokeError(error, 'close auction')
    }
  }

  /**
   * Finalize the auction - Close and save all teams with next day lineup
   */
  async finalizeAuction(auctionId: string): Promise<void> {
    this.ensureConnected()
    
    console.log('📨 Invoking FinalizeAuctionAsync:', auctionId)
    
    try {
      await this.hubConnection!.invoke('FinalizeAuctionAsync', auctionId)
      console.log('✅ FinalizeAuctionAsync invoked')
    } catch (error) {
      console.error('❌ FinalizeAuctionAsync failed:', error)
      throw this.handleInvokeError(error, 'finalize auction')
    }
  }

  /**
   * Reopen a finalized auction
   */
  async reopenAuction(auctionId: string): Promise<void> {
    this.ensureConnected()
    
    console.log('📨 Invoking ReopenAuctionAsync:', auctionId)
    
    try {
      await this.hubConnection!.invoke('ReopenAuctionAsync', auctionId)
      console.log('✅ ReopenAuctionAsync invoked')
    } catch (error) {
      console.error('❌ ReopenAuctionAsync failed:', error)
      throw this.handleInvokeError(error, 'reopen auction')
    }
  }

  /**
   * Close current auction (open for call)
   */
  async closeCurrentAuction(auctionId: string): Promise<void> {
    this.ensureConnected()
    
    console.log('📨 Invoking CloseCurrentAuctionAsync:', auctionId)
    
    try {
      await this.hubConnection!.invoke('CloseCurrentAuctionAsync', auctionId)
      console.log('✅ CloseCurrentAuctionAsync invoked')
    } catch (error) {
      console.error('❌ CloseCurrentAuctionAsync failed:', error)
      throw this.handleInvokeError(error, 'close current auction')
    }
  }

  /**
   * Get discarded players for a specific role
   */
  async getDiscardedPlayers(auctionId: string, role: number): Promise<StatPlayer[]> {
    this.ensureConnected()
    
    console.log('📨 Invoking GetDiscardedPlayersAsync:', { auctionId, role })
    
    try {
      const rawResult: StatPlayerRaw[] = await this.hubConnection!.invoke('GetDiscardedPlayersAsync', auctionId, role)
      console.log('✅ GetDiscardedPlayersAsync raw result:', rawResult)
      
      // Map raw data to StatPlayer
      const players = (rawResult || []).map(mapRawStatPlayerToStatPlayer)
      console.log('✅ Mapped players:', players)
      
      return players
    } catch (error) {
      console.error('❌ GetDiscardedPlayersAsync failed:', error)
      throw this.handleInvokeError(error, 'get discarded players')
    }
  }

  /**
   * Change timer duration
   */
  async changeTimer(auctionId: string, seconds: number): Promise<void> {
    this.ensureConnected()
    
    console.log('📨 Invoking ChangeTimerAsync:', { auctionId, seconds })
    
    try {
      await this.hubConnection!.invoke('ChangeTimerAsync', auctionId, seconds)
      console.log('✅ ChangeTimerAsync invoked')
    } catch (error) {
      console.error('❌ ChangeTimerAsync failed:', error)
      throw this.handleInvokeError(error, 'change timer')
    }
  }

  /**
   * Reset timer (restart countdown)
   */
  async resetTimer(auctionId: string): Promise<void> {
    this.ensureConnected()
    
    console.log('📨 Invoking TimerResetAsync:', auctionId)
    
    try {
      await this.hubConnection!.invoke('TimerResetAsync', auctionId)
      console.log('✅ TimerResetAsync invoked')
    } catch (error) {
      console.error('❌ TimerResetAsync failed:', error)
      throw this.handleInvokeError(error, 'reset timer')
    }
  }

  /**
   * Complete reset - clear all bids for current player
   */
  async completeReset(auctionId: string): Promise<void> {
    this.ensureConnected()
    
    console.log('📨 Invoking CompleteResetAsync:', auctionId)
    
    try {
      await this.hubConnection!.invoke('CompleteResetAsync', auctionId)
      console.log('✅ CompleteResetAsync invoked')
    } catch (error) {
      console.error('❌ CompleteResetAsync failed:', error)
      throw this.handleInvokeError(error, 'complete reset')
    }
  }

  /**
   * Change player status in a team
   */
  async changePlayerStatus(
    auctionId: string,
    owner: string,
    playerKey: string,
    status: number,
    revenue: number | null
  ): Promise<void> {
    this.ensureConnected()
    
    console.log('📨 Invoking ChangePlayerStatusAsync:', { auctionId, owner, playerKey, status, revenue })
    
    try {
      await this.hubConnection!.invoke('ChangePlayerStatusAsync', auctionId, owner, playerKey, status, revenue)
      console.log('✅ ChangePlayerStatusAsync invoked')
    } catch (error) {
      console.error('❌ ChangePlayerStatusAsync failed:', error)
      throw this.handleInvokeError(error, 'change player status')
    }
  }

  // ============================================
  // ERROR HANDLING
  // ============================================

  /**
   * Handle errors from SignalR invoke calls
   */
  private handleInvokeError(error: unknown, operation: string): Error {
    const errorMsg = error instanceof Error ? error.message : String(error)
    
    // Check for authentication errors
    if (errorMsg.includes('401') || errorMsg.includes('Unauthorized')) {
      return new Error('Sessione scaduta - ricarica la pagina per continuare')
    }
    
    // Check for connection errors
    if (errorMsg.includes('Connection') || errorMsg.includes('connection')) {
      return new Error('Connessione persa - verifica la tua connessione internet')
    }
    
    // Generic error
    return new Error(`Errore durante ${operation}: ${errorMsg}`)
  }
}

// ============================================
// SINGLETON EXPORT
// ============================================

/**
 * Singleton instance of AuctionService
 * Use this for all auction-related operations
 */
export const auctionService = new AuctionService()
