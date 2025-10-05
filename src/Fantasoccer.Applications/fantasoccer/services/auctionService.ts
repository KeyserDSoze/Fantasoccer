// Auction service - HTTP calls and SignalR real-time connection
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr'
import { buildApiUrl, getCommonHeaders, ApiError } from '../configurations/apiConfig'
import { tokenStorageService } from './tokenStorageService'
import type { Role } from '../types/group'
import type { 
  AuctionType, 
  AuctionState, 
  PlayerShown, 
  PlayerWithStats,
  AuctionTeam,
  AvailablePlayers
} from '../types/auction'

// SignalR event handlers type
export interface AuctionEventHandlers {
  onConnected?: (message: string) => void
  onError?: (message: string) => void
  onUserJoined?: (email: string) => void
  onUserLeft?: (email: string) => void
  onPlayerShown?: (player: PlayerShown) => void
  onPlayerAssigned?: (data: {
    owner: string
    teamName: string
    price: number
    playerName: string
    teamCost: number
    teamPlayersCount: number
  }) => void
  onAuctionUpdated?: () => void
  onAuctionPaused?: () => void
  onAuctionResumed?: () => void
  onAuctionClosed?: () => void
  onAuctionState?: (state: AuctionState) => void
  onTeamsData?: (teams: AuctionTeam[]) => void
  onCurrentPlayer?: (player: PlayerWithStats | null) => void
  onAvailablePlayers?: (data: AvailablePlayers) => void
}

export class AuctionService {
  private hubConnection: HubConnection | null = null
  private eventHandlers: AuctionEventHandlers = {}

  // ============================================
  // HTTP METHODS (Auction Creation)
  // ============================================

  /**
   * Create a new auction (HTTP call)
   */
  async createAuction(auctionType: AuctionType): Promise<string> {
    try {
      const token = await tokenStorageService.ensureValidToken()
      if (!token) {
        throw new ApiError('No authentication token available', 401)
      }

      const url = buildApiUrl('/Auction/CreateAuction', {
        auctionType: auctionType.toString()
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

    const token = await tokenStorageService.ensureValidToken()
    if (!token) {
      throw new Error('No authentication token available')
    }

    console.log('🔑 Token available for SignalR connection')

    const apiConfig = await import('../configurations/apiConfig').then(m => m.getApiConfig())
    const hubUrl = `${apiConfig.baseUrl}/auctionhub`
    
    console.log('🔌 Connecting to SignalR hub:', hubUrl)

    this.hubConnection = new HubConnectionBuilder()
      .withUrl(hubUrl, {
        accessTokenFactory: async () => {
          const currentToken = await tokenStorageService.ensureValidToken()
          console.log('🔑 Token factory called, token exists:', !!currentToken)
          return currentToken || ''
        }
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Information)
      .build()

    // Register all event handlers
    this.registerEventHandlers()
    
    // Add connection close handler for debugging
    this.hubConnection.onclose((error) => {
      console.log('❌ SignalR connection closed', error)
    })

    this.hubConnection.onreconnecting((error) => {
      console.log('🔄 SignalR reconnecting...', error)
    })

    this.hubConnection.onreconnected((connectionId) => {
      console.log('✅ SignalR reconnected:', connectionId)
    })

    try {
      await this.hubConnection.start()
      console.log('✅ Connected to auction hub, connectionId:', this.hubConnection.connectionId)
    } catch (error) {
      console.error('❌ Failed to connect to auction hub:', error)
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
    this.hubConnection.on('PlayerShown', (player: PlayerShown) => {
      console.log('Player shown:', player)
      this.eventHandlers.onPlayerShown?.(player)
    })

    this.hubConnection.on('PlayerAssigned', (data: any) => {
      console.log('Player assigned:', data)
      if (this.eventHandlers.onPlayerAssigned) {
        this.eventHandlers.onPlayerAssigned(data)
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
    this.hubConnection.on('AuctionState', (state: AuctionState) => {
      console.log('Auction state received:', state)
      this.eventHandlers.onAuctionState?.(state)
    })

    this.hubConnection.on('TeamsData', (teams: AuctionTeam[]) => {
      console.log('Teams data received:', teams)
      this.eventHandlers.onTeamsData?.(teams)
    })

    this.hubConnection.on('CurrentPlayer', (player: PlayerWithStats | null) => {
      console.log('Current player:', player)
      this.eventHandlers.onCurrentPlayer?.(player)
    })

    this.hubConnection.on('AvailablePlayers', (data: AvailablePlayers) => {
      console.log('Available players:', data)
      this.eventHandlers.onAvailablePlayers?.(data)
    })
  }

  /**
   * Set event handlers (call before connecting)
   */
  setEventHandlers(handlers: AuctionEventHandlers): void {
    this.eventHandlers = { ...this.eventHandlers, ...handlers }
  }

  // ============================================
  // SIGNALR HUB METHODS (INVOKE)
  // ============================================

  /**
   * Join an auction room
   */
  async joinAuction(auctionId: string): Promise<boolean> {
    if (!this.hubConnection) throw new Error('Not connected to hub')
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
    await this.hubConnection.invoke('SendAuctionStateAsync', auctionId)
  }

  /**
   * Get all teams
   */
  async getAllTeams(auctionId: string): Promise<void> {
    if (!this.hubConnection) throw new Error('Not connected to hub')
    await this.hubConnection.invoke('GetAllTeamsAsync', auctionId)
  }

  /**
   * Get current player
   */
  async getCurrentPlayer(auctionId: string): Promise<void> {
    if (!this.hubConnection) throw new Error('Not connected to hub')
    await this.hubConnection.invoke('GetCurrentPlayerAsync', auctionId)
  }

  /**
   * Get available players by role
   */
  async getAvailablePlayersByRole(auctionId: string, role: Role): Promise<void> {
    if (!this.hubConnection) throw new Error('Not connected to hub')
    await this.hubConnection.invoke('GetAvailablePlayersByRoleAsync', auctionId, role)
  }

  // ============================================
  // AUCTIONEER METHODS (Admin/SuperAdmin only)
  // ============================================

  /**
   * Show next player for bidding
   */
  async showNextPlayer(auctionId: string, role: Role): Promise<void> {
    if (!this.hubConnection) throw new Error('Not connected to hub')
    await this.hubConnection.invoke('ShowNextPlayerAsync', auctionId, role)
  }

  /**
   * Assign current player to a team
   */
  async assignPlayerToTeam(auctionId: string, ownerEmail: string, price: number): Promise<void> {
    if (!this.hubConnection) throw new Error('Not connected to hub')
    await this.hubConnection.invoke('AssignPlayerToTeamAsync', auctionId, ownerEmail, price)
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
}

// Export singleton instance
export const auctionService = new AuctionService()
