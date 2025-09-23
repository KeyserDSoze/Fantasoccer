import { Platform } from 'react-native'

// Common MSAL configuration
export const MSAL_CONFIG = {
  clientId: '95de3061-5eab-437d-a2cc-6d71cfaef4d1',
  authority: 'https://login.microsoftonline.com/consumers',
  scopes: ['email', 'profile', 'openid'],
}

// Platform-specific configurations
export const getMsalConfig = () => {
  const baseConfig = {
    auth: {
      clientId: MSAL_CONFIG.clientId,
      authority: MSAL_CONFIG.authority,
    },
  }

  if (Platform.OS === 'web') {
    return {
      ...baseConfig,
      auth: {
        ...baseConfig.auth,
        redirectUri: typeof window !== 'undefined' ? window.location.origin : undefined,
      },
      cache: {
        cacheLocation: 'sessionStorage',
        storeAuthStateInCookie: false,
      },
    }
  }

  // React Native configuration
  return {
    ...baseConfig,
    auth: {
      ...baseConfig.auth,
      redirectUri: Platform.OS === 'ios' 
        ? 'msauth.com.fantasoccer.app://auth' 
        : 'msauth://com.fantasoccer.app/auth',
    },
  }
}

export const LOGIN_REQUEST = {
  scopes: MSAL_CONFIG.scopes,
}

export const GRAPH_CONFIG = {
  graphMeEndpoint: 'https://graph.microsoft.com/v1.0/me',
}