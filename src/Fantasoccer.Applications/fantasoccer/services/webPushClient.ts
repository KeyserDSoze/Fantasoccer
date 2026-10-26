import { Platform } from 'react-native'
import { PushSubscription } from 'types/pushNotification'

const SERVICE_WORKER_PATH = '/FCServiceWorker.js'
let applicationServerPublicKey: string | null = null

const base64ToUint8Array = (base64String: string): Uint8Array => {
  const padding = '='.repeat((4 - (base64String.length % 4)) % 4)
  const base64 = (base64String + padding).replace(/-/g, '+').replace(/_/g, '/')
  let rawData: string
  const globalAny: any = typeof globalThis !== 'undefined' ? (globalThis as any) : {}
  if (typeof globalAny.atob === 'function') {
    rawData = globalAny.atob(base64)
  } else if (globalAny.Buffer?.from) {
    rawData = globalAny.Buffer.from(base64, 'base64').toString('binary')
  } else {
    throw new Error('webPushClient: base64 decoding non supportato nell\'ambiente corrente')
  }

  return Uint8Array.from({ length: rawData.length }, (_, index) => rawData.charCodeAt(index))
}

const mapBrowserSubscriptionToDomain = (subscription: globalThis.PushSubscription): PushSubscription => {
  const json = subscription.toJSON()
  return {
    endpoint: json.endpoint ?? '',
    p256dh: json.keys?.p256dh ?? '',
    auth: json.keys?.auth ?? ''
  }
}

const isWebPushSupportedInternally = (): boolean => {
  return (
    Platform.OS === 'web' &&
    typeof window !== 'undefined' &&
    window.isSecureContext !== false &&
    'serviceWorker' in navigator &&
    'PushManager' in window &&
    'Notification' in window
  )
}

export type PushSupportState =
  | { supported: true; permission: NotificationPermission }
  | { supported: false; reason: string }

let registrationPromise: Promise<ServiceWorkerRegistration | null> | null = null
let updateInterval: ReturnType<typeof setInterval> | null = null
const subscriptionChangeListeners = new Set<(subscription: PushSubscription) => void>()
let messageListenerRegistered = false

export const subscribeToPushSubscriptionChanges = (
  listener: (subscription: PushSubscription) => void
) => {
  subscriptionChangeListeners.add(listener)
  if (!messageListenerRegistered && typeof navigator !== 'undefined' && 'serviceWorker' in navigator) {
    messageListenerRegistered = true
    navigator.serviceWorker.addEventListener('message', (event) => {
      if (event.data?.type !== 'PUSH_SUBSCRIPTION_CHANGED' || !event.data.subscription) return
      const subscription = event.data.subscription
      const mapped: PushSubscription = {
        endpoint: subscription.endpoint ?? '',
        p256dh: subscription.keys?.p256dh ?? '',
        auth: subscription.keys?.auth ?? '',
      }
      if (mapped.endpoint) subscriptionChangeListeners.forEach((callback) => callback(mapped))
    })
  }
  return () => subscriptionChangeListeners.delete(listener)
}

const postVapidKeyToWorker = (worker?: ServiceWorker | null) => {
  if (!worker) {
    return
  }
  try {
    worker.postMessage({ type: 'SET_VAPID_KEY', value: applicationServerPublicKey })
  } catch (error) {
    console.error('webPushClient: unable to post VAPID key to worker', error)
  }
}

const pushVapidKeyToRegistration = (registration: ServiceWorkerRegistration) => {
  postVapidKeyToWorker(registration.active)
  postVapidKeyToWorker(registration.waiting)

  const installing = registration.installing
  if (installing) {
    const listener = () => {
      if (installing.state === 'activated') {
        postVapidKeyToWorker(registration.active)
        installing.removeEventListener('statechange', listener)
      }
    }
    installing.addEventListener('statechange', listener)
  }
}

const ensureServiceWorkerRegistration = async (): Promise<ServiceWorkerRegistration | null> => {
  if (!isWebPushSupportedInternally()) {
    return null
  }

  if (!registrationPromise) {
    registrationPromise = navigator.serviceWorker
      .register(SERVICE_WORKER_PATH)
      .then(async (registration) => {
        if (!registration.active) {
          let timeout: ReturnType<typeof setTimeout> | undefined
          try {
            await Promise.race([
              navigator.serviceWorker.ready,
              new Promise<never>((_, reject) => { timeout = setTimeout(() => reject(new Error('Attivazione service worker scaduta. Riprova.')), 15000) }),
            ])
          } finally { if (timeout) clearTimeout(timeout) }
        }
        // Auto-update: check for updates every 60 seconds
        if (updateInterval) clearInterval(updateInterval)
        updateInterval = setInterval(() => {
          void registration.update().catch(error => console.warn('webPushClient: update failed', error))
        }, 60000)

        return registration
      })
      .catch((error) => {
        console.error('webPushClient: service worker registration failed', error)
        registrationPromise = null
        throw error
      })
  }

  const registration = await registrationPromise
  if (registration) {
    pushVapidKeyToRegistration(registration)
  }
  return registration
}

export const getPushSupportState = async (): Promise<PushSupportState> => {
  if (!isWebPushSupportedInternally()) {
    return { supported: false, reason: 'Le push notification sono disponibili solo su browser compatibili.' }
  }

  const permission = Notification.permission
  return { supported: true, permission }
}

export const requestNotificationPermission = async (): Promise<NotificationPermission> => {
  if (!isWebPushSupportedInternally()) {
    return 'denied'
  }

  const permission = await Notification.requestPermission()
  return permission
}

const getExistingBrowserSubscription = async (): Promise<globalThis.PushSubscription | null> => {
  const registration = await ensureServiceWorkerRegistration()
  if (!registration) {
    return null
  }

  return registration.pushManager.getSubscription()
}

export const getExistingSubscription = async (): Promise<PushSubscription | null> => {
  const existing = await getExistingBrowserSubscription()
  if (!existing) {
    return null
  }

  return mapBrowserSubscriptionToDomain(existing)
}

export const configurePushPublicKey = (publicKey: string | null) => { applicationServerPublicKey = publicKey }

export const subscribeForPush = async (publicKey: string | null = applicationServerPublicKey, allowKeyRotation = false): Promise<PushSubscription | null> => {
  if (!isWebPushSupportedInternally()) return null
  // Ask directly in the click handler before worker/network awaits (Safari).
  const permission = Notification.permission === 'default'
    ? await requestNotificationPermission()
    : Notification.permission

  if (permission !== 'granted') {
    console.warn('webPushClient: notification permission not granted')
    return null
  }
  if (!publicKey) throw new Error('Chiave pubblica push non configurata sul server.')
  applicationServerPublicKey = publicKey
  const registration = await ensureServiceWorkerRegistration()
  if (!registration) return null

  try {
    const existing = await registration.pushManager.getSubscription()
    if (existing) {
      const expected = base64ToUint8Array(publicKey)
      const current = existing.options?.applicationServerKey ? new Uint8Array(existing.options.applicationServerKey) : null
      const matches = current && current.length === expected.length && current.every((value, index) => value === expected[index])
      if (matches) return mapBrowserSubscriptionToDomain(existing)
      if (!allowKeyRotation) throw new Error('Chiave push cambiata: riattiva esplicitamente questo dispositivo dalle impostazioni.')
      if (!await existing.unsubscribe()) throw new Error('Impossibile disattivare la vecchia chiave push. Riprova.')
    }

    const applicationServerKey = base64ToUint8Array(publicKey)
    const newSubscription = await registration.pushManager.subscribe({
      userVisibleOnly: true,
      applicationServerKey: applicationServerKey as unknown as BufferSource
    })

    return mapBrowserSubscriptionToDomain(newSubscription)
  } catch (error) {
    console.error('webPushClient: failed to subscribe for push notifications', error)
    throw error
  }
}

export const unsubscribeFromPush = async (): Promise<PushSubscription | null> => {
  if (Platform.OS !== 'web' || typeof navigator === 'undefined' || !('serviceWorker' in navigator)) return null
  // Logout must work offline and must not install/register a worker merely to
  // turn notifications off.
  const registration = await navigator.serviceWorker.getRegistration(SERVICE_WORKER_PATH)
  if (!registration) {
    return null
  }

  try {
    const notifications = await registration.getNotifications()
    notifications.forEach(notification => notification.close())
    const existing = await registration.pushManager.getSubscription()
    if (!existing) {
      return null
    }

    const mapped = mapBrowserSubscriptionToDomain(existing)
    const unsubscribed = await existing.unsubscribe()
    if (!unsubscribed) {
      throw new Error('Il browser non ha confermato la disattivazione delle push.')
    }
    return mapped
  } catch (error) {
    console.error('webPushClient: failed to unsubscribe from push notifications', error)
    throw error
  }
}

export const resetServiceWorkerRegistration = async (): Promise<void> => {
  if (!isWebPushSupportedInternally()) {
    return
  }

  try {
    const registration = await navigator.serviceWorker.getRegistration(SERVICE_WORKER_PATH)
    if (registration) await registration.unregister()
  } catch (error) {
    console.error('webPushClient: failed to reset service worker registrations', error)
  } finally {
    registrationPromise = null
    if (updateInterval) clearInterval(updateInterval)
    updateInterval = null
  }
}

export const webPushSupport = {
  configure: configurePushPublicKey,
  isSupported: isWebPushSupportedInternally,
  getSupportState: getPushSupportState,
  requestPermission: requestNotificationPermission,
  subscribe: subscribeForPush,
  unsubscribe: unsubscribeFromPush,
  getExistingSubscription,
  resetRegistration: resetServiceWorkerRegistration,
}
