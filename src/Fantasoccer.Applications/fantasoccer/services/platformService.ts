import { apiRequest } from './httpClient'
import type { GroupForIdentity } from '../types/identity'

export interface PlatformPage<T> { items: T[]; total: number; offset: number; limit: number }
export interface PlatformUser { id: string; email: string | null; displayName: string; isEnabled: boolean; isGlobalAdministrator: boolean; groups: GroupForIdentity[] }
export interface PlatformGroup { id: string; name: string; revision: number; members: number; leagues: number }
export interface PlatformGroupDetails { id: string; name: string; members: { userId: string; email: string | null; displayName: string; roles: number; isActive: boolean }[]; leagues: { id: string; name: string; isArchived: boolean }[] }
export interface GroupCode { id: string; label: string; createdAtUtc: string; createdByUserId: string; status: 'Available' | 'Used' | 'Revoked'; redeemedAtUtc: string | null; redeemedByUserId: string | null; redeemedGroupId: string | null }
export interface IssuedGroupCode { code: string; details: GroupCode }
const page = (offset: number, query = '') => new URLSearchParams({ offset: String(offset), limit: '25', query }).toString()

export const platformService = {
  users: (offset = 0, query = '', signal?: AbortSignal) => apiRequest<PlatformPage<PlatformUser>>(`/api/platform/users?${page(offset, query)}`, 'GET', undefined, signal),
  groups: (offset = 0, query = '', signal?: AbortSignal) => apiRequest<PlatformPage<PlatformGroup>>(`/api/platform/groups?${page(offset, query)}`, 'GET', undefined, signal),
  group: (id: string, signal?: AbortSignal) => apiRequest<PlatformGroupDetails>(`/api/platform/groups/${encodeURIComponent(id)}`, 'GET', undefined, signal),
  codes: (offset = 0, signal?: AbortSignal) => apiRequest<PlatformPage<GroupCode>>(`/api/platform/group-codes?${page(offset)}`, 'GET', undefined, signal),
  issueCode: (label: string, signal?: AbortSignal) => apiRequest<IssuedGroupCode>('/api/platform/group-codes', 'POST', { label }, signal),
  revokeCode: (id: string, signal?: AbortSignal) => apiRequest<void>(`/api/platform/group-codes/${encodeURIComponent(id)}/revoke`, 'POST', undefined, signal),
}
