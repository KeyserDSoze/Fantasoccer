// Types matching the C# Market structure
import { Player, PlayerRaw, mapRawPlayerToPlayer, mapPlayerToRawPlayer } from './team'
import { MarketStatus } from '../utils/marketRules'
export { MarketHelper, MarketStatus, MarketStatusHelper } from '../utils/marketRules'

// ============================================
// ENUMS (matching C# enums)
// ============================================

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
