import type { UserSettings } from '../types/settings'
import { apiRequest, apiUpload } from './httpClient'
import { getActualFantacalcioYear } from '../utils/timezoneHelpers'

const settingsListeners = new Set<(settings: UserSettings) => void>()
export const subscribeToUserSettings = (listener: (settings: UserSettings) => void) => {
  settingsListeners.add(listener); return () => settingsListeners.delete(listener)
}
export interface ChangeTeamNameResponse { messages: string[]; success: boolean }
export interface CopyLogoParams { groupId: string; year: number; owner: string; basketId: string; teamId?: string }
export interface UploadLogoParams extends CopyLogoParams { image: File | Blob }
export interface ChangeTeamNameParams { groupId: string; owner: string; newName: string; basketId: string; year?: number; teamId?: string }
const assetQuery = (params: CopyLogoParams) => {
  const query = new URLSearchParams({ groupId: params.groupId, basketId: params.basketId, year: String(params.year), owner: params.owner })
  if (params.teamId) query.set('teamId', params.teamId)
  return query
}

class SettingsService {
  getUserSettings(): Promise<UserSettings> { return apiRequest('/api/accounts/me/settings') }
  async updateUserSettings(settings: UserSettings) {
    await apiRequest('/api/accounts/me/settings', 'PUT', { autoOptimalFormation: settings.autoOptimalFormation, liveWidgetEnabled: settings.liveWidgetEnabled })
    settingsListeners.forEach(listener => listener(settings)); return true
  }
  async uploadLogo(params: UploadLogoParams) {
    const body = new FormData(); body.append('image', params.image, 'logo.png')
    await apiUpload(`/api/team-assets/logo?${assetQuery(params)}`, body); return true
  }
  async copyLogoFromPreviousYear(params: CopyLogoParams) {
    await apiRequest(`/api/team-assets/logo/copy-previous?${assetQuery(params)}`, 'POST'); return true
  }
  async *changeTeamNameStream(params: ChangeTeamNameParams): AsyncGenerator<string, void, unknown> {
    yield 'Salvataggio nome squadra...'
    await apiRequest('/api/team-assets/name', 'PATCH', { ...params, year: params.year ?? getActualFantacalcioYear() })
    yield 'Nome squadra aggiornato.'
  }
  async changeTeamName(params: ChangeTeamNameParams): Promise<ChangeTeamNameResponse> {
    const messages: string[] = []
    for await (const message of this.changeTeamNameStream(params)) messages.push(message)
    return { success: true, messages }
  }
  async updateUsername(username: string) { await apiRequest('/api/accounts/me/profile', 'PATCH', { username }); return true }
}
export const settingsService = new SettingsService()
