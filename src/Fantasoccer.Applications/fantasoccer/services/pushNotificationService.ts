import { authService } from './authService'
import { tokenStorageService } from './tokenStorageService'
import { buildApiUrl, getCommonHeaders } from '../configurations/apiConfig'
import {
  PushNotificationSettings, PushNotificationSummary, PushSubscription, RegisteredNotificationDevice,
  assertPushSettings, buildPushNotificationSummary,
} from '../types/pushNotification'
import { readDeviceProof, saveDeviceProof } from './pushDeviceCapability'
import { ApiError } from '../configurations/apiConfig'

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

  private async request(email: string, path = '', method = 'GET', body?: unknown): Promise<unknown> {
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
    if (!response.ok) {
      const problem = await response.json().catch(() => null)
      throw new ApiError(problem?.code === 'device_reactivation_required'
        ? 'Il dispositivo appartiene a un’altra sessione. Disattiva e riattiva le push su questo browser.'
        : `Impossibile ${method === 'GET' ? 'caricare' : 'salvare'} le notifiche push (${response.status}). Riprova.`, response.status, problem)
    }
    return response.json()
  }

  private async settings(email: string, path = '', method = 'GET', body?: unknown): Promise<PushNotificationSettings> {
    const result = await this.request(email, path, method, body)
    assertPushSettings(result)
    return result
  }

  getSettings(email: string): Promise<PushNotificationSettings> { return this.settings(email) }
  async getCurrentUserSettings(): Promise<PushNotificationSettings> { return this.getSettings(await this.getCurrentUserEmail()) }
  async getSettingsSummary(email: string): Promise<PushNotificationSummary> { return buildPushNotificationSummary(await this.getSettings(email)) }
  async getCurrentUserSettingsSummary(): Promise<PushNotificationSummary> { return buildPushNotificationSummary(await this.getCurrentUserSettings()) }

  // Mutations are partial server-side commands. Never GET/merge/replace a whole
  // settings document, particularly after a failed or stale read.
  updateSettings(email: string, updates: UpdatePushNotificationSettingsInput): Promise<PushNotificationSettings> {
    return this.settings(email, '', 'PATCH', updates)
  }
  async updateCurrentUserSettings(updates: UpdatePushNotificationSettingsInput): Promise<PushNotificationSettings> {
    return this.updateSettings(await this.getCurrentUserEmail(), updates)
  }
  async addDevice(email: string, subscription: PushSubscription, previousEndpoint?: string): Promise<PushNotificationSettings> {
    const deviceProof = await readDeviceProof(subscription.endpoint)
    const result = await this.request(email, '/devices', 'POST', { subscription, previousEndpoint, deviceProof }) as RegisteredNotificationDevice
    assertPushSettings(result?.settings)
    if (typeof result.deviceProof !== 'string' || !/^[A-Za-z0-9_-]{43}$/.test(result.deviceProof)) throw new Error('Risposta non valida dal servizio notifiche push.')
    await saveDeviceProof(subscription.endpoint, result.deviceProof)
    return result.settings
  }
  async addDeviceForCurrentUser(subscription: PushSubscription, previousEndpoint?: string): Promise<PushNotificationSettings> {
    return this.addDevice(await this.getCurrentUserEmail(), subscription, previousEndpoint)
  }
  removeDevice(email: string, endpoint: string): Promise<PushNotificationSettings> {
    return this.settings(email, '/devices/remove', 'POST', { endpoint })
  }
  async removeDeviceForCurrentUser(endpoint: string): Promise<PushNotificationSettings> {
    return this.removeDevice(await this.getCurrentUserEmail(), endpoint)
  }
  disableAll(email: string): Promise<PushNotificationSettings> { return this.settings(email, '/disable', 'POST') }
  async disableAllForCurrentUser(): Promise<PushNotificationSettings> { return this.disableAll(await this.getCurrentUserEmail()) }
}

export const pushNotificationService = new PushNotificationService()
