import assert from 'node:assert/strict'
import { createRequire } from 'node:module'
import { readFileSync } from 'node:fs'
import test from 'node:test'

const require = createRequire(import.meta.url)
const { loadTypeScript } = require('./helpers/loadTypeScript.cjs')
const { transferNotice, formatTransferDate } = loadTypeScript('utils/playerTransfer.ts')
const napoli = { name: 'Napoli', abbreviation: 'nap' }

test('transfer notices describe departures, moves and arrivals with the discovery date', () => {
  const at = new Date(2027, 0, 10, 2, 30).toISOString()
  assert.equal(transferNotice({ leftSerieA: true, previousTeam: napoli, transferredAtUtc: at }), 'Ha lasciato la Serie A il 10/01')
  assert.equal(transferNotice({ previousTeam: napoli, transferredAtUtc: at }), 'Trasferito dal Napoli il 10/01')
  assert.equal(transferNotice({ previousTeam: null, transferredAtUtc: at }), 'Arrivato in Serie A il 10/01')
  // Departures recorded before tracking have no date; a roster flag alone is enough.
  assert.equal(transferNotice({ leftSerieA: true, previousTeam: napoli, transferredAtUtc: null }), 'Ha lasciato la Serie A')
  assert.equal(transferNotice(null, true), 'Ha lasciato la Serie A')
  assert.equal(transferNotice({ previousTeam: null, transferredAtUtc: null }), null)
  assert.equal(transferNotice(undefined), null)
  assert.equal(formatTransferDate('not a date'), '—')
  assert.equal(formatTransferDate(at), '10/01/2027')
})

test('the full player list exposes current club, previous club and transfer date (also in the CSV)', () => {
  const screen = readFileSync(new URL('../screens/players.tsx', import.meta.url), 'utf8')
  for (const label of ['Squadra attuale', 'Squadra precedente', 'Data di trasferimento']) assert.match(screen, new RegExp(`label: '${label}'`))
  assert.match(screen, /leftSerieA \? 'Fuori Serie A'/)
  assert.match(screen, /columnDefinitions\.map\(col => \{\s*const value = getColumnValue/)
})
