import { buildApiUrl, ApiError } from '../configurations/apiConfig'
import { tokenStorageService } from './tokenStorageService'

export async function apiRequest<T>(path: string, method = 'GET', body?: unknown, signal?: AbortSignal): Promise<T> {
  const token = await tokenStorageService.ensureValidToken()
  if (!token) throw new ApiError('Accesso richiesto.', 401)
  const send = (bearer: string) => fetch(buildApiUrl(path), {
    method, signal,
    headers: { Accept: 'application/json', Authorization: `Bearer ${bearer}`, ...(body === undefined ? {} : { 'Content-Type': 'application/json' }) },
    ...(body === undefined ? {} : { body: JSON.stringify(body) }),
  })
  let response = await send(token)
  if (response.status === 401) {
    const refreshed = await tokenStorageService.forceRefreshToken()
    if (refreshed) response = await send(refreshed)
  }
  if (!response.ok) {
    const problem = await response.json().catch(() => null) as { detail?: string; title?: string } | null
    throw new ApiError(problem?.detail || problem?.title || `Operazione non riuscita (${response.status}).`, response.status)
  }
  return response.status === 204 ? undefined as T : await response.json() as T
}

export async function apiUpload<T>(path: string, body: FormData, signal?: AbortSignal): Promise<T> {
  const token = await tokenStorageService.ensureValidToken()
  if (!token) throw new ApiError('Accesso richiesto.', 401)
  const send = (bearer: string) => fetch(buildApiUrl(path), { method: 'POST', signal,
    headers: { Accept: 'application/json', Authorization: `Bearer ${bearer}` }, body })
  let response = await send(token)
  if (response.status === 401) {
    const renewed = await tokenStorageService.forceRefreshToken()
    if (renewed) response = await send(renewed)
  }
  if (!response.ok) {
    const problem = await response.json().catch(() => null) as { detail?: string } | null
    throw new ApiError(problem?.detail || `Caricamento non riuscito (${response.status}).`, response.status)
  }
  return response.status === 204 ? undefined as T : response.json() as Promise<T>
}
