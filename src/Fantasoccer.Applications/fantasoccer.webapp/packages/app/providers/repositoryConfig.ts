// Repository Framework Configuration
import { RepositoryServices } from "rystem.repository.client";
import { tokenStorageService } from '../services/tokenStorageService';
import { authService } from '../services/authService';
import { AppIdentityRaw } from "app/types/identity";
import { RealCalendarRaw } from "../types/serieA";
import { buildApiUrl } from "app/services/apiConfig";
import { GroupRaw } from "app/types/group";

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
      }
    } catch (refreshError) {
      console.error("❌ Token refresh failed:", refreshError);
      
      // Clear invalid tokens
      await tokenStorageService.removeToken();
      
      // Refresh failed, user needs to re-login
      // In a real app, you might want to trigger a logout or redirect to login
      console.log('🚪 User needs to re-authenticate');
    }
  }
  return false; // Don't retry
};