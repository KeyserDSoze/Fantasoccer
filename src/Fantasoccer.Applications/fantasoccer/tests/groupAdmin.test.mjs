import assert from 'node:assert/strict'
import test from 'node:test'
import {
  copyAnnualLeagueToYear,
  copyMissingTeams,
  getAnnualLeagueForYear,
  preserveRawLeagueSetting,
  upsertAnnualLeague
} from '../utils/groupAdmin.ts'
import { getCurrentSeasonYear } from '../utils/season.ts'

const settings = {
  votes: {},
  startingMoney: 1000,
  delayedDay: 2,
  cancelledDay: 1,
  pointForFirstGoal: 66,
  pointForNextGoal: 6,
  pointForOwnGoal: 6,
  differencePointForOwnGoal: 6,
  pointInHome: 0,
  pointForVictory: 3,
  pointForDefeat: 0,
  pointForDraw: 1,
  pointForStrongDefense: 2,
  pointForStrongDefense4: 4,
  pointForStrongDefense5: 6,
  pointForGoodPeople: 2,
  pointForCleanSheet: 1,
  moneyForGoal: 5,
  moneyForSufferedGoal: 3,
  randomAuction: false,
  rankWithValuePoints: false,
  market: 0
}

test('creates editable defaults for a missing league year', () => {
  const league = {
    id: 'serie-a',
    name: 'Serie A',
    isMain: true,
    type: 1,
    years: [],
    basketsId: []
  }

  const annualLeague = getAnnualLeagueForYear(league, 15, settings)

  assert.equal(annualLeague.year, 15)
  assert.equal(annualLeague.type, 1)
  assert.deepEqual(annualLeague.settings, settings)
  assert.notStrictEqual(annualLeague.settings, settings)
})

test('copies settings to a new year without mutating the source year', () => {
  const source = { year: 14, type: 2, settings }
  const copied = copyAnnualLeagueToYear(source, 15, 1)

  copied.settings.startingMoney = 750

  assert.equal(copied.year, 15)
  assert.equal(copied.type, 2)
  assert.equal(source.settings.startingMoney, 1000)
})

test('resolves an inherited source type when copying to a new year', () => {
  const source = { year: 14, type: 0, settings }

  assert.equal(copyAnnualLeagueToYear(source, 15, 4).type, 4)
})

test('upserts an annual league without creating duplicate years', () => {
  const previous = { year: 14, type: 1, settings }
  const current = { year: 15, type: 2, settings }
  const updated = upsertAnnualLeague([previous, current], { ...current, type: 4 })

  assert.equal(updated.length, 2)
  assert.equal(updated.find(year => year.year === 15)?.type, 4)
})

test('copies only missing teams and clones co-owner arrays', () => {
  const existing = [{ name: 'Existing', owner: 'one@example.com', additionalOwners: [] }]
  const source = [
    { name: 'One', owner: 'one@example.com', additionalOwners: [] },
    { name: 'Two', owner: 'two@example.com', additionalOwners: ['three@example.com'] }
  ]

  const copied = copyMissingTeams(source, existing)
  copied[0].additionalOwners.push('four@example.com')

  assert.deepEqual(copied.map(team => team.owner), ['two@example.com'])
  assert.deepEqual(source[1].additionalOwners, ['three@example.com'])
})

test('preserves backend-only settings while updating editable values', () => {
  const settingWithRawFields = {
    ...settings,
    startingMoney: 750,
    raw: {
      frm: 1,
      lt: { custom: 'backend-only' }
    }
  }

  const raw = preserveRawLeagueSetting(settingWithRawFields, {})

  assert.equal(raw.s, 750)
  assert.equal(raw.frm, 1)
  assert.deepEqual(raw.lt, { custom: 'backend-only' })
})

test('uses the same UTC season boundary as the backend', () => {
  assert.equal(getCurrentSeasonYear(new Date('2026-08-09T23:59:59Z')), 14)
  assert.equal(getCurrentSeasonYear(new Date('2026-08-10T00:00:00Z')), 15)
})
