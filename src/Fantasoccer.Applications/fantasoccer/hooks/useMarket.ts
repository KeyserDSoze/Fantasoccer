import { useState, useEffect, useCallback } from 'react'
import { marketService } from '../services/marketService'
import { Market } from '../types/market'

// ============================================
// LEAGUE MARKETS HOOK
// ============================================

export interface UseMarketsOptions {
  /** Auto-refresh interval in milliseconds (default: 0 = no auto-refresh) */
  refreshInterval?: number
  /** Whether to fetch on mount (default: true) */
  enabled?: boolean
}

export interface UseMarketsResult {
  markets: Market[]
  loading: boolean
  error: string | null
  refetch: () => Promise<void>
}

/**
 * Hook to manage all markets for a specific league
 */
export function useMarkets(
  groupId: string | null,
  year: number | null,
  leagueId: string | null,
  options: UseMarketsOptions = {}
): UseMarketsResult {
  const [markets, setMarkets] = useState<Market[]>([])
  const [loading, setLoading] = useState<boolean>(false)
  const [error, setError] = useState<string | null>(null)

  const { refreshInterval = 0, enabled = true } = options

  const fetchMarkets = useCallback(async () => {
    if (!groupId || !year || !leagueId || !enabled) {
      setMarkets([])
      setError(null)
      return
    }

    setLoading(true)
    setError(null)

    try {
      const marketsData = await marketService.getMarketsByLeague(groupId, year, leagueId)
      setMarkets(marketsData)
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to fetch markets'
      setError(errorMessage)
      console.error('Error fetching markets:', err)
    } finally {
      setLoading(false)
    }
  }, [groupId, year, leagueId, enabled])

  // Initial fetch
  useEffect(() => {
    fetchMarkets()
  }, [fetchMarkets])

  // Auto-refresh setup
  useEffect(() => {
    if (refreshInterval > 0 && enabled) {
      const interval = setInterval(fetchMarkets, refreshInterval)
      return () => clearInterval(interval)
    }
  }, [fetchMarkets, refreshInterval, enabled])

  return {
    markets,
    loading,
    error,
    refetch: fetchMarkets
  }
}

// ============================================
// USER MARKETS HOOK
// ============================================

export interface UseUserMarketsResult {
  markets: Market[]
  loading: boolean
  error: string | null
  refetch: () => Promise<void>
}

/**
 * Hook to manage markets for a specific user (as buyer or seller)
 */
export function useUserMarkets(
  groupId: string | null,
  year: number | null,
  leagueId: string | null,
  userEmail: string | null,
  options: UseMarketsOptions = {}
): UseUserMarketsResult {
  const [markets, setMarkets] = useState<Market[]>([])
  const [loading, setLoading] = useState<boolean>(false)
  const [error, setError] = useState<string | null>(null)

  const { refreshInterval = 0, enabled = true } = options

  const fetchUserMarkets = useCallback(async () => {
    if (!groupId || !year || !leagueId || !userEmail || !enabled) {
      setMarkets([])
      setError(null)
      return
    }

    setLoading(true)
    setError(null)

    try {
      const marketsData = await marketService.getMarketsForUser(groupId, year, leagueId, userEmail)
      setMarkets(marketsData)
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to fetch user markets'
      setError(errorMessage)
      console.error('Error fetching user markets:', err)
    } finally {
      setLoading(false)
    }
  }, [groupId, year, leagueId, userEmail, enabled])

  // Initial fetch
  useEffect(() => {
    fetchUserMarkets()
  }, [fetchUserMarkets])

  // Auto-refresh setup
  useEffect(() => {
    if (refreshInterval > 0 && enabled) {
      const interval = setInterval(fetchUserMarkets, refreshInterval)
      return () => clearInterval(interval)
    }
  }, [fetchUserMarkets, refreshInterval, enabled])

  return {
    markets,
    loading,
    error,
    refetch: fetchUserMarkets
  }
}

// ============================================
// PENDING MARKETS HOOK (for approval)
// ============================================

export interface UsePendingMarketsResult {
  markets: Market[]
  loading: boolean
  error: string | null
  refetch: () => Promise<void>
}

/**
 * Hook to manage pending markets that need approval from user
 */
export function usePendingMarkets(
  groupId: string | null,
  year: number | null,
  leagueId: string | null,
  userEmail: string | null,
  options: UseMarketsOptions = {}
): UsePendingMarketsResult {
  const [markets, setMarkets] = useState<Market[]>([])
  const [loading, setLoading] = useState<boolean>(false)
  const [error, setError] = useState<string | null>(null)

  const { refreshInterval = 0, enabled = true } = options

  const fetchPendingMarkets = useCallback(async () => {
    if (!groupId || !year || !leagueId || !userEmail || !enabled) {
      setMarkets([])
      setError(null)
      return
    }

    setLoading(true)
    setError(null)

    try {
      const marketsData = await marketService.getPendingMarketsForApproval(
        groupId, 
        year, 
        leagueId, 
        userEmail
      )
      setMarkets(marketsData)
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to fetch pending markets'
      setError(errorMessage)
      console.error('Error fetching pending markets:', err)
    } finally {
      setLoading(false)
    }
  }, [groupId, year, leagueId, userEmail, enabled])

  // Initial fetch
  useEffect(() => {
    fetchPendingMarkets()
  }, [fetchPendingMarkets])

  // Auto-refresh setup
  useEffect(() => {
    if (refreshInterval > 0 && enabled) {
      const interval = setInterval(fetchPendingMarkets, refreshInterval)
      return () => clearInterval(interval)
    }
  }, [fetchPendingMarkets, refreshInterval, enabled])

  return {
    markets,
    loading,
    error,
    refetch: fetchPendingMarkets
  }
}

// ============================================
// SINGLE MARKET HOOK
// ============================================

export interface UseMarketResult {
  market: Market | null
  loading: boolean
  error: string | null
  refetch: () => Promise<void>
  approve: () => Promise<boolean>
  deny: () => Promise<boolean>
  cancel: () => Promise<boolean>
  update: (market: Market) => Promise<boolean>
}

/**
 * Hook to manage a single market with operations
 */
export function useMarket(
  groupId: string | null,
  year: number | null,
  leagueId: string | null,
  marketId: string | null,
  options: UseMarketsOptions = {}
): UseMarketResult {
  const [market, setMarket] = useState<Market | null>(null)
  const [loading, setLoading] = useState<boolean>(false)
  const [error, setError] = useState<string | null>(null)

  const { refreshInterval = 0, enabled = true } = options

  const fetchMarket = useCallback(async () => {
    if (!groupId || !year || !leagueId || !marketId || !enabled) {
      setMarket(null)
      setError(null)
      return
    }

    setLoading(true)
    setError(null)

    try {
      const marketData = await marketService.getMarket(groupId, year, leagueId, marketId)
      setMarket(marketData)
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to fetch market'
      setError(errorMessage)
      console.error('Error fetching market:', err)
    } finally {
      setLoading(false)
    }
  }, [groupId, year, leagueId, marketId, enabled])

  // Approve market
  const approve = useCallback(async (): Promise<boolean> => {
    if (!groupId || !year || !leagueId || !marketId) {
      return false
    }

    try {
      const success = await marketService.approveMarket(groupId, year, leagueId, marketId)
      if (success) {
        await fetchMarket() // Refresh data
      }
      return success
    } catch (err) {
      console.error('Error approving market:', err)
      return false
    }
  }, [groupId, year, leagueId, marketId, fetchMarket])

  // Deny market
  const deny = useCallback(async (): Promise<boolean> => {
    if (!groupId || !year || !leagueId || !marketId) {
      return false
    }

    try {
      const success = await marketService.denyMarket(groupId, year, leagueId, marketId)
      if (success) {
        await fetchMarket() // Refresh data
      }
      return success
    } catch (err) {
      console.error('Error denying market:', err)
      return false
    }
  }, [groupId, year, leagueId, marketId, fetchMarket])

  // Cancel market
  const cancel = useCallback(async (): Promise<boolean> => {
    if (!groupId || !year || !leagueId || !marketId) {
      return false
    }

    try {
      const success = await marketService.cancelMarket(groupId, year, leagueId, marketId)
      if (success) {
        await fetchMarket() // Refresh data
      }
      return success
    } catch (err) {
      console.error('Error cancelling market:', err)
      return false
    }
  }, [groupId, year, leagueId, marketId, fetchMarket])

  // Update market
  const update = useCallback(async (updatedMarket: Market): Promise<boolean> => {
    if (!groupId || !year || !leagueId) {
      return false
    }

    try {
      const success = await marketService.updateMarket(groupId, year, leagueId, updatedMarket)
      if (success) {
        await fetchMarket() // Refresh data
      }
      return success
    } catch (err) {
      console.error('Error updating market:', err)
      return false
    }
  }, [groupId, year, leagueId, fetchMarket])

  // Initial fetch
  useEffect(() => {
    fetchMarket()
  }, [fetchMarket])

  // Auto-refresh setup
  useEffect(() => {
    if (refreshInterval > 0 && enabled) {
      const interval = setInterval(fetchMarket, refreshInterval)
      return () => clearInterval(interval)
    }
  }, [fetchMarket, refreshInterval, enabled])

  return {
    market,
    loading,
    error,
    refetch: fetchMarket,
    approve,
    deny,
    cancel,
    update
  }
}

// ============================================
// ACTIVE MARKETS HOOK
// ============================================

export interface UseActiveMarketsResult {
  markets: Market[]
  loading: boolean
  error: string | null
  refetch: () => Promise<void>
}

/**
 * Hook to get all active (pending) markets
 */
export function useActiveMarkets(
  groupId: string | null,
  year: number | null,
  leagueId: string | null,
  options: UseMarketsOptions = {}
): UseActiveMarketsResult {
  const [markets, setMarkets] = useState<Market[]>([])
  const [loading, setLoading] = useState<boolean>(false)
  const [error, setError] = useState<string | null>(null)

  const { refreshInterval = 0, enabled = true } = options

  const fetchActiveMarkets = useCallback(async () => {
    if (!groupId || !year || !leagueId || !enabled) {
      setMarkets([])
      setError(null)
      return
    }

    setLoading(true)
    setError(null)

    try {
      const marketsData = await marketService.getActiveMarkets(groupId, year, leagueId)
      setMarkets(marketsData)
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to fetch active markets'
      setError(errorMessage)
      console.error('Error fetching active markets:', err)
    } finally {
      setLoading(false)
    }
  }, [groupId, year, leagueId, enabled])

  // Initial fetch
  useEffect(() => {
    fetchActiveMarkets()
  }, [fetchActiveMarkets])

  // Auto-refresh setup
  useEffect(() => {
    if (refreshInterval > 0 && enabled) {
      const interval = setInterval(fetchActiveMarkets, refreshInterval)
      return () => clearInterval(interval)
    }
  }, [fetchActiveMarkets, refreshInterval, enabled])

  return {
    markets,
    loading,
    error,
    refetch: fetchActiveMarkets
  }
}
