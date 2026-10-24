import { buildApiUrl } from '../configurations/apiConfig'
import { tokenStorageService } from './tokenStorageService'

export interface McpConnection {
  clientId: string
  displayName: string
  lastAuthorizedUtc: string | null
}

async function request(path: string, body?: object): Promise<Response> {
  const token = await tokenStorageService.getTokenString()
  if (!token) throw new Error('Accedi nuovamente per gestire i collegamenti.')
  return fetch(buildApiUrl(path), {
    method: body ? 'POST' : 'GET',
    cache: 'no-store',
    headers: { Authorization: `Bearer ${token}`, ...(body ? { 'Content-Type': 'application/json' } : {}) },
    body: body ? JSON.stringify(body) : undefined,
  })
}

export const mcpConnectionService = {
  async list(): Promise<McpConnection[] | null> {
    const response = await request('/connect/connections')
    if (response.status === 404) return null // MCP intentionally disabled on this backend.
    if (!response.ok) throw new Error('Impossibile caricare i collegamenti. Riprova.')
    return response.json()
  },
  async disconnect(clientId: string): Promise<void> {
    const response = await request('/connect/disconnect', { clientId })
    if (!response.ok) throw new Error('Revoca non completata. Aggiorna e riprova.')
  },
}
