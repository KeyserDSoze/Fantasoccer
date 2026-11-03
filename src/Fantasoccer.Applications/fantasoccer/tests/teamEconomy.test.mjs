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
