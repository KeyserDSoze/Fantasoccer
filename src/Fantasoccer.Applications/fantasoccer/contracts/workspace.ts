// Readable PostgreSQL API contracts. No Raw types, aliases or serialization mappers.
export interface WorkspaceAccount { userId: string; email: string; displayName: string; canCreateGroups: boolean }
export interface WorkspaceGroup { groupId: string; name: string; roles: number }
export interface WorkspaceMember { userId: string; email: string | null; displayName: string; roles: number; isActive: boolean }
export interface WorkspaceMemberLookup { email: string; displayName: string | null; found: boolean; isMember: boolean }
// kind: 0 autonomous, 1 derived from originBasketId (carbon copy of its rosters).
export interface WorkspaceBasket { basketId: string; name: string; seasonIndices: number[]; kind?: number; originBasketId?: string | null }
export interface WorkspaceLeagueSource { sourceLeagueId: string; qualifiedTeams: number; stage?: string | null }
// mode: 0 autonomous, 1 derived (participants qualified from the source leagues).
export interface WorkspaceLeague { leagueId: string; name: string; isMain: boolean; mode?: number
  seasons: { seasonIndex: number; basketIds: string[]; sources?: WorkspaceLeagueSource[] | null; participantTeamIds?: string[] | null }[] }
// A derived team (originTeamId) shows the owners of its origin team.
export interface WorkspaceTeam { teamId: string; basketId: string; seasonIndex: number; name: string; owners: { userId: string; displayName: string; isPrimary: boolean }[]; originTeamId?: string | null }
export interface WorkspaceDetails extends WorkspaceGroup {
  members: WorkspaceMember[]; baskets: WorkspaceBasket[]; leagues: WorkspaceLeague[]; teams: WorkspaceTeam[]
}
export interface SetWorkspaceMember { email: string; displayName: string; roles: number }
export interface UpdateWorkspaceMember { isActive: boolean; roles: number; email?: string }
// For a derived team set originTeamId and leave the owner empty (00000000-0000-0000-0000-000000000000).
export interface CreateWorkspaceTeam { basketId: string; seasonIndex: number; name: string; ownerUserId: string; additionalOwnerUserIds: string[]; originTeamId?: string }
