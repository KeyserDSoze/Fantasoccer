// Export all types for easy import
export * from './identity';
export * from './serieA';

// Re-export commonly used types with shorter names for convenience
export type {
  AppIdentity,
  AppIdentityRaw,
  AppIdentityState,
  AppIdentityStateRaw,
  GroupForIdentity,
  GroupForIdentityRaw
} from './identity';

export type {
  RealTeamRaw as RawRealTeam,
  RealGameRaw as RawRealGame,
  RealDayRaw as RawRealDay,
  RealCalendarRaw,
  RealTeam,
  RealGame,
  RealDay,
  RealCalendar,
  EnhancedRealCalendar
} from './serieA';

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

export {
  mapRawTeamToTeam,
  mapRawGameToGame,
  mapRawDayToDay,
  mapRawCalendarToCalendar,
  RealCalendarHelper,
  enhanceRealCalendar,
  convertUTCToItalianTimeUtil
} from './serieA';