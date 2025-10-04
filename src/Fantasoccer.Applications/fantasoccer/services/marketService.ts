// Market service using Repository Framework
import { RepositoryServices } from 'rystem.repository.client'
import { 
  Market, 
  MarketRaw, 
  MarketWrapper,
  MarketWrapperRaw,
  MarketKey,
  MarketKeyRaw,
  MarketStatus,
  mapRawMarketToMarket,
  mapRawMarketWrapperToMarketWrapper,
  mapMarketToRawMarket,
  mapMarketWrapperToRawMarketWrapper,
  mapRawMarketKeyToMarketKey,
  mapMarketKeyToRawMarketKey,
  MarketHelper,
  MarketStatusHelper
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
      // Create the key for the repository query
      const key: MarketKeyRaw = {
        g: groupId,
        y: year,
        l: leagueId,
        i: '' // Empty string for listing all markets
      }

      // Get the repository instance
      const repository = RepositoryServices
        .Repository<MarketWrapperRaw, MarketKeyRaw>("Market")
      
      // Fetch the market data
      const result = await repository.get(key)
      
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
    try {
      // Create the key
      const key: MarketKeyRaw = {
        g: groupId,
        y: year,
        l: leagueId,
        i: market.id
      }

      // Get the repository instance
      const repository = RepositoryServices
        .Repository<MarketWrapperRaw, MarketKeyRaw>("Market")

      // Get existing markets
      const existingWrapper = await repository.get({
        g: groupId,
        y: year,
        l: leagueId,
        i: ''
      })

      let markets: MarketRaw[] = existingWrapper?.m ?? []

      // Find if market already exists
      const existingIndex = markets.findIndex(m => m.i === market.id)

      // Convert market to raw format
      const rawMarket = mapMarketToRawMarket(market)

      if (existingIndex >= 0) {
        // Update existing market
        markets[existingIndex] = rawMarket
      } else {
        // Add new market
        markets.push(rawMarket)
      }

      // Create wrapper
      const wrapper: MarketWrapperRaw = { m: markets }

      // Update the repository
      await repository.update(key, wrapper)

      return true
    } catch (error) {
      console.error('Error updating market:', error)
      return false
    }
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
    try {
      // Create the key
      const key: MarketKeyRaw = {
        g: groupId,
        y: year,
        l: leagueId,
        i: ''
      }

      // Get the repository instance
      const repository = RepositoryServices
        .Repository<MarketWrapperRaw, MarketKeyRaw>("Market")

      // Get existing markets
      const existingWrapper = await repository.get(key)

      if (!existingWrapper || !existingWrapper.m) {
        return false
      }

      // Filter out the market to delete
      const markets = existingWrapper.m.filter(m => m.i !== marketId)

      // Create wrapper
      const wrapper: MarketWrapperRaw = { m: markets }

      // Update the repository
      await repository.update(key, wrapper)

      return true
    } catch (error) {
      console.error('Error deleting market:', error)
      return false
    }
  }

  // ============================================
  // MARKET APPROVAL OPERATIONS
  // ============================================

  /**
   * Approve a market
   */
  async approveMarket(
    groupId: string, 
    year: number, 
    leagueId: string,
    marketId: string,
    userEmail: string
  ): Promise<boolean> {
    try {
      const market = await this.getMarket(groupId, year, leagueId, marketId)
      
      if (!market || !MarketStatusHelper.canBeModified(market.status)) {
        return false
      }

      // Check if user already voted
      if (MarketHelper.hasUserVoted(market, userEmail)) {
        console.warn('User has already voted on this market')
        return false
      }

      // Add user to approvers
      market.approvers.push(userEmail)

      // Update market
      return await this.updateMarket(groupId, year, leagueId, market)
    } catch (error) {
      console.error('Error approving market:', error)
      return false
    }
  }

  /**
   * Deny a market
   */
  async denyMarket(
    groupId: string, 
    year: number, 
    leagueId: string,
    marketId: string,
    userEmail: string
  ): Promise<boolean> {
    try {
      const market = await this.getMarket(groupId, year, leagueId, marketId)
      
      if (!market || !MarketStatusHelper.canBeModified(market.status)) {
        return false
      }

      // Check if user already voted
      if (MarketHelper.hasUserVoted(market, userEmail)) {
        console.warn('User has already voted on this market')
        return false
      }

      // Add user to deniers
      market.deniers.push(userEmail)

      // Update market
      return await this.updateMarket(groupId, year, leagueId, market)
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
    marketId: string,
    userEmail: string
  ): Promise<boolean> {
    try {
      const market = await this.getMarket(groupId, year, leagueId, marketId)
      
      if (!market || !MarketStatusHelper.canBeModified(market.status)) {
        return false
      }

      // Check if user is involved in the market
      if (!MarketHelper.isUserInvolved(market, userEmail)) {
        console.warn('User is not involved in this market')
        return false
      }

      // Update status to cancelled
      market.status = MarketStatus.Cancelled

      // Update market
      return await this.updateMarket(groupId, year, leagueId, market)
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
}

// Export singleton instance
export const marketService = new MarketService()
