'use strict'

const DEFAULT_PUBLIC_KEY = 'BC-_PiSEDN7I_pQsMKcDZeIDSPG-_2cCVvseMFvug6SNCI0hI7lqR8viPNdCKig7ny2iSdOeGI7ioBup2vd4tJ8'
const applicationServerPublicKey = self.__fantasoccerVapidKey ?? DEFAULT_PUBLIC_KEY

const urlB64ToUint8Array = (base64String) => {
  const padding = '='.repeat((4 - (base64String.length % 4)) % 4)
  const base64 = (base64String + padding)
    .replace(/-/g, '+')
    .replace(/_/g, '/')

  const rawData = atob(base64)
  const outputArray = new Uint8Array(rawData.length)

  for (let i = 0; i < rawData.length; ++i) {
    outputArray[i] = rawData.charCodeAt(i)
  }
  return outputArray
}

// Auto-update: activate new service worker immediately
self.addEventListener('install', (event) => {
  console.log('[Service Worker] Installing new version...')
  self.skipWaiting()
})

self.addEventListener('activate', (event) => {
  console.log('[Service Worker] Activating new version...')
  event.waitUntil(
    self.clients.claim().then(() => {
      console.log('[Service Worker] Claiming all clients')
    })
  )
})

self.addEventListener('message', (event) => {
  if (!event.data) {
    return
  }

  if (event.data.type === 'SET_VAPID_KEY' && typeof event.data.value === 'string') {
    self.__fantasoccerVapidKey = event.data.value
  }
  
  // Force update on request
  if (event.data.type === 'SKIP_WAITING') {
    self.skipWaiting()
  }
})

self.addEventListener('push', (event) => {
  if (!event.data) {
    return
  }

  const value = (() => {
    try {
      return JSON.parse(event.data.text())
    } catch (error) {
      console.error('[Service Worker] Unable to parse push payload', error)
      return null
    }
  })()

  if (!value) {
    return
  }

  const options = {
    body: value.Body,
    data: value.Data,
    icon: value.Icon ?? new URL('android-chrome-192x192.png', self.registration.scope).href,
    badge: value.Badge ?? new URL('favicon-32x32.png', self.registration.scope).href,
    vibrate: value.Vibrate ?? [500, 110, 500, 110, 450, 110, 200, 110, 170, 40, 450, 110, 200, 110, 170, 40, 500],
    requireInteraction: value.RequireInteraction ?? false
  }

  event.waitUntil(self.registration.showNotification(value.Title ?? 'FantaZone', options))
})

self.addEventListener('notificationclick', (event) => {
  event.notification.close()
  let target = event.notification.data?.Uri ?? event.notification.data?.url
  if (target && target.includes("fantacalcio.xyz")){
    target = target.replace("fantacalcio.xyz", "fanta.zone")
  }
  if (!target) {
    return
  }

  event.waitUntil(self.clients.openWindow(target))
})

self.addEventListener('pushsubscriptionchange', (event) => {
  console.log('[Service Worker]: "pushsubscriptionchange" event fired.')
  const applicationServerKey = urlB64ToUint8Array(applicationServerPublicKey)
  event.waitUntil(
    self.registration.pushManager.subscribe({
      userVisibleOnly: true,
      applicationServerKey
    }).then((newSubscription) => {
      console.log('[Service Worker] New subscription: ', newSubscription)
      // TODO: notify clients to sync the new subscription via postMessage/fetch
    }).catch((error) => {
      console.error('[Service Worker] Failed to resubscribe', error)
    })
  )
})
