// Market service using Repository Framework
import { buildApiUrl, getCommonHeaders } from '../configurations/apiConfig'
import { tokenStorageService } from './tokenStorageService'
import { 
  Market, 
  MarketRaw, 
  mapRawMarketWrapperToMarketWrapper,
  mapMarketToRawMarket,
  MarketHelper
} from 'types/market'

export class MarketService {

  // ============================================
  // MAIN MARKET OPERATIONS (using Repository Framework)
  // ============================================

  /**
   * Get all markets for a specific league
   */
  async getMarketsByLeague(
    groupId: string, 
    year: number, 
    leagueId: string
  ): Promise<Market[]> {
    try {
      const token = await tokenStorageService.ensureValidToken()
      if (!token) return []
      const response = await fetch(buildApiUrl('/Market/Get', { groupId, year, leagueId }), {
        headers: getCommonHeaders(true, token)
      })
      if (!response.ok) return []
      const result = await response.json()
      
      if (result && result.m) {
        // Map raw data to clean interface
        const wrapper = mapRawMarketWrapperToMarketWrapper(result)
        return wrapper.markets
      }
      
      return []
    } catch (error) {
      console.error('Error fetching markets:', error)
      return []
    }
  }

  /**
   * Get a specific market by ID
   */
  async getMarket(
    groupId: string, 
    year: number, 
    leagueId: string, 
    marketId: string
  ): Promise<Market | null> {
    try {
      const markets = await this.getMarketsByLeague(groupId, year, leagueId)
      return markets.find(m => m.id === marketId) ?? null
    } catch (error) {
      console.error('Error fetching market:', error)
      return null
    }
  }

  /**
   * Create or update a market
   */
  async updateMarket(
    groupId: string, 
    year: number, 
    leagueId: string,
    market: Market
  ): Promise<boolean> {
    return this.executeCommand('/Market/Create', { groupId, year, leagueId }, mapMarketToRawMarket(market))
  }

  /**
   * Delete a market
   */
  async deleteMarket(
    groupId: string, 
    year: number, 
    leagueId: string,
    marketId: string
  ): Promise<boolean> {
    return this.executeCommand('/Market/Cancel', { groupId, year, leagueId, marketId })
  }

  // ============================================
  // MARKET APPROVAL OPERATIONS
  // ============================================

  /**
   * Approve a market (or change vote from deny to approve)
   */
  async approveMarket(
    groupId: string, 
    year: number, 
    leagueId: string,
    marketId: string
  ): Promise<boolean> {
    try {
      return await this.executeCommand('/Market/Approve', { groupId, year, leagueId, marketId })
    } catch (error) {
      console.error('Error approving market:', error)
      return false
    }
  }

  /**
   * Deny a market (or change vote from approve to deny)
   */
  async denyMarket(
    groupId: string, 
    year: number, 
    leagueId: string,
    marketId: string
  ): Promise<boolean> {
    try {
      return await this.executeCommand('/Market/Deny', { groupId, year, leagueId, marketId })
    } catch (error) {
      console.error('Error denying market:', error)
      return false
    }
  }

  /**
   * Cancel a market (only by buyer or seller)
   */
  async cancelMarket(
    groupId: string, 
    year: number, 
    leagueId: string,
    marketId: string
  ): Promise<boolean> {
    try {
      return await this.executeCommand('/Market/Cancel', { groupId, year, leagueId, marketId })
    } catch (error) {
      console.error('Error cancelling market:', error)
      return false
    }
  }

  // ============================================
  // FILTERED QUERIES
  // ============================================

  /**
   * Get markets for a specific user (buyer or seller)
   */
  async getMarketsForUser(
    groupId: string, 
    year: number, 
    leagueId: string,
    userEmail: string
  ): Promise<Market[]> {
    try {
      const markets = await this.getMarketsByLeague(groupId, year, leagueId)
      return MarketHelper.getMarketsForUser(markets, userEmail)
    } catch (error) {
      console.error('Error fetching markets for user:', error)
      return []
    }
  }

  /**
   * Get pending markets that need approval from user
   */
  async getPendingMarketsForApproval(
    groupId: string, 
    year: number, 
    leagueId: string,
    userEmail: string
  ): Promise<Market[]> {
    try {
      const markets = await this.getMarketsByLeague(groupId, year, leagueId)
      return MarketHelper.getPendingMarketsForApproval(markets, userEmail)
    } catch (error) {
      console.error('Error fetching pending markets:', error)
      return []
    }
  }

  /**
   * Get active (pending) markets
   */
  async getActiveMarkets(
    groupId: string, 
    year: number, 
    leagueId: string
  ): Promise<Market[]> {
    try {
      const markets = await this.getMarketsByLeague(groupId, year, leagueId)
      return MarketHelper.getActiveMarkets(markets)
    } catch (error) {
      console.error('Error fetching active markets:', error)
      return []
    }
  }

  /**
   * Get completed markets
   */
  async getCompletedMarkets(
    groupId: string, 
    year: number, 
    leagueId: string
  ): Promise<Market[]> {
    try {
      const markets = await this.getMarketsByLeague(groupId, year, leagueId)
      return MarketHelper.getCompletedMarkets(markets)
    } catch (error) {
      console.error('Error fetching completed markets:', error)
      return []
    }
  }

  private async executeCommand(
    path: string,
    parameters: Record<string, string | number>,
    body?: MarketRaw
  ): Promise<boolean> {
    try {
      const token = await tokenStorageService.ensureValidToken()
      if (!token) return false
      const response = await fetch(buildApiUrl(path, parameters), {
        method: 'POST',
        headers: {
          ...getCommonHeaders(true, token),
          'Content-Type': 'application/json'
        },
        body: body ? JSON.stringify(body) : undefined
      })
      if (!response.ok) return false
      const result = await response.json() as { isOk?: boolean }
      return result.isOk === true
    } catch (error) {
      console.error(`Error executing ${path}:`, error)
      return false
    }
  }
}

// Export singleton instance
export const marketService = new MarketService()
