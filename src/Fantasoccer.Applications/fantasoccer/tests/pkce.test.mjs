import assert from 'node:assert/strict'
import test from 'node:test'
import { webcrypto, createHash } from 'node:crypto'
import { createRequire } from 'node:module'

const require = createRequire(import.meta.url)
const { loadTypeScript } = require('./helpers/loadTypeScript.cjs')

function setup() {
  const values = new Map()
  const sessionStorage = {
    getItem: key => values.get(key) ?? null,
    setItem: (key, value) => values.set(key, value),
    removeItem: key => values.delete(key),
  }
  const { PKCEHelper } = loadTypeScript('utils/pkceHelper.ts', {
    'react-native': { Platform: { OS: 'web' } }, './storage': { storage: {} },
  }, { crypto: webcrypto, TextEncoder, btoa, window: { sessionStorage } })
  return { helper: PKCEHelper, values }
}

const config = { clientId: 'test-client', authority: 'https://login.microsoftonline.com/consumers', redirectUri: 'https://example.com/' }

test('PKCE uses a fresh random verifier and S256, retained only for the login transaction', async () => {
  const { helper } = setup()
  const first = new URL(await helper.buildAuthorizeUrl(config))
  const verifier = await helper.consumeTransaction(first.searchParams.get('state'))
  assert.equal(first.searchParams.get('code_challenge'), createHash('sha256').update(verifier).digest('base64url'))
  assert.equal(first.searchParams.get('code_challenge_method'), 'S256')
  const second = new URL(await helper.buildAuthorizeUrl(config))
  assert.notEqual(first.searchParams.get('code_challenge'), second.searchParams.get('code_challenge'))
  assert.notEqual(first.searchParams.get('state'), second.searchParams.get('state'))
})

test('callback state must match and a transaction cannot be consumed twice', async () => {
  const { helper } = setup()
  const url = new URL(await helper.buildAuthorizeUrl(config))
  await assert.rejects(() => helper.consumeTransaction('attacker-state'))
  await assert.rejects(() => helper.consumeTransaction(url.searchParams.get('state')))
  const next = new URL(await helper.buildAuthorizeUrl(config))
  await helper.consumeTransaction(next.searchParams.get('state'))
  await assert.rejects(() => helper.consumeTransaction(next.searchParams.get('state')))
})

test('expired login transactions cannot be redeemed', async () => {
  const { helper, values } = setup()
  const url = new URL(await helper.buildAuthorizeUrl(config))
  const [key, value] = [...values.entries()][0]
  const transaction = JSON.parse(value)
  transaction.createdAt = Date.now() - 11 * 60 * 1000
  values.set(key, JSON.stringify(transaction))
  await assert.rejects(() => helper.consumeTransaction(url.searchParams.get('state')))
})
