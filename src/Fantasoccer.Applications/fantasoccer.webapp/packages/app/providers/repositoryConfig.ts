// Repository Framework Configuration
import { RepositoryServices } from "rystem.repository.client";
import { tokenStorageService } from '../services/tokenStorageService';
import { authService } from '../services/authService';
import { AppIdentityRaw } from "app/types/identity";

// Repository setup
export const setupRepositoryServices = () => {
  const baseUrl = 'https://fcapi.azurewebsites.net/api/';

  RepositoryServices
    .Create(baseUrl)
    // AppIdentity
    .addRepository<AppIdentityRaw, string>(x => {
      x.name = "AppIdentity";
      x.path = "AppIdentity";
      x.addHeadersEnricher(() => getAuthHeaders());
      x.addErrorHandler(handleAuthError);
    });
};

// Helper function for auth headers
const getAuthHeaders = async () : Promise<HeadersInit> => {
  const accessToken = tokenStorageService.getTokenString();
  return accessToken ? {
    "Authorization": `Bearer ${accessToken}`,
    "Content-Type": "application/json"
  } : {
    "Content-Type": "application/json"
  };
};

// Error handler for auth issues
const handleAuthError = async (endpoint: any, uri: string, method: string, headers: any, body: any, err: any) => {
  if (err.status === 401) {
    try {
      const storedData = await tokenStorageService.loadToken();
      if (storedData?.refreshToken) {
        await authService.refreshToken(storedData.refreshToken);
        return true; // Retry the request
      }
    } catch (refreshError) {
      console.error("Token refresh failed", refreshError);
      // Refresh failed, user needs to re-login
      // You could trigger a logout here
    }
  }
  return false; // Don't retry
};