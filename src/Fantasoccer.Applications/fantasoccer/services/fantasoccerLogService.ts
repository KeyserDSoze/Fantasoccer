// FantasoccerLog service using Repository Framework
import { RepositoryServices } from 'rystem.repository.client'
import { 
  FantasoccerLog, 
  FantasoccerLogRaw, 
  mapFantasoccerLog,
  sortLogsByTimestamp,
  filterLogsByError,
  searchLogs
} from 'types/log'

export class FantasoccerLogService {

  // ============================================
  // MAIN LOG OPERATIONS (using Repository Framework)
  // ============================================

  /**
   * Get all platform logs (super admin only)
   * Uses repository.query().execute() to fetch all entries
   */
  async getAllLogs(): Promise<FantasoccerLog[]> {
    try {
      const repository = RepositoryServices
        .Repository<FantasoccerLogRaw, string>("FantasoccerLog")
      
      // Query all logs - query() returns all items when called without parameters
      const results = await repository.query().execute()
      
      if (results && Array.isArray(results) && results.length > 0) {
        // Map all raw data to clean interfaces
        const logs = results.map(x => mapFantasoccerLog(x.value))
        
        // Return sorted by timestamp (newest first)
        return sortLogsByTimestamp(logs)
      }
      
      return []
    } catch (error) {
      console.error('Error fetching all logs:', error)
      return []
    }
  }

  /**
   * Get a specific log by key
   * @param key - Log key (PrimaryKey)
   */
  async getLog(key: string): Promise<FantasoccerLog | null> {
    try {
      const repository = RepositoryServices
        .Repository<FantasoccerLogRaw, string>("FantasoccerLog")
      
      const result = await repository.get(key)
      
      if (result) {
        return mapFantasoccerLog(result)
      }
      
      return null
    } catch (error) {
      console.error('Error fetching log:', error)
      return null
    }
  }

  // ============================================
  // QUERY/FILTER OPERATIONS
  // ============================================

  /**
   * Get all error logs
   */
  async getErrorLogs(): Promise<FantasoccerLog[]> {
    try {
      const allLogs = await this.getAllLogs()
      return filterLogsByError(allLogs, true)
    } catch (error) {
      console.error('Error fetching error logs:', error)
      return []
    }
  }

  /**
   * Get all info logs (logs without errors)
   */
  async getInfoLogs(): Promise<FantasoccerLog[]> {
    try {
      const allLogs = await this.getAllLogs()
      return filterLogsByError(allLogs, false)
    } catch (error) {
      console.error('Error fetching info logs:', error)
      return []
    }
  }

  /**
   * Search logs by query string
   * @param query - Search query (searches in message, error, keys)
   */
  async searchLogs(query: string): Promise<FantasoccerLog[]> {
    try {
      const allLogs = await this.getAllLogs()
      return searchLogs(allLogs, query)
    } catch (error) {
      console.error('Error searching logs:', error)
      return []
    }
  }

  /**
   * Get logs from a specific date range
   * @param startDate - Start date
   * @param endDate - End date
   */
  async getLogsByDateRange(startDate: Date, endDate: Date): Promise<FantasoccerLog[]> {
    try {
      const allLogs = await this.getAllLogs()
      return allLogs.filter(log => 
        log.timestamp >= startDate && log.timestamp <= endDate
      )
    } catch (error) {
      console.error('Error fetching logs by date range:', error)
      return []
    }
  }

  /**
   * Get recent logs (last N logs)
   * @param count - Number of recent logs to fetch
   */
  async getRecentLogs(count: number = 100): Promise<FantasoccerLog[]> {
    try {
      const allLogs = await this.getAllLogs()
      return allLogs.slice(0, count)
    } catch (error) {
      console.error('Error fetching recent logs:', error)
      return []
    }
  }

  /**
   * Get logs by primary key pattern
   * @param primaryKeyPattern - Primary key search pattern
   */
  async getLogsByPrimaryKey(primaryKeyPattern: string): Promise<FantasoccerLog[]> {
    try {
      const allLogs = await this.getAllLogs()
      const lowerPattern = primaryKeyPattern.toLowerCase()
      return allLogs.filter(log => 
        log.primaryKey.toLowerCase().includes(lowerPattern)
      )
    } catch (error) {
      console.error('Error fetching logs by primary key:', error)
      return []
    }
  }

  /**
   * Get logs by secondary key pattern
   * @param secondaryKeyPattern - Secondary key search pattern
   */
  async getLogsBySecondaryKey(secondaryKeyPattern: string): Promise<FantasoccerLog[]> {
    try {
      const allLogs = await this.getAllLogs()
      const lowerPattern = secondaryKeyPattern.toLowerCase()
      return allLogs.filter(log => 
        log.secondaryKey.toLowerCase().includes(lowerPattern)
      )
    } catch (error) {
      console.error('Error fetching logs by secondary key:', error)
      return []
    }
  }

  // ============================================
  // STATISTICS OPERATIONS
  // ============================================

  /**
   * Get log statistics
   */
  async getLogStatistics(): Promise<{
    total: number
    errors: number
    info: number
    today: number
    lastHour: number
  }> {
    try {
      const allLogs = await this.getAllLogs()
      const now = new Date()
      const todayStart = new Date(now.getFullYear(), now.getMonth(), now.getDate())
      const lastHourStart = new Date(now.getTime() - 60 * 60 * 1000)

      return {
        total: allLogs.length,
        errors: filterLogsByError(allLogs, true).length,
        info: filterLogsByError(allLogs, false).length,
        today: allLogs.filter(log => log.timestamp >= todayStart).length,
        lastHour: allLogs.filter(log => log.timestamp >= lastHourStart).length
      }
    } catch (error) {
      console.error('Error fetching log statistics:', error)
      return {
        total: 0,
        errors: 0,
        info: 0,
        today: 0,
        lastHour: 0
      }
    }
  }
}

// Export singleton instance
export const fantasoccerLogService = new FantasoccerLogService()
