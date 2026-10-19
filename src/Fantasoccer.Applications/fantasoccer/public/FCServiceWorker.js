'use strict'

const DEFAULT_PUBLIC_KEY = 'BC-_PiSEDN7I_pQsMKcDZeIDSPG-_2cCVvseMFvug6SNCI0hI7lqR8viPNdCKig7ny2iSdOeGI7ioBup2vd4tJ8'
let applicationServerPublicKey = self.__fantasoccerVapidKey ?? DEFAULT_PUBLIC_KEY
const PUSH_STATE_CACHE = 'fantasoccer-push-state-v1'
const RENEWAL_PATH = '/__fantasoccer_push_renewal__'
const DEVICE_PATH = '/__fantasoccer_push_device__'

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
    applicationServerPublicKey = event.data.value
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
    icon: value.Icon ?? new URL('icon.png', self.registration.scope).href,
    badge: value.Badge ?? new URL('adaptive-icon.png', self.registration.scope).href,
    sound: value.Sound,
    tag: value.Tag,
    vibrate: value.Vibrate ?? [500, 110, 500, 110, 450, 110, 200, 110, 170, 40, 450, 110, 200, 110, 170, 40, 500],
    requireInteraction: value.RequireInteraction ?? false
  }

  event.waitUntil((async () => {
    const cache = await caches.open(PUSH_STATE_CACHE)
    const bindingResponse = await cache.match(DEVICE_PATH)
    if (bindingResponse) {
      const binding = await bindingResponse.json()
      if (binding.enabled === false) return
      if (value.Data?.RecipientEmail && binding.email?.toLowerCase() !== value.Data.RecipientEmail.toLowerCase()) return
    }
    await self.registration.showNotification(value.Title ?? 'FantaZone', options)
  })())
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

  event.waitUntil((async () => {
    const clients = await self.clients.matchAll({ type: 'window', includeUncontrolled: true })
    const existing = clients.find((client) => client.url.startsWith(self.registration.scope))
    if (existing && 'focus' in existing) {
      await existing.focus()
      if ('navigate' in existing && target) await existing.navigate(target)
      return
    }
    await self.clients.openWindow(target)
  })())
})

self.addEventListener('pushsubscriptionchange', (event) => {
  event.waitUntil((async () => {
    const cache = await caches.open(PUSH_STATE_CACHE)
    const previous = await cache.match(RENEWAL_PATH)
    const saved = previous ? await previous.json() : null
    const bindingResponse = await cache.match(DEVICE_PATH)
    const binding = bindingResponse ? await bindingResponse.json() : null
    if (binding?.enabled === false) return
    const oldEndpoint = saved?.oldEndpoint || event.oldSubscription?.endpoint || binding?.endpoint || ''
    const persist = (value) => cache.put(RENEWAL_PATH, new Response(JSON.stringify(value), { headers: { 'Content-Type': 'application/json' } }))
    // Persist even before subscribe: reconnection can recover a failed renewal.
    await persist({ oldEndpoint })
    const newSubscription = event.newSubscription ?? await self.registration.pushManager.subscribe({
      userVisibleOnly: true,
      applicationServerKey: urlB64ToUint8Array(applicationServerPublicKey)
    })
    const json = newSubscription.toJSON()
    await persist({ oldEndpoint, subscription: { endpoint: json.endpoint, p256dh: json.keys?.p256dh, auth: json.keys?.auth } })
    const clients = await self.clients.matchAll({ type: 'window', includeUncontrolled: true })
    for (const client of clients) {
      client.postMessage({
        type: 'PUSH_SUBSCRIPTION_CHANGED',
        subscription: newSubscription.toJSON(),
      })
    }
  })().catch((error) => {
    console.error('[Service Worker] Failed to resubscribe', error)
  }))
})
