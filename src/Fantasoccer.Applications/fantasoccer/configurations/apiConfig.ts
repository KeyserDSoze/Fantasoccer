// Central API configuration for Fantasoccer
export interface ApiConfig {
  baseUrl: string
}

// Configuration for different environments. EXPO_PUBLIC_API_URL (set by the deploy pipeline) overrides the host.
export const API_CONFIGS = {
  development: { baseUrl: 'https://localhost:7223' },
  production: { baseUrl: 'https://apifcv2.azurewebsites.net' },
} as const

const configuredApiUrl = process.env.EXPO_PUBLIC_API_URL?.replace(/\/$/, '')

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
  return {
    ...API_CONFIGS[env],
    baseUrl: configuredApiUrl || API_CONFIGS[env].baseUrl,
  }
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
