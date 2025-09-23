// Central API configuration for Fantasoccer
export interface ApiEndpoints {
  // Authentication
  tokenExchange: string
  userInfo: string
}

export interface ApiConfig {
  baseUrl: string
  endpoints: ApiEndpoints
}

// Configuration for different environments
export const API_CONFIGS = {
  development: {
    baseUrl: 'https://localhost:7223',
    // baseUrl: 'https://apifc.azurewebsites.net',
    endpoints: {
      // Authentication endpoints
      tokenExchange: '/api/Authentication/Social/Token',
      userInfo: '/api/Authentication/Social/User',
    },
  },
  production: {
    baseUrl: 'https://apifc.azurewebsites.net',
    endpoints: {
      // Authentication endpoints
      tokenExchange: '/api/Authentication/Social/Token',
      userInfo: '/api/Authentication/Social/User'
    },
  },
} as const

// Environment detection
export const getEnvironment = (): 'development' | 'production' => {
  if (typeof window !== 'undefined') {
    // Web environment
    return window.location.hostname === 'localhost' ? 'development' : 'production'
  }
  // React Native environment
  return __DEV__ ? 'development' : 'production'
}

// Get current API configuration
export const getApiConfig = (): ApiConfig => {
  const env = getEnvironment()
  return API_CONFIGS[env]
}

// Helper to build API URLs
export const buildApiUrl = (endpoint: string, params?: Record<string, string | number>): string => {
  const config = getApiConfig()
  let url = `${config.baseUrl}${endpoint}`
  
  if (params) {
    const searchParams = new URLSearchParams()
    Object.entries(params).forEach(([key, value]) => {
      searchParams.append(key, String(value))
    })
    url += `?${searchParams.toString()}`
  }
  
  return url
}

// Common request headers
export const getCommonHeaders = (includeAuth: boolean = false, token?: string) => {
  const headers: Record<string, string> = {
    'Accept': 'application/json',
  }
  
  if (includeAuth && token) {
    headers['Authorization'] = `Bearer ${token}`
  }
  
  return headers
}

// API response wrapper type
export interface ApiResponse<T> {
  data: T
  success: boolean
  message?: string
  errors?: string[]
}

// Common API error handling
export class ApiError extends Error {
  constructor(
    message: string,
    public status?: number,
    public response?: any
  ) {
    super(message)
    this.name = 'ApiError'
  }
}

// Environment info for debugging
export const getApiInfo = () => {
  const config = getApiConfig()
  return {
    environment: getEnvironment(),
    baseUrl: config.baseUrl,
    endpoints: Object.keys(config.endpoints),
  }
}