import assert from 'node:assert/strict'
import test from 'node:test'
import { leagueBasketTeams, listMyTeams } from '../utils/myTeams.ts'

const group = {
  id: 'group', name: 'Amici', revision: 1, leagues: [], users: [],
  baskets: [
    { id: 'serie-a', name: 'Serie A', kind: 0, years: [{ year: 15, teams: [
      { teamId: 'draghi', name: 'Draghi FC', owner: 'mario@example.test', additionalOwners: [] },
      { teamId: 'koopa', name: 'Koopa FC', owner: 'luigi@example.test', additionalOwners: ['MARIO@example.test'] }] }] },
    { id: 'premier', name: 'Premier', kind: 0, years: [{ year: 15, teams: [{ teamId: 'leoni', name: 'Leoni FC', owner: 'mario@example.test', additionalOwners: [] }] },
      { year: 14, teams: [{ teamId: 'old', name: 'Old', owner: 'mario@example.test', additionalOwners: [] }] }] },
    { id: 'champions', name: 'Champions', kind: 1, originBasketId: 'serie-a', years: [{ year: 15, teams: [
      { teamId: 'draghi-cl', name: 'Draghi FC', owner: 'mario@example.test', additionalOwners: [], originTeamId: 'draghi' }] }] },
  ],
}

test('every owned or co-owned team of the season is listed, never only the first one', () => {
  const teams = listMyTeams(group, 'mario@example.test', 15)
  // Autonomous teams first (by basket), derived copies last.
  assert.deepEqual(teams.map(team => team.teamId), ['leoni', 'draghi', 'koopa', 'draghi-cl'])
  assert.deepEqual(new Set(teams.map(team => team.teamId)), new Set(['draghi', 'koopa', 'leoni', 'draghi-cl']))
  assert.equal(teams.at(-1).teamId, 'draghi-cl')
  assert.equal(teams.at(-1).isDerived, true)
})

test('derived teams can be excluded and a league restricts the candidates to its participants', () => {
  assert.equal(listMyTeams(group, 'mario@example.test', 15, { includeDerived: false }).some(team => team.isDerived), false)
  assert.deepEqual(listMyTeams(group, 'mario@example.test', 15, { teamIds: ['draghi-cl', 'koopa-cl'] }).map(team => team.teamId), ['draghi-cl'])
  assert.deepEqual(listMyTeams(group, 'peach@example.test', 15), [])
  assert.deepEqual(listMyTeams(null, 'mario@example.test', 15), [])
})

test('league teams cover every linked basket and only explicit participants', () => {
  const withLeagues = { ...group, leagues: [
    { id: 'serie', name: 'Serie A', isMain: true, type: 1, basketsId: ['serie-a'], years: [{ year: 15, type: 1, settings: {}, participantTeamIds: ['draghi', 'koopa'] }] },
    { id: 'cl', name: 'Champions', isMain: false, type: 1, mode: 1, basketsId: ['champions', 'premier'], years: [{ year: 15, type: 1, settings: {}, participantTeamIds: ['draghi-cl'] }] },
  ] }
  assert.deepEqual(leagueBasketTeams(withLeagues, 'serie', 15).flatMap(entry => entry.teams.map(team => team.teamId)), ['draghi', 'koopa'])
  // Linked basket without admitted teams is skipped; nothing is enrolled implicitly.
  assert.deepEqual(leagueBasketTeams(withLeagues, 'cl', 15).map(entry => entry.basketId), ['champions'])
  assert.deepEqual(leagueBasketTeams(withLeagues, 'missing', 15), [])
})
