import assert from 'node:assert/strict'
import { createRequire } from 'node:module'
import test from 'node:test'

const require = createRequire(import.meta.url)
const { loadTypeScript } = require('./helpers/loadTypeScript.cjs')

test('historical rank enrichment retains reconstructed metadata and unknown balances rather than presenting zero', () => {
  const { enhanceRank, DefaultRankedTeam } = loadTypeScript('types/rank.ts')
  const rank = enhanceRank({ serieADay: 7, snapshotDay: 8, isReconstructed: true, rounds: {
    '@': [{ ...DefaultRankedTeam, name: 'Historical team', point: 7, plusMoney: 18, money: null, valueAssets: null }],
  } })
  assert.equal(rank.isReconstructed, true)
  assert.equal(rank.snapshotDay, 8)
  assert.equal(rank.serieADay, 7)
  assert.equal(rank.totalTeamsCount, 1)
  assert.equal(rank.rounds['@'][0].money, null)
  assert.equal(rank.rounds['@'][0].valueAssets, null)
  assert.equal(rank.rounds['@'][0].plusMoney, 18)
})

test('rank balance aggregation propagates unknowns while known current and hall values remain numeric', () => {
  const { RankHelper, DefaultRankedTeam } = loadTypeScript('types/rank.ts')
  const known = { ...DefaultRankedTeam, money: 123, plusMoney: 8, valueAssets: 131 }
  const historical = { ...DefaultRankedTeam, money: null, plusMoney: 18, valueAssets: null }
  const sum = RankHelper.addRankedTeams(known, historical)
  assert.equal(sum.money, null)
  assert.equal(sum.valueAssets, null)
  assert.equal(sum.plusMoney, 26)
  const current = RankHelper.addRankedTeams(known, known)
  assert.equal(current.money, 246)
  assert.equal(current.valueAssets, 262)
  const negative = { ...DefaultRankedTeam, money: -5, valueAssets: -5 }
  const sorted = RankHelper.getTeamsSortedByValueAssets({ rounds: { '@': [historical, negative, known] } }, '@')
  assert.deepEqual(Array.from(sorted, row => row.valueAssets), [131, -5, null])
})
