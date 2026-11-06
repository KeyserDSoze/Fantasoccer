import assert from 'node:assert/strict'
import { createRequire } from 'node:module'
import test from 'node:test'

const require = createRequire(import.meta.url)
const { loadTypeScript } = require('./helpers/loadTypeScript.cjs')

test('the team economy matches the server: refunds per status, sale price returned, credits from rank, market and opening prize', () => {
  const { TeamHelper } = loadTypeScript('types/team.ts')
  const row = (status, price, revenue = 0) => ({ status, price, revenue })
  const team = { moneyFromRank: 40, marketBalanceAdjustment: -15, openingCompetitionPrize: 30,
    players: [row(0, 100), row(1, 41), row(2, 30), row(3, 41, 58), row(5, 41, 20)] }
  assert.equal(TeamHelper.getRefund(row(1, 41)), 20)
  assert.equal(TeamHelper.getRefund(row(3, 41, 58)), 58)
  assert.equal(TeamHelper.getRefund(row(5, 41, 20)), 20)
  assert.equal(TeamHelper.getRefund(row(2, 30)), 0)
  assert.equal(TeamHelper.getCost(team), 155 - 40 + 15 - 30)
  const economy = TeamHelper.getEconomy(team, 1000)
  assert.equal(economy.balance, 900)
  assert.equal(economy.activeCost, 100)
  assert.equal(economy.refunds, 98)
  assert.equal(economy.releasedCost, 41 - 20 + 30 + 41 - 58 + 41 - 20)
})

test('goal credits follow the main league unless the league chooses explicitly', () => {
  const { goalCreditsEnabled } = loadTypeScript('utils/rankMoney.ts')
  assert.equal(goalCreditsEnabled(true, {}), true)
  assert.equal(goalCreditsEnabled(false, {}), false)
  assert.equal(goalCreditsEnabled(false, { goalCreditsEnabled: null }), false)
  assert.equal(goalCreditsEnabled(false, { goalCreditsEnabled: true }), true)
  assert.equal(goalCreditsEnabled(true, { goalCreditsEnabled: false }), false)
  assert.equal(goalCreditsEnabled(undefined, undefined), false)
})

test('the swap window offers empty cards for the stands and for the free bench places of the role', () => {
  const { emptySlotsFor } = loadTypeScript('utils/swapSlots.ts')
  const labels = slots => Array.from(slots, x => `${x.area}:${x.position}:${x.label}`)
  // A forward starter (position 3); the first forward bench place (9) is taken, the second (10) is free.
  assert.deepEqual(labels(emptySlotsFor(3, 3, [0, 1, 2, 3, 9, 4], true)), ['bench:10:2° panchinaro', 'tribune:11:In tribuna'])
  // Nobody on the bench for his role: two empty bench cards, first and second.
  assert.deepEqual(labels(emptySlotsFor(1, 1, [0, 1, 4], true)), ['bench:5:1° panchinaro', 'bench:6:2° panchinaro', 'tribune:11:In tribuna'])
  // A goalkeeper has one bench place.
  assert.deepEqual(labels(emptySlotsFor(0, 11, [], true)), ['bench:4:Panchinaro'])
  // A starter the formation cannot do without goes nowhere; a bench player can go to the stands or to the other bench place.
  assert.deepEqual(labels(emptySlotsFor(2, 2, [], false)), [])
  assert.deepEqual(labels(emptySlotsFor(2, 7, [7], false)), ['bench:8:2° panchinaro', 'tribune:11:In tribuna'])
  // Everything taken: only the stands.
  assert.deepEqual(labels(emptySlotsFor(3, 3, [9, 10], true)), ['tribune:11:In tribuna'])
})
