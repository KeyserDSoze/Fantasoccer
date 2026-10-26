import assert from 'node:assert/strict'
import test from 'node:test'
import { createWorkspaceApi, WorkspaceApiError } from '../services/workspaceApiClient.ts'

function client(fetch, refresh = async () => null) {
  return createWorkspaceApi({ baseUrl: 'http://local.test/api/workspace/', token: async () => 'initial', refresh, fetch })
}

test('uses readable DTOs directly, including group-scoped team ownership', async () => {
  const details = { groupId: 'group-a', name: 'Friends', roles: 4, members: [], baskets: [], leagues: [], teams: [] }
  const requests = []
  const api = client(async (url, options) => {
    requests.push({ url, ...options })
    return new Response(JSON.stringify(details), { headers: { 'Content-Type': 'application/json' } })
  })
  assert.deepEqual(await api.details('group-a'), details)
  assert.equal(requests[0].url, 'http://local.test/api/workspace/groups/group-a')
  assert.equal(requests[0].headers.Authorization, 'Bearer initial')
  const team = { basketId: 'basket', seasonIndex: 15, name: 'Team', ownerUserId: 'owner', additionalOwnerUserIds: ['friend'] }
  await api.createTeam('group-a', team)
  assert.deepEqual(JSON.parse(requests[1].body), team)
  assert.equal(requests[1].method, 'POST')
  assert.equal(requests[1].url, 'http://local.test/api/workspace/groups/group-a/teams')
})

test('refreshes exactly once on 401 and does not silently fall back to legacy repositories', async () => {
  let calls = 0, refreshes = 0
  const api = client(async (_url, options) => {
    calls++
    if (calls === 1) return new Response('', { status: 401 })
    assert.equal(options.headers.Authorization, 'Bearer renewed')
    return new Response('[]')
  }, async () => { refreshes++; return 'renewed' })
  assert.deepEqual(await api.groups(), [])
  assert.equal(calls, 2)
  assert.equal(refreshes, 1)
})

test('a forbidden group does not become an empty group and does not trigger token refresh', async () => {
  const api = client(async () => new Response(JSON.stringify({ code: 'group_access_denied', detail: 'Forbidden' }), { status: 403 }),
    async () => assert.fail('403 is not an expired token'))
  await assert.rejects(api.details('other-group'), error => error instanceof WorkspaceApiError && error.status === 403 && error.code === 'group_access_denied')
})

test('partial commands use explicit names and correctly handle no-content responses', async () => {
  let request
  const api = client(async (url, options) => { request = { url, ...options }; return new Response(null, { status: 204 }) })
  assert.equal(await api.setMember('group-a', { email: 'friend@example.test', displayName: 'Friend', roles: 2 }), undefined)
  assert.equal(request.method, 'PUT')
  assert.deepEqual(JSON.parse(request.body), { email: 'friend@example.test', displayName: 'Friend', roles: 2 })
})

test('propagates cancellation for group switching and never issues a request without a token', async () => {
  const controller = new AbortController()
  const api = client(async (_url, options) => { assert.equal(options.signal, controller.signal); return new Response('[]') })
  await api.groups(controller.signal)
  const anonymous = createWorkspaceApi({ baseUrl: 'http://local.test', token: async () => null, refresh: async () => null, fetch: async () => assert.fail('anonymous request') })
  await assert.rejects(anonymous.groups(), error => error.status === 401)
})

test('group activation keeps the secret code in the POST body, never in the URL or query string', async () => {
  let request
  const api = client(async (url, options) => { request = { url, ...options }; return new Response('{"groupId":"new-group"}') })
  await api.redeemGroupCode('My group', 'FZ-private-code')
  assert.equal(request.url, 'http://local.test/api/workspace/groups/redeem-code')
  assert.equal(request.method, 'POST')
  assert.deepEqual(JSON.parse(request.body), { name: 'My group', code: 'FZ-private-code' })
})

test('member lookup is exact-email and group scoped rather than listing the global account directory', async () => {
  let request
  const api = client(async (url, options) => { request = { url, ...options }; return new Response('{"email":"friend@example.test","found":true,"isMember":false,"displayName":"Friend"}') })
  await api.lookupMember('my-group', ' friend@example.test ')
  assert.equal(request.url, 'http://local.test/api/workspace/groups/my-group/members/lookup?email=friend%40example.test')
  assert.equal(request.method, 'GET')
})

test('member status changes are scoped to a group and stable user ID, not a fabricated email', async () => {
  let request
  const api = client(async (url, options) => { request = { url, ...options }; return new Response(null, { status: 204 }) })
  await api.updateMember('group-a', 'historical-user', { isActive: false, roles: 0 })
  assert.equal(request.url, 'http://local.test/api/workspace/groups/group-a/members/historical-user')
  assert.equal(request.method, 'PATCH')
  assert.deepEqual(JSON.parse(request.body), { isActive: false, roles: 0 })
  await api.updateMember('group-a', 'historical-user', { isActive: true, roles: 2, email: 'real@example.test' })
  assert.deepEqual(JSON.parse(request.body), { isActive: true, roles: 2, email: 'real@example.test' })
})
