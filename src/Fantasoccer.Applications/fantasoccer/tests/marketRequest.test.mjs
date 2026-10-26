import assert from 'node:assert/strict'
import test from 'node:test'
import { marketRequest } from '../services/marketRequest.ts'

test('trade creation only sends party/player IDs, agreed money and server roster versions', () => {
  const request = marketRequest({ buyerTeamId: 'buyer', sellerTeamId: 'seller', buyer: 'forged@example.test', status: 1,
    approvers: ['forged@example.test'], buyerPlayers: [{ playerId: 'first', price: 0 }], sellerPlayers: [{ playerId: 'second', price: 9999 }],
    moneyFromBuyer: 10, moneyFromSeller: 0, expectedBuyerRosterVersion: 3, expectedSellerRosterVersion: 5 })
  assert.deepEqual(request, { buyerTeamId: 'buyer', sellerTeamId: 'seller', buyerPlayerIds: ['first'], sellerPlayerIds: ['second'],
    moneyFromBuyer: 10, moneyFromSeller: 0, expectedBuyerRosterVersion: 3, expectedSellerRosterVersion: 5 })
  assert.throws(() => marketRequest({ buyerTeamId: 'buyer', sellerTeamId: 'seller', buyerPlayers: [{ name: 'Missing ID' }], sellerPlayers: [] }))
})
