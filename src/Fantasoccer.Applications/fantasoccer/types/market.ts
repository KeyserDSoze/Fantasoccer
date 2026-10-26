import type { Player } from './team'
import type { MarketStatus } from '../utils/marketRules'
export { MarketHelper, MarketStatus, MarketStatusHelper } from '../utils/marketRules'

export interface Market {
  id: string
  buyerTeamId: string
  sellerTeamId: string
  buyer: string
  seller: string
  sellerPlayers: Player[]
  buyerPlayers: Player[]
  moneyFromBuyer: number
  moneyFromSeller: number
  approvers: string[]
  deniers: string[]
  sellerAccepted: boolean
  status: MarketStatus
  creationTime: string | Date
  version: number
}
export interface MarketDraft extends Market { expectedBuyerRosterVersion: number; expectedSellerRosterVersion: number }
export interface MarketWrapper { markets: Market[] }
export interface MarketKey { groupId: string; year: number; leagueId: string; marketId: string }
