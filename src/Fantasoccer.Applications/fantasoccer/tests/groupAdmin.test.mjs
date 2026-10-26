import assert from 'node:assert/strict'
import test from 'node:test'
import {
  copyAnnualLeagueToYear,
  copyMissingTeams,
  getAnnualLeagueForYear,
  isLeagueSettingValid,
  cloneLeagueSetting,
  upsertAnnualLeague
} from '../utils/groupAdmin.ts'
import { getCurrentSeasonYear } from '../utils/season.ts'

const settings = {
  votes: {},
  formation: 0,
  typeSettings: null,
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
  const completeSettings = {
    ...settings,
    votes: {
      '-1': {
        goal: 3,
        penalty: 3,
        sufferedGoal: -1,
        stoppedPenalty: 3,
        wrongedPenalty: -3,
        ownGoal: -3,
        assist: 1,
        yellowCard: -0.5,
        redCard: -1,
        injury: 0,
        manOfTheMatch: 2
      },
      0: {
        goal: 4,
        penalty: 3,
        sufferedGoal: -1,
        stoppedPenalty: 4,
        wrongedPenalty: -3,
        ownGoal: -3,
        assist: 1,
        yellowCard: -0.5,
        redCard: -1,
        injury: 0,
        manOfTheMatch: 2
      }
    },
    pointForDraw: 2,
    pointForDefeat: -1,
    pointForStrongDefense: 3,
    pointForStrongDefense4: 5,
    pointForStrongDefense5: 7,
    pointForGoodPeople: 4,
    pointForCleanSheet: 2,
    moneyForGoal: 8,
    moneyForSufferedGoal: -2,
    formation: 1,
    typeSettings: {
      calendarType: 0,
      rounds: [{ name: '@', type: 0, fromStart: true, fromRankingStartTeam: null, fromRankingEndTeam: null }],
      numbers: { maxPlayersInTeam: 25, maxGoalKeepersInTeam: 1, maxDefendersInTeam: 5, maxMidfieldersInTeam: 4, maxForwardsInTeam: 2, maxGoalKeepersInBench: 1, maxDefendersInBench: 1, maxMidfieldersInBench: 1, maxForwardsInBench: 1 },
      fromPreviousYear: null,
      cardTrainer: { maxCardsPerType: { 0: 2 } }
    }
  }
  const source = { year: 14, type: 2, settings: completeSettings }
  const copied = copyAnnualLeagueToYear(source, 15, 1)

  copied.settings.startingMoney = 750
  copied.settings.votes[0].goal = 9
  copied.settings.typeSettings.rounds[0].name = 'changed'

  assert.equal(copied.year, 15)
  assert.equal(copied.type, 2)
  assert.equal(source.settings.startingMoney, 1000)
  assert.equal(source.settings.votes[0].goal, 4)
  assert.equal(source.settings.typeSettings.rounds[0].name, '@')
  assert.equal(copied.settings.pointForStrongDefense5, 7)
  assert.equal(copied.settings.moneyForSufferedGoal, -2)
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

test('clones descriptive DTOs without a compact shadow object', () => {
  const descriptive = {
    ...settings,
    startingMoney: 750,
    formation: 1,
    futureRule: { custom: 'backend-only' }
  }

  const dto = cloneLeagueSetting(descriptive)
  assert.equal(dto.startingMoney, 750)
  assert.equal(dto.formation, 1)
  assert.deepEqual(dto.futureRule, { custom: 'backend-only' })
  assert.equal('raw' in dto, false)
})

test('sends every editable setting and vote value using descriptive JSON', () => {
  const complete = {
    ...settings,
    votes: {
      '-1': {
        goal: 3,
        penalty: 3,
        sufferedGoal: -1,
        stoppedPenalty: 3,
        wrongedPenalty: -3,
        ownGoal: -3,
        assist: 1,
        yellowCard: -0.5,
        redCard: -1,
        injury: 0,
        manOfTheMatch: 2
      },
      1: {
        goal: 2.5,
        penalty: 3,
        sufferedGoal: 0,
        stoppedPenalty: 0,
        wrongedPenalty: -3,
        ownGoal: -3,
        assist: 1.5,
        yellowCard: -0.5,
        redCard: -1,
        injury: -1,
        manOfTheMatch: 2
      }
    },
    formation: 1,
    typeSettings: {
      calendarType: 0, rounds: [],
      numbers: { maxPlayersInTeam: 25, maxGoalKeepersInTeam: 1, maxDefendersInTeam: 5, maxMidfieldersInTeam: 4, maxForwardsInTeam: 2, maxGoalKeepersInBench: 1, maxDefendersInBench: 1, maxMidfieldersInBench: 1, maxForwardsInBench: 1 },
      fromPreviousYear: null, cardTrainer: { maxCardsPerType: {} }
    },
    pointForDraw: 2,
    pointForDefeat: -1,
    pointForStrongDefense: 3,
    pointForStrongDefense4: 5,
    pointForStrongDefense5: 7,
    pointForGoodPeople: 4,
    pointForCleanSheet: 2,
    moneyForGoal: 8,
    moneyForSufferedGoal: -2
  }

  const dto = JSON.parse(JSON.stringify(complete))
  assert.equal(dto.formation, 1)
  assert.deepEqual(dto.typeSettings, complete.typeSettings)
  assert.equal(dto.pointForDraw, 2)
  assert.equal(dto.pointForDefeat, -1)
  assert.equal(dto.pointForStrongDefense, 3)
  assert.equal(dto.pointForStrongDefense4, 5)
  assert.equal(dto.pointForStrongDefense5, 7)
  assert.equal(dto.pointForGoodPeople, 4)
  assert.equal(dto.pointForCleanSheet, 2)
  assert.equal(dto.moneyForGoal, 8)
  assert.equal(dto.moneyForSufferedGoal, -2)
  assert.equal(dto.votes['-1'].goal, 3)
  assert.equal(dto.votes['1'].assist, 1.5)
})

test('uses the same UTC season boundary as the backend', () => {
  assert.equal(getCurrentSeasonYear(new Date('2026-08-09T23:59:59Z')), 14)
  assert.equal(getCurrentSeasonYear(new Date('2026-08-10T00:00:00Z')), 15)
})

test('rejects settings that can break calendar or vote calculations', () => {
  const fallbackVote = {
    goal: 3,
    penalty: 3,
    sufferedGoal: -1,
    stoppedPenalty: 3,
    wrongedPenalty: -3,
    ownGoal: -3,
    assist: 1,
    yellowCard: -0.5,
    redCard: -1,
    injury: 0,
    manOfTheMatch: 2
  }
  const valid = { ...settings, votes: { '-1': fallbackVote } }

  assert.equal(isLeagueSettingValid(valid), true)
  assert.equal(isLeagueSettingValid({ ...valid, pointForNextGoal: 0 }), false)
  assert.equal(isLeagueSettingValid({ ...valid, delayedDay: 38 }), false)
  assert.equal(isLeagueSettingValid({ ...valid, votes: {} }), false)
})
