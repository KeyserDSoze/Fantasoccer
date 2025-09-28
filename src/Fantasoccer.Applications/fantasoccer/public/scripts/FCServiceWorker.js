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

self.addEventListener('message', (event) => {
  if (!event.data) {
    return
  }

  if (event.data.type === 'SET_VAPID_KEY' && typeof event.data.value === 'string') {
    self.__fantasoccerVapidKey = event.data.value
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
    icon: value.Icon ?? 'https://fantacalcio.xyz/shared/fantaicon.jpg',
    badge: value.Badge ?? 'https://fantacalcio.xyz/shared/favicon-96x96.png',
    vibrate: value.Vibrate ?? [500, 110, 500, 110, 450, 110, 200, 110, 170, 40, 450, 110, 200, 110, 170, 40, 500],
    requireInteraction: value.RequireInteraction ?? false
  }

  event.waitUntil(self.registration.showNotification(value.Title ?? 'Fantasoccer', options))
})

self.addEventListener('notificationclick', (event) => {
  event.notification.close()
  const target = event.notification.data?.Uri ?? event.notification.data?.url
  if (!target) {
    return
  }

  event.waitUntil(clients.openWindow(target))
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
