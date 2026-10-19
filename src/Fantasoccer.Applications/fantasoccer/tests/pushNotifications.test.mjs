import assert from 'node:assert/strict'
import test from 'node:test'
import fs from 'node:fs'
import vm from 'node:vm'
import { createRequire } from 'node:module'
const require = createRequire(import.meta.url)
const { loadTypeScript } = require('./helpers/loadTypeScript.cjs')

const defaults = () => ({ matchEvents: true, opponentMatchEvents: false, deploymentReminder: false, endDay: false,
  marketEvents: false, liveChangesReminder: false, liveFormationChangeEvents: false, subscriptions: [] })
const device = (endpoint = 'https://push.test/device', p256dh = 'key') => ({ endpoint, p256dh, auth: 'auth' })
const silentConsole = { log() {}, warn() {}, error() {} }
const loadService = fetch => {
  let refreshed = 0
  const module = loadTypeScript('services/pushNotificationService.ts', {
    './authService': { authService: { getUserInfo: async () => ({ email: 'user@test.local' }) } },
    './tokenStorageService': { tokenStorageService: { ensureValidToken: async () => 'token', forceRefreshToken: async () => { refreshed++; return 'new-token' } } },
    '../configurations/apiConfig': { buildApiUrl: (path, query) => `https://api.test${path}?${new URLSearchParams(query)}`, getCommonHeaders: (_, token) => ({ Authorization: `Bearer ${token}` }) },
  }, { fetch })
  return { service: new module.PushNotificationService(), refreshed: () => refreshed }
}

test('HTTP 503 is an error, not a successful save or an insert fallback', async () => {
  const calls = []
  const { service } = loadService(async (url, options) => { calls.push(options.method); return new Response('{}', { status: 503 }) })
  await assert.rejects(service.updateSettings('user@test.local', { endDay: true }), /503/)
  assert.deepEqual(calls, ['PATCH'])
})

test('updates use one partial server command and never load/replace devices', async () => {
  const calls = []
  const { service } = loadService(async (url, options) => { calls.push({ url, ...options }); return Response.json({ ...defaults(), matchEvents: false, endDay: true, subscriptions: [device()] }) })
  const result = await service.updateSettings('user+push@test.local', { endDay: true })
  assert.equal(result.subscriptions.length, 1)
  assert.equal(result.matchEvents, false)
  assert.equal(calls.length, 1)
  assert.deepEqual(JSON.parse(calls[0].body), { endDay: true })
  assert.equal(new URL(calls[0].url).searchParams.get('email'), 'user+push@test.local')
})

test('failed loads do not return empty defaults', async () => {
  const { service } = loadService(async () => new Response('', { status: 503 }))
  await assert.rejects(service.getSettings('user@test.local'), /503/)
})

test('malformed success and failed repository State cannot masquerade as settings', async () => {
  for (const value of [null, { isOk: false }, { matchEvents: true }]) {
    const { service } = loadService(async () => Response.json(value))
    await assert.rejects(service.disableAll('user@test.local'), /Risposta non valida/)
  }
})

test('401 refreshes once and keeps the expected account on the retry', async () => {
  const calls = []
  const { service, refreshed } = loadService(async (url, options) => {
    calls.push({ url, ...options })
    return calls.length === 1 ? new Response('{}', { status: 401 }) : Response.json(defaults())
  })
  await service.getSettings('user@test.local')
  assert.equal(refreshed(), 1)
  assert.equal(calls[0].url, calls[1].url)
  assert.equal(calls[1].headers.Authorization, 'Bearer new-token')
})

test('device renewal sends an upsert and the previous endpoint', async () => {
  let body
  const { service } = loadService(async (_url, options) => { body = JSON.parse(options.body); return Response.json(defaults()) })
  await service.addDevice('user@test.local', device('https://push.test/new'), 'https://push.test/old')
  assert.equal(body.previousEndpoint, 'https://push.test/old')
  assert.equal(body.subscription.endpoint, 'https://push.test/new')
})

test('default mapping respects saved false and upsert replaces same-endpoint keys', () => {
  const types = loadTypeScript('types/pushNotification.ts')
  assert.equal(types.mapRawPushNotificationSettingsToSettings(null).matchEvents, true)
  assert.equal(types.mapRawPushNotificationSettingsToSettings({ MatchEvents: false }).matchEvents, false)
  const settings = { ...defaults(), subscriptions: [device()] }
  const updated = types.addSubscription(settings, device(undefined, 'new-key'))
  assert.equal(updated.subscriptions.length, 1)
  assert.equal(updated.subscriptions[0].p256dh, 'new-key')
  assert.equal(settings.subscriptions[0].p256dh, 'key')
})

function memoryCache() {
  const values = new Map()
  const cache = { match: async key => values.get(key)?.clone(), put: async (key, value) => { values.set(key, value.clone()) }, delete: async key => values.delete(key) }
  return { cache, caches: { open: async () => cache } }
}
function workerFixture() {
  const handlers = {}
  const { cache, caches } = memoryCache()
  const notifications = []
  const self = { addEventListener: (type, callback) => { handlers[type] = callback },
    registration: { scope: 'https://fanta.test/', pushManager: { subscribe: async () => ({ toJSON: () => ({ endpoint: 'https://push.test/new', keys: { p256dh: 'new-key', auth: 'auth' } }) }) },
      showNotification: async (title, options) => { notifications.push({ title, ...options }) } },
    clients: { matchAll: async () => [] } }
  vm.runInNewContext(fs.readFileSync(new URL('../public/FCServiceWorker.js', import.meta.url), 'utf8'), {
    self, caches, Response, URL, atob, Uint8Array, console: silentConsole,
  })
  const dispatch = async (type, data = {}) => { let work; handlers[type]({ ...data, waitUntil: promise => { work = promise } }); await work }
  return { handlers, cache, dispatch, notifications, self }
}

test('worker persists renewal with no open windows and retains the original endpoint over multiple renewals', async () => {
  const { dispatch, cache } = workerFixture()
  await dispatch('pushsubscriptionchange', { oldSubscription: { endpoint: 'https://push.test/original' } })
  let pending = await (await cache.match('/__fantasoccer_push_renewal__')).json()
  assert.equal(pending.oldEndpoint, 'https://push.test/original')
  assert.equal(pending.subscription.endpoint, 'https://push.test/new')
  await dispatch('pushsubscriptionchange', { oldSubscription: { endpoint: 'https://push.test/intermediate' } })
  pending = await (await cache.match('/__fantasoccer_push_renewal__')).json()
  assert.equal(pending.oldEndpoint, 'https://push.test/original')
})

test('worker suppresses private notifications after logout and for another account', async () => {
  const { dispatch, cache, notifications } = workerFixture()
  const send = () => dispatch('push', { data: { text: () => JSON.stringify({ Title: 'Test', Body: 'Private', Data: { RecipientEmail: 'user@test.local' } }) } })
  await cache.put('/__fantasoccer_push_device__', Response.json({ email: 'user@test.local', enabled: false }))
  await send()
  await cache.put('/__fantasoccer_push_device__', Response.json({ email: 'other@test.local', enabled: true }))
  await send()
  assert.equal(notifications.length, 0)
  await cache.put('/__fantasoccer_push_device__', Response.json({ email: 'USER@test.local', enabled: true }))
  await send()
  assert.equal(notifications.length, 1)
})

test('worker keeps a renewal marker even when subscribing fails offline', async () => {
  const { dispatch, cache, self } = workerFixture()
  self.registration.pushManager.subscribe = async () => { throw new Error('Offline') }
  await dispatch('pushsubscriptionchange', { oldSubscription: { endpoint: 'https://push.test/old' } })
  const pending = await (await cache.match('/__fantasoccer_push_renewal__')).json()
  assert.equal(pending.oldEndpoint, 'https://push.test/old')
})

function lifecycleFixture({ binding, settings = defaults(), existing = device(), pending } = {}) {
  const { cache, caches } = memoryCache()
  const state = new Map(binding ? [['fantasoccer_push_device', JSON.stringify(binding)]] : [])
  const calls = { upserts: [], unsubscribed: 0, removed: [] }
  const webPushSupport = { isSupported: () => true, getSupportState: async () => ({ supported: true, permission: 'granted' }),
    getExistingSubscription: async () => existing, subscribe: async () => device('https://push.test/new'),
    unsubscribe: async () => { calls.unsubscribed++; return existing } }
  const module = loadTypeScript('services/pushDeviceService.ts', {
    'react-native': { Platform: { OS: 'web' } },
    '../utils/storage': { storage: { getItem: async key => state.get(key) ?? null, setItem: async (key, value) => { state.set(key, value) } } },
    './webPushClient': { webPushSupport },
    './pushNotificationService': { pushNotificationService: {
      getSettings: async () => settings,
      addDevice: async (...args) => { calls.upserts.push(args); await calls.onUpsert?.(); return settings },
      removeDevice: async (...args) => { calls.removed.push(args); return settings },
    } },
  }, { caches })
  return { ...module, calls, state, cache, setup: async () => { if (pending) await cache.put('/__fantasoccer_push_renewal__', Response.json(pending)) } }
}

test('authenticated reopen reconciles renewal and removes the pending marker only on success', async () => {
  const old = device('https://push.test/old')
  const next = device('https://push.test/new')
  const fixture = lifecycleFixture({ binding: { email: 'user@test.local', endpoint: old.endpoint, enabled: true }, settings: { ...defaults(), subscriptions: [old] },
    existing: next, pending: { oldEndpoint: old.endpoint, subscription: next } })
  await fixture.setup()
  await fixture.reconcilePushDevice('user@test.local')
  assert.equal(fixture.calls.upserts.length, 1)
  assert.equal(fixture.calls.upserts[0][2], old.endpoint)
  assert.equal(await fixture.cache.match('/__fantasoccer_push_renewal__'), undefined)
})

test('remote device removal is respected and is not silently undone by reconciliation', async () => {
  const fixture = lifecycleFixture({ binding: { email: 'user@test.local', endpoint: device().endpoint, enabled: true }, settings: defaults() })
  await fixture.reconcilePushDevice('user@test.local')
  assert.equal(fixture.calls.upserts.length, 0)
  assert.equal(JSON.parse(fixture.state.get('fantasoccer_push_device')).enabled, false)
})

test('failed renewal synchronization retains its durable marker for a later retry', async () => {
  const old = device('https://push.test/old')
  const fixture = lifecycleFixture({ binding: { email: 'user@test.local', endpoint: old.endpoint, enabled: true },
    settings: { ...defaults(), subscriptions: [old] }, existing: device('https://push.test/new'), pending: { oldEndpoint: old.endpoint } })
  await fixture.setup()
  fixture.calls.onUpsert = async () => { throw new Error('Offline') }
  await assert.rejects(fixture.reconcilePushDevice('user@test.local'), /Offline/)
  assert.ok(await fixture.cache.match('/__fantasoccer_push_renewal__'))
  fixture.calls.onUpsert = undefined
  await fixture.reconcilePushDevice('user@test.local')
  assert.equal(await fixture.cache.match('/__fantasoccer_push_renewal__'), undefined)
})

test('local disable overtaking an in-flight renewal is not undone by its response', async () => {
  const old = device('https://push.test/old')
  const next = device('https://push.test/new')
  const fixture = lifecycleFixture({ binding: { email: 'user@test.local', endpoint: old.endpoint, enabled: true },
    settings: { ...defaults(), subscriptions: [old] }, existing: next })
  let entered, release
  const reached = new Promise(resolve => { entered = resolve })
  const pending = new Promise(resolve => { release = resolve })
  fixture.calls.onUpsert = async () => { entered(); await pending }
  const reconciliation = fixture.reconcilePushDevice('user@test.local')
  await reached
  await fixture.rememberPushDevice('user@test.local', old.endpoint, false)
  release()
  await reconciliation
  assert.equal(JSON.parse(fixture.state.get('fantasoccer_push_device')).enabled, false)
  assert.equal(fixture.calls.removed[0][1], next.endpoint)
})

test('account switch disconnects the previous browser subscription without opting in the next user', async () => {
  const fixture = lifecycleFixture({ binding: { email: 'previous@test.local', endpoint: device().endpoint, enabled: true } })
  await fixture.reconcilePushDevice('next@test.local')
  assert.equal(fixture.calls.upserts.length, 0)
  assert.equal(fixture.calls.unsubscribed, 1)
  assert.equal(JSON.parse(fixture.state.get('fantasoccer_push_device')).enabled, false)
})

test('logout disables worker delivery and removes only the current device', async () => {
  const fixture = lifecycleFixture({ binding: { email: 'user@test.local', endpoint: device().endpoint, enabled: true } })
  await fixture.disconnectPushDevice('user@test.local')
  assert.equal(fixture.calls.unsubscribed, 1)
  assert.equal(fixture.calls.removed[0][1], device().endpoint)
  assert.equal((await (await fixture.cache.match('/__fantasoccer_push_device__')).json()).enabled, false)
})

test('service worker registration can recover after a transient failure and waits for activation', async () => {
  let registrations = 0
  let activated = false
  let subscribed = false
  const registration = { active: null, update: async () => {}, pushManager: { getSubscription: async () => null,
    subscribe: async () => { assert.equal(activated, true); subscribed = true; return { toJSON: () => ({ endpoint: 'https://push.test/new', keys: {} }) } } } }
  const navigator = { serviceWorker: { register: async () => { if (++registrations === 1) throw new Error('Offline'); return registration },
    ready: Promise.resolve().then(() => { activated = true; registration.active = { postMessage() {} }; return registration }) } }
  const module = loadTypeScript('services/webPushClient.ts', { 'react-native': { Platform: { OS: 'web' } } }, {
    window: { PushManager() {}, Notification: {}, isSecureContext: true }, navigator, Notification: { permission: 'granted' },
    setInterval: () => 1, clearInterval() {}, console: silentConsole,
  })
  await assert.rejects(module.subscribeForPush(), /Offline/)
  await module.subscribeForPush()
  assert.equal(registrations, 2)
  assert.equal(subscribed, true)
})

test('logout unsubscribes an existing worker offline without registering or updating it', async () => {
  let unsubscribed = 0, closed = 0
  const registration = { getNotifications: async () => [{ close() { closed++ } }], pushManager: { getSubscription: async () => ({
    toJSON: () => ({ endpoint: 'https://push.test/device', keys: { p256dh: 'key', auth: 'auth' } }),
    unsubscribe: async () => { unsubscribed++; return true },
  }) } }
  const module = loadTypeScript('services/webPushClient.ts', { 'react-native': { Platform: { OS: 'web' } } }, {
    navigator: { serviceWorker: { getRegistration: async () => registration, register: async () => { throw new Error('Must not register during logout') } } }, console: silentConsole,
  })
  const removed = await module.unsubscribeFromPush()
  assert.equal(removed.endpoint, 'https://push.test/device')
  assert.equal(unsubscribed, 1)
  assert.equal(closed, 1)
})
