import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'

const read = path => readFileSync(new URL(path, import.meta.url), 'utf8')
const json = path => JSON.parse(read(path))

test('release versions match across package, lockfile, Expo, UI, release notes and API', () => {
  const version = json('../package.json').version
  const lock = json('../package-lock.json')
  assert.match(version, /^\d+\.\d+\.\d+$/)
  assert.equal(lock.version, version)
  assert.equal(lock.packages[''].version, version)
  assert.equal(json('../app.json').expo.version, version)
  assert.equal(read('../config/version.ts').match(/APP_VERSION\s*=\s*'([^']+)'/)?.[1], version)
  assert.equal(read('../screens/patch-notes.tsx').match(/version:\s*'([^']+)'/)?.[1], version)
  assert.equal(read('../../Fantasoccer.Application.Api/Fantasoccer.Application.Api.csproj').match(/<Version>([^<]+)<\/Version>/)?.[1], version)
})

test('the latest release notes and UI use the same release date', () => {
  const date = read('../config/version.ts').match(/RELEASE_DATE\s*=\s*'([^']+)'/)?.[1]
  assert.ok(date)
  assert.equal(read('../screens/patch-notes.tsx').match(/date:\s*'([^']+)'/)?.[1], date)
})
