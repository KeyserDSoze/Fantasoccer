// React hooks for FantasoccerLog data management
import { useState, useEffect, useCallback } from 'react'
import { fantasoccerLogService } from 'services/fantasoccerLogService'
import { FantasoccerLog } from 'types/log'

// ============================================
// HOOK INTERFACES
// ============================================

export interface UseAllLogsOptions {
  refreshInterval?: number // Auto-refresh interval in milliseconds
  filterErrors?: boolean // If true, only fetch error logs
  limit?: number // Limit number of logs returned
}

export interface UseAllLogsResult {
  logs: FantasoccerLog[]
  loading: boolean
  error: string | null
  refetch: () => Promise<void>
  statistics: LogStatistics | null
}

export interface LogStatistics {
  total: number
  errors: number
  info: number
  today: number
  lastHour: number
}

export interface UseLogSearchOptions {
  debounceMs?: number // Debounce search in milliseconds
}

export interface UseLogSearchResult {
  logs: FantasoccerLog[]
  loading: boolean
  error: string | null
  search: (query: string) => void
  clearSearch: () => void
}

export interface UseLogStatisticsResult {
  statistics: LogStatistics | null
  loading: boolean
  error: string | null
  refetch: () => Promise<void>
}

// ============================================
// HOOK: ALL LOGS
// ============================================

/**
 * Hook to fetch all platform logs (super admin only)
 * @param options - Configuration options
 * @returns Logs data, loading state, error state, and refetch function
 * 
 * @example
 * ```tsx
 * const { logs, loading, error, refetch } = useAllLogs({
 *   refreshInterval: 30000, // Auto-refresh every 30 seconds
 *   limit: 100 // Only show last 100 logs
 * })
 * ```
 */
export function useAllLogs(options: UseAllLogsOptions = {}): UseAllLogsResult {
  const { refreshInterval, filterErrors, limit } = options

  const [logs, setLogs] = useState<FantasoccerLog[]>([])
  const [statistics, setStatistics] = useState<LogStatistics | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const fetchLogs = useCallback(async () => {
    setLoading(true)
    setError(null)

    try {
      let result = await fantasoccerLogService.getAllLogs()
      // Apply limit if specified
      if (limit && limit > 0) {
        result = result.slice(0, limit)
      }

      setLogs(result)

      // Fetch statistics
      const stats = await fantasoccerLogService.getLogStatistics()
      setStatistics(stats)
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to fetch logs'
      setError(errorMessage)
      console.error('Error in useAllLogs:', err)
    } finally {
      setLoading(false)
    }
  }, [filterErrors, limit])

  // Initial fetch
  useEffect(() => {
    fetchLogs()
  }, [fetchLogs])

  // Auto-refresh
  useEffect(() => {
    if (!refreshInterval || refreshInterval <= 0) return

    const interval = setInterval(() => {
      fetchLogs()
    }, refreshInterval)

    return () => clearInterval(interval)
  }, [refreshInterval, fetchLogs])

  return {
    logs,
    loading,
    error,
    refetch: fetchLogs,
    statistics
  }
}

// ============================================
// HOOK: LOG SEARCH
// ============================================

/**
 * Hook to search logs by query string
 * @param options - Configuration options
 * @returns Search results, loading state, and search function
 * 
 * @example
 * ```tsx
 * const { logs, loading, search, clearSearch } = useLogSearch()
 * 
 * // Search logs
 * search('error message')
 * 
 * // Clear search
 * clearSearch()
 * ```
 */
export function useLogSearch(options: UseLogSearchOptions = {}): UseLogSearchResult {
  const { debounceMs = 300 } = options

  const [logs, setLogs] = useState<FantasoccerLog[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [searchQuery, setSearchQuery] = useState<string>('')

  // Debounced search
  useEffect(() => {
    if (!searchQuery) {
      setLogs([])
      return
    }

    const timeoutId = setTimeout(async () => {
      setLoading(true)
      setError(null)

      try {
        const result = await fantasoccerLogService.searchLogs(searchQuery)
        setLogs(result)
      } catch (err) {
        const errorMessage = err instanceof Error ? err.message : 'Failed to search logs'
        setError(errorMessage)
        console.error('Error in useLogSearch:', err)
      } finally {
        setLoading(false)
      }
    }, debounceMs)

    return () => clearTimeout(timeoutId)
  }, [searchQuery, debounceMs])

  const search = useCallback((query: string) => {
    setSearchQuery(query)
  }, [])

  const clearSearch = useCallback(() => {
    setSearchQuery('')
    setLogs([])
  }, [])

  return {
    logs,
    loading,
    error,
    search,
    clearSearch
  }
}

// ============================================
// HOOK: LOG STATISTICS
// ============================================

/**
 * Hook to fetch log statistics
 * @returns Statistics data, loading state, and refetch function
 * 
 * @example
 * ```tsx
 * const { statistics, loading, error, refetch } = useLogStatistics()
 * 
 * if (statistics) {
 *   console.log(`Total logs: ${statistics.total}`)
 *   console.log(`Errors: ${statistics.errors}`)
 * }
 * ```
 */
export function useLogStatistics(): UseLogStatisticsResult {
  const [statistics, setStatistics] = useState<LogStatistics | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const fetchStatistics = useCallback(async () => {
    setLoading(true)
    setError(null)

    try {
      const stats = await fantasoccerLogService.getLogStatistics()
      setStatistics(stats)
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to fetch statistics'
      setError(errorMessage)
      console.error('Error in useLogStatistics:', err)
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    fetchStatistics()
  }, [fetchStatistics])

  return {
    statistics,
    loading,
    error,
    refetch: fetchStatistics
  }
}

// ============================================
// HOOK: RECENT LOGS
// ============================================

/**
 * Hook to fetch recent logs
 * @param count - Number of recent logs to fetch (default: 100)
 * @returns Recent logs, loading state, and refetch function
 * 
 * @example
 * ```tsx
 * const { logs, loading, error, refetch } = useRecentLogs(50)
 * ```
 */
export function useRecentLogs(count: number = 100): Omit<UseAllLogsResult, 'statistics'> {
  const [logs, setLogs] = useState<FantasoccerLog[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const fetchRecentLogs = useCallback(async () => {
    setLoading(true)
    setError(null)

    try {
      const result = await fantasoccerLogService.getRecentLogs(count)
      setLogs(result)
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to fetch recent logs'
      setError(errorMessage)
      console.error('Error in useRecentLogs:', err)
    } finally {
      setLoading(false)
    }
  }, [count])

  useEffect(() => {
    fetchRecentLogs()
  }, [fetchRecentLogs])

  return {
    logs,
    loading,
    error,
    refetch: fetchRecentLogs
  }
}
