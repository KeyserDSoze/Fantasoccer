// Auction service - HTTP calls and SignalR real-time connection
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
  PlayerAssignedData,
  PlayerWithStats,
  StatPlayer
} from '../types/auction'
import {
  mapRawActualAuction,
  statPlayerToPlayerWithStats,
  mapRawAuctionHubWrapperToAuctionState
} from '../types/auction'

// SignalR event handlers type
export interface AuctionEventHandlers {
  onConnected?: (message: string) => void
  onError?: (message: string) => void
  onUserJoined?: (email: string) => void
  onUserLeft?: (email: string) => void
  onPlayerShown?: (player: StatPlayer) => void
  onPlayerAssigned?: (data: PlayerAssignedData) => void
  onBidPlaced?: (data: { Amount: number; BidderEmail: string; BidderName: string; PlayerName: string; Substituted: string | null }) => void
  onAuctionUpdated?: () => void
  onAuctionPaused?: () => void
  onAuctionResumed?: () => void
  onAuctionClosed?: () => void
  onAuctionState?: (state: AuctionState) => void  // ✅ Now returns clean AuctionState
  onCurrentPlayer?: (player: PlayerWithStats | null) => void
  onCurrentAuctionReset?: () => void  // Reset all bids for current player
  onTimerReset?: () => void  // Restart timer and unlock bidding buttons
}

export class AuctionService {
  private hubConnection: HubConnection | null = null
  private eventHandlers: AuctionEventHandlers = {}
  private userEmail: string | null = null  // Store current user email for mapping

  // ============================================
  // HTTP METHODS (Auction Creation)
  // ============================================

  /**
   * Create a new auction (HTTP call)
   */
  async createAuction(auctionType: AuctionType, auctionKind: AuctionKind): Promise<string> {
    try {
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
    } catch (error) {
      if (error instanceof ApiError) throw error
      throw new ApiError(
        `Failed to create auction: ${error instanceof Error ? error.message : 'Unknown error'}`
      )
    }
  }

  // ============================================
  // SIGNALR CONNECTION MANAGEMENT
  // ============================================

  /**
   * Connect to auction hub
   */
  async connect(): Promise<void> {
    if (this.hubConnection?.state === 'Connected') {
      console.log('Already connected to auction hub')
      return
    }

    // ALWAYS force refresh token before connecting to ensure maximum validity
    console.log('🔄 Force refreshing token before SignalR connection...')
    const token = await tokenStorageService.forceRefreshToken()
    if (!token) {
      throw new Error('Failed to refresh authentication token before connection')
    }

    console.log('✅ Fresh token obtained for SignalR connection')

    const apiConfig = await import('../configurations/apiConfig').then(m => m.getApiConfig())
    const hubUrl = `${apiConfig.baseUrl}/auctionhub`
    
    console.log('🔌 Connecting to SignalR hub:', hubUrl)

    try {
      this.hubConnection = new HubConnectionBuilder()
        .withUrl(hubUrl, {
          accessTokenFactory: async () => {
            const currentToken = await tokenStorageService.ensureValidToken()
            console.log('🔑 Token factory called, token exists:', !!currentToken)
            if (!currentToken) {
              throw new Error('Token not available in factory')
            }
            return currentToken
          },
          skipNegotiation: false,
          transport: undefined, // Let SignalR choose the best transport
        })
        .withAutomaticReconnect({
          nextRetryDelayInMilliseconds: (retryContext) => {
            // Don't retry on auth errors
            if (retryContext.elapsedMilliseconds > 60000) {
              console.log('❌ Max reconnection time exceeded')
              return null // Stop retrying
            }
            // Exponential backoff: 2s, 4s, 8s, 16s, 30s
            const delay = Math.min(2000 * Math.pow(2, retryContext.previousRetryCount), 30000)
            console.log(`🔄 Retry attempt ${retryContext.previousRetryCount + 1}, waiting ${delay}ms`)
            return delay
          }
        })
        .configureLogging(LogLevel.Information)
        .build()

      // Register all event handlers BEFORE starting
      this.registerEventHandlers()
      
      // Add connection close handler for debugging
      this.hubConnection.onclose((error) => {
        console.log('❌ SignalR connection closed', error)
        if (error) {
          // Check if it's an auth error
          const errorMsg = error.message || error.toString()
          if (errorMsg.includes('401') || errorMsg.includes('Unauthorized')) {
            console.error('🔐 Authentication error - token may be invalid')
          }
        }
      })

      this.hubConnection.onreconnecting((error) => {
        console.log('🔄 SignalR reconnecting...', error)
      })

      this.hubConnection.onreconnected((connectionId) => {
        console.log('✅ SignalR reconnected:', connectionId)
      })

      await this.hubConnection.start()
      console.log('✅ Connected to auction hub, connectionId:', this.hubConnection?.connectionId)
    } catch (error) {
      console.error('❌ Failed to connect to auction hub:', error)
      
      const errorMsg = error instanceof Error ? error.message : String(error)
      
      // Check for authentication errors
      if (errorMsg.includes('401') || errorMsg.includes('Unauthorized') || errorMsg.includes('EventSource failed to connect')) {
        console.error('🔐 Authentication error detected')
        // Reset connection on auth failure
        this.hubConnection = null
        throw new Error('401 Unauthorized - Token invalido o scaduto')
      }
      
      console.error('Error details:', {
        message: error instanceof Error ? error.message : 'Unknown error',
        stack: error instanceof Error ? error.stack : undefined,
        hubConnectionState: this.hubConnection?.state
      })
      
      // Reset connection on failure
      this.hubConnection = null
      throw error
    }
  }

  /**
   * Disconnect from auction hub
   */
  async disconnect(): Promise<void> {
    if (this.hubConnection) {
      const state = this.hubConnection.state
      console.log('🔌 Disconnecting from auction hub, current state:', state)
      
      try {
        // Only stop if not already disconnected
        if (state === 'Connected' || state === 'Connecting' || state === 'Reconnecting') {
          await this.hubConnection.stop()
        }
        this.hubConnection = null
        console.log('✅ Disconnected from auction hub')
      } catch (error) {
        console.warn('⚠️ Error during disconnect (non-critical):', error)
        // Set to null anyway to allow reconnection
        this.hubConnection = null
      }
    }
  }

  /**
   * Check if connected
   */
  isConnected(): boolean {
    return this.hubConnection?.state === 'Connected'
  }

  /**
   * Register event handlers with SignalR hub
   */
  private registerEventHandlers(): void {
    if (!this.hubConnection) return

    // Connection events
    this.hubConnection.on('Connected', (message: string) => {
      console.log('Hub Connected:', message)
      this.eventHandlers.onConnected?.(message)
    })

    // Error events
    this.hubConnection.on('Error', (message: string) => {
      console.error('Hub Error:', message)
      this.eventHandlers.onError?.(message)
    })

    // User events
    this.hubConnection.on('UserJoined', (email: string) => {
      console.log('User joined:', email)
      this.eventHandlers.onUserJoined?.(email)
    })

    this.hubConnection.on('UserLeft', (email: string) => {
      console.log('User left:', email)
      this.eventHandlers.onUserLeft?.(email)
    })

    // Auction events
    this.hubConnection.on('PlayerShown', (actualAuction: ActualAuctionRaw) => {
      console.log('Player shown (raw):', actualAuction)
      const mapped = mapRawActualAuction(actualAuction)
      this.eventHandlers.onPlayerShown?.(mapped.player)
    })

    this.hubConnection.on('PlayerAssigned', (data: any) => {
      console.log('Player assigned:', data)
      if (this.eventHandlers.onPlayerAssigned) {
        this.eventHandlers.onPlayerAssigned(data)
      }
    })

    this.hubConnection.on('BidPlaced', (data: any) => {
      console.log('Bid placed:', data)
      if (this.eventHandlers.onBidPlaced) {
        this.eventHandlers.onBidPlaced(data)
      }
    })

    this.hubConnection.on('AuctionUpdated', () => {
      console.log('Auction updated')
      if (this.eventHandlers.onAuctionUpdated) {
        this.eventHandlers.onAuctionUpdated()
      }
    })

    this.hubConnection.on('AuctionPaused', () => {
      console.log('Auction paused')
      if (this.eventHandlers.onAuctionPaused) {
        this.eventHandlers.onAuctionPaused()
      }
    })

    this.hubConnection.on('AuctionResumed', () => {
      console.log('Auction resumed')
      if (this.eventHandlers.onAuctionResumed) {
        this.eventHandlers.onAuctionResumed()
      }
    })

    this.hubConnection.on('AuctionClosed', () => {
      console.log('Auction closed')
      if (this.eventHandlers.onAuctionClosed) {
        this.eventHandlers.onAuctionClosed()
      }
    })

    // State events
    this.hubConnection.on('AuctionState', (wrapperRaw: AuctionHubWrapperRaw) => {
      console.log('📦 Auction state received (raw):', wrapperRaw)
      console.log('📧 Current userEmail for mapping:', this.userEmail)
      
      try {
        console.log('✅ Auction state mapped successfully:', wrapperRaw)
        // Map raw data to clean AuctionState with current user context
        const mapped = mapRawAuctionHubWrapperToAuctionState(wrapperRaw, this.userEmail)
        console.log('✅ Auction state mapped successfully:', mapped)
        
        if (this.eventHandlers.onAuctionState) {
          console.log('🔔 Calling onAuctionState handler')
          this.eventHandlers.onAuctionState(mapped)
        } else {
          console.warn('⚠️ No onAuctionState handler registered')
        }
      } catch (error) {
        console.error('❌ Error mapping auction state:', error)
      }
    })

    this.hubConnection.on('CurrentPlayer', (player: PlayerWithStats | null) => {
      console.log('Current player:', player)
      this.eventHandlers.onCurrentPlayer?.(player)
    })

    // Reset events
    this.hubConnection.on('CurrentAuctionReset', () => {
      console.log('🔄 Current auction reset (all bids cleared)')
      this.eventHandlers.onCurrentAuctionReset?.()
    })

    this.hubConnection.on('TimerReset', () => {
      console.log('⏱️ Timer reset (countdown restarted)')
      this.eventHandlers.onTimerReset?.()
    })
  }

  /**
   * Set event handlers (call before connecting)
   */
  setEventHandlers(handlers: AuctionEventHandlers): void {
    this.eventHandlers = { ...this.eventHandlers, ...handlers }
  }

  /**
   * Set current user email for mapping auction state
   * Must be called before connecting to ensure correct isAuctioneer detection
   */
  setUserEmail(email: string | null): void {
    console.log('📧 Setting user email:', email)
    this.userEmail = email
    if (!email) {
      console.warn('⚠️ User email is null or undefined - isAuctioneer will always be false')
    }
  }

  // ============================================
  // SIGNALR HUB METHODS (INVOKE)
  // ============================================

  /**
   * Join an auction room
   */
  async joinAuction(auctionId: string): Promise<boolean> {
    if (!this.hubConnection) throw new Error('Not connected to hub')
    if (this.hubConnection.state !== 'Connected') {
      throw new Error(`Cannot join auction: connection state is ${this.hubConnection.state}`)
    }
    try {
      const result = await this.hubConnection.invoke<boolean>('JoinAuctionAsync', auctionId)
      return result
    } catch (error) {
      console.error('Failed to join auction:', error)
      throw error
    }
  }

  /**
   * Leave auction room
   */
  async leaveAuction(auctionId: string): Promise<void> {
    if (!this.hubConnection) return
    
    // Only invoke if connected
    if (this.hubConnection.state !== 'Connected') {
      console.log('⚠️ Cannot leave auction, connection state:', this.hubConnection.state)
      return
    }
    
    try {
      await this.hubConnection.invoke('LeaveAuctionAsync', auctionId)
      console.log('✅ Left auction:', auctionId)
    } catch (error) {
      console.warn('⚠️ Failed to leave auction (non-critical):', error)
    }
  }

  /**
   * Request current auction state
   */
  async getAuctionState(auctionId: string): Promise<void> {
    if (!this.hubConnection) throw new Error('Not connected to hub')
    console.log('📨 Requesting auction state for:', auctionId)
    await this.hubConnection.invoke('SendAuctionStateAsync', auctionId)
    console.log('✅ Auction state request sent')
  }

  /**
   * Get current player
   */
  async getCurrentPlayer(auctionId: string): Promise<void> {
    if (!this.hubConnection) throw new Error('Not connected to hub')
    await this.hubConnection.invoke('GetCurrentPlayerAsync', auctionId)
  }

  // ============================================
  // AUCTIONEER METHODS (Admin/SuperAdmin only)
  // ============================================

  /**
   * Place a bid on current player
   * @param auctionId - The auction ID
   * @param amount - Bid amount
   * @param bidderEmail - Optional: email of team to bid for (auctioneer only)
   * @param substituted - Optional: playerKey of player to substitute (repair auction only)
   */
  async placeBid(auctionId: string, amount: number, bidderEmail?: string, substituted?: string): Promise<void> {
    if (!this.hubConnection) throw new Error('Not connected to hub')
    await this.hubConnection.invoke('PlaceBidAsync', auctionId, amount, bidderEmail, substituted)
  }

  /**
   * Show next player for bidding
   */
  async showNextPlayer(auctionId: string, role: Role): Promise<void> {
    if (!this.hubConnection) throw new Error('Not connected to hub')
    await this.hubConnection.invoke('ShowNextPlayerAsync', auctionId, role)
  }

  /**
   * Assign current player to a team (uses current bid data from backend)
   */
  async assignPlayerToTeam(auctionId: string): Promise<void> {
    if (!this.hubConnection) throw new Error('Not connected to hub')
    await this.hubConnection.invoke('AssignPlayerToTeamAsync', auctionId)
  }

  /**
   * Pause auction
   */
  async pauseAuction(auctionId: string): Promise<void> {
    if (!this.hubConnection) throw new Error('Not connected to hub')
    await this.hubConnection.invoke('PauseAuctionAsync', auctionId)
  }

  /**
   * Resume auction
   */
  async resumeAuction(auctionId: string): Promise<void> {
    if (!this.hubConnection) throw new Error('Not connected to hub')
    await this.hubConnection.invoke('ResumeAuctionAsync', auctionId)
  }

  /**
   * Close auction permanently
   */
  async closeAuction(auctionId: string): Promise<void> {
    if (!this.hubConnection) throw new Error('Not connected to hub')
    await this.hubConnection.invoke('CloseAuctionAsync', auctionId)
  }

  /**
   * Change timer duration (Auctioneer only)
   */
  async changeTimer(auctionId: string, seconds: number): Promise<void> {
    if (!this.hubConnection) throw new Error('Not connected to hub')
    await this.hubConnection.invoke('ChangeTimerAsync', auctionId, seconds)
  }

  /**
   * Reset timer - restart countdown and unlock bidding (Auctioneer only)
   */
  async resetTimer(auctionId: string): Promise<void> {
    if (!this.hubConnection) throw new Error('Not connected to hub')
    await this.hubConnection.invoke('TimerResetAsync', auctionId)
  }

  /**
   * Complete reset - clear all bids for current player (Auctioneer only)
   */
  async completeReset(auctionId: string): Promise<void> {
    if (!this.hubConnection) throw new Error('Not connected to hub')
    await this.hubConnection.invoke('CompleteResetAsync', auctionId)
  }

  /**
   * Change player status in team (Auctioneer only)
   */
  async changePlayerStatus(
    auctionId: string, 
    owner: string, 
    playerKey: string, 
    status: number, 
    revenue: number | null
  ): Promise<void> {
    if (!this.hubConnection) throw new Error('Not connected to hub')
    await this.hubConnection.invoke('ChangePlayerStatusAsync', auctionId, owner, playerKey, status, revenue)
  }
}

// Export singleton instance
export const auctionService = new AuctionService()
