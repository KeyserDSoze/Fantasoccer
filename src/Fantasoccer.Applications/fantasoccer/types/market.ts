// Types matching the C# Market structure
import { Player, PlayerRaw, mapRawPlayerToPlayer, mapPlayerToRawPlayer } from './team'

// ============================================
// ENUMS (matching C# enums)
// ============================================

export enum MarketStatus {
  Pending = 0,
  Approved = 1,
  Denied = 2,
  Cancelled = 3,
  NoMoney = 4,
  NoPlayers = 5,
  Expired = 6
}

export const MarketStatusHelper = {
  asLabel: (status: MarketStatus): string => {
    switch (status) {
      case MarketStatus.Pending:
        return 'In attesa'
      case MarketStatus.Approved:
        return 'Approvato'
      case MarketStatus.Denied:
        return 'Rifiutato'
      case MarketStatus.Cancelled:
        return 'Annullato'
      case MarketStatus.NoMoney:
        return 'Fondi insufficienti'
      case MarketStatus.NoPlayers:
        return 'Giocatori non disponibili'
      case MarketStatus.Expired:
        return 'Scaduto'
      default:
        return 'Sconosciuto'
    }
  },
  isPending: (status: MarketStatus): boolean => status === MarketStatus.Pending,
  isCompleted: (status: MarketStatus): boolean => 
    status === MarketStatus.Approved || 
    status === MarketStatus.Denied || 
    status === MarketStatus.Cancelled ||
    status === MarketStatus.NoMoney ||
    status === MarketStatus.NoPlayers ||
    status === MarketStatus.Expired,
  isSuccessful: (status: MarketStatus): boolean => status === MarketStatus.Approved,
  canBeModified: (status: MarketStatus): boolean => status === MarketStatus.Pending
}

// ============================================
// RAW INTERFACES (matching JSON property names from C#)
// ============================================

export interface MarketRaw {
  i: string                 // Id
  b: string                 // Buyer
  s: string                 // Seller
  sp: PlayerRaw[]          // SellerPlayers
  bp: PlayerRaw[]          // BuyerPlayers
  mb: number                // MoneyFromBuyer
  ms: number                // MoneyFromSeller
  a: string[]              // Approvers
  d: string[]              // Deniers
  st: MarketStatus         // Status
  ct: string               // CreationTime (DateTime as ISO string)
}

export interface MarketWrapperRaw {
  m: MarketRaw[]           // Markets
}

// ============================================
// CLEAN INTERFACES (with readable property names)
// ============================================

export interface Market {
  id: string
  buyer: string
  seller: string
  sellerPlayers: Player[]
  buyerPlayers: Player[]
  moneyFromBuyer: number
  moneyFromSeller: number
  approvers: string[]
  deniers: string[]
  status: MarketStatus
  creationTime: Date
}

export interface MarketWrapper {
  markets: Market[]
}

// ============================================
// MARKET KEY (for repository operations)
// ============================================

export interface MarketKeyRaw {
  g: string   // GroupId
  y: number   // Year
  l: string   // LeagueId
  i: string   // MarketId
}

export interface MarketKey {
  groupId: string
  year: number
  leagueId: string
  marketId: string
}

// ============================================
// MAPPING FUNCTIONS (Raw → Clean)
// ============================================

export const mapRawMarketToMarket = (raw: MarketRaw): Market => ({
  id: raw.i,
  buyer: raw.b,
  seller: raw.s,
  sellerPlayers: raw.sp.map(mapRawPlayerToPlayer),
  buyerPlayers: raw.bp.map(mapRawPlayerToPlayer),
  moneyFromBuyer: raw.mb,
  moneyFromSeller: raw.ms,
  approvers: raw.a,
  deniers: raw.d,
  status: raw.st,
  creationTime: new Date(raw.ct)
})

export const mapRawMarketWrapperToMarketWrapper = (raw: MarketWrapperRaw): MarketWrapper => ({
  markets: raw.m.map(mapRawMarketToMarket)
})

export const mapRawMarketKeyToMarketKey = (raw: MarketKeyRaw): MarketKey => ({
  groupId: raw.g,
  year: raw.y,
  leagueId: raw.l,
  marketId: raw.i
})

// ============================================
// REVERSE MAPPING FUNCTIONS (Clean → Raw)
// ============================================

export const mapMarketToRawMarket = (market: Market): MarketRaw => ({
  i: market.id,
  b: market.buyer,
  s: market.seller,
  sp: market.sellerPlayers.map(mapPlayerToRawPlayer),
  bp: market.buyerPlayers.map(mapPlayerToRawPlayer),
  mb: market.moneyFromBuyer,
  ms: market.moneyFromSeller,
  a: market.approvers,
  d: market.deniers,
  st: market.status,
  ct: market.creationTime.toISOString()
})

export const mapMarketWrapperToRawMarketWrapper = (wrapper: MarketWrapper): MarketWrapperRaw => ({
  m: wrapper.markets.map(mapMarketToRawMarket)
})

export const mapMarketKeyToRawMarketKey = (key: MarketKey): MarketKeyRaw => ({
  g: key.groupId,
  y: key.year,
  l: key.leagueId,
  i: key.marketId
})

// ============================================
// HELPER FUNCTIONS
// ============================================

export class MarketHelper {
  /**
   * Get markets for a specific user (as buyer or seller)
   */
  static getMarketsForUser(markets: Market[], userEmail: string): Market[] {
    return markets.filter(m => 
      m.buyer.toLowerCase() === userEmail.toLowerCase() || 
      m.seller.toLowerCase() === userEmail.toLowerCase()
    )
  }

  /**
   * Get pending markets that need approval from user
   */
  static getPendingMarketsForApproval(markets: Market[], userEmail: string): Market[] {
    return markets.filter(m => 
      MarketStatusHelper.isPending(m.status) &&
      m.buyer.toLowerCase() !== userEmail.toLowerCase() &&
      m.seller.toLowerCase() !== userEmail.toLowerCase() &&
      !m.approvers.some(a => a.toLowerCase() === userEmail.toLowerCase()) &&
      !m.deniers.some(d => d.toLowerCase() === userEmail.toLowerCase())
    )
  }

  /**
   * Check if user has already voted on this market
   */
  static hasUserVoted(market: Market, userEmail: string): boolean {
    const email = userEmail.toLowerCase()
    return market.approvers.some(a => a.toLowerCase() === email) ||
           market.deniers.some(d => d.toLowerCase() === email)
  }

  /**
   * Check if user is involved in this market (buyer or seller)
   */
  static isUserInvolved(market: Market, userEmail: string): boolean {
    const email = userEmail.toLowerCase()
    return market.buyer.toLowerCase() === email || 
           market.seller.toLowerCase() === email
  }

  /**
   * Get total value exchanged (for display)
   */
  static getTotalValue(market: Market): {
    buyerGives: number  // Money + players value
    sellerGives: number // Money + players value
  } {
    const buyerPlayersValue = market.buyerPlayers.reduce((sum, p) => sum + p.price, 0)
    const sellerPlayersValue = market.sellerPlayers.reduce((sum, p) => sum + p.price, 0)

    return {
      buyerGives: market.moneyFromBuyer + buyerPlayersValue,
      sellerGives: market.moneyFromSeller + sellerPlayersValue
    }
  }

  /**
   * Sort markets by creation time (newest first)
   */
  static sortByNewest(markets: Market[]): Market[] {
    return [...markets].sort((a, b) => b.creationTime.getTime() - a.creationTime.getTime())
  }

  /**
   * Sort markets by creation time (oldest first)
   */
  static sortByOldest(markets: Market[]): Market[] {
    return [...markets].sort((a, b) => a.creationTime.getTime() - b.creationTime.getTime())
  }

  /**
   * Filter markets by status
   */
  static filterByStatus(markets: Market[], status: MarketStatus): Market[] {
    return markets.filter(m => m.status === status)
  }

  /**
   * Get active (pending) markets
   */
  static getActiveMarkets(markets: Market[]): Market[] {
    return markets.filter(m => MarketStatusHelper.isPending(m.status))
  }

  /**
   * Get completed markets
   */
  static getCompletedMarkets(markets: Market[]): Market[] {
    return markets.filter(m => MarketStatusHelper.isCompleted(m.status))
  }

  /**
   * Calculate voting quorum for a market
   * Returns the minimum number of votes needed (50% + 1 of league size)
   */
  static calculateQuorum(leagueSize: number): number {
    return Math.floor(leagueSize / 2) + 1
  }

  /**
   * Check if market has reached approval quorum
   * @param market - The market to check
   * @param leagueSize - Total number of teams in the league
   * @returns true if market has enough approvals
   */
  static hasReachedApprovalQuorum(market: Market, leagueSize: number): boolean {
    const quorum = this.calculateQuorum(leagueSize)
    return market.approvers.length >= quorum
  }

  /**
   * Check if market has reached denial quorum
   * @param market - The market to check
   * @param leagueSize - Total number of teams in the league
   * @returns true if market has enough denials
   */
  static hasReachedDenialQuorum(market: Market, leagueSize: number): boolean {
    const quorum = this.calculateQuorum(leagueSize)
    return market.deniers.length >= quorum
  }

  /**
   * Get user's vote on a market
   * @returns 'approve', 'deny', or null if not voted
   */
  static getUserVote(market: Market, userEmail: string): 'approve' | 'deny' | null {
    const email = userEmail.toLowerCase()
    if (market.approvers.some(a => a.toLowerCase() === email)) {
      return 'approve'
    }
    if (market.deniers.some(d => d.toLowerCase() === email)) {
      return 'deny'
    }
    return null
  }
}
