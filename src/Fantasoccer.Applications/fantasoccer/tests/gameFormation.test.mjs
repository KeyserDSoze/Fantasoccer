import assert from 'node:assert/strict'
import test from 'node:test'
import { formationRequest } from '../services/gameFormation.ts'

const team = () => ({
  teamId: 'team-uuid', version: 0, sourceVersion: 7, name: 'Ignored name', owner: 'owner@example.test', moneyFromRank: 9999,
  players: [
    { current: { playerId: 'first-uuid', name: 'Changed client name', price: 0, revenue: 9999, status: 0 }, currentPosition: 1 },
    { current: { playerId: 'second-uuid', price: 9999, status: 0 }, currentPosition: 5 },
    { current: { playerId: 'first-uuid', status: 3 }, currentPosition: 11 },
  ],
})

test('formation writes contain only stable IDs, positions and concurrency versions', () => {
  assert.deepEqual(formationRequest(team(), true, false), {
    teamId: 'team-uuid', expectedVersion: 0, sourceVersion: 7, live: true, asAdmin: false,
    positions: [{ playerId: 'first-uuid', position: 1 }, { playerId: 'second-uuid', position: 5 }],
  })
})
test('missing or duplicate active IDs and missing source versions stop before sending', () => {
  assert.throws(() => formationRequest({ ...team(), sourceVersion: null }))
  const duplicate = team(); duplicate.players[1].current.playerId = 'first-uuid'
  assert.throws(() => formationRequest(duplicate))
  const missing = team(); delete missing.players[0].current.playerId
  assert.throws(() => formationRequest(missing))
})
