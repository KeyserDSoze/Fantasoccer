import assert from 'node:assert/strict'
import { createRequire } from 'node:module'
import test from 'node:test'

const require = createRequire(import.meta.url)
const { loadTypeScript } = require('./helpers/loadTypeScript.cjs')

const at = offset => new Date(Date.now() + offset).toISOString()
const minute = 60 * 1000, day = 24 * 60 * minute
const game = (home, date, extra = {}) => ({ home: { name: home, abbreviation: home }, away: { name: `${home} away`, abbreviation: '' }, date,
  homeGoals: null, awayGoals: null, delayed: false, ...extra })

test('a recovery played months later is live on its own without reopening its original matchday', () => {
  const { RealCalendarHelper } = loadTypeScript('types/serieA.ts')
  const recovery = game('cag', at(-30 * minute), { wasPostponed: true, isFinished: false, originalDate: at(-60 * day) })
  const calendar = { year: 15, days: [
    { year: 15, serieADay: 26, games: [game('mil', at(-60 * day), { isFinished: true }), recovery] },
    { year: 15, serieADay: 27, games: [game('int', at(-53 * day), { isFinished: true })] },
    { year: 15, serieADay: 28, games: [game('nap', at(2 * day))] },
  ] }
  assert.equal(RealCalendarHelper.getLiveDay(calendar), null)
  assert.equal(RealCalendarHelper.getLastDay(calendar).serieADay, 27)
  assert.equal(RealCalendarHelper.getNextDay(calendar).serieADay, 28)
  assert.deepEqual(RealCalendarHelper.getLiveGames(calendar).map(x => x.home.name), ['cag'])
  assert.equal(RealCalendarHelper.isLive(calendar), true)

  // Still waiting for a date: never live and never part of the day window.
  const waiting = { ...calendar, days: [{ ...calendar.days[0], games: [calendar.days[0].games[0], { ...recovery, date: null, delayed: true }] }, ...calendar.days.slice(1)] }
  assert.deepEqual(RealCalendarHelper.getLiveGames(waiting), [])
  assert.equal(RealCalendarHelper.getNextDay(waiting).serieADay, 28)
})

test('league settings default to closing postponed games with a political 6 for every role', () => {
  const { DefaultLeagueSetting } = loadTypeScript('types/group.ts')
  assert.deepEqual({ ...DefaultLeagueSetting.postponements }, { waitForRecovery: false, goalkeeper: 6, defender: 6, midfielder: 6, forward: 6 })
})
