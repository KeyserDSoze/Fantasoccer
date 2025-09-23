// Export all services for easy import
export { authService, TokenExchangeResponse } from './authService'
export { serieAService } from './serieAService'
export { tokenStorageService } from './tokenStorageService'
export type { StoredTokenData } from './tokenStorageService'

// Export types from centralized types folder
export * from '../types'

// Repository Framework exports
export { 
  setupRepositoryServices
} from '../providers/repositoryConfig'