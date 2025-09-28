// Serie A service using Repository Framework
import { authService } from './authService';
import { tokenStorageService } from './tokenStorageService';
import { 
  RealCalendarRaw, 
  RealCalendar, 
  mapRawCalendarToCalendar, 
  EnhancedRealCalendar, 
  enhanceRealCalendar, 
  RealRankRaw, 
  RealRank, 
  mapRawRankToRank 
} from 'types/serieA';
import { RepositoryServices } from 'rystem.repository.client';

export class SerieAService {
  private async getCurrentUserYear(): Promise<number | null> {
    try {
      const accessToken = await tokenStorageService.getTokenString();
      if (!accessToken) {
        console.warn('No valid access token available');
        return null;
      }

      const user = await authService.getUserInfo(accessToken);
      const year = user?.state?.year;

      if (!year) {
        console.warn('User year not available');
        return null;
      }

      return year;
    } catch (error) {
      console.error('Error retrieving user year:', error);
      return null;
    }
  }

  // Get Serie A calendar for a specific year
  async getCalendarByYear(year: number): Promise<RealCalendar | null> {
    try {
      // Get the repository instance
      const repository = RepositoryServices
        .Repository<RealCalendarRaw, number>("RealCalendar");
      
      // Fetch the calendar data
      const result = await repository.get(year);
      
      if (result) {
        // Map raw data to clean interface
        return mapRawCalendarToCalendar(result);
      }
      
      return null;
    } catch (error) {
      console.error('Error fetching Serie A calendar:', error);
      return null;
    }
  }

  // Get Serie A calendar for the current user's year
  async getCurrentUserCalendar(): Promise<RealCalendar | null> {
    const year = await this.getCurrentUserYear();
    if (!year) {
      return null;
    }

    return this.getCalendarByYear(year);
  }

  // Get enhanced calendar with computed properties for current user
  async getEnhancedCurrentUserCalendar(): Promise<EnhancedRealCalendar | null> {
    const calendar = await this.getCurrentUserCalendar();
    
    if (!calendar) {
      return null;
    }

    return enhanceRealCalendar(calendar);
  }

  // Get enhanced calendar with computed properties for specific year
  async getEnhancedCalendarByYear(year: number): Promise<EnhancedRealCalendar | null> {
    const calendar = await this.getCalendarByYear(year);
    
    if (!calendar) {
      return null;
    }

    return enhanceRealCalendar(calendar);
  }

  // Helper method to check if a calendar year is available
  async isCalendarAvailable(year: number): Promise<boolean> {
    const calendar = await this.getCalendarByYear(year);
    return calendar !== null && calendar.days.length > 0;
  }

  // Get Serie A rank for a specific year
  async getRankByYear(year: number): Promise<RealRank | null> {
    try {
      const repository = RepositoryServices.Repository<RealRankRaw, number>('RealRank');
      const result = await repository.get(year);

      return result ? mapRawRankToRank(result) : null;
    } catch (error) {
      console.error('Error fetching Serie A rank:', error);
      return null;
    }
  }

  // Get Serie A rank for the current user's year
  async getCurrentUserRank(): Promise<RealRank | null> {
    const year = await this.getCurrentUserYear();
    if (!year) {
      return null;
    }

    return this.getRankByYear(year);
  }

  // Get available years (this would need a separate endpoint)
  async getAvailableYears(): Promise<number[]> {
    // This would typically come from a separate API endpoint
    // For now, return current and previous years as an example
    const currentYear = new Date().getFullYear();
    return [currentYear, currentYear - 1, currentYear - 2];
  }
}

// Export singleton instance
export const serieAService = new SerieAService();