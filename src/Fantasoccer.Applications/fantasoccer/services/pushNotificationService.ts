import { authService } from './authService'
import { tokenStorageService } from './tokenStorageService'
import { buildApiUrl, getCommonHeaders } from '../configurations/apiConfig'
import {
  PushNotificationSettings, PushNotificationSettingsRaw, PushNotificationSummary, PushSubscription,
  mapRawPushNotificationSettingsToSettings, buildPushNotificationSummary,
} from '../types/pushNotification'

export interface UpdatePushNotificationSettingsInput {
  matchEvents?: boolean
  opponentMatchEvents?: boolean
  deploymentReminder?: boolean
  endDay?: boolean
  marketEvents?: boolean
  liveChangesReminder?: boolean
  liveFormationChangeEvents?: boolean
}

export class PushNotificationService {
  private async getCurrentUserEmail(): Promise<string> {
    const token = await tokenStorageService.ensureValidToken()
    if (!token) throw new Error('Accedi per gestire le notifiche push.')
    const identity = await authService.getUserInfo(token)
    if (!identity?.email) throw new Error('Impossibile identificare l’utente delle notifiche push.')
    return identity.email
  }

  private async request(email: string, path = '', method = 'GET', body?: unknown): Promise<PushNotificationSettings> {
    const token = await tokenStorageService.ensureValidToken()
    if (!token) throw new Error('Accedi per gestire le notifiche push.')
    const url = buildApiUrl(`/api/push-notifications${path}`, { email })
    const send = (accessToken: string) => fetch(url, {
      method,
      headers: { ...getCommonHeaders(true, accessToken), 'Content-Type': 'application/json' },
      ...(body === undefined ? {} : { body: JSON.stringify(body) }),
    })
    let response = await send(token)
    if (response.status === 401) {
      const refreshed = await tokenStorageService.forceRefreshToken()
      if (refreshed) response = await send(refreshed)
    }
    if (!response.ok) throw new Error(`Impossibile ${method === 'GET' ? 'caricare' : 'salvare'} le notifiche push (${response.status}). Riprova.`)
    const raw = await response.json() as PushNotificationSettingsRaw | null
    if (!raw || typeof (raw.matchEvents ?? raw.MatchEvents) !== 'boolean' || !Array.isArray(raw.subscriptions ?? raw.Subscriptions)) {
      throw new Error('Risposta non valida dal servizio notifiche push.')
    }
    return mapRawPushNotificationSettingsToSettings(raw)
  }

  getSettings(email: string): Promise<PushNotificationSettings> { return this.request(email) }
  async getCurrentUserSettings(): Promise<PushNotificationSettings> { return this.getSettings(await this.getCurrentUserEmail()) }
  async getSettingsSummary(email: string): Promise<PushNotificationSummary> { return buildPushNotificationSummary(await this.getSettings(email)) }
  async getCurrentUserSettingsSummary(): Promise<PushNotificationSummary> { return buildPushNotificationSummary(await this.getCurrentUserSettings()) }

  // Mutations are partial server-side commands. Never GET/merge/replace a whole
  // settings document, particularly after a failed or stale read.
  updateSettings(email: string, updates: UpdatePushNotificationSettingsInput): Promise<PushNotificationSettings> {
    return this.request(email, '', 'PATCH', updates)
  }
  async updateCurrentUserSettings(updates: UpdatePushNotificationSettingsInput): Promise<PushNotificationSettings> {
    return this.updateSettings(await this.getCurrentUserEmail(), updates)
  }
  addDevice(email: string, subscription: PushSubscription, previousEndpoint?: string): Promise<PushNotificationSettings> {
    return this.request(email, '/devices', 'POST', { subscription, previousEndpoint })
  }
  async addDeviceForCurrentUser(subscription: PushSubscription, previousEndpoint?: string): Promise<PushNotificationSettings> {
    return this.addDevice(await this.getCurrentUserEmail(), subscription, previousEndpoint)
  }
  removeDevice(email: string, endpoint: string): Promise<PushNotificationSettings> {
    return this.request(email, '/devices/remove', 'POST', { endpoint })
  }
  async removeDeviceForCurrentUser(endpoint: string): Promise<PushNotificationSettings> {
    return this.removeDevice(await this.getCurrentUserEmail(), endpoint)
  }
  disableAll(email: string): Promise<PushNotificationSettings> { return this.request(email, '/disable', 'POST') }
  async disableAllForCurrentUser(): Promise<PushNotificationSettings> { return this.disableAll(await this.getCurrentUserEmail()) }
}

export const pushNotificationService = new PushNotificationService()
