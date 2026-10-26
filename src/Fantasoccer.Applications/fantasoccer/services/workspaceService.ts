import { buildApiUrl } from '../configurations/apiConfig'
import { tokenStorageService } from './tokenStorageService'
import { createWorkspaceApi } from './workspaceApiClient'

export const workspaceService = createWorkspaceApi({
  baseUrl: buildApiUrl('/api/workspace'),
  token: () => tokenStorageService.ensureValidToken(),
  refresh: () => tokenStorageService.forceRefreshToken(),
  fetch: (...args) => fetch(...args),
})
