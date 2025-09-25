// Route definitions that match the prompt_frontendapp specification
import { Platform } from 'react-native'

// Platform-proxy hook: when code imports `useAppNavigation` from this module
// we delegate at runtime to the platform-specific implementation. This
// avoids bundling `next/navigation` into native builds and keeps a single
// import path for consumers.
export const useAppNavigation = () => {
  if (Platform.OS === 'web') {
    // eslint-disable-next-line @typescript-eslint/no-var-requires
    const { useAppNavigation: webHook } = require('./useAppNavigation.web') as { useAppNavigation: any }
    return webHook()
  }

  // native fallback
  // eslint-disable-next-line @typescript-eslint/no-var-requires
  const { useAppNavigation: nativeHook } = require('./useAppNavigation.native') as { useAppNavigation: any }
  return nativeHook()
}

// Route definitions that match the prompt_frontendapp specification
export const ROUTES = {
  // Public / Auth
  LOGIN: '/login',
  LOGOUT: '/logout',
  
  // Main
  HOME: '/',
  RANKINGS: '/ranking',
  STANDINGS: '/standings',
  CALENDAR: '/calendar',
  LIVE: '/live',
  
  // League
  TEAMS: '/teams',
  TEAM_DETAILS: (teamId: string) => `/teams/${teamId}`,
  
  // Players
  PLAYERS: '/players',
  PLAYER_DETAILS: (playerId: string) => `/players/${playerId}`,
  FREE_PLAYERS: '/free-players',
  
  // Market
  MARKET: '/market',
  MARKET_TRADES: '/market/trades',
  MARKET_TRADES_HISTORY: '/market/trades/history',
  MARKET_TRADES_NEW: '/market/trades/new',
  
  // Hall of Fame
  HALL_OF_FAME: '/hall-of-fame',
  
  // Serie A
  SERIEA_STANDINGS: '/seriea/standings',
  SERIEA_CALENDAR: '/seriea/calendar',
  
  // Auction
  AUCTION: '/auction',
  AUCTION_MANAGE: '/auction/manage',
  
  // My Team
  MY_TEAM: '/my-team',
  
  // User Settings
  SETTINGS: '/settings',
  SETTINGS_TEAM: '/settings/team',
  SETTINGS_LEAGUE: '/settings/league',
  SETTINGS_SEASONS: '/settings/seasons',
  
  // Admin
  ADMIN: '/admin',
  ADMIN_GROUPS: '/admin/groups',
  ADMIN_AUCTION: '/admin/auction',
  
  // Super Admin
  SUPERADMIN: '/superadmin',
  SUPERADMIN_USERS: '/superadmin/users',
  SUPERADMIN_LOGS: '/superadmin/logs',
  SUPERADMIN_ERRORS: '/superadmin/errors',
  SUPERADMIN_LEAGUE_CALENDAR: '/superadmin/league-calendar',
  SUPERADMIN_SERIEA_CALENDAR: '/superadmin/seriea-calendar',
} as const

// Helper to get route title for display
export const getRouteTitle = (path: string): string => {
  const routeTitles: Record<string, string> = {
    '/': 'Home',
    '/ranking': 'Rankings',
    '/standings': 'Fantasy Standings',
    '/calendar': 'Fantasy Calendar', 
    '/live': 'Live',
    '/teams': 'Teams',
    '/players': 'All Players',
    '/free-players': 'Free Players',
    '/market': 'Market Hub',
    '/market/trades': 'Current Trades',
    '/market/trades/history': 'Trade History',
    '/market/trades/new': 'New Trade',
    '/hall-of-fame': 'Hall of Fame',
    '/seriea/standings': 'Serie A Standings',
    '/seriea/calendar': 'Serie A Calendar',
    '/auction': 'Auction',
    '/auction/manage': 'Manage Auction',
    '/my-team': 'My Team',
    '/settings': 'Settings',
    '/settings/team': 'Team Settings',
    '/settings/league': 'League Settings',
    '/settings/seasons': 'Seasons',
    '/admin': 'Admin Dashboard',
    '/admin/groups': 'Manage Groups',
    '/admin/auction': 'Auction Control',
    '/superadmin': 'Super Admin',
    '/superadmin/users': 'Manage Users',
    '/superadmin/logs': 'System Logs',
    '/superadmin/errors': 'Error Reports',
    '/superadmin/league-calendar': 'League Calendar',
    '/superadmin/seriea-calendar': 'Serie A Calendar',
  }
  
  return routeTitles[path] || path
}

// Mobile screen name mapping for React Navigation
export const getScreenNameFromPath = (path: string): string => {
  const pathToScreen: Record<string, string> = {
    '/': 'Home',
    '/ranking': 'Rankings',
    '/standings': 'Standings',
    '/calendar': 'Calendar',
    '/live': 'Live',
    '/teams': 'Teams',
    '/players': 'Players',
    '/free-players': 'FreePlayers',
    '/market': 'Market',
    '/market/trades': 'MarketTrades',
    '/market/trades/history': 'TradeHistory',
    '/market/trades/new': 'NewTrade',
    '/hall-of-fame': 'HallOfFame',
    '/seriea/standings': 'SerieAStandings',
    '/seriea/calendar': 'SerieACalendar',
    '/auction': 'Auction',
    '/auction/manage': 'AuctionManage',
    '/my-team': 'MyTeam',
    '/settings': 'Settings',
    '/settings/team': 'TeamSettings',
    '/settings/league': 'LeagueSettings',
    '/settings/seasons': 'SeasonsSettings',
    '/admin': 'Admin',
    '/admin/groups': 'AdminGroups',
    '/admin/auction': 'AdminAuction',
    '/superadmin': 'SuperAdmin',
    '/superadmin/users': 'SuperAdminUsers',
    '/superadmin/logs': 'SuperAdminLogs',
    '/superadmin/errors': 'SuperAdminErrors',
    '/superadmin/league-calendar': 'SuperAdminLeagueCalendar',
    '/superadmin/seriea-calendar': 'SuperAdminSerieACalendar',
  }
  
  return pathToScreen[path] || 'Home'
}

// Note: platform-specific `useAppNavigation` hooks are provided in
// `useAppNavigation.web.ts` (web - Next.js App Router) and
// `useAppNavigation.native.ts` (React Native). Importing the hook
// by bare module specifier will resolve to the correct file for
// the target platform (e.g. bundlers that support .web/.native
// resolution such as Metro and Next's webpack config).