import { apiRequest } from './httpClient'
export interface SqlJobState { name: string; year: number; nextRunAtUtc: string; leaseUntilUtc: string | null; lastStatus: string | null }
export interface SqlJobResult { runId: string; name: string; year: number; status: 'Succeeded' | 'Partial' | 'Failed' | 'Canceled' | 'LeaseLost'; processed: number; skipped: number; code: string | null }
export const operationsService = {
  jobs(): Promise<SqlJobState[]> { return apiRequest('/api/operations/jobs') },
  run(name: string, year: number): Promise<SqlJobResult> { return apiRequest('/api/operations/jobs/run', 'POST', { name, year }) },
}
