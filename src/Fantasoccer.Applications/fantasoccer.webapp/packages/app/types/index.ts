// Export all types for easy import
export * from './identity';
export * from './serieA';
export * from './group';

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

export type {
  // Raw interfaces
  VoteLeagueSettingRaw,
  LeagueSettingRaw,
  AnnualLeagueRaw,
  LeagueRaw,
  AnnualTeamRaw,
  YearlyBasketRaw,
  BasketRaw,
  UserOfAGroupRaw,
  GroupRaw,
  
  // Clean interfaces
  VoteLeagueSetting,
  LeagueSetting,
  AnnualLeague,
  League,
  AnnualTeam,
  YearlyBasket,
  Basket,
  UserOfAGroup,
  Group,
  EnhancedGroup
} from './group';

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

// Group types and utilities
export {
  // Enums
  Role,
  LeagueType,
  MarketType,
  
  // Extensions
  RoleExtensions,
  LeagueTypeExtensions,
  
  // Mapping functions
  mapRawVoteLeagueSettingToVoteLeagueSetting,
  mapRawLeagueSettingToLeagueSetting,
  mapRawAnnualLeagueToAnnualLeague,
  mapRawLeagueToLeague,
  mapRawAnnualTeamToAnnualTeam,
  mapRawYearlyBasketToYearlyBasket,
  mapRawBasketToBasket,
  mapRawUserOfAGroupToUserOfAGroup,
  mapRawGroupToGroup,
  
  // Helper classes
  GroupHelper,
  
  // Constants
  DefaultLeagueSetting,
  
  // Enhancement functions
  enhanceGroup,
} from './group';