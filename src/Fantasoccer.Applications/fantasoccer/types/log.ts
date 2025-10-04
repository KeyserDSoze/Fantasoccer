// ============================================
// LOG TYPES
// ============================================

/**
 * Raw interface - matches C# FantasoccerLog structure exactly
 */
export interface FantasoccerLogRaw {
  primaryKey?: string
  secondaryKey?: string
  timestamp?: string // ISO date string from backend
  message?: string
  error?: string | null // Can be null from backend
}

/**
 * Clean interface - user-friendly property names
 */
export interface FantasoccerLog {
  primaryKey: string
  secondaryKey: string
  timestamp: Date
  message: string
  error: string
  raw: FantasoccerLogRaw
}

// ============================================
// MAPPING FUNCTIONS
// ============================================

/**
 * Map raw log data to clean interface
 */
export const mapFantasoccerLog = (raw: FantasoccerLogRaw): FantasoccerLog => {
  return {
    primaryKey: raw.primaryKey || '',
    secondaryKey: raw.secondaryKey || '',
    timestamp: raw.timestamp && raw.timestamp !== '0001-01-01T00:00:00' 
      ? new Date(raw.timestamp) 
      : new Date(),
    message: raw.message || '',
    error: raw.error || '',
    raw
  }
}

/**
 * Map clean log data back to raw format
 */
export const mapFantasoccerLogToRaw = (log: FantasoccerLog): FantasoccerLogRaw => {
  return {
    primaryKey: log.primaryKey,
    secondaryKey: log.secondaryKey,
    timestamp: log.timestamp.toISOString(),
    message: log.message,
    error: log.error
  }
}

// ============================================
// HELPER FUNCTIONS
// ============================================

/**
 * Check if log has an error
 */
export const logHasError = (log: FantasoccerLog): boolean => {
  return log.error !== null && log.error !== undefined && log.error !== ''
}

/**
 * Get log severity level based on error presence
 */
export const getLogSeverity = (log: FantasoccerLog): 'info' | 'error' => {
  return logHasError(log) ? 'error' : 'info'
}

/**
 * Format timestamp for display
 */
export const formatLogTimestamp = (log: FantasoccerLog): string => {
  return log.timestamp.toLocaleString('it-IT', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit'
  })
}

/**
 * Sort logs by timestamp (newest first)
 */
export const sortLogsByTimestamp = (logs: FantasoccerLog[]): FantasoccerLog[] => {
  return [...logs].sort((a, b) => b.timestamp.getTime() - a.timestamp.getTime())
}

/**
 * Filter logs by error presence
 */
export const filterLogsByError = (logs: FantasoccerLog[], hasError: boolean): FantasoccerLog[] => {
  return logs.filter(log => logHasError(log) === hasError)
}

/**
 * Search logs by message or error text
 */
export const searchLogs = (logs: FantasoccerLog[], query: string): FantasoccerLog[] => {
  const lowerQuery = query.toLowerCase()
  return logs.filter(log => 
    log.message.toLowerCase().includes(lowerQuery) ||
    log.error.toLowerCase().includes(lowerQuery) ||
    log.primaryKey.toLowerCase().includes(lowerQuery) ||
    log.secondaryKey.toLowerCase().includes(lowerQuery)
  )
}
