// Serie A service - handles Serie A calendar and related data
import { buildApiUrl, getCommonHeaders, ApiError } from './apiConfig'
import { tokenStorageService } from './tokenStorageService'
import { 
  RealCalendar, 
  EnhancedRealCalendar, 
  RealDay, 
  RealGame,
  RawRealCalendar,
  enhanceRealCalendar,
  mapRawCalendarToCalendar
} from '../types/serieATypes'

export class SerieAService {
  // Get Serie A calendar for a specific year
  async getRealCalendar(year: number, accessToken?: string): Promise<EnhancedRealCalendar> {
    try {
      console.log(`🏆 Fetching Serie A calendar for year ${year}...`)
      
      const url = buildApiUrl('/api/RealCalendar/Get', { key: year.toString() })
      
      console.log('📍 Serie A Calendar URL:', url)

      // Get token if not provided
      let token = accessToken
      if (!token) {
        const storedData = await tokenStorageService.loadToken()
        if (storedData) {
          token = storedData.accessToken
        }
      }

      const response = await fetch(url, {
        method: 'GET',
        headers: getCommonHeaders(!!token, token),
      })

      console.log('📊 Serie A Calendar response status:', response.status)

      if (!response.ok) {
        const errorText = await response.text()
        console.error('❌ Serie A Calendar fetch failed:', errorText)
        
        if (response.status === 401) {
          throw new ApiError('UNAUTHORIZED', response.status, errorText)
        }
        
        throw new ApiError(
          `Serie A Calendar fetch failed: ${response.status} ${response.statusText}`, 
          response.status, 
          errorText
        )
      }

      const rawData: RawRealCalendar = await response.json()
      console.log(`✅ Serie A Calendar fetched successfully! ${rawData.d.length} days found`)
      console.log('📅 Calendar year:', rawData.y)

      // Map raw data to clean interfaces
      const cleanCalendar = mapRawCalendarToCalendar(rawData)
      
      // Enhance with computed properties
      const enhancedCalendar = enhanceRealCalendar(cleanCalendar)
      
      console.log('📊 Calendar status:')
      console.log('- Live day:', enhancedCalendar.liveDay?.serieADay || 'None')
      console.log('- Last completed day:', enhancedCalendar.lastDay?.serieADay || 'None')
      console.log('- Next day:', enhancedCalendar.nextDay?.serieADay || 'None')
      console.log('- Is live:', enhancedCalendar.isLive)
      console.log('- Live games:', enhancedCalendar.liveGames?.length || 0)

      return enhancedCalendar
    } catch (error) {
      console.error('❌ Serie A Calendar fetch error:', error)
      
      if (error instanceof ApiError) throw error
      throw new ApiError(`Failed to fetch Serie A calendar: ${error instanceof Error ? error.message : 'Unknown error'}`)
    }
  }

  // Get current user's year calendar
  async getCurrentYearCalendar(): Promise<EnhancedRealCalendar> {
    try {
      // Get user data to determine current year
      const storedData = await tokenStorageService.loadToken()
      if (!storedData || !storedData.user) {
        throw new ApiError('No user data found. Please login first.')
      }

      const currentYear = storedData.user.s?.y
      if (!currentYear) {
        throw new ApiError('User year not found in stored data.')
      }

      console.log(`🎯 Fetching current year calendar (Year ${currentYear}) for user...`)
      
      return await this.getRealCalendar(currentYear, storedData.accessToken)
    } catch (error) {
      console.error('❌ Current year calendar fetch error:', error)
      
      if (error instanceof ApiError) throw error
      throw new ApiError(`Failed to fetch current year calendar: ${error instanceof Error ? error.message : 'Unknown error'}`)
    }
  }

  // Get specific Serie A day
  async getSerieADay(year: number, dayNumber: number): Promise<RealDay | null> {
    try {
      const calendar = await this.getRealCalendar(year)
      return calendar.days.find(day => day.serieADay === dayNumber) || null
    } catch (error) {
      console.error(`❌ Failed to fetch Serie A day ${dayNumber} for year ${year}:`, error)
      throw error
    }
  }

  // Get live games
  async getLiveGames(): Promise<RealGame[]> {
    try {
      const calendar = await this.getCurrentYearCalendar()
      return calendar.liveGames || []
    } catch (error) {
      console.error('❌ Failed to fetch live games:', error)
      throw error
    }
  }

  // Check if Serie A is currently live
  async isSerieALive(): Promise<boolean> {
    try {
      const calendar = await this.getCurrentYearCalendar()
      return calendar.isLive || false
    } catch (error) {
      console.error('❌ Failed to check if Serie A is live:', error)
      return false
    }
  }

  // Get current Serie A day number
  async getCurrentSerieADay(): Promise<number> {
    try {
      const calendar = await this.getCurrentYearCalendar()
      return calendar.liveSerieADay || calendar.lastDay?.serieADay || 0
    } catch (error) {
      console.error('❌ Failed to get current Serie A day:', error)
      return 0
    }
  }

  // Get next Serie A day
  async getNextSerieADay(): Promise<RealDay | null> {
    try {
      const calendar = await this.getCurrentYearCalendar()
      return calendar.nextDay || null
    } catch (error) {
      console.error('❌ Failed to get next Serie A day:', error)
      return null
    }
  }

  // Get games for a specific team (by team name)
  async getTeamGames(year: number, teamName: string): Promise<RealGame[]> {
    try {
      const calendar = await this.getRealCalendar(year)
      const games: RealGame[] = []
      
      calendar.days.forEach(day => {
        day.games.forEach(game => {
          if (game.home.name === teamName || game.away.name === teamName) {
            games.push(game)
          }
        })
      })
      
      return games
    } catch (error) {
      console.error(`❌ Failed to fetch games for team ${teamName}:`, error)
      throw error
    }
  }

  // Get completed games
  async getCompletedGames(year?: number): Promise<RealGame[]> {
    try {
      const calendar = year 
        ? await this.getRealCalendar(year)
        : await this.getCurrentYearCalendar()
      
      const completedGames: RealGame[] = []
      
      calendar.days.forEach(day => {
        day.games.forEach(game => {
          if (game.date && !game.delayed && game.homeGoals !== null && game.awayGoals !== null) {
            completedGames.push(game)
          }
        })
      })
      
      return completedGames
    } catch (error) {
      console.error('❌ Failed to fetch completed games:', error)
      throw error
    }
  }

  // Get upcoming games
  async getUpcomingGames(year?: number): Promise<RealGame[]> {
    try {
      const calendar = year 
        ? await this.getRealCalendar(year)
        : await this.getCurrentYearCalendar()
      
      const now = new Date()
      const upcomingGames: RealGame[] = []
      
      calendar.days.forEach(day => {
        day.games.forEach(game => {
          if (game.date && !game.delayed && new Date(game.date) > now) {
            upcomingGames.push(game)
          }
        })
      })
      
      // Sort by date
      upcomingGames.sort((a, b) => 
        new Date(a.date!).getTime() - new Date(b.date!).getTime()
      )
      
      return upcomingGames
    } catch (error) {
      console.error('❌ Failed to fetch upcoming games:', error)
      throw error
    }
  }
}

// Export singleton instance
export const serieAService = new SerieAService()