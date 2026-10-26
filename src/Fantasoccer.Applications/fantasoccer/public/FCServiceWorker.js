'use strict'

let applicationServerPublicKey = null
const PUSH_STATE_CACHE = 'fantasoccer-push-state-v1'
const RENEWAL_PATH = '/__fantasoccer_push_renewal__'
const DEVICE_PATH = '/__fantasoccer_push_device__'
const KEY_PATH = '/__fantasoccer_push_public_key__'

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

  if (event.data.type === 'SET_VAPID_KEY' && (event.data.value === null || typeof event.data.value === 'string')) {
    applicationServerPublicKey = event.data.value
    event.waitUntil((async () => {
      const cache = await caches.open(PUSH_STATE_CACHE)
      await cache.put(KEY_PATH, Response.json({ publicKey: applicationServerPublicKey }))
    })())
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

  if (!value || typeof value.title !== 'string' || typeof value.body !== 'string') {
    return
  }

  const options = {
    body: value.body,
    data: value.data,
    icon: value.icon ?? new URL('icon.png', self.registration.scope).href,
    badge: value.badge ?? new URL('adaptive-icon.png', self.registration.scope).href,
    tag: value.tag,
    requireInteraction: value.requireInteraction ?? false
  }

  event.waitUntil((async () => {
    const cache = await caches.open(PUSH_STATE_CACHE)
    const bindingResponse = await cache.match(DEVICE_PATH)
    if (!bindingResponse) return
    const binding = await bindingResponse.json()
    if (binding.enabled !== true || typeof value.data?.recipientEmail !== 'string' || binding.email?.toLowerCase() !== value.data.recipientEmail.toLowerCase()) return
    await self.registration.showNotification(value.title, options)
  })())
})

self.addEventListener('notificationclick', (event) => {
  event.notification.close()
  const uri = event.notification.data?.uri
  if (typeof uri !== 'string') return
  let target
  try {
    target = new URL(uri, self.registration.scope)
    if (target.origin !== new URL(self.registration.scope).origin || !['https:', 'http:'].includes(target.protocol)) return
  } catch { return }

  event.waitUntil((async () => {
    const clients = await self.clients.matchAll({ type: 'window', includeUncontrolled: true })
    const existing = clients.find((client) => client.url.startsWith(self.registration.scope))
    if (existing && 'focus' in existing) {
      await existing.focus()
      if ('navigate' in existing) await existing.navigate(target.href)
      return
    }
    await self.clients.openWindow(target.href)
  })())
})

self.addEventListener('pushsubscriptionchange', (event) => {
  event.waitUntil((async () => {
    const cache = await caches.open(PUSH_STATE_CACHE)
    const previous = await cache.match(RENEWAL_PATH)
    const saved = previous ? await previous.json() : null
    const bindingResponse = await cache.match(DEVICE_PATH)
    const binding = bindingResponse ? await bindingResponse.json() : null
    if (binding?.enabled !== true) return
    const oldEndpoint = saved?.oldEndpoint || event.oldSubscription?.endpoint || binding?.endpoint || ''
    const persist = (value) => cache.put(RENEWAL_PATH, new Response(JSON.stringify(value), { headers: { 'Content-Type': 'application/json' } }))
    // Persist even before subscribe: reconnection can recover a failed renewal.
    await persist({ oldEndpoint })
    const keyResponse = await cache.match(KEY_PATH)
    const publicKey = keyResponse ? (await keyResponse.json()).publicKey : applicationServerPublicKey
    if (!publicKey) return // Authenticated foreground reconciliation will retry.
    const oldKey = event.oldSubscription?.options?.applicationServerKey
    if (oldKey) {
      const oldBytes = new Uint8Array(oldKey)
      const expected = urlB64ToUint8Array(publicKey)
      if (oldBytes.length !== expected.length || oldBytes.some((value, index) => value !== expected[index])) {
        await persist({ oldEndpoint, keyRotationRequired: true })
        return // Different VAPID key: foreground explicit reactivation only.
      }
    }
    const newSubscription = event.newSubscription ?? await self.registration.pushManager.subscribe({
      userVisibleOnly: true,
      applicationServerKey: urlB64ToUint8Array(publicKey)
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
