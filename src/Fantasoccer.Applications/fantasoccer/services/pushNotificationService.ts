import { RepositoryServices } from 'rystem.repository.client'
import { authService } from './authService'
import { tokenStorageService } from './tokenStorageService'
import {
  PushNotificationSettings,
  PushNotificationSettingsRaw,
  PushNotificationSummary,
  PushSubscription,
  EMPTY_PUSH_NOTIFICATION_SETTINGS,
  mapRawPushNotificationSettingsToSettings,
  mapPushNotificationSettingsToRaw,
  mergePushNotificationSettings,
  addSubscription,
  removeSubscription,
  buildPushNotificationSummary
} from 'types/pushNotification'

export interface UpdatePushNotificationSettingsInput {
  matchEvents?: boolean
  opponentMatchEvents?: boolean
  deploymentReminder?: boolean
  endDay?: boolean
  marketEvents?: boolean
  liveChangesReminder?: boolean
  liveFormationChangeEvents?: boolean
  subscriptions?: PushSubscription[]
}

export class PushNotificationService {
  // ============================================
  // INTERNAL HELPERS
  // ============================================

  private getRepository() {
    return RepositoryServices.Repository<PushNotificationSettingsRaw, string>('PushNotificationSettings')
  }

  private async getCurrentUserEmail(): Promise<string | null> {
    try {
      const accessToken = await tokenStorageService.getTokenString()
      if (!accessToken) {
        console.warn('PushNotificationService: missing access token')
        return null
      }

      const identity = await authService.getUserInfo(accessToken)
      if (!identity?.email) {
        console.warn('PushNotificationService: user email not available')
        return null
      }

      return identity.email
    } catch (error) {
      console.error('PushNotificationService: unable to resolve current user email', error)
      return null
    }
  }

  private async saveSettings(email: string, settings: PushNotificationSettings): Promise<boolean> {
    const repository = this.getRepository()
    const rawSettings = mapPushNotificationSettingsToRaw(settings)

    try {
      await repository.update(email, rawSettings)
      return true
    } catch (error) {
      console.warn('PushNotificationService: update failed, attempting insert', error)
      try {
        await repository.insert(email, rawSettings)
        return true
      } catch (insertError) {
        console.error('PushNotificationService: failed to persist settings', insertError)
        return false
      }
    }
  }

  private async fetchSettings(email: string): Promise<PushNotificationSettings> {
    try {
      const repository = this.getRepository()
      const raw = await repository.get(email)
      return mapRawPushNotificationSettingsToSettings(raw)
    } catch (error) {
      // Repository throws when the key is missing, fall back to defaults
      console.info('PushNotificationService: no settings found, returning defaults', error)
      return { ...EMPTY_PUSH_NOTIFICATION_SETTINGS }
    }
  }

  // ============================================
  // PUBLIC METHODS - SETTINGS RETRIEVAL
  // ============================================

  async getSettings(email: string): Promise<PushNotificationSettings> {
    return this.fetchSettings(email)
  }

  async getCurrentUserSettings(): Promise<PushNotificationSettings | null> {
    const email = await this.getCurrentUserEmail()
    if (!email) {
      return null
    }
    return this.fetchSettings(email)
  }

  async getSettingsSummary(email: string): Promise<PushNotificationSummary> {
    const settings = await this.fetchSettings(email)
    return buildPushNotificationSummary(settings)
  }

  async getCurrentUserSettingsSummary(): Promise<PushNotificationSummary | null> {
    const settings = await this.getCurrentUserSettings()
    if (!settings) {
      return null
    }
    return buildPushNotificationSummary(settings)
  }

  // ============================================
  // PUBLIC METHODS - MUTATIONS
  // ============================================

  async updateSettings(email: string, updates: UpdatePushNotificationSettingsInput): Promise<PushNotificationSettings> {
    const current = await this.fetchSettings(email)
    const merged = mergePushNotificationSettings(current, updates)

    const persisted = await this.saveSettings(email, merged)
    if (!persisted) {
      throw new Error('Failed to save push notification settings')
    }

    return merged
  }

  async updateCurrentUserSettings(updates: UpdatePushNotificationSettingsInput): Promise<PushNotificationSettings | null> {
    const email = await this.getCurrentUserEmail()
    if (!email) {
      return null
    }

    return this.updateSettings(email, updates)
  }

  async addDevice(email: string, subscription: PushSubscription): Promise<PushNotificationSettings> {
    const current = await this.fetchSettings(email)
    const updated = addSubscription(current, subscription)

    const persisted = await this.saveSettings(email, updated)
    if (!persisted) {
      throw new Error('Failed to save push subscription')
    }

    return updated
  }

  async addDeviceForCurrentUser(subscription: PushSubscription): Promise<PushNotificationSettings | null> {
    const email = await this.getCurrentUserEmail()
    if (!email) {
      return null
    }

    return this.addDevice(email, subscription)
  }

  async removeDevice(email: string, endpoint: string): Promise<PushNotificationSettings> {
    const current = await this.fetchSettings(email)
    const updated = removeSubscription(current, endpoint)

    const persisted = await this.saveSettings(email, updated)
    if (!persisted) {
      throw new Error('Failed to remove push subscription')
    }

    return updated
  }

  async removeDeviceForCurrentUser(endpoint: string): Promise<PushNotificationSettings | null> {
    const email = await this.getCurrentUserEmail()
    if (!email) {
      return null
    }

    return this.removeDevice(email, endpoint)
  }

  async disableAll(email: string): Promise<PushNotificationSettings> {
    const disabled: PushNotificationSettings = {
      matchEvents: false,
      opponentMatchEvents: false,
      deploymentReminder: false,
      endDay: false,
      marketEvents: false,
      liveChangesReminder: false,
      liveFormationChangeEvents: false,
      subscriptions: []
    }

    const persisted = await this.saveSettings(email, disabled)
    if (!persisted) {
      throw new Error('Failed to disable push notifications')
    }

    return disabled
  }

  async disableAllForCurrentUser(): Promise<PushNotificationSettings | null> {
    const email = await this.getCurrentUserEmail()
    if (!email) {
      return null
    }

    return this.disableAll(email)
  }
}

export const pushNotificationService = new PushNotificationService()
