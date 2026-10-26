import { Platform } from 'react-native'
import { storage } from '../utils/storage'
import { PushSubscription } from '../types/pushNotification'
import { pushNotificationService } from './pushNotificationService'
import { webPushSupport } from './webPushClient'

const BINDING_KEY = 'fantasoccer_push_device'
const CACHE_NAME = 'fantasoccer-push-state-v1'
const RENEWAL_PATH = '/__fantasoccer_push_renewal__'
const DEVICE_PATH = '/__fantasoccer_push_device__'
interface DeviceBinding { email: string; endpoint: string; enabled: boolean }
interface PendingRenewal { oldEndpoint: string; subscription?: PushSubscription; keyRotationRequired?: boolean }
let deviceRevision = 0
let bindingWrites: Promise<void> = Promise.resolve()

const sameUser = (a: string, b: string) => a.toLowerCase() === b.toLowerCase()
const readBinding = async (): Promise<DeviceBinding | null> => {
  const value = await storage.getItem(BINDING_KEY)
  return value ? JSON.parse(value) : null
}
const cacheState = async () => Platform.OS === 'web' && typeof caches !== 'undefined' ? caches.open(CACHE_NAME) : null

export async function acknowledgePushActivation(endpoint: string): Promise<void> {
  const cache = await cacheState()
  const response = await cache?.match(RENEWAL_PATH)
  const pending = response ? await response.json() as PendingRenewal : null
  if (pending && (pending.keyRotationRequired || pending.subscription?.endpoint === endpoint)) await cache?.delete(RENEWAL_PATH)
}

export async function rememberPushDevice(email: string, endpoint: string, enabled: boolean): Promise<void> {
  deviceRevision++
  const binding = { email, endpoint, enabled }
  // Worker state also prevents personal notifications after offline logout or
  // for the previous account while a provider delivery is already in flight.
  const write = bindingWrites.catch(() => undefined).then(async () => {
    const cache = await cacheState()
    if (cache) await cache.put(DEVICE_PATH, new Response(JSON.stringify(binding), { headers: { 'Content-Type': 'application/json' } }))
    await storage.setItem(BINDING_KEY, JSON.stringify(binding))
  })
  bindingWrites = write
  await write
}

export async function disconnectPushDevice(email: string): Promise<void> {
  if (Platform.OS !== 'web') return
  const binding = await readBinding()
  await rememberPushDevice(email, binding?.endpoint ?? '', false)
  let endpoint = binding?.endpoint
  try {
    const subscription = await webPushSupport.unsubscribe()
    endpoint = subscription?.endpoint ?? endpoint
  } finally {
    if (endpoint) await pushNotificationService.removeDevice(email, endpoint)
  }
}

// Authenticated reconciliation, also retried on reconnect/focus. It never
// re-enables a device removed remotely, or opts a different account in silently.
export async function reconcilePushDevice(email: string, isCurrent: () => boolean = () => true): Promise<void> {
  const revision = deviceRevision
  const stillCurrent = () => isCurrent() && revision === deviceRevision
  if (Platform.OS !== 'web' || !webPushSupport.isSupported()) return
  const support = await webPushSupport.getSupportState()
  if (!support.supported || support.permission !== 'granted') return
  const binding = await readBinding()
  if (!stillCurrent()) return
  if (binding && !sameUser(binding.email, email)) {
    await rememberPushDevice(binding.email, binding.endpoint, false)
    await webPushSupport.unsubscribe()
    if (isCurrent()) await rememberPushDevice(email, '', false)
    return
  }
  if (binding?.enabled === false) return
  const cache = await cacheState()
  const pendingResponse = await cache?.match(RENEWAL_PATH)
  const pending = pendingResponse ? await pendingResponse.json() as PendingRenewal : null
  if (pending?.keyRotationRequired) return
  const settings = await pushNotificationService.getSettings(email)
  webPushSupport.configure(settings.publicKey)
  if (!stillCurrent()) return
  let existing = await webPushSupport.getExistingSubscription()
  const knownEndpoint = pending?.oldEndpoint || binding?.endpoint || existing?.endpoint
  if (!knownEndpoint || !settings.subscriptions.some(s => s.endpoint === knownEndpoint || s.endpoint === existing?.endpoint)) {
    if (binding) await rememberPushDevice(email, binding.endpoint, false)
    return
  }
  // Renewal may use the same configured key, but key rotation requires a user
  // gesture. Never silently re-enable a removed device or migrate accounts.
  if (!existing && (pending || binding?.enabled)) existing = await webPushSupport.subscribe(settings.publicKey)
  if (!existing || !stillCurrent()) return
  const saved = settings.subscriptions.find(s => s.endpoint === existing!.endpoint)
  if (!saved || saved.p256dh !== existing.p256dh || saved.auth !== existing.auth || pending) {
    await pushNotificationService.addDevice(email, existing, pending?.oldEndpoint ?? binding?.endpoint)
  }
  if (!stillCurrent()) {
    // A local disable/logout may have overtaken this in-flight renewal.
    const latestBinding = await readBinding()
    if (latestBinding && sameUser(latestBinding.email, email) && !latestBinding.enabled) {
      await pushNotificationService.removeDevice(email, existing.endpoint)
    }
    return
  }
  await rememberPushDevice(email, existing.endpoint, true)
  // Do not acknowledge a newer renewal that arrived during this request.
  const latest = await cache?.match(RENEWAL_PATH)
  if (latest && pending && JSON.stringify(await latest.json()) === JSON.stringify(pending)) await cache?.delete(RENEWAL_PATH)
}
