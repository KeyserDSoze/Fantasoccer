// Readable PostgreSQL API contracts. No Raw types, aliases or serialization mappers.
export interface WorkspaceAccount { userId: string; email: string; displayName: string; canCreateGroups: boolean }
export interface WorkspaceGroup { groupId: string; name: string; roles: number }
export interface WorkspaceMember { userId: string; email: string | null; displayName: string; roles: number; isActive: boolean }
export interface WorkspaceMemberLookup { email: string; displayName: string | null; found: boolean; isMember: boolean }
export interface WorkspaceBasket { basketId: string; name: string; seasonIndices: number[] }
export interface WorkspaceLeague { leagueId: string; name: string; isMain: boolean; seasons: { seasonIndex: number; basketIds: string[] }[] }
export interface WorkspaceTeam { teamId: string; basketId: string; seasonIndex: number; name: string; owners: { userId: string; displayName: string; isPrimary: boolean }[] }
export interface WorkspaceDetails extends WorkspaceGroup {
  members: WorkspaceMember[]; baskets: WorkspaceBasket[]; leagues: WorkspaceLeague[]; teams: WorkspaceTeam[]
}
export interface SetWorkspaceMember { email: string; displayName: string; roles: number }
export interface UpdateWorkspaceMember { isActive: boolean; roles: number; email?: string }
export interface CreateWorkspaceTeam { basketId: string; seasonIndex: number; name: string; ownerUserId: string; additionalOwnerUserIds: string[] }
