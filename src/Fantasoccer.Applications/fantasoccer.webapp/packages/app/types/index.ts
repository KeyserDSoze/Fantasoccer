// Export all types for easy import
export * from './identity';

// Re-export commonly used types with shorter names for convenience
export type {
  AppIdentity,
  AppIdentityRaw,
  AppIdentityState,
  AppIdentityStateRaw,
  GroupForIdentity,
  GroupForIdentityRaw
} from './identity';

export {
  IdentityRole,
  hasRole,
  getUserRoleNames,
  mapAppIdentity,
  mapAppIdentityState,
  mapGroupForIdentity,
  mapAppIdentityToRaw,
  updateAppIdentityState,
  addGroupToAppIdentity,
  removeGroupFromAppIdentity
} from './identity';