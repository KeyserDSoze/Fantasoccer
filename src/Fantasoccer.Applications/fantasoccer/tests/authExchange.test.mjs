import assert from 'node:assert/strict'
import { createRequire } from 'node:module'
import test from 'node:test'

const require = createRequire(import.meta.url)
const { loadTypeScript } = require('./helpers/loadTypeScript.cjs')

test('Microsoft exchange always posts this transaction verifier; application refresh keeps its existing protocol', async () => {
  const calls = []
  const { AuthService } = loadTypeScript('services/authService.ts', {
    '../configurations/apiConfig': {
      buildApiUrl: (path, params) => `https://api.example${path}?${new URLSearchParams(params)}`,
      getCommonHeaders: () => ({ Accept: 'application/json' }),
      ApiError: Error,
    },
    './tokenStorageService': { tokenStorageService: {} },
    './httpClient': { apiRequest: () => assert.fail('Unexpected API request') },
  }, {
    fetch: async (url, options) => {
      calls.push({ url: new URL(url), options })
      return Response.json({ accessToken: 'application-access', refreshToken: 'application-refresh', expiresIn: 86400 })
    },
  })
  const auth = new AuthService()
  const verifier = 'x'.repeat(43)
  await auth.exchangeToken('microsoft-code', verifier)
  assert.equal(calls[0].options.method, 'POST')
  assert.equal(calls[0].url.searchParams.get('provider'), '2')
  assert.equal(calls[0].url.searchParams.has('code_verifier'), false)
  assert.deepEqual(JSON.parse(calls[0].options.body), { code_verifier: verifier })
  await auth.refreshToken('existing-refresh')
  assert.equal(calls[1].url.searchParams.get('provider'), '0')
  assert.equal(calls[1].url.searchParams.get('code'), 'existing-refresh')
  assert.equal(calls[1].options.body, undefined)
})
