import { useCallback } from 'react'
import { Platform } from 'react-native'

// Hook for navigation that works across web and mobile
export const useAppNavigation = () => {
  const navigate = useCallback((path: string) => {
    if (Platform.OS === 'web') {
      // For web - would integrate with Next.js router
      // For now, using window.location for demo
      if (typeof window !== 'undefined') {
        // In a real app, you'd use Next.js router:
        // import { useRouter } from 'next/router'
        // const router = useRouter()
        // router.push(path)
        
        console.log('Navigate to:', path)
        // Temporary: just log or redirect to home for demo
        if (path === '/') {
          window.location.reload()
        } else {
          alert(`Navigation to: ${path}\n\nThis would use Next.js router in production.`)
        }
      }
    } else {
      // For React Native - would use React Navigation
      // import { useNavigation } from '@react-navigation/native'
      // const navigation = useNavigation()
      // navigation.navigate(routeName, params)
      
      console.log('React Native navigation to:', path)
    }
  }, [])

  const canGoBack = useCallback(() => {
    if (Platform.OS === 'web') {
      return typeof window !== 'undefined' && window.history.length > 1
    }
    return true // React Navigation handles this
  }, [])

  const goBack = useCallback(() => {
    if (Platform.OS === 'web') {
      if (typeof window !== 'undefined') {
        window.history.back()
      }
    } else {
      // React Navigation goBack
      console.log('React Native go back')
    }
  }, [])

  return {
    navigate,
    canGoBack,
    goBack,
  }
}

// Route definitions that match the prompt_frontendapp specification
export const ROUTES = {
  // Public / Auth
  LOGIN: '/login',
  LOGOUT: '/logout',
  
  // Main
  HOME: '/',
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