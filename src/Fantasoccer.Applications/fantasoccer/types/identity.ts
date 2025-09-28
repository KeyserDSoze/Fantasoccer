// Identity types based on Fantasoccer.Domain.Identity models
// These match the C# domain models exactly

// ============================================================================
// RAW TYPES (Single character properties - match JSON serialization)
// ============================================================================

/**
 * Raw GroupForIdentity interface
 * Matches: Fantasoccer.Domain.Identity.GroupForIdentity.cs
 */
export interface GroupForIdentityRaw {
  /** Group ID - JsonPropertyName("i") */
  i: string;
  /** Group Name - JsonPropertyName("n") */
  n: string;
  /** Role flags - JsonPropertyName("r") */
  r: IdentityRole;
}

/**
 * Raw AppIdentityState interface
 * Matches: Fantasoccer.Domain.Identity.AppIdentityState.cs
 */
export interface AppIdentityStateRaw {
  /** Group ID - JsonPropertyName("g") */
  g?: string;
  /** League ID - JsonPropertyName("l") */
  l?: string;
  /** Year - JsonPropertyName("y") */
  y?: number;
  /** Me or Leader Email - JsonPropertyName("m") */
  m?: string;
}

/**
 * Raw AppIdentity interface
 * Matches: Fantasoccer.Domain.Identity.AppIdentity.cs
 */
export interface AppIdentityRaw {
  /** Username - JsonPropertyName("u") */
  u?: string;
  /** Email - JsonPropertyName("e") */
  e: string;
  /** State - JsonPropertyName("s") */
  s: AppIdentityStateRaw;
  /** Groups - JsonPropertyName("g") */
  g: GroupForIdentityRaw[];
}

// ============================================================================
// MAPPED TYPES (Meaningful property names for application use)
// ============================================================================

/**
 * Identity Role enum with meaningful names
 */
export enum IdentityRole {
  None = 0,
  Reader = 1,
  Participant = 2,
  Admin = 4,
  SuperAdmin = 8
}

/**
 * Group information with meaningful property names
 */
export interface GroupForIdentity {
  id: string;
  name: string;
  role: IdentityRole;
  // Keep raw data for compatibility
  raw: GroupForIdentityRaw;
}

/**
 * Application identity state with meaningful property names
 */
export interface AppIdentityState {
  groupId?: string;
  leagueId?: string;
  year?: number;
  meOrLeaderEmail?: string;
  // Keep raw data for compatibility
  raw: AppIdentityStateRaw;
}

/**
 * Application identity with meaningful property names
 */
export interface AppIdentity {
  id?: string; // User identifier for repository operations
  username?: string;
  email: string;
  state: AppIdentityState;
  groups: GroupForIdentity[];
  // Keep raw data for compatibility
  raw: AppIdentityRaw;
}

// ============================================================================
// UTILITY FUNCTIONS
// ============================================================================

/**
 * Role utility functions (same as existing authService)
 */
export const hasRole = (userRole: IdentityRole, role: IdentityRole): boolean => {
  return (userRole & role) === role;
};

export const getUserRoleNames = (roleFlags: IdentityRole): string[] => {
  const roles: string[] = [];
  if (hasRole(roleFlags, IdentityRole.Reader)) roles.push('Reader');
  if (hasRole(roleFlags, IdentityRole.Participant)) roles.push('Participant');
  if (hasRole(roleFlags, IdentityRole.Admin)) roles.push('Admin');
  if (hasRole(roleFlags, IdentityRole.SuperAdmin)) roles.push('SuperAdmin');
  return roles;
};

// ============================================================================
// MAPPING FUNCTIONS
// ============================================================================

/**
 * Map raw GroupForIdentity to meaningful interface
 */
export const mapGroupForIdentity = (raw: GroupForIdentityRaw): GroupForIdentity => {
  return {
    id: raw.i,
    name: raw.n,
    role: raw.r,
    raw: raw
  };
};

/**
 * Map raw AppIdentityState to meaningful interface
 */
export const mapAppIdentityState = (raw: AppIdentityStateRaw): AppIdentityState => {
  return {
    groupId: raw.g,
    leagueId: raw.l,
    year: raw.y,
    meOrLeaderEmail: raw.m,
    raw: raw
  };
};

/**
 * Map raw AppIdentity to meaningful interface
 */
export const mapAppIdentity = (raw: AppIdentityRaw): AppIdentity => {
  return {
    id: raw.u || raw.e, // Use username or email as ID
    username: raw.u,
    email: raw.e,
    state: mapAppIdentityState(raw.s),
    groups: raw.g.map(mapGroupForIdentity),
    raw: raw
  };
};

/**
 * Map meaningful AppIdentity back to raw format (for API calls)
 */
export const mapAppIdentityToRaw = (identity: AppIdentity): AppIdentityRaw => {
  return {
    u: identity.username,
    e: identity.email,
    s: {
      g: identity.state.groupId,
      l: identity.state.leagueId,
      y: identity.state.year,
      m: identity.state.meOrLeaderEmail
    },
    g: identity.groups.map(group => ({
      i: group.id,
      n: group.name,
      r: group.role
    }))
  };
};

/**
 * Helper function to create new AppIdentity with updated state
 */
export const updateAppIdentityState = (
  identity: AppIdentity, 
  stateUpdates: Partial<AppIdentityState>
): AppIdentity => {
  return {
    ...identity,
    state: {
      ...identity.state,
      ...stateUpdates,
      raw: {
        ...identity.state.raw,
        ...(stateUpdates.groupId !== undefined && { g: stateUpdates.groupId }),
        ...(stateUpdates.leagueId !== undefined && { l: stateUpdates.leagueId }),
        ...(stateUpdates.year !== undefined && { y: stateUpdates.year }),
        ...(stateUpdates.meOrLeaderEmail !== undefined && { m: stateUpdates.meOrLeaderEmail })
      }
    },
    raw: {
      ...identity.raw,
      s: {
        ...identity.raw.s,
        ...(stateUpdates.groupId !== undefined && { g: stateUpdates.groupId }),
        ...(stateUpdates.leagueId !== undefined && { l: stateUpdates.leagueId }),
        ...(stateUpdates.year !== undefined && { y: stateUpdates.year }),
        ...(stateUpdates.meOrLeaderEmail !== undefined && { m: stateUpdates.meOrLeaderEmail })
      }
    }
  };
};

/**
 * Helper function to add a group to AppIdentity
 */
export const addGroupToAppIdentity = (
  identity: AppIdentity,
  newGroup: { id: string; name: string; role: IdentityRole }
): AppIdentity => {
  const mappedGroup = mapGroupForIdentity({
    i: newGroup.id,
    n: newGroup.name,
    r: newGroup.role
  });
  
  return {
    ...identity,
    groups: [...identity.groups, mappedGroup],
    raw: {
      ...identity.raw,
      g: [...identity.raw.g, mappedGroup.raw]
    }
  };
};

/**
 * Helper function to remove a group from AppIdentity
 */
export const removeGroupFromAppIdentity = (
  identity: AppIdentity,
  groupIdToRemove: string
): AppIdentity => {
  return {
    ...identity,
    groups: identity.groups.filter(group => group.id !== groupIdToRemove),
    raw: {
      ...identity.raw,
      g: identity.raw.g.filter(group => group.i !== groupIdToRemove)
    }
  };
};