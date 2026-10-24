import assert from 'node:assert/strict'
import test from 'node:test'
import { createRequire } from 'node:module'

const require = createRequire(import.meta.url)
const { loadTypeScript } = require('./helpers/loadTypeScript.cjs')

function setup(responses, token = 'frontend-bearer') {
  const calls = []
  const { mcpConnectionService } = loadTypeScript('services/mcpConnectionService.ts', {
    '../configurations/apiConfig': { buildApiUrl: path => `https://api.example${path}` },
    './tokenStorageService': { tokenStorageService: { getTokenString: async () => token } },
  }, {
    fetch: async (url, options) => {
      calls.push({ url, options })
      const response = responses.shift()
      return { status: response.status, ok: response.status >= 200 && response.status < 300, json: async () => response.body }
    },
  })
  return { service: mcpConnectionService, calls }
}

test('lists connections with the existing bearer and never puts credentials in the URL', async () => {
  const connections = [{ clientId: 'chatgpt', displayName: 'ChatGPT', lastAuthorizedUtc: null }]
  const { service, calls } = setup([{ status: 200, body: connections }])
  assert.deepEqual(await service.list(), connections)
  assert.equal(calls[0].url, 'https://api.example/connect/connections')
  assert.equal(calls[0].options.headers.Authorization, 'Bearer frontend-bearer')
  assert.equal(calls[0].options.cache, 'no-store')
})

test('disabled MCP is distinguished from an authentication or server failure', async () => {
  const { service } = setup([{ status: 404 }, { status: 401 }, { status: 500 }])
  assert.equal(await service.list(), null)
  await assert.rejects(() => service.list())
  await assert.rejects(() => service.list())
})

test('revokes only the selected client and reports conflicts instead of claiming success', async () => {
  const { service, calls } = setup([{ status: 204 }, { status: 409 }])
  await service.disconnect('chatgpt')
  assert.equal(calls[0].options.method, 'POST')
  assert.deepEqual(JSON.parse(calls[0].options.body), { clientId: 'chatgpt' })
  await assert.rejects(() => service.disconnect('other'))
})

test('missing login never sends a request', async () => {
  const { service, calls } = setup([], null)
  await assert.rejects(() => service.list())
  assert.equal(calls.length, 0)
})
