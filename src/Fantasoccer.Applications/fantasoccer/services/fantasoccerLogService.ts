import { apiRequest } from './httpClient'
import type { FantasoccerLog } from '../types/log'
import type { LogStatistics } from '../hooks/useFantasoccerLog'

export class FantasoccerLogService {
  getAllLogs(limit = 100, errorsOnly?: boolean): Promise<FantasoccerLog[]> {
    return apiRequest(`/api/operations/logs?${new URLSearchParams({ limit: String(limit), ...(errorsOnly == null ? {} : { errorsOnly: String(errorsOnly) }) })}`)
  }
  getLog(id: string): Promise<FantasoccerLog> { return apiRequest(`/api/operations/logs/${encodeURIComponent(id)}`) }
  getErrorLogs() { return this.getAllLogs(500, true) }
  getInfoLogs() { return this.getAllLogs(500, false) }
  searchLogs(query: string): Promise<FantasoccerLog[]> { return apiRequest(`/api/operations/logs?limit=500&query=${encodeURIComponent(query)}`) }
  async getLogsByDateRange(start: Date, end: Date) { return (await this.getAllLogs(500)).filter(log => log.timestamp && Date.parse(log.timestamp) >= start.getTime() && Date.parse(log.timestamp) <= end.getTime()) }
  getRecentLogs(count = 100) { return this.getAllLogs(count) }
  async getLogsByPrimaryKey(pattern: string) { return (await this.searchLogs(pattern)).filter(log => log.primaryKey.toLowerCase().includes(pattern.toLowerCase())) }
  async getLogsBySecondaryKey(pattern: string) { return (await this.searchLogs(pattern)).filter(log => log.secondaryKey.toLowerCase().includes(pattern.toLowerCase())) }
  getLogStatistics(): Promise<LogStatistics> { return apiRequest('/api/operations/logs/statistics') }
}
export const fantasoccerLogService = new FantasoccerLogService()
