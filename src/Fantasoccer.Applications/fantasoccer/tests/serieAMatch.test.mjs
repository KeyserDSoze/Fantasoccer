import assert from 'node:assert/strict'
import { createRequire } from 'node:module'
import { readFileSync } from 'node:fs'
import test from 'node:test'

const require = createRequire(import.meta.url)
const { loadTypeScript } = require('./helpers/loadTypeScript.cjs')
const { matchRefreshInterval, toFieldGame, lineupNote, defaultDay, seasonRefreshInterval, rankAtDay } = loadTypeScript('utils/serieAMatch.ts')
const { playerEvents, sortByImportance, voteBand, formatVote } = loadTypeScript('utils/matchEvents.ts')
const { RealCalendarHelper } = loadTypeScript('types/serieA.ts')

const HOUR = 3600 * 1000
const eq = (actual, expected) => assert.equal(JSON.stringify(actual), JSON.stringify(expected))
const kickoff = Date.parse('2027-01-10T14:00:00Z')
const game = (overrides = {}) => ({ ordinal: 0, home: { name: 'A' }, away: { name: 'B' }, date: new Date(kickoff).toISOString(), homeGoals: null, awayGoals: null, delayed: false, ...overrides })
const vote = (overrides = {}) => ({ role: 0, value: 6, isFinal: false, goals: 0, penalties: 0, assists: 0, stoppedPenalties: 0, sufferedGoals: 0,
  wrongedPenalties: 0, ownGoals: 0, status: 0, manOfTheMatch: false, hasVote: true, isOut: false, isIn: false, injured: false, ...overrides })

test('a score pulses only while the match is being played, with the same rule as the server', () => {
  const playing = now => RealCalendarHelper.isGamePlaying(game(), now)
  assert.equal(playing(kickoff - 1), false)
  assert.equal(playing(kickoff), true)
  assert.equal(playing(kickoff + 134 * 60000), true)
  assert.equal(playing(kickoff + 136 * 60000), false)
  assert.equal(RealCalendarHelper.isGamePlaying(game({ isFinished: false }), kickoff + 200 * 60000), true)
  assert.equal(RealCalendarHelper.isGamePlaying(game({ isFinished: false }), kickoff + 241 * 60000), false)
  assert.equal(RealCalendarHelper.isGamePlaying(game({ isFinished: true }), kickoff + 60000), false)
  assert.equal(RealCalendarHelper.isGamePlaying(game({ delayed: true }), kickoff + 60000), false)
  assert.equal(RealCalendarHelper.isGamePlaying(game({ date: null }), kickoff), false)
})

test('a match page refreshes quickly around the match, slowly before and after, never when it is old', () => {
  const at = offset => matchRefreshInterval({ game: game() }, kickoff + offset)
  assert.equal(at(-4 * HOUR), 5 * 60000)
  assert.equal(at(-2 * HOUR), 15000)
  assert.equal(at(4 * HOUR), 15000)
  assert.equal(at(10 * HOUR), 120000)
  assert.equal(at(40 * HOUR), null)
  assert.equal(at(-72 * HOUR), null)
  assert.equal(matchRefreshInterval(null), null)
  assert.equal(matchRefreshInterval({ game: game({ date: null }) }), 300000)
})

test('a season refreshes while a match may be running or others are due, and stops once it is over', () => {
  const season = days => ({ year: 1, days: [{ year: 1, serieADay: 1, games: days.map(date => game({ date: new Date(date).toISOString() })) }] })
  assert.equal(seasonRefreshInterval(season([kickoff]), kickoff + HOUR), 30000)
  assert.equal(seasonRefreshInterval(season([kickoff]), kickoff - 30 * HOUR), 300000)
  assert.equal(seasonRefreshInterval(season([kickoff]), kickoff + 30 * HOUR), 300000)
  assert.equal(seasonRefreshInterval(season([kickoff]), kickoff + 400 * HOUR), null)
  assert.equal(seasonRefreshInterval(null), null)
})

test('the club field is read-only and shows no live-change counters', () => {
  const field = toFieldGame({ lineup: { canEdit: true, isLiveFormationWindow: true, liveFormationDeadline: 'x', liveFormationChangesRemaining: 2,
    teams: [{ name: 'A', canEdit: true, formationChangesMaximum: 0, formationChangesUsed: 0, formationChangesRemaining: 0 }] } })
  assert.equal(field.canEdit, false); assert.equal(field.isLiveFormationWindow, false); assert.equal(field.liveFormationChangesRemaining, null)
  eq([field.teams[0].formationChangesMaximum, field.teams[0].formationChangesRemaining, field.teams[0].canEdit], [null, null, false])
})

test('the lineup note says how the lineup was obtained', () => {
  assert.equal(lineupNote(['official', 'official']), 'Formazione ricostruita dai voti: panchina e tribuna stimate')
  assert.equal(lineupNote(['live', 'official']), 'Formazione ricostruita dai voti: panchina e tribuna stimate')
  assert.equal(lineupNote(['probable', 'probable']), 'Probabili formazioni')
  assert.equal(lineupNote(['unavailable', 'unavailable']), 'Formazione non disponibile')
  assert.match(lineupNote(['official', 'unavailable']), /non disponibile per una delle due/)
})

test('the section opens on the day in progress, else the next one, else the last', () => {
  const day = (serieADay, ...dates) => ({ year: 1, serieADay, games: dates.map(date => game({ date: new Date(date).toISOString(), isFinished: date < Date.now() ? true : null })) })
  const past = Date.now() - 400 * HOUR; const future = Date.now() + 400 * HOUR
  assert.equal(defaultDay({ year: 1, days: [day(1, past), day(2, future)] }), 2)
  assert.equal(defaultDay({ year: 1, days: [day(1, past), day(2, past + HOUR)] }), 2)
  assert.equal(defaultDay({ year: 1, days: [] }), null)
  assert.equal(defaultDay(null), null)
})

test('events are listed by importance with counts, and substitutions only when asked', () => {
  const scorer = vote({ goals: 2, assists: 1, status: 1 })
  eq(playerEvents(scorer).map(e => [e.kind, e.count, e.label]), [['goal', 2, 'Gol'], ['assist', 1, 'Assist'], ['yellowCard', 1, 'Ammonito']])
  eq(playerEvents(vote({ isIn: true })), [])
  eq(playerEvents(vote({ isIn: true }), true).map(e => e.kind), ['in'])
  eq(playerEvents(vote({ ownGoals: 1, sufferedGoals: 2 })).map(e => [e.kind, e.label]), [['ownGoal', 'Autogol'], ['sufferedGoal', 'Gol subiti']])
  const players = [{ name: 'Yellow', vote: vote({ status: 1 }) }, { name: 'Nothing', vote: vote() }, { name: 'Assist', vote: vote({ assists: 1 }) },
    { name: 'Hat-trick', vote: vote({ goals: 3 }) }, { name: 'Goal', vote: vote({ goals: 1 }) }, { name: 'Red', vote: vote({ status: 2 }) }]
  eq(sortByImportance(players).map(p => p.name), ['Hat-trick', 'Goal', 'Assist', 'Red', 'Yellow'])
})

test('the vote is coloured by its value, not by how provisional it is', () => {
  const bands = [[8, 'great'], [7, 'great'], [6.5, 'good'], [6, 'fair'], [5.5, 'low'], [4.5, 'poor']]
  for (const [value, key] of bands) assert.equal(voteBand({ value, hasVote: true }).key, key)
  assert.equal(new Set(bands.map(([value]) => voteBand({ value, hasVote: true }).background)).size, 5)
  assert.equal(voteBand({ value: 6, hasVote: false }).key, 'none')
  assert.equal(voteBand(null).key, 'none')
  assert.equal(formatVote({ value: 6.5, hasVote: true }), '6.5'); assert.equal(formatVote({ value: 7, hasVote: true }), '7.0'); assert.equal(formatVote({ value: 6, hasVote: false }), 's.v.')
})

test('the match page is wired: route, menu, pulsing score and no refresh button', () => {
  const read = path => readFileSync(new URL(`../${path}`, import.meta.url), 'utf8')
  const navigation = read('navigation/index.tsx')
  assert.match(navigation, /SerieAMatch: 'serie-a\/match\/:year\?\/:day\?\/:ordinal\?'/)
  assert.match(navigation, /name="SerieA" component=\{SerieAScreen\}/)
  assert.match(read('components/layout/NavigationBar.tsx'), /route: 'SerieA', label: 'Serie A'/)
  assert.match(read('components/seriea/SerieAMatchRow.tsx'), /<LiveScore active=\{playing\}>/)
  for (const screen of ['screens/serie-a.tsx', 'screens/serie-a-match.tsx']) assert.doesNotMatch(read(screen), /Aggiorna|RefreshCw|refetch/)
  assert.match(read('components/seriea/SerieALiveWidget.tsx'), /navigate\('SerieAMatch'/)
  assert.match(read('screens/serie-a-match.tsx'), /editMode="view"/)
})

test('the standings are rebuilt as they were after the chosen day', () => {
  const t = (name, extra = {}) => ({ name, ...extra })
  const match = (home, away, homeGoals, awayGoals, extra = {}) => game({ home: t(home), away: t(away), homeGoals, awayGoals, isFinished: true, date: new Date(kickoff - 900 * HOUR).toISOString(), ...extra })
  const calendar = { year: 1, days: [
    { year: 1, serieADay: 1, games: [match('A', 'B', 2, 0), match('C', 'D', 1, 1)] },
    { year: 1, serieADay: 2, games: [match('A', 'C', 0, 1), match('B', 'D', 3, 0)] },
    { year: 1, serieADay: 3, games: [match('A', 'D', 1, 1, { isFinished: null, homeGoals: null, awayGoals: null, date: new Date(kickoff + 900 * HOUR).toISOString() }), match('B', 'C', 0, 0, { delayed: true, homeGoals: null, awayGoals: null })] },
  ] }
  const row = (table, name) => JSON.parse(JSON.stringify(table.find(team => team.name === name)))
  const afterOne = rankAtDay(calendar, 1)
  eq(afterOne.map(team => team.name), ['A', 'C', 'D', 'B'])
  eq([row(afterOne, 'A').points, row(afterOne, 'A').played, row(afterOne, 'C').points, row(afterOne, 'B').defeats], [3, 1, 1, 1])
  const afterTwo = rankAtDay(calendar, 2)
  eq(afterTwo.map(team => team.name), ['C', 'B', 'A', 'D'])
  eq([row(afterTwo, 'C').points, row(afterTwo, 'A').goalDifference, row(afterTwo, 'B').goalsFor, row(afterTwo, 'D').played], [4, 1, 3, 2])
  // The last day has only unplayed or postponed matches: nothing is added, but every club is still listed.
  assert.equal(JSON.stringify(rankAtDay(calendar, 3)), JSON.stringify(afterTwo))
  assert.equal(rankAtDay(calendar, 0).every(team => team.played === 0), true)
  // A match in progress is not counted.
  const live = { year: 1, days: [{ year: 1, serieADay: 1, games: [game({ home: t('A'), away: t('B'), homeGoals: 1, awayGoals: 0, date: new Date(Date.now() - 30 * 60000).toISOString() })] }] }
  assert.equal(rankAtDay(live, 1).every(team => team.played === 0), true)
  assert.equal(rankAtDay(null, 1).length, 0)
})
