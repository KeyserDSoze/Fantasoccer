export interface User {
  id: string
  email: string
  name: string
  displayName?: string
  // Add more user properties based on backend response
  [key: string]: any
}

export interface AuthState {
  isAuthenticated: boolean
  user: User | null
  msalToken: string | null      // MSAL access token
  backendToken: string | null   // Backend JWT token
  isLoading: boolean
  error: string | null
}

export interface AuthContextType extends AuthState {
  login: () => Promise<void>
  logout: () => Promise<void>
  acquireTokenSilent: () => Promise<string | null>
  refreshUserInfo: () => Promise<void>
}

export interface UserContext {
  userId: string
  leagueId?: string
  groupId?: string
  year: number
}

export interface AuthResponse {
  jwtToken: string
  userContext: UserContext
  user: User
}