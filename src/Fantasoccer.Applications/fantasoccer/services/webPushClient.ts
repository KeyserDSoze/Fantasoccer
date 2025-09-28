import { Platform } from 'react-native'
import { PushSubscription } from 'types/pushNotification'

const SERVICE_WORKER_PATH = '/scripts/FCServiceWorker.js'
const APPLICATION_SERVER_PUBLIC_KEY = 'BC-_PiSEDN7I_pQsMKcDZeIDSPG-_2cCVvseMFvug6SNCI0hI7lqR8viPNdCKig7ny2iSdOeGI7ioBup2vd4tJ8'

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
    'serviceWorker' in navigator &&
    'PushManager' in window &&
    'Notification' in window
  )
}

export type PushSupportState =
  | { supported: true; permission: NotificationPermission }
  | { supported: false; reason: string }

let registrationPromise: Promise<ServiceWorkerRegistration | null> | null = null

const postVapidKeyToWorker = (worker?: ServiceWorker | null) => {
  if (!worker) {
    return
  }
  try {
    worker.postMessage({ type: 'SET_VAPID_KEY', value: APPLICATION_SERVER_PUBLIC_KEY })
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
      .catch((error) => {
        console.error('webPushClient: service worker registration failed', error)
        return null
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

export const subscribeForPush = async (): Promise<PushSubscription | null> => {
  const registration = await ensureServiceWorkerRegistration()
  if (!registration) {
    return null
  }

  const permission = Notification.permission === 'default'
    ? await requestNotificationPermission()
    : Notification.permission

  if (permission !== 'granted') {
    console.warn('webPushClient: notification permission not granted')
    return null
  }

  try {
    const existing = await registration.pushManager.getSubscription()
    if (existing) {
      return mapBrowserSubscriptionToDomain(existing)
    }

    const applicationServerKey = base64ToUint8Array(APPLICATION_SERVER_PUBLIC_KEY)
    const newSubscription = await registration.pushManager.subscribe({
      userVisibleOnly: true,
      applicationServerKey: applicationServerKey as unknown as BufferSource
    })

    return mapBrowserSubscriptionToDomain(newSubscription)
  } catch (error) {
    console.error('webPushClient: failed to subscribe for push notifications', error)
    return null
  }
}

export const unsubscribeFromPush = async (): Promise<PushSubscription | null> => {
  const registration = await ensureServiceWorkerRegistration()
  if (!registration) {
    return null
  }

  try {
    const existing = await registration.pushManager.getSubscription()
    if (!existing) {
      return null
    }

    const mapped = mapBrowserSubscriptionToDomain(existing)
    const unsubscribed = await existing.unsubscribe()
    if (!unsubscribed) {
      console.warn('webPushClient: unsubscribe returned false')
    }
    return mapped
  } catch (error) {
    console.error('webPushClient: failed to unsubscribe from push notifications', error)
    return null
  }
}

export const resetServiceWorkerRegistration = async (): Promise<void> => {
  if (!isWebPushSupportedInternally()) {
    return
  }

  try {
    const registrations = await navigator.serviceWorker.getRegistrations()
    await Promise.all(registrations.map((registration) => registration.unregister()))
  } catch (error) {
    console.error('webPushClient: failed to reset service worker registrations', error)
  } finally {
    registrationPromise = null
  }
}

export const webPushSupport = {
  isSupported: isWebPushSupportedInternally,
  getSupportState: getPushSupportState,
  requestPermission: requestNotificationPermission,
  subscribe: subscribeForPush,
  unsubscribe: unsubscribeFromPush,
  getExistingSubscription,
  resetRegistration: resetServiceWorkerRegistration,
}
