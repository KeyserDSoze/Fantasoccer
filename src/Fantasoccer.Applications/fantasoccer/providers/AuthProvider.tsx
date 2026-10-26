import React, { createContext, useContext, useEffect, useState, ReactNode } from 'react'
import { Platform } from 'react-native'
import { authService } from '../services/authService'
import { AppIdentity } from '../types/identity'
import { tokenStorageService } from '../services/tokenStorageService'
import { PKCEHelper } from '../utils/pkceHelper'
import { useGlobalLoading } from '../providers/LoadingProvider'
import { useToastController } from '@tamagui/toast'
import { disconnectPushDevice } from '../services/pushDeviceService'

export type AuthUser = AppIdentity

export interface AuthState {
  isAuthenticated: boolean
  isLoading: boolean
  user: AuthUser | null
  token: string | null
  error: string | null
}

export interface AuthContextType extends AuthState {
  login: () => Promise<void>
  loginWithPKCE: () => Promise<void>
  logout: () => Promise<void>
  clearError: () => void
  refreshAuth: () => Promise<void>
  updateUser: (updatedUser: AppIdentity) => void
}

// Context
const AuthContext = createContext<AuthContextType | undefined>(undefined)

// Hook
export const useAuth = (): AuthContextType => {
  const context = useContext(AuthContext)
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider')
  }
  return context
}

// Provider Props
interface AuthProviderProps {
  children: ReactNode
}

// MSAL Configuration
const getMsalConfig = () => ({
  auth: {
    clientId: process.env.EXPO_PUBLIC_MICROSOFT_CLIENT_ID || "95de3061-5eab-437d-a2cc-6d71cfaef4d1",
    authority: 'https://login.microsoftonline.com/consumers',
    redirectUri: Platform.select({
      web: typeof window !== 'undefined' ? window.location.origin + '/' : 'http://localhost:3000',
      default: 'msauth://com.keyserdsoze.fantasoccer/auth'
    }),
  },
  cache: {
    cacheLocation: 'localStorage',
    storeAuthStateInCookie: false,
  },
  scopes: ['openid', 'profile', 'email'],
})

// Provider Component
export const AuthProvider: React.FC<AuthProviderProps> = ({ children }) => {
  const { showLoading, hideLoading } = useGlobalLoading()
  const toast = useToastController()

  const [state, setState] = useState<AuthState>({
    isAuthenticated: false,
    isLoading: true,
    user: null,
    token: null,
    error: null,
  })

  // MSAL instances (lazy loaded)
  const [msalInstance, setMsalInstance] = useState<any>(null)
  const [isInitialized, setIsInitialized] = useState(false)

  // Initialize MSAL
  const initializeMsal = async () => {
    try {
      showLoading('Inizializzazione...')
      // debug
      // eslint-disable-next-line no-console
      console.log('[AuthProvider] initializeMsal start')
      
      if (Platform.OS === 'web') {
        // Web MSAL initialization
        const { PublicClientApplication } = await import('@azure/msal-browser')
        const instance = new PublicClientApplication(getMsalConfig())
        await instance.initialize()
        setMsalInstance(instance)
      } else {
        // React Native - MSAL may not be available, fallback to PKCE only
        setMsalInstance(null) // Will use PKCE flow instead
      }
      
      setIsInitialized(true)
      // Initialization finished, hide the global loader started above.
      // eslint-disable-next-line no-console
      console.debug('[AuthProvider] initializeMsal done')
      hideLoading()
    } catch (error) {
      const errorMessage = `MSAL initialization failed: ${error instanceof Error ? error.message : 'Unknown error'}`
      setState(prev => ({ 
        ...prev, 
        error: errorMessage,
        isLoading: false 
      }))
      // Show error toast
      toast.show(errorMessage, { 
        variant: 'error',
        duration: 5000,
      })
      hideLoading()
    }
  }

  // Check existing authentication
  const checkExistingAuth = async () => {
    try {
      showLoading('Verifica credenziali...')
      // eslint-disable-next-line no-console
      console.debug('[AuthProvider] checkExistingAuth start')
      
      // Check stored token
      const storedData = await tokenStorageService.loadToken()
      if (storedData) {
        showLoading('Caricamento profilo utente...')
        
        try {
          // Verify token with backend (includes automatic refresh if needed)
          const userInfo = await authService.getUserInfo(storedData.accessToken, storedData.refreshToken)
          
          setState(prev => ({
            ...prev,
            isAuthenticated: true,
            user: userInfo,
            token: storedData.accessToken,
            isLoading: false,
            error: null,
          }))
          
          // eslint-disable-next-line no-console
          console.debug('[AuthProvider] checkExistingAuth: user loaded')
          hideLoading()
          return
        } catch (error) {
          // If refresh failed or unauthorized, redirect to login
          if (error instanceof Error && (error.message === 'REFRESH_FAILED' || error.message === 'UNAUTHORIZED')) {
            await tokenStorageService.removeToken()
            // Don't set error state, just continue to show login screen
          } else {
            throw error // Re-throw other errors
          }
        }
      }
      
      setState(prev => ({ ...prev, isLoading: false }))
      // eslint-disable-next-line no-console
      console.debug('[AuthProvider] checkExistingAuth: no token')
      hideLoading()
      
    } catch (error) {
      // Clear invalid token
      await tokenStorageService.removeToken()
      setState(prev => ({ 
        ...prev, 
        isLoading: false,
        error: null // Don't show error for expired/invalid tokens
      }))
      // eslint-disable-next-line no-console
      console.debug('[AuthProvider] checkExistingAuth: error', error)
      hideLoading()
    }
  }

  // Initialize on mount
  useEffect(() => {
    const initialize = async () => {
      // eslint-disable-next-line no-console
      console.debug('[AuthProvider] overall initialize start')
      await initializeMsal()

      // Check for OAuth callback with authorization code
      if (Platform.OS === 'web' && typeof window !== 'undefined') {
        // eslint-disable-next-line no-console
        console.debug('[AuthProvider] detecting OAuth callback...')
        await handleOAuthCallback()
      }

      // After init (regardless of state variable timing), check existing auth
      // eslint-disable-next-line no-console
      console.debug('[AuthProvider] calling checkExistingAuth')
      await checkExistingAuth()
    }

    // Run once on mount
    initialize()
    return () => {
      console.debug('[AuthProvider] unmounted');
    };
  }, [])

  // Handle OAuth callback with authorization code
  const handleOAuthCallback = async () => {
    try {
      const urlParams = new URLSearchParams(window.location.search)
      const authCode = urlParams.get('code')
      const error = urlParams.get('error')
      const state = urlParams.get('state')

      if (error) {
        const errorMessage = `OAuth error: ${error}`
        setState(prev => ({ 
          ...prev, 
          error: errorMessage,
          isLoading: false 
        }))
        // Show error toast
        toast.show(errorMessage, { 
          variant: 'error',
          duration: 5000,
        })
        // Ensure global loader is hidden when an OAuth error occurs
        hideLoading()
        return
      }

      if (authCode) {
        const codeVerifier = await PKCEHelper.consumeTransaction(state)
        // Show loading indicator during token exchange
        showLoading('Autenticazione in corso...')
        
        // Clear URL parameters
        window.history.replaceState({}, document.title, window.location.pathname)
        
        setState(prev => ({ ...prev, isLoading: true, error: null }))
        
        try {
          // Exchange authorization code for backend token using PKCE
          const tokenResponse = await authService.exchangeToken(authCode, {
            isPKCE: true,
            codeVerifier,
            redirectPath: '/',
          })
          
          // Get user info using the new accessToken
          const userInfo = await authService.getUserInfo(tokenResponse.accessToken, tokenResponse.refreshToken)

          // Store complete token data
          await tokenStorageService.saveToken({
            tokenType: tokenResponse.tokenType,
            accessToken: tokenResponse.accessToken,
            refreshToken: tokenResponse.refreshToken,
            expiresAt: Date.now() + (tokenResponse.expiresIn * 1000),
            user: userInfo,
          })

          setState(prev => ({
            ...prev,
            isAuthenticated: true,
            user: userInfo,
            token: tokenResponse.accessToken, // Store accessToken in state
            isLoading: false,
            error: null,
          }))
          // Hide global loader after successful token exchange and user load
          hideLoading()

          // Check for saved return URL and redirect if exists
          const { returnUrlService } = await import('../services/returnUrlService')
          const returnUrl = await returnUrlService.consumeReturnUrl()
          
          if (returnUrl) {
            console.log('🔄 Navigating to saved URL:', returnUrl)
            
            // Use setTimeout to ensure state is updated before navigation
            setTimeout(() => {
              if (Platform.OS === 'web') {
                // Use history API to change URL without full page reload
                // This allows React Navigation to handle the routing properly
                window.history.replaceState({}, '', returnUrl)
                // Dispatch a popstate event to trigger React Navigation's listener
                window.dispatchEvent(new PopStateEvent('popstate'))
              } else {
                // For React Native, parse the URL and navigate
                // Example: /auction/123 -> navigate to AuctionLive with auctionId: 123
                const urlParts = returnUrl.split('/')
                if (urlParts[1] === 'auction' && urlParts[2]) {
                  // We can't navigate directly here without access to navigation
                  // Store a flag that the app can check on mount
                  console.log('📱 React Native: Will navigate to auction-live with ID:', urlParts[2])
                  // The navigation will be handled by useEffect in the main app component
                }
              }
            }, 100)
          }

        } catch (exchangeError) {
          const errorMessage = `Token exchange failed: ${exchangeError instanceof Error ? exchangeError.message : 'Unknown error'}`
          setState(prev => ({
            ...prev,
            isLoading: false,
            error: errorMessage,
          }))
          // Show error toast
          toast.show(errorMessage, { 
            variant: 'error',
            duration: 5000,
          })
          hideLoading()
        }
      }
    } catch (error) {
      const errorMessage = `Callback handling failed: ${error instanceof Error ? error.message : 'Unknown error'}`
      setState(prev => ({ 
        ...prev, 
        error: errorMessage,
        isLoading: false 
      }))
      // Show error toast
      toast.show(errorMessage, { 
        variant: 'error',
        duration: 5000,
      })
      // Ensure global loader is hidden on errors
      hideLoading()
    }
  }

  // Login with OAuth authorization code flow (not MSAL tokens)
  const login = async () => {
    try {
      setState(prev => ({ ...prev, isLoading: true, error: null }))
      
      // Always use PKCE flow to get authorization code (not tokens)
      return await loginWithPKCE()

    } catch (error) {
      const errorMessage = `Login failed: ${error instanceof Error ? error.message : 'Unknown error'}`
      setState(prev => ({
        ...prev,
        isLoading: false,
        error: errorMessage,
      }))
      // Show error toast
      toast.show(errorMessage, { 
        variant: 'error',
        duration: 5000,
      })
      throw error
    }
  }

  // Login with PKCE (manual OAuth flow)
  const loginWithPKCE = async () => {
    try {
      setState(prev => ({ ...prev, isLoading: true, error: null }))

      // Check PKCE support
      if (!PKCEHelper.isSupported()) {
        throw new Error('PKCE not supported in this environment')
      }

      const config = getMsalConfig()
      
      // Build OAuth authorize URL
      const authorizeUrl = await PKCEHelper.buildAuthorizeUrl({
        clientId: config.auth.clientId,
        authority: config.auth.authority,
        redirectUri: config.auth.redirectUri!,
        scopes: config.scopes,
      })

      if (Platform.OS === 'web') {
        // Web: redirect to authorize URL
        window.location.href = authorizeUrl
      } else {
        // React Native: open authorize URL and handle redirect
        const { Linking } = await import('react-native')
        await Linking.openURL(authorizeUrl)
      }

    } catch (error) {
      const errorMessage = `PKCE login failed: ${error instanceof Error ? error.message : 'Unknown error'}`
      setState(prev => ({
        ...prev,
        isLoading: false,
        error: errorMessage,
      }))
      // Show error toast
      toast.show(errorMessage, { 
        variant: 'error',
        duration: 5000,
      })
      throw error
    }
  }

  // Logout
  const logout = async () => {
    try {
      setState(prev => ({ ...prev, isLoading: true }))

      // Clear stored token
      if (state.user?.email) {
        try { await disconnectPushDevice(state.user.email) }
        catch (error) { console.warn('Pulizia push al logout non completata:', error) }
      }
      await tokenStorageService.removeToken()

      setState({
        isAuthenticated: false,
        isLoading: false,
        user: null,
        token: null,
        error: null,
      })

    } catch (error) {
      const errorMessage = `Logout failed: ${error instanceof Error ? error.message : 'Unknown error'}`
      setState(prev => ({
        ...prev,
        isLoading: false,
        error: errorMessage,
      }))
      // Show error toast
      toast.show(errorMessage, { 
        variant: 'error',
        duration: 5000,
      })
    }
  }

  // Clear error
  const clearError = () => {
    setState(prev => ({ ...prev, error: null }))
  }

  // Refresh authentication
  const refreshAuth = async () => {
    await checkExistingAuth()
  }

  // Update user information
  const updateUser = (updatedUser: AppIdentity) => {
    setState(prev => ({
      ...prev,
      user: updatedUser
    }))
  }

  const contextValue: AuthContextType = {
    ...state,
    login,
    loginWithPKCE,
    logout,
    clearError,
    refreshAuth,
    updateUser,
  }

  return (
    <AuthContext.Provider value={contextValue}>
      {children}
    </AuthContext.Provider>
  )
}
