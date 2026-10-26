// The JSON contract is the application model. No compact keys or duplicate Raw state.
export enum IdentityRole { None = 0, Reader = 1, Participant = 2, Admin = 4, SuperAdmin = 8 }
export interface GroupForIdentity { id: string; name: string; role: IdentityRole }
export interface AppIdentityState { groupId: string | null; leagueId: string | null; year: number | null; meOrLeaderEmail: string | null }
export interface AppIdentity { id?: string; username: string; email: string; state: AppIdentityState; groups: GroupForIdentity[] }
export const hasRole = (roles: IdentityRole, role: IdentityRole) => (roles & role) === role
export const getUserRoleNames = (roles: IdentityRole) => [IdentityRole.Reader, IdentityRole.Participant, IdentityRole.Admin, IdentityRole.SuperAdmin]
  .filter(role => hasRole(roles, role)).map(role => IdentityRole[role])
export const updateAppIdentityState = (identity: AppIdentity, state: Partial<AppIdentityState>): AppIdentity => ({ ...identity, state: { ...identity.state, ...state } })
export const addGroupToAppIdentity = (identity: AppIdentity, group: GroupForIdentity): AppIdentity => ({ ...identity, groups: [...identity.groups, group] })
export const removeGroupFromAppIdentity = (identity: AppIdentity, id: string): AppIdentity => ({ ...identity, groups: identity.groups.filter(group => group.id !== id) })
