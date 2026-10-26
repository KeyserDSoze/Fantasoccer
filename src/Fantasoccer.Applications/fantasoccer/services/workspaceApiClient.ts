import type { WorkspaceAccount, WorkspaceGroup, WorkspaceDetails, SetWorkspaceMember, CreateWorkspaceTeam } from '../contracts/workspace'

export class WorkspaceApiError extends Error {
  constructor(public status: number, public code: string, message: string) { super(message); this.name = 'WorkspaceApiError' }
}

export function createWorkspaceApi(options: {
  baseUrl: string
  token: () => Promise<string | null>
  refresh: () => Promise<string | null>
  fetch: typeof fetch
}) {
  async function request<T>(path: string, method = 'GET', body?: unknown, signal?: AbortSignal): Promise<T> {
    const token = await options.token()
    if (!token) throw new WorkspaceApiError(401, 'authentication_required', 'Accedi per continuare.')
    const send = (bearer: string) => options.fetch(options.baseUrl.replace(/\/$/, '') + path, {
      method, signal, headers: { Accept: 'application/json', Authorization: `Bearer ${bearer}`, ...(body === undefined ? {} : { 'Content-Type': 'application/json' }) },
      ...(body === undefined ? {} : { body: JSON.stringify(body) }),
    })
    let response = await send(token)
    if (response.status === 401) {
      const renewed = await options.refresh()
      if (renewed) response = await send(renewed)
    }
    if (!response.ok) {
      const problem = await response.json().catch(() => ({})) as { code?: string; detail?: string }
      throw new WorkspaceApiError(response.status, problem.code || 'request_failed', problem.detail || `Operazione non riuscita (${response.status}).`)
    }
    return response.status === 204 ? undefined as T : await response.json() as T
  }
  const group = (id: string) => `/groups/${encodeURIComponent(id)}`
  return {
    account: (signal?: AbortSignal) => request<WorkspaceAccount>('/account', 'GET', undefined, signal),
    groups: (signal?: AbortSignal) => request<WorkspaceGroup[]>('/groups', 'GET', undefined, signal),
    details: (id: string, signal?: AbortSignal) => request<WorkspaceDetails>(group(id), 'GET', undefined, signal),
    createGroup: (name: string) => request<WorkspaceGroup>('/groups', 'POST', { name }),
    redeemGroupCode: (name: string, code: string, signal?: AbortSignal) => request<WorkspaceGroup>('/groups/redeem-code', 'POST', { name, code }, signal),
    renameGroup: (id: string, name: string) => request<void>(group(id), 'PATCH', { name }),
    setMember: (id: string, member: SetWorkspaceMember) => request<void>(group(id) + '/members', 'PUT', member),
    createBasket: (id: string, name: string, seasonIndex: number) => request<{ basketId: string }>(group(id) + '/baskets', 'POST', { name, seasonIndex }),
    createLeague: (id: string, name: string, seasonIndex: number, basketIds: string[]) => request<{ leagueId: string }>(group(id) + '/leagues', 'POST', { name, seasonIndex, basketIds }),
    createTeam: (id: string, team: CreateWorkspaceTeam) => request<{ teamId: string }>(group(id) + '/teams', 'POST', team),
  }
}
