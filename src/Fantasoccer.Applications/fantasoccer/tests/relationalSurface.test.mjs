import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import test from 'node:test'
import { createRequire } from 'node:module'
const require = createRequire(import.meta.url)
const { loadTypeScript } = require('./helpers/loadTypeScript.cjs')

test('the active frontend has no repository client or compact transport mapper', () => {
  const root = new URL('../', import.meta.url)
  const folders = ['services', 'types', 'contracts']
  for (const folder of folders) {
    for (const entry of fs.readdirSync(new URL(folder, root), { withFileTypes: true })) {
      if (!entry.isFile() || !entry.name.endsWith('.ts')) continue
      const source = fs.readFileSync(new URL(`${folder}/${entry.name}`, root), 'utf8')
      assert.doesNotMatch(source, /from\s+['"]rystem\.repository\.client|RepositoryServices|\bmapRaw\w*|\binterface\s+\w+Raw\b/, path.join(folder, entry.name))
    }
  }
  const pkg = JSON.parse(fs.readFileSync(new URL('package.json', root)))
  assert.equal(pkg.dependencies['rystem.repository.client'], undefined)
})
test('operational timestamps remain nullable and never manufacture a current timestamp', () => {
  const { formatLogTimestamp, sortLogsByTimestamp } = loadTypeScript('types/log.ts')
  assert.equal(formatLogTimestamp({ timestamp: null }), 'Data non disponibile')
  assert.deepEqual(Array.from(sortLogsByTimestamp([{ id: 'unknown', timestamp: null }, { id: 'saved', timestamp: '2026-01-01T00:00:00Z' }]), x => x.id), ['saved', 'unknown'])
})
