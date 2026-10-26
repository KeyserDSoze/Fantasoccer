export interface FantasoccerLog { id: string; primaryKey: string; secondaryKey: string; timestamp: string | null; message: string; error: string; source: string }
export const logHasError = (log: FantasoccerLog) => log.primaryKey === 'AError'
export const getLogSeverity = (log: FantasoccerLog): 'info' | 'error' => logHasError(log) ? 'error' : 'info'
export const formatLogTimestamp = (log: FantasoccerLog) => log.timestamp ? new Date(log.timestamp).toLocaleString('it-IT') : 'Data non disponibile'
export const sortLogsByTimestamp = (logs: FantasoccerLog[]) => [...logs].sort((a, b) => (b.timestamp ? Date.parse(b.timestamp) : 0) - (a.timestamp ? Date.parse(a.timestamp) : 0))
export const filterLogsByError = (logs: FantasoccerLog[], errors: boolean) => logs.filter(log => logHasError(log) === errors)
export const searchLogs = (logs: FantasoccerLog[], query: string) => logs.filter(log => [log.message, log.error, log.primaryKey, log.secondaryKey].some(text => text.toLowerCase().includes(query.toLowerCase())))
