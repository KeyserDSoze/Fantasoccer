import assert from 'node:assert/strict'
import { createRequire } from 'node:module'
import test from 'node:test'
const require = createRequire(import.meta.url)
const { loadTypeScript } = require('./helpers/loadTypeScript.cjs')
const { homeMatchDisplay } = loadTypeScript('utils/homeMatchDisplay.ts')
const previous = { serieADay: 9, number: 7, games: [{ id: 'old', result: { homeGoals: 1, awayGoals: 0 } }] }
const current = { serieADay: 10, number: 8, games: [{ id: 'current', result: null }] }
const rank = { serieADay: 10, rounds: {} }
const live = { year: 15, serieADay: 10, isInLiveWindow: true, isPlaying: true,
  leagues: [{ id: 'league', rounds: { '@': current }, rank, isConsolidated: false }] }

test('home uses actual Live snapshot and Live standings inside the server window', () => {
  const display = homeMatchDisplay([previous, current], live, 'league', 15)
  assert.equal(display.isLive, true)
  assert.equal(display.days[0], current)
  assert.equal(display.rank, rank)
  assert.equal(display.pulseResults, true)
})
test('two hours before kickoff and after the games the Live window has no score animation', () => {
  const display = homeMatchDisplay([previous], { ...live, isPlaying: false }, 'league', 15)
  assert.equal(display.isLive, true)
  assert.equal(display.pulseResults, false)
})
test('consolidated results return home to the latest played day even inside the time window', () => {
  const display = homeMatchDisplay([previous, { ...current, games: [{ result: {} }] }], {
    ...live, leagues: [{ ...live.leagues[0], isConsolidated: true }],
  }, 'league', 15)
  assert.equal(display.isLive, false)
  assert.equal(display.serieADay, 10)
  assert.equal(display.pulseResults, false)
})
test('outside the window home ignores future days and displays all rounds of the latest played day', () => {
  const display = homeMatchDisplay([previous, { ...previous, number: 8 }, current], { ...live, isInLiveWindow: false }, 'league', 15)
  assert.equal(display.days.length, 2)
  assert.equal(display.serieADay, 9)
  assert.equal(display.isLive, false)
})
test('different years/leagues, missing data and a cold Live cache never invent a match', () => {
  assert.equal(homeMatchDisplay([previous], live, 'other', 15).isLive, false)
  assert.equal(homeMatchDisplay([previous], live, 'league', 14).isLive, false)
  assert.equal(homeMatchDisplay([], null, 'league', 15).days.length, 0)
  const cold = homeMatchDisplay([previous], { ...live, leagues: [{ ...live.leagues[0], rounds: {}, rank: null }] }, 'league', 15)
  assert.equal(cold.isLive, false)
  assert.equal(cold.days[0], previous)
})

test('home requests the selected season, not the season saved in the login profile', async () => {
  let profileCalls = 0
  const { liveGroupService } = loadTypeScript('services/liveGroupService.ts', {
    './tokenStorageService': { tokenStorageService: { ensureValidToken: async () => 'token' } },
    './authService': { authService: { getUserInfo: async () => { profileCalls++; return { state: { year: 15 } } } } },
    './httpClient': { apiRequest: async path => path },
  })
  assert.match(await liveGroupService.getLiveGroup('group', { year: 14 }), /year=14$/)
  assert.equal(profileCalls, 0)
  assert.match(await liveGroupService.getLiveGroup('group'), /year=15$/)
  assert.equal(profileCalls, 1)
})
