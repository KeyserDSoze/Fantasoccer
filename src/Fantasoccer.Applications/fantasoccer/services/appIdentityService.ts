import type { AppIdentity, AppIdentityState, GroupForIdentity } from '../types/identity'
import { IdentityRole } from '../types/identity'
import { apiRequest } from './httpClient'
import { authService } from './authService'
import { tokenStorageService } from './tokenStorageService'

export class AppIdentityService {
  getAllIdentities(): Promise<AppIdentity[]> { return apiRequest('/api/accounts') }
  async getAppIdentity(email: string): Promise<AppIdentity | null> {
    const token = await tokenStorageService.ensureValidToken()
    if (!token) throw new Error('Accesso richiesto.')
    const own = await authService.getUserInfo(token)
    if (own.email.toLowerCase() === email.toLowerCase()) return own
    const accounts = await apiRequest<AppIdentity[]>(`/api/accounts?email=${encodeURIComponent(email)}`)
    return accounts[0] ?? null
  }
  async updateAppIdentity(email: string, identity: AppIdentity): Promise<boolean> {
    const previous = await this.getAppIdentity(email)
    if (!previous) throw new Error('Account non trovato.')
    const token = await tokenStorageService.ensureValidToken()
    if (!token) throw new Error('Accesso richiesto.')
    const own = await authService.getUserInfo(token)
    const changes: GroupForIdentity[] = identity.groups.filter(group => previous.groups.find(value => value.id === group.id)?.role !== group.role)
    for (const removed of previous.groups.filter(group => !identity.groups.some(value => value.id === group.id))) changes.push({ ...removed, role: 0 })
    if (changes.length) await apiRequest('/api/accounts', 'POST', { email, username: identity.username || email, groups: changes })
    if (own.email.toLowerCase() === email.toLowerCase()) {
      if (identity.username !== previous.username) await apiRequest('/api/accounts/me/profile', 'PATCH', { username: identity.username })
      if (JSON.stringify(identity.state) !== JSON.stringify(previous.state)) await authService.updateUserState(identity)
    }
    return true
  }
  async insertAppIdentity(identity: AppIdentity): Promise<boolean> { await apiRequest('/api/accounts', 'POST', { email: identity.email, username: identity.username, groups: identity.groups }); return true }
  async deleteAppIdentity(email: string): Promise<boolean> { await apiRequest(`/api/accounts/${encodeURIComponent(email)}`, 'DELETE'); return true }
  async updateUserState(email: string, update: Partial<AppIdentityState>): Promise<boolean> {
    const identity = await this.getAppIdentity(email)
    if (!identity) throw new Error('Account non trovato.')
    return this.updateAppIdentity(email, { ...identity, state: { ...identity.state, ...update } })
  }
  updateUserGroup(email: string, groupId: string) { return this.updateUserState(email, { groupId, leagueId: null, year: null, meOrLeaderEmail: null }) }
  updateUserLeague(email: string, leagueId: string) { return this.updateUserState(email, { leagueId, year: null }) }
  updateUserYear(email: string, year: number) { return this.updateUserState(email, { year }) }
  updateUserWorkingEmail(email: string, meOrLeaderEmail: string) { return this.updateUserState(email, { meOrLeaderEmail }) }
  async addUserGroup(email: string, group: GroupForIdentity): Promise<boolean> {
    const identity = await this.getAppIdentity(email)
    await apiRequest('/api/accounts', 'POST', { email, username: identity?.username || email, groups: [group] }); return true
  }
  removeUserGroup(email: string, id: string) { return this.addUserGroup(email, { id, name: '', role: 0 }) }
  updateUserRoleInGroup(email: string, id: string, role: IdentityRole) { return this.addUserGroup(email, { id, name: '', role }) }
  async getUserGroups(email: string) { return (await this.getAppIdentity(email))?.groups ?? [] }
  async getUserState(email: string) { return (await this.getAppIdentity(email))?.state ?? null }
  async userHasRoleInGroup(email: string, groupId: string, role: IdentityRole) { return (await this.getUserGroups(email)).some(group => group.id === groupId && (group.role & role) === role) }
  async userIsAdminInGroup(email: string, groupId: string) { return (await this.getUserGroups(email)).some(group => group.id === groupId && (group.role & (IdentityRole.Admin | IdentityRole.SuperAdmin)) !== 0) }
}
export const appIdentityService = new AppIdentityService()
