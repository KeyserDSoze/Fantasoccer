import type { AppIdentity, GroupForIdentity } from '../types/identity'
import { buildApiUrl, getCommonHeaders, ApiError } from '../configurations/apiConfig'
import { tokenStorageService } from './tokenStorageService'
import { apiRequest } from './httpClient'

export interface TokenExchangeResponse { tokenType: string; accessToken: string; expiresIn: number; refreshToken: string }

export class AuthService {
  // Authorization-code (PKCE) exchange: the verifier of this very login is always sent in the body.
  async exchangeToken(code: string, codeVerifier: string, redirectPath = '/'): Promise<TokenExchangeResponse> {
    const response = await fetch(buildApiUrl('/api/Authentication/Social/Token', { code, provider: '2', redirectPath }), {
      method: 'POST',
      headers: { ...getCommonHeaders(), 'Content-Type': 'application/json' },
      body: JSON.stringify({ code_verifier: codeVerifier }),
    })
    if (!response.ok) throw new ApiError(`Token exchange failed: ${response.status}`, response.status)
    return response.json()
  }
  async refreshToken(refreshToken: string): Promise<TokenExchangeResponse> {
    const response = await fetch(buildApiUrl('/api/Authentication/Social/Token', { code: refreshToken, provider: '0' }), { headers: getCommonHeaders() })
    if (!response.ok) throw new ApiError('REFRESH_FAILED', response.status)
    return response.json()
  }
  async getUserInfo(accessToken: string, refreshToken?: string): Promise<AppIdentity> {
    const response = await fetch(buildApiUrl('/api/accounts/me'), { headers: getCommonHeaders(true, accessToken) })
    if (response.status === 401 && refreshToken) {
      try {
        const renewed = await this.refreshToken(refreshToken)
        await tokenStorageService.saveToken({ ...renewed, expiresAt: Date.now() + renewed.expiresIn * 1000 })
        return await this.getUserInfo(renewed.accessToken)
      } catch { throw new ApiError('REFRESH_FAILED', 401) }
    }
    if (response.status === 401 || response.status === 403) throw new ApiError('UNAUTHORIZED', response.status)
    if (!response.ok) throw new ApiError('Impossibile caricare il profilo.', response.status)
    return response.json() as Promise<AppIdentity>
  }
  async updateUserState(user: AppIdentity, signal?: AbortSignal): Promise<boolean> { await apiRequest('/api/accounts/me/selection', 'PUT', user.state, signal); return true }
  updateUserYear(user: AppIdentity, year: number) { return this.updateUserState({ ...user, state: { ...user.state, year } }) }
  updateUserLeague(user: AppIdentity, leagueId: string) { return this.updateUserState({ ...user, state: { ...user.state, leagueId } }) }
  updateUserGroup(user: AppIdentity, groupId: string) { return this.updateUserState({ ...user, state: { groupId, leagueId: null, year: null, meOrLeaderEmail: null } }) }
  updateUserWorkingEmail(user: AppIdentity, meOrLeaderEmail: string) { return this.updateUserState({ ...user, state: { ...user.state, meOrLeaderEmail } }) }
  async addUserGroup(user: AppIdentity, group: GroupForIdentity) { await apiRequest('/api/accounts', 'POST', { email: user.email, username: user.username, groups: [group] }); return true }
  removeUserGroup(user: AppIdentity, groupId: string) { return this.addUserGroup(user, { id: groupId, name: '', role: 0 }) }
  updateUserGroupInfo(user: AppIdentity, groupId: string, update: { name?: string; role?: number }) {
    const group = user.groups.find(value => value.id === groupId)
    if (!group) throw new Error('Gruppo non trovato.')
    return this.addUserGroup(user, { ...group, role: update.role ?? group.role })
  }
}
export const authService = new AuthService()
