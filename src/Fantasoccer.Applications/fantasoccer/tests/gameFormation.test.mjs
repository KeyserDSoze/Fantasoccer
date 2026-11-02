import assert from 'node:assert/strict'
import test from 'node:test'
import { displayedPlayers, formationRequest, savedPlayer, sameLineupLeagues } from '../services/gameFormation.ts'

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
test('a live change carries the confirmed leagues only when it is live', () => {
  assert.deepEqual(formationRequest(team(), true, false, ['league-a', 'league-b']).applyToLeagues, ['league-a', 'league-b'])
  assert.equal('applyToLeagues' in formationRequest(team(), true, false, []), false)
  assert.equal('applyToLeagues' in formationRequest(team(), true, false), false)
  assert.equal('applyToLeagues' in formationRequest(team(), false, false, ['league-a']), false)
})
test('only the competitions with the very same lineup are offered', () => {
  const sources = [{ leagueName: 'A', sameLineup: true }, { leagueName: 'B', sameLineup: false }, { leagueName: 'C' }, { leagueName: 'D', sameLineup: true }]
  assert.deepEqual(sameLineupLeagues(sources).map(source => source.leagueName), ['A', 'D'])
  assert.deepEqual(sameLineupLeagues([]), [])
})
test('missing or duplicate active IDs and missing source versions stop before sending', () => {
  assert.throws(() => formationRequest({ ...team(), sourceVersion: null }))
  const duplicate = team(); duplicate.players[1].current.playerId = 'first-uuid'
  assert.throws(() => formationRequest(duplicate))
  const missing = team(); delete missing.players[0].current.playerId
  assert.throws(() => formationRequest(missing))
})

test('the pitch shows the automatic substitutions while saves keep the lineup as saved', () => {
  const players = [
    { current: { playerId: 'starter', status: 0, position: 1 }, currentPosition: 1, projectedPosition: 5 },
    { current: { playerId: 'bench', status: 0, position: 5 }, currentPosition: 5, projectedPosition: 1 },
    { current: { playerId: 'other', status: 0, position: 2 }, currentPosition: 2, projectedPosition: 2 },
    { current: { playerId: 'old', status: 0, position: 3 }, currentPosition: 3 },
  ]
  const shown = displayedPlayers(players)
  assert.deepEqual(shown.map(x => x.currentPosition), [5, 1, 2, 3])
  // The bench player that came in is still, originally, a bench player.
  assert.equal(shown[1].current.position, 5)
  // Untouched players are the very same objects; the substituted ones resolve back to the saved player.
  assert.equal(shown[2], players[2]); assert.equal(shown[3], players[3])
  assert.equal(savedPlayer(shown[0]), players[0]); assert.equal(savedPlayer(shown[1]), players[1]); assert.equal(savedPlayer(players[2]), players[2])
  // Nothing is mutated: a save built from the same players still carries the saved positions.
  assert.deepEqual(players.map(x => x.currentPosition), [1, 5, 2, 3])
  const request = formationRequest({ ...team(), players }, true, false)
  assert.deepEqual(request.positions, [
    { playerId: 'starter', position: 1 }, { playerId: 'bench', position: 5 }, { playerId: 'other', position: 2 }, { playerId: 'old', position: 3 }])
})
test('with unsaved edits the saved positions are shown, since the edits are expressed on them', () => {
  const players = [{ current: { playerId: 'starter', status: 0 }, currentPosition: 5, projectedPosition: 1 }]
  assert.equal(displayedPlayers(players, true)[0], players[0])
})
