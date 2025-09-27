// Repository Framework Configuration
import { RepositoryServices } from "rystem.repository.client";
import { tokenStorageService } from '../services/tokenStorageService';
import { authService } from '../services/authService';
import { AppIdentityRaw } from "types/identity";
import { RealCalendarRaw } from "../types/serieA";
import { CalendarRaw } from "types/calendar";
import { buildApiUrl } from "./apiConfig";
import { GroupRaw } from "types/group";
import { RankRaw, LeagueKeyRaw } from "types/rank";
import { PlayedGameRaw, GameKeyRaw } from "types/game";

// Repository setup
export const setupRepositoryServices = () => {
  const baseUrl = buildApiUrl('/api/');

  RepositoryServices
    .Create(baseUrl)
    // AppIdentity
    .addRepository<AppIdentityRaw, string>(x => {
      x.name = "AppIdentity";
      x.path = "AppIdentity";
      x.addHeadersEnricher(() => getAuthHeaders());
      x.addErrorHandler(handleAuthError);
    })
    // RealCalendar (Serie A)
    .addRepository<RealCalendarRaw, number>(x => {
      x.name = "RealCalendar";
      x.path = "RealCalendar";
      x.addHeadersEnricher(() => getAuthHeaders());
      x.addErrorHandler(handleAuthError);
    })
    // Group for leagues
    .addRepository<GroupRaw, string>(x => {
      x.name = "Group";
      x.path = "Group";
      x.addHeadersEnricher(() => getAuthHeaders());
      x.addErrorHandler(handleAuthError);
    })
      // PlayedGame repository for detailed match data
      .addRepository<PlayedGameRaw, GameKeyRaw>(x => {
        x.name = "PlayedGame";
        x.path = "PlayedGame";
        x.addHeadersEnricher(() => getAuthHeaders());
        x.addErrorHandler(handleAuthError);
      })
    // Calendar for fantasy leagues
    .addRepository<CalendarRaw, LeagueKeyRaw>(x => {
      x.name = "Calendar";
      x.path = "Calendar";
      x.addHeadersEnricher(() => getAuthHeaders());
      x.addErrorHandler(handleAuthError);
    })
    // Rank for daily rankings with LeagueKey (YearDayLeague)
    .addRepository<RankRaw, LeagueKeyRaw>(x => {
      x.name = "Rank";
      x.path = "day/dailyrank";  // Matching the blob storage path "day", "dailyrank"
      x.addHeadersEnricher(() => getAuthHeaders());
      x.addErrorHandler(handleAuthError);
    });
};

// Helper function for auth headers with automatic token refresh
const getAuthHeaders = async () : Promise<HeadersInit> => {
  // Use the new ensureValidToken method that handles refresh automatically
  const accessToken = await tokenStorageService.ensureValidToken();
  
  return accessToken ? {
    "Authorization": `Bearer ${accessToken}`,
    "Content-Type": "application/json"
  } : {
    "Content-Type": "application/json"
  };
};

// Error handler for auth issues with automatic token refresh and save
const handleAuthError = async (endpoint: any, uri: string, method: string, headers: any, body: any, err: any) => {
  if (err.status === 401) {
    console.log('🔐 401 Unauthorized - attempting token refresh...');
    try {
      const storedData = await tokenStorageService.loadToken();
      if (storedData?.refreshToken) {
        console.log('🔄 Refreshing token...');
        const newTokenData = await authService.refreshToken(storedData.refreshToken);
        
        // Calculate expiration time (assuming token is valid for the specified seconds)
        const expiresAt = Date.now() + (newTokenData.expiresIn * 1000);
        
        // Save the new token data
        const updatedTokenData = {
          tokenType: newTokenData.tokenType,
          accessToken: newTokenData.accessToken,
          refreshToken: newTokenData.refreshToken,
          expiresAt
        };
        
        await tokenStorageService.saveToken(updatedTokenData);
        console.log('✅ Token refreshed and saved successfully');
        
        return true; // Retry the request with new token
      } else {
        console.log('❌ No refresh token available');
        await handleAuthenticationFailure();
      }
    } catch (refreshError) {
      console.error("❌ Token refresh failed:", refreshError);
      await handleAuthenticationFailure();
    }
  }
  return false; // Don't retry
};

// Handle authentication failure by clearing tokens and redirecting to login
const handleAuthenticationFailure = async () => {
  try {
    // Clear all stored tokens
    await tokenStorageService.removeToken();
    console.log('🧹 Cleared invalid tokens');
    
    // If we're in browser environment, redirect to login
    if (typeof window !== 'undefined') {
      console.log('🚪 Redirecting to login (client-side) ...');
      try {
        // Prefer client-side navigation via history API so Next App Router can handle it
        window.history.pushState({}, '', '/login')
        window.dispatchEvent(new PopStateEvent('popstate'))
      } catch (err) {
        // Fallback to full reload if history API fails
        console.warn('Client-side redirect failed, falling back to full redirect', err)
        window.location.href = '/login'
      }
    }
  } catch (error) {
    console.error('❌ Error handling authentication failure:', error);
  }
};