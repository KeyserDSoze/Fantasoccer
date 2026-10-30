import assert from 'node:assert/strict'
import { createRequire } from 'node:module'
import test from 'node:test'

const require = createRequire(import.meta.url)
const { loadTypeScript } = require('./helpers/loadTypeScript.cjs')

test('overdue provisional votes are announced with the time of the last update and never when up to date', () => {
  const { LiveGroupHelper } = loadTypeScript('types/liveGroup.ts')
  const updatedAt = new Date(2026, 9, 30, 20, 14).toISOString()
  assert.equal(LiveGroupHelper.getVotesDelayNotice(null), null)
  assert.equal(LiveGroupHelper.getVotesDelayNotice({ liveVotesStale: false, liveVotesUpdatedAtUtc: updatedAt }), null)
  assert.equal(LiveGroupHelper.getVotesDelayNotice({ liveVotesUpdatedAtUtc: updatedAt }), null)
  const overdue = LiveGroupHelper.getVotesDelayNotice({ liveVotesStale: true, liveVotesUpdatedAtUtc: updatedAt })
  assert.match(overdue, /non aggiornati dalle 20:14/)
  assert.match(overdue, /ultimo dato disponibile/)
})

test('when the live job never published votes the notice says so instead of inventing a time', () => {
  const { LiveGroupHelper } = loadTypeScript('types/liveGroup.ts')
  for (const liveVotesUpdatedAtUtc of [null, undefined, 'not a date']) {
    const notice = LiveGroupHelper.getVotesDelayNotice({ liveVotesStale: true, liveVotesUpdatedAtUtc })
    assert.match(notice, /non ancora disponibili/)
    assert.doesNotMatch(notice, /dalle/)
  }
})
