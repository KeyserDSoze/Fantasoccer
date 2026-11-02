import assert from 'node:assert/strict'
import { createRequire } from 'node:module'
import { readFileSync } from 'node:fs'
import test from 'node:test'

const require = createRequire(import.meta.url)
const { loadTypeScript } = require('./helpers/loadTypeScript.cjs')
const source = file => readFileSync(new URL(`../${file}`, import.meta.url), 'utf8')

// Inspect the JSX returned by the real widgets at both layout sizes. Native/UI
// boundaries are stubbed; data-state branching and button handlers are real.
function widgets(width) {
  const jsx = (type, props) => ({ type, props })
  const component = name => name
  const ui = Object.fromEntries(['Button', 'Card', 'ScrollView', 'Separator', 'Spinner', 'Text', 'XStack', 'YStack'].map(name => [name, component(name)]))
  const mocks = {
    react: { useMemo: fn => fn(), useEffect: () => {}, useState: value => [typeof value === 'function' ? value() : value, () => {}] },
    'react/jsx-runtime': { jsx, jsxs: jsx, Fragment: 'Fragment' },
    'react-native': { Platform: { OS: 'ios' }, useWindowDimensions: () => ({ width }) },
    tamagui: { ...ui, useMedia: () => ({ gtMd: width > 900 }) },
    '@tamagui/lucide-icons-2': { AlertTriangle: 'AlertTriangle', CalendarRange: 'CalendarRange', RefreshCw: 'RefreshCw', Info: 'Info' },
    '@react-navigation/native': { useNavigation: () => ({}) },
    hooks: { useCurrentGroup: () => ({}) },
    'providers/AppProvider': { useApp: () => ({}) },
    '../team/TeamLogo': { TeamLogo: 'TeamLogo' },
    './LuckDetailModal': { LuckDetailModal: 'LuckDetailModal' },
    'utils/luckCalculator': { LuckCalculator: {} },
  }
  return {
    calendar: loadTypeScript('components/calendar/LeagueCalendarWidget.tsx', mocks).LeagueCalendarWidget,
    rank: loadTypeScript('components/rank/LeagueRankingWidget.tsx', mocks).LeagueRankingWidget,
  }
}

function buttons(node) {
  if (!node || typeof node !== 'object') return []
  if (Array.isArray(node)) return node.flatMap(buttons)
  return [...(node.type === 'Button' ? [node] : []), ...buttons(node.props?.children)]
}

function text(node) {
  if (typeof node === 'string' || typeof node === 'number') return String(node)
  if (!node || typeof node !== 'object') return ''
  if (Array.isArray(node)) return node.map(text).join(' ')
  return text(node.props?.children)
}

for (const width of [390, 1280]) {
  test(`calendar and standings have no reload buttons when loaded or empty (${width}px)`, () => {
    const { calendar, rank } = widgets(width)
    const retry = () => { throw new Error('not a refresh action') }
    const days = [{ number: 1, serieADay: 1, games: [] }]
    const teams = [{ name: 'Test', owner: 'owner', point: 3, victories: 1, draws: 0, defeats: 0, goal: 1, sufferedGoal: 0, valuePoint: 70, sufferedValuePoint: 60, plusMoney: 0, money: null }]
    for (const tree of [calendar({ days, onRetry: retry }), calendar({ days: [], onRetry: retry }),
      rank({ rankedTeams: teams, onRetry: retry }), rank({ rankedTeams: [], onRetry: retry })]) {
      assert.equal(buttons(tree).filter(button => button.props.onPress === retry).length, 0)
      for (const button of buttons(tree)) assert.doesNotMatch(text(button), /Aggiorna|Ricarica|Riprova/)
    }
  })

  test(`retry remains available only after a failed request (${width}px)`, () => {
    const { calendar, rank } = widgets(width)
    let retries = 0
    const retry = () => { retries++ }
    for (const tree of [calendar({ days: [], error: 'Offline', onRetry: retry }), rank({ rankedTeams: [], error: 'Offline', onRetry: retry })]) {
      const actions = buttons(tree).filter(button => button.props.onPress === retry)
      assert.equal(actions.length, 1)
      assert.equal(actions[0].props.children, 'Riprova')
      actions[0].props.onPress()
    }
    assert.equal(retries, 2)
  })
}

test('all screens losing reload controls retain automatic updates', () => {
  for (const [file, expression] of [
    ['screens/live.tsx', /refreshInterval: 15000/],
    ['screens/calendar.tsx', /refreshInterval: 120000/],
    ['screens/ranking.tsx', /refreshInterval: 120000/],
    ['screens/logs.tsx', /refreshInterval: 60000/],
    ['screens/players.tsx', /autoRefresh: true/],
    ['screens/push-notification.tsx', /useCurrentUserPushNotifications\(\{ autoRefresh: true \}\)/],
    ['screens/home.tsx', /enablePolling pollOnlyWhenLive=\{false\}/],
  ]) {
    const text = source(file)
    assert.match(text, expression, file)
    assert.doesNotMatch(text, /refreshLabel=|>\s*(Aggiorna|Ricarica)\s*<\//, file)
  }
  const players = source('screens/players.tsx')
  assert.match(players, /setInterval\(\(\) => \{ void loadTeams\(\) \}, 120000\)/)
  assert.match(players, /clearInterval\(interval\)/)
  assert.match(players, /request === teamsRequest\.current/)
  assert.match(players, /teamsRequest\.current\+\+/)
})

test('post-save reloads and non-polling editors keep their required actions', () => {
  assert.match(source('components/lineup/SoccerField.tsx'), /await onRefresh\(\)/)
  assert.match(source('screens/serie-a-admin.tsx'), /enablePolling: false/)
  assert.match(source('screens/serie-a-admin.tsx'), /Ricarica/)
  assert.match(source('components/ServiceWorkerUpdateNotification.tsx'), /Aggiorna Ora/)
})
