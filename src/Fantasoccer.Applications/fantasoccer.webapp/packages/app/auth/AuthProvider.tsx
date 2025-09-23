import React, { createContext, useContext, useEffect, useState, ReactNode } from 'react'
import { Platform } from 'react-native'
import { authService, TokenExchangeResponse, UserInfoResponse } from '../services/authService'
import { tokenStorageService } from '../services/tokenStorageService'
import { PKCEHelper } from '../utils/pkceHelper'
import { idTokenDecoder } from '../utils/idTokenDecoder'

// Helper function to convert backend user data to AuthUser
const mapUserInfoToAuthUser = (userInfo: UserInfoResponse): AuthUser => {
  return {
    username: userInfo.u,
    email: userInfo.e,
    name: userInfo.u, // Use username as name
    displayName: userInfo.u, // Use username as display name
    state: {
      groupId: userInfo.s.g,
      leagueId: userInfo.s.l,
      year: userInfo.s.y,
      workingEmail: userInfo.s.m,
    },
    groups: userInfo.g.map(group => ({
      id: group.i,
      name: group.n,
      role: group.r,
    })),
    rawData: userInfo, // Keep original data for compatibility
  }
}

// Types
export interface AuthUser {
  username: string       // u -> username
  email: string          // e -> email
  name: string           // derived from username or email
  displayName: string    // derived from username
  state: {               // s -> state
    groupId: string      // g -> current group ID
    leagueId: string     // l -> current league ID
    year: number         // y -> year
    workingEmail: string // m -> working email
  }
  groups: Array<{        // g -> groups
    id: string           // i -> group ID
    name: string         // n -> group name
    role: number         // r -> role flags
  }>
  // Raw backend data for compatibility
  rawData: any
}

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
    clientId: "95de3061-5eab-437d-a2cc-6d71cfaef4d1",
    authority: 'https://login.microsoftonline.com/consumers',
    redirectUri: Platform.select({
      web: typeof window !== 'undefined' ? window.location.origin + '/' : 'http://localhost:19006',
      default: 'msauth://com.fantasoccer.app/auth'
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
      console.log('🔄 Initializing MSAL for platform:', Platform.OS)
      
      if (Platform.OS === 'web') {
        // Web MSAL initialization
        const { PublicClientApplication } = await import('@azure/msal-browser')
        const instance = new PublicClientApplication(getMsalConfig())
        await instance.initialize()
        setMsalInstance(instance)
        console.log('✅ Web MSAL initialized')
      } else {
        // React Native - MSAL may not be available, fallback to PKCE only
        console.log('📱 React Native detected - using PKCE flow')
        setMsalInstance(null) // Will use PKCE flow instead
      }
      
      setIsInitialized(true)
    } catch (error) {
      console.error('❌ MSAL initialization failed:', error)
      setState(prev => ({ 
        ...prev, 
        error: `MSAL initialization failed: ${error instanceof Error ? error.message : 'Unknown error'}`,
        isLoading: false 
      }))
    }
  }

  // Check existing authentication
  const checkExistingAuth = async () => {
    try {
      console.log('🔍 Checking existing authentication...')
      
      // Check stored token
      const storedData = await tokenStorageService.loadToken()
      if (storedData) {
        console.log('🎫 Found stored token, fetching user info...')
        
        try {
          // Verify token with backend (includes automatic refresh if needed)
          const userInfo = await authService.getUserInfo(storedData.accessToken, storedData.refreshToken)
          
          setState(prev => ({
            ...prev,
            isAuthenticated: true,
            user: mapUserInfoToAuthUser(userInfo),
            token: storedData.accessToken,
            isLoading: false,
            error: null,
          }))
          
          console.log('✅ Authentication restored from storage')
          return
        } catch (error) {
          // If refresh failed or unauthorized, redirect to login
          if (error instanceof Error && (error.message === 'REFRESH_FAILED' || error.message === 'UNAUTHORIZED')) {
            console.log('🔄 Token refresh failed, user needs to re-login')
            await tokenStorageService.removeToken()
            // Don't set error state, just continue to show login screen
          } else {
            throw error // Re-throw other errors
          }
        }
      }
      
      console.log('ℹ️ No stored token found')
      setState(prev => ({ ...prev, isLoading: false }))
      
    } catch (error) {
      console.error('❌ Auth check failed:', error)
      // Clear invalid token
      await tokenStorageService.removeToken()
      setState(prev => ({ 
        ...prev, 
        isLoading: false,
        error: null // Don't show error for expired/invalid tokens
      }))
    }
  }

  // Initialize on mount
  useEffect(() => {
    const initialize = async () => {
      await initializeMsal()
      
      // Check for OAuth callback with authorization code
      if (Platform.OS === 'web' && typeof window !== 'undefined') {
        await handleOAuthCallback()
      }
      
      if (isInitialized) {
        await checkExistingAuth()
      }
    }
    
    initialize()
  }, [isInitialized])

  // Handle OAuth callback with authorization code
  const handleOAuthCallback = async () => {
    try {
      const urlParams = new URLSearchParams(window.location.search)
      const authCode = urlParams.get('code')
      const error = urlParams.get('error')
      const state = urlParams.get('state')

      if (error) {
        console.error('❌ OAuth callback error:', error)
        setState(prev => ({ 
          ...prev, 
          error: `OAuth error: ${error}`,
          isLoading: false 
        }))
        return
      }

      if (authCode) {
        console.log('✅ Authorization code received:', authCode.substring(0, 20) + '...')
        console.log('🔗 Full authorization code:', authCode)
        
        // Clear URL parameters
        window.history.replaceState({}, document.title, window.location.pathname)
        
        setState(prev => ({ ...prev, isLoading: true, error: null }))
        
        try {
          // Exchange authorization code for backend token using PKCE
          console.log('🔄 Exchanging authorization code with PKCE...')
          const tokenResponse = await authService.exchangeToken(authCode, { isPKCE: true })
          
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
            user: mapUserInfoToAuthUser(userInfo),
            token: tokenResponse.accessToken, // Store accessToken in state
            isLoading: false,
            error: null,
          }))

          console.log('✅ OAuth callback login completed successfully!')

        } catch (exchangeError) {
          console.error('❌ Token exchange failed:', exchangeError)
          setState(prev => ({
            ...prev,
            isLoading: false,
            error: `Token exchange failed: ${exchangeError instanceof Error ? exchangeError.message : 'Unknown error'}`,
          }))
        }
      }
    } catch (error) {
      console.error('❌ OAuth callback handling failed:', error)
      setState(prev => ({ 
        ...prev, 
        error: `Callback handling failed: ${error instanceof Error ? error.message : 'Unknown error'}`,
        isLoading: false 
      }))
    }
  }

  // Login with OAuth authorization code flow (not MSAL tokens)
  const login = async () => {
    try {
      setState(prev => ({ ...prev, isLoading: true, error: null }))
      
      console.log('🔄 Starting OAuth authorization code flow...')
      
      // Always use PKCE flow to get authorization code (not tokens)
      return await loginWithPKCE()

    } catch (error) {
      console.error('❌ Login failed:', error)
      setState(prev => ({
        ...prev,
        isLoading: false,
        error: `Login failed: ${error instanceof Error ? error.message : 'Unknown error'}`,
      }))
      throw error
    }
  }

  // Login with PKCE (manual OAuth flow)
  const loginWithPKCE = async () => {
    try {
      setState(prev => ({ ...prev, isLoading: true, error: null }))
      console.log('🔄 Starting PKCE login...')

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
      console.error('❌ PKCE login failed:', error)
      setState(prev => ({
        ...prev,
        isLoading: false,
        error: `PKCE login failed: ${error instanceof Error ? error.message : 'Unknown error'}`,
      }))
      throw error
    }
  }

  // Logout
  const logout = async () => {
    try {
      setState(prev => ({ ...prev, isLoading: true }))
      console.log('🔄 Logging out...')

      // Clear stored token
      await tokenStorageService.removeToken()

      setState({
        isAuthenticated: false,
        isLoading: false,
        user: null,
        token: null,
        error: null,
      })

      console.log('✅ Logout completed')

    } catch (error) {
      console.error('❌ Logout failed:', error)
      setState(prev => ({
        ...prev,
        isLoading: false,
        error: `Logout failed: ${error instanceof Error ? error.message : 'Unknown error'}`,
      }))
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

  const contextValue: AuthContextType = {
    ...state,
    login,
    loginWithPKCE,
    logout,
    clearError,
    refreshAuth,
  }

  return (
    <AuthContext.Provider value={contextValue}>
      {children}
    </AuthContext.Provider>
  )
}