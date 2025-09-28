// Push notification types based on Fantasoccer.Domain.Business.PushNotification.PushNotificationSettings

// =============================================================
// RAW INTERFACES (matching backend JSON payloads)
// =============================================================

export interface PushSubscriptionRaw {
  endpoint?: string
  Endpoint?: string
  p256dh?: string
  P256DH?: string
  auth?: string
  Auth?: string
}

export interface PushNotificationSettingsRaw {
  matchEvents?: boolean
  MatchEvents?: boolean
  opponentMatchEvents?: boolean
  OpponentMatchEvents?: boolean
  deploymentReminder?: boolean
  DeploymentReminder?: boolean
  endDay?: boolean
  EndDay?: boolean
  marketEvents?: boolean
  MarketEvents?: boolean
  subscriptions?: PushSubscriptionRaw[]
  Subscriptions?: PushSubscriptionRaw[]
}

// =============================================================
// CLEAN INTERFACES (friendly property names)
// =============================================================

export interface PushSubscription {
  endpoint: string
  p256dh: string
  auth: string
}

export interface PushNotificationSettings {
  matchEvents: boolean
  opponentMatchEvents: boolean
  deploymentReminder: boolean
  endDay: boolean
  marketEvents: boolean
  subscriptions: PushSubscription[]
}

export interface PushNotificationSummary {
  totalDevices: number
  hasMatchNotifications: boolean
  hasMarketNotifications: boolean
  hasAnyEnabled: boolean
}

// =============================================================
// DEFAULTS
// =============================================================

export const EMPTY_PUSH_NOTIFICATION_SETTINGS: PushNotificationSettings = {
  matchEvents: false,
  opponentMatchEvents: false,
  deploymentReminder: false,
  endDay: false,
  marketEvents: false,
  subscriptions: []
}

// =============================================================
// MAPPING UTILITIES
// =============================================================

const sanitizeString = (value?: string | null): string => value?.trim() ?? ''

const mapRawPushSubscriptionToSubscription = (raw: PushSubscriptionRaw): PushSubscription => ({
  endpoint: sanitizeString(raw.endpoint ?? raw.Endpoint),
  p256dh: sanitizeString(raw.p256dh ?? raw.P256DH),
  auth: sanitizeString(raw.auth ?? raw.Auth)
})

export const mapRawPushNotificationSettingsToSettings = (
  raw?: PushNotificationSettingsRaw | null
): PushNotificationSettings => {
  if (!raw) {
    return { ...EMPTY_PUSH_NOTIFICATION_SETTINGS }
  }

  const subscriptionsRaw = raw.subscriptions ?? raw.Subscriptions ?? []

  return {
    matchEvents: !!(raw.matchEvents ?? raw.MatchEvents),
    opponentMatchEvents: !!(raw.opponentMatchEvents ?? raw.OpponentMatchEvents),
    deploymentReminder: !!(raw.deploymentReminder ?? raw.DeploymentReminder),
    endDay: !!(raw.endDay ?? raw.EndDay),
    marketEvents: !!(raw.marketEvents ?? raw.MarketEvents),
    subscriptions: subscriptionsRaw.map(mapRawPushSubscriptionToSubscription).filter(x => x.endpoint.length > 0)
  }
}

export const mapSubscriptionToRaw = (subscription: PushSubscription): PushSubscriptionRaw => ({
  endpoint: subscription.endpoint,
  p256dh: subscription.p256dh,
  auth: subscription.auth
})

export const mapPushNotificationSettingsToRaw = (
  settings: PushNotificationSettings
): PushNotificationSettingsRaw => ({
  matchEvents: settings.matchEvents,
  opponentMatchEvents: settings.opponentMatchEvents,
  deploymentReminder: settings.deploymentReminder,
  endDay: settings.endDay,
  marketEvents: settings.marketEvents,
  subscriptions: settings.subscriptions.map(mapSubscriptionToRaw)
})

// =============================================================
// HELPERS
// =============================================================

export const buildPushNotificationSummary = (
  settings: PushNotificationSettings | null | undefined
): PushNotificationSummary => {
  if (!settings) {
    return {
      totalDevices: 0,
      hasMatchNotifications: false,
      hasMarketNotifications: false,
      hasAnyEnabled: false
    }
  }

  const totalDevices = settings.subscriptions.length
  const hasMatchNotifications = settings.matchEvents || settings.opponentMatchEvents
  const hasMarketNotifications = settings.marketEvents
  const hasAnyEnabled =
    settings.matchEvents ||
    settings.opponentMatchEvents ||
    settings.deploymentReminder ||
    settings.endDay ||
    settings.marketEvents

  return {
    totalDevices,
    hasMatchNotifications,
    hasMarketNotifications,
    hasAnyEnabled
  }
}

export const mergePushNotificationSettings = (
  base: PushNotificationSettings,
  updates: Partial<PushNotificationSettings>
): PushNotificationSettings => ({
  ...base,
  ...updates,
  subscriptions: updates.subscriptions ?? base.subscriptions
})

export const addSubscription = (
  settings: PushNotificationSettings,
  subscription: PushSubscription
): PushNotificationSettings => {
  const exists = settings.subscriptions.some(x => x.endpoint === subscription.endpoint)
  return exists
    ? settings
    : {
        ...settings,
        subscriptions: [...settings.subscriptions, subscription]
      }
}

export const removeSubscription = (
  settings: PushNotificationSettings,
  endpoint: string
): PushNotificationSettings => ({
  ...settings,
  subscriptions: settings.subscriptions.filter(x => x.endpoint !== endpoint)
})
