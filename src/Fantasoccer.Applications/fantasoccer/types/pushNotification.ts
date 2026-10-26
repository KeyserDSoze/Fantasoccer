export interface PushSubscription { endpoint: string; p256dh: string; auth: string; version?: number; isValid?: boolean }
export interface PushNotificationSettings {
  matchEvents: boolean; opponentMatchEvents: boolean; deploymentReminder: boolean; endDay: boolean; marketEvents: boolean
  liveChangesReminder: boolean; liveFormationChangeEvents: boolean; subscriptions: PushSubscription[]
  version: number; publicKey: string | null; deliveryEnabled: boolean
}
export interface RegisteredNotificationDevice { settings: PushNotificationSettings; deviceProof: string }
export interface PushNotificationSummary { totalDevices: number; hasMatchNotifications: boolean; hasMarketNotifications: boolean; hasAnyEnabled: boolean }
export const EMPTY_PUSH_NOTIFICATION_SETTINGS: PushNotificationSettings = { matchEvents: true, opponentMatchEvents: false,
  deploymentReminder: false, endDay: false, marketEvents: false, liveChangesReminder: false, liveFormationChangeEvents: false,
  subscriptions: [], version: 0, publicKey: null, deliveryEnabled: false }
// Validate the readable contract; never turn a failed/malformed load into defaults.
export function assertPushSettings(value: unknown): asserts value is PushNotificationSettings {
  const settings = value as PushNotificationSettings | null
  if (!settings || !['matchEvents', 'opponentMatchEvents', 'deploymentReminder', 'endDay', 'marketEvents', 'liveChangesReminder', 'liveFormationChangeEvents', 'deliveryEnabled']
    .every(key => typeof settings[key as keyof PushNotificationSettings] === 'boolean') || !Number.isInteger(settings.version) ||
    !(settings.publicKey === null || typeof settings.publicKey === 'string') || !Array.isArray(settings.subscriptions) ||
    settings.subscriptions.some(device => !device || !['endpoint', 'p256dh', 'auth'].every(key => typeof device[key as keyof PushSubscription] === 'string')))
    throw new Error('Risposta non valida dal servizio notifiche push.')
}
export const buildPushNotificationSummary = (settings: PushNotificationSettings | null | undefined): PushNotificationSummary => ({
  totalDevices: settings?.subscriptions.length ?? 0,
  hasMatchNotifications: !!settings && (settings.matchEvents || settings.opponentMatchEvents || settings.liveFormationChangeEvents),
  hasMarketNotifications: settings?.marketEvents ?? false,
  hasAnyEnabled: !!settings && (settings.matchEvents || settings.opponentMatchEvents || settings.deploymentReminder || settings.endDay || settings.marketEvents || settings.liveChangesReminder || settings.liveFormationChangeEvents),
})
