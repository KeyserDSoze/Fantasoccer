import { HubConnectionBuilder, LogLevel, type HubConnection } from '@microsoft/signalr'
import { getApiConfig } from '../configurations/apiConfig'
import { apiRequest } from './httpClient'
import { tokenStorageService } from './tokenStorageService'
import { appIdentityService } from './appIdentityService'
import type { Role } from '../types/group'
import type { Entity } from '../types/default'
import { TeamHelper, type Team, type TeamKey } from '../types/team'
import type { AuctionType, AuctionKind, AuctionState, ActualAuction, PlayerAssignedMessage, BidPlaced, StatPlayer, AuctionCommand, AuctionUpdate } from '../types/auction'

export interface AuctionEventHandlers {
  onConnected?: (connectionId: string) => void
  onReconnecting?: () => void
  onReconnected?: (connectionId: string) => void
  onDisconnected?: (error?: Error) => void
  onError?: (message: string) => void
  onStateUpdated?: (state: AuctionState) => void
  onUserJoined?: (email: string) => void
  onUserLeft?: (email: string) => void
  onEmojiReceived?: (data: { emoji: string; sender: string }) => void
  onAuctioneerChanged?: (data: { oldAuctioneer: string; newAuctioneer: string }) => void
  onCurrentRoleChanged?: (data: { currentRole: number }) => void
  onPlayerShown?: (actual: ActualAuction) => void
  onPlayerAssigned?: (data: PlayerAssignedMessage) => void
  onBidPlaced?: (data: BidPlaced) => void
  onTeamUpdated?: (team: Entity<Team, TeamKey>) => void
  onTimerUpdate?: (seconds: number) => void
  onAuctionPaused?: () => void
  onAuctionResumed?: () => void
  onAuctionClosed?: () => void
  onCurrentAuctionReset?: () => void
  onCurrentAuctionClosed?: () => void
  onTimerReset?: () => void
}
export type ConnectionStatus = 'Disconnected' | 'Connecting' | 'Connected' | 'Reconnecting' | 'Failed'
export class AuctionService {
  private connection: HubConnection | null = null
  private handlers: AuctionEventHandlers = {}
  private currentAuctionId: string | null = null
  private state: AuctionState | null = null
  private users: string[] = []
  private refreshQueue: Promise<void> = Promise.resolve()
  private epoch = 0

  createAuction(type: AuctionType, kind: AuctionKind, groupId: string, leagueId: string, year: number): Promise<string> {
    return apiRequest('/api/auctions', 'POST', { groupId, leagueId, year, type, kind })
  }
  async getAuction(id: string): Promise<AuctionState> {
    const epoch = this.epoch
    const state = await apiRequest<AuctionState>(`/api/auctions/${encodeURIComponent(id)}`)
    if (epoch === this.epoch && (!this.currentAuctionId || this.currentAuctionId === id)) this.state = state
    return state
  }
  setEventHandlers(handlers: AuctionEventHandlers) { this.handlers = { ...handlers } }
  async connect(id: string, _email: string) {
    if (this.connection?.state === 'Connected' && this.currentAuctionId === id) return
    await this.disconnect()
    this.currentAuctionId = id
    const connection = new HubConnectionBuilder().withUrl(`${getApiConfig().baseUrl}/auctionhub`, {
      accessTokenFactory: async () => { const token = await tokenStorageService.ensureValidToken(); if (!token) throw new Error('Accesso richiesto.'); return token },
    }).withAutomaticReconnect([0, 2000, 4000, 8000, 16000, 30000]).configureLogging(LogLevel.Warning).build()
    this.connection = connection
    connection.onclose(error => this.handlers.onDisconnected?.(error))
    connection.onreconnecting(() => this.handlers.onReconnecting?.())
    connection.onreconnected(async connectionId => { await this.joinAuction(id); this.handlers.onStateUpdated?.(await this.getAuction(id)); this.handlers.onReconnected?.(connectionId ?? '') })
    connection.on('AuctionUpdated', (update: AuctionUpdate) => {
      this.refreshQueue = this.refreshQueue.catch(() => undefined).then(() => this.receive(update)).catch(error => this.handlers.onError?.(error instanceof Error ? error.message : 'Aggiornamento asta non riuscito.'))
    })
    connection.on('PresenceChanged', () => { void this.refreshUsers(id).catch(error => this.handlers.onError?.(String(error))) })
    connection.on('EmojiReceived', (data: { emoji: string; sender: string }) => this.handlers.onEmojiReceived?.(data))
    await connection.start()
    this.handlers.onConnected?.(connection.connectionId ?? '')
  }
  async disconnect() {
    const connection = this.connection
    this.epoch++; this.connection = null; this.currentAuctionId = null; this.users = []; this.state = null
    if (connection) await connection.stop()
  }
  getConnectionState(): ConnectionStatus { return (this.connection?.state ?? 'Disconnected') as ConnectionStatus }
  isConnected() { return this.connection?.state === 'Connected' }
  async joinAuction(id: string) { if (!this.connection) throw new Error('Connessione non disponibile.'); await this.connection.invoke('JoinAsync', id); await this.refreshUsers(id); return true }
  async leaveAuction(id: string) { if (this.isConnected()) await this.connection!.invoke('LeaveAsync', id) }
  getConnectedUsers(id: string): Promise<string[]> { return apiRequest(`/api/auctions/${encodeURIComponent(id)}/presence`) }
  getDiscardedPlayers(id: string, role: number): Promise<StatPlayer[]> { return apiRequest(`/api/auctions/${encodeURIComponent(id)}/discarded?role=${role}`) }
  async sendEmoji(id: string, emoji: string) { await this.connection?.invoke('EmojiAsync', id, emoji) }

  private async command(id: string, operation: string, payload: AuctionCommand = {}) {
    const state = this.state?.wrapper.id === id ? this.state : await this.getAuction(id)
    const request = { ...payload, expectedVersion: payload.expectedVersion ?? state.wrapper.version }
    if (this.isConnected()) await this.connection!.invoke('CommandAsync', id, operation, request)
    else await apiRequest(`/api/auctions/${encodeURIComponent(id)}/commands/${operation}`, 'POST', request)
    this.handlers.onStateUpdated?.(await this.getAuction(id))
  }
  private async team(id: string, identifier: string | undefined) {
    const state = this.state?.wrapper.id === id ? this.state : await this.getAuction(id)
    const matches = state.teams.filter(team => identifier ? team.value.teamId === identifier
      : [team.value.owner, ...team.value.additionalOwners].some(email => email.toLowerCase() === state.userEmail.toLowerCase()))
    if (matches.length !== 1 || !matches[0].value.teamId) throw new Error('Selezionare la squadra tramite il suo ID.')
    return matches[0]
  }
  private player(identifier: string | null | undefined) {
    if (!identifier) return undefined
    const matches = this.state?.realPlayers.filter(player => player.playerId === identifier) ?? []
    if (matches.length !== 1 || !matches[0].playerId) throw new Error('Ricaricare il catalogo e selezionare il giocatore con il suo ID.')
    return matches[0].playerId
  }
  async placeBid(id: string, amount: number, bidderTeam?: string, substituted?: string) {
    const team = await this.team(id, bidderTeam)
    await this.command(id, 'bid', { amount, teamId: team.value.teamId, substitutedPlayerId: this.player(substituted) })
  }
  async showNextPlayer(id: string, role: Role, playerId?: string | null) { await this.command(id, 'show', { role, playerId: this.player(playerId) }) }
  async assignPlayerToTeam(id: string) { await this.command(id, 'assign') }
  async pauseAuction(id: string) { await this.command(id, 'pause') }
  async resumeAuction(id: string) { await this.command(id, 'resume') }
  async closeAuction(id: string) { await this.command(id, 'close') }
  async finalizeAuction(id: string) { await this.command(id, 'finalize') }
  async reopenAuction(id: string) { await this.command(id, 'reopen') }
  async closeCurrentAuction(id: string) { await this.command(id, 'close-current') }
  async completeReset(id: string) { await this.command(id, 'reset-current') }
  async timerReset(id: string) { await this.command(id, 'reset-timer') }
  async resetTimer(id: string) { await this.command(id, 'reset-timer') }
  async setCurrentRole(id: string, role: number) { await this.command(id, 'role', { role }) }
  async changeTimer(id: string, timer: number) { await this.command(id, 'timer', { timer }) }
  async transferAuctioneerRole(id: string, email: string) {
    const account = await appIdentityService.getAppIdentity(email)
    if (!account?.id) throw new Error('Account non trovato.')
    await this.command(id, 'transfer-auctioneer', { newAuctioneerUserId: account.id })
  }
  async changePlayerStatus(id: string, teamIdentifier: string, playerIdentifier: string, status: number, revenue?: number | null, ordinal?: number) {
    const team = await this.team(id, teamIdentifier)
    const playerId = this.player(playerIdentifier)
    const matches = team.value.players.filter(player => player.playerId === playerId && (ordinal == null || player.ordinal === ordinal))
    if (matches.length !== 1 || matches[0].ordinal == null) throw new Error('Selezionare una riga attiva e univoca della rosa.')
    await this.command(id, 'player-status', { teamId: team.value.teamId, playerId, playerStatus: status, revenue: revenue ?? undefined,
      rosterOrdinal: matches[0].ordinal, expectedRosterVersion: team.value.version })
  }
  private async refreshUsers(id: string) {
    const next = await this.getConnectedUsers(id)
    for (const email of next.filter(email => !this.users.includes(email))) this.handlers.onUserJoined?.(email)
    for (const email of this.users.filter(email => !next.includes(email))) this.handlers.onUserLeft?.(email)
    this.users = next
  }
  private async receive(update: AuctionUpdate) {
    if (update.auctionId !== this.currentAuctionId) return
    const epoch = this.epoch
    const previous = this.state
    const next = await this.getAuction(update.auctionId)
    if (epoch !== this.epoch || update.auctionId !== this.currentAuctionId) return
    this.handlers.onStateUpdated?.(next)
    const actual = next.wrapper.actual
    const team = next.teams.find(team => team.value.teamId === actual?.winningTeamId)
    switch (update.operation) {
      case 'show': if (actual) this.handlers.onPlayerShown?.(actual); break
      case 'bid': if (actual && team) this.handlers.onBidPlaced?.({ amount: actual.price, bidderEmail: team.value.owner, bidderName: team.value.name, playerName: actual.player.name, substituted: actual.substituted }); break
      case 'assign': {
        const win = next.wrapper.winnings.at(-1)
        const wonTeam = next.teams.find(team => team.value.teamId === win?.teamId)
        const player = next.realPlayers.find(player => player.playerId === win?.playerId)
        if (win && wonTeam && player) this.handlers.onPlayerAssigned?.({ team: wonTeam, playerAssignedInAuction: { owner: wonTeam.value.owner, teamName: wonTeam.value.name,
          price: win.price, playerName: player.name, teamCost: TeamHelper.getCost(wonTeam.value), teamPlayersCount: TeamHelper.getActivePlayers(wonTeam.value).length } })
        break
      }
      case 'player-status': for (const team of next.teams) if (previous?.teams.find(old => old.value.teamId === team.value.teamId)?.value.version !== team.value.version) this.handlers.onTeamUpdated?.(team); break
      case 'role': this.handlers.onCurrentRoleChanged?.({ currentRole: next.wrapper.currentRole }); break
      case 'timer': this.handlers.onTimerUpdate?.(next.wrapper.secondsPerAuction); break
      case 'pause': case 'reopen': this.handlers.onAuctionPaused?.(); break
      case 'resume': this.handlers.onAuctionResumed?.(); break
      case 'close': case 'finalize': this.handlers.onAuctionClosed?.(); break
      case 'close-current': this.handlers.onCurrentAuctionClosed?.(); break
      case 'reset-current': this.handlers.onCurrentAuctionReset?.(); break
      case 'reset-timer': this.handlers.onTimerReset?.(); break
      case 'transfer-auctioneer': this.handlers.onAuctioneerChanged?.({ oldAuctioneer: previous?.wrapper.creator ?? '', newAuctioneer: next.wrapper.creator }); break
    }
  }
}
export const auctionService = new AuctionService()
