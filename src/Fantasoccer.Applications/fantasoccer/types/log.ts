// ============================================
// LOG TYPES
// ============================================

/**
 * Raw interface - matches C# FantasoccerLog structure exactly
 */
export interface FantasoccerLogRaw {
  PrimaryKey: string
  SecondaryKey: string
  Timestamp: string // ISO date string from backend
  Message: string
  Error: string
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
    primaryKey: raw.PrimaryKey,
    secondaryKey: raw.SecondaryKey,
    timestamp: new Date(raw.Timestamp),
    message: raw.Message,
    error: raw.Error,
    raw
  }
}

/**
 * Map clean log data back to raw format
 */
export const mapFantasoccerLogToRaw = (log: FantasoccerLog): FantasoccerLogRaw => {
  return {
    PrimaryKey: log.primaryKey,
    SecondaryKey: log.secondaryKey,
    Timestamp: log.timestamp.toISOString(),
    Message: log.message,
    Error: log.error
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
