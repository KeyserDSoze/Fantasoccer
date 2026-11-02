import assert from 'node:assert/strict'
import { createRequire } from 'node:module'
import test from 'node:test'

const require = createRequire(import.meta.url)
const { loadTypeScript } = require('./helpers/loadTypeScript.cjs')
const settle = () => new Promise(resolve => setImmediate(resolve))
const match = name => ({ game: { home: name, away: 'Away', result: null }, teams: [], serieADay: 1, canEdit: false })

function text(node) {
  if (typeof node === 'string' || typeof node === 'number') return String(node)
  if (!node || typeof node !== 'object') return ''
  if (Array.isArray(node)) return node.map(text).join(' ')
  return text(node.props?.children)
}

// Execute the real screen with a minimal hook lifecycle and controllable network
// promises: the calendar and the game deliberately finish in different orders.
function screen(calendar = { calendar: null, isLoading: false, error: null }) {
  let params = { gameId: 'match-a', groupId: 'group', leagueId: 'league', year: '15', editMode: 'view' }
  let state = [], effects = [], pending = [], stateIndex = 0, effectIndex = 0, key
  const requests = []
  const jsx = (type, props, key) => ({ type, props, key })
  const ui = Object.fromEntries(['YStack', 'XStack', 'Text', 'H3', 'Separator', 'Button', 'Card', 'Spinner'].map(name => [name, name]))
  const { default: GameScreen } = loadTypeScript('screens/game.tsx', {
    react: {
      useMemo: fn => fn(),
      useState: initial => {
        const index = stateIndex++
        if (!(index in state)) state[index] = typeof initial === 'function' ? initial() : initial
        return [state[index], value => { state[index] = typeof value === 'function' ? value(state[index]) : value }]
      },
      useEffect: (callback, deps) => {
        const index = effectIndex++
        const previous = effects[index]
        if (!previous || deps.some((value, i) => !Object.is(value, previous.deps[i]))) {
          effects[index] = { deps, cleanup: previous?.cleanup }
          pending.push(() => { effects[index].cleanup?.(); effects[index].cleanup = callback() })
        }
      },
    },
    'react/jsx-runtime': { jsx, jsxs: jsx, Fragment: 'Fragment' },
    'react-native': { View: 'View' },
    tamagui: { ...ui, useMedia: () => ({ sm: false, gtSm: true, gtMd: true }) },
    '@tamagui/toast': { useToastController: () => ({ show: () => {} }) },
    '@tamagui/lucide-icons-2': { Calculator: 'Calculator' },
    '@react-navigation/native': { useRoute: () => ({ params }), useNavigation: () => ({}) },
    'components/layout/ScreenContent': { ScreenContent: 'ScreenContent' },
    'components/lineup': { SoccerField: 'SoccerField' },
    'components/lineup/CopyFormationDialog': { CopyFormationDialog: 'CopyFormationDialog' },
    'components/team/TeamLogo': { TeamLogo: 'TeamLogo' },
    'components/icons/BonusIcons': { TeamBonuses: 'TeamBonuses' },
    'hooks/useSerieACalendar': { useSerieACalendar: () => calendar },
    'hooks/useFieldZoom': { useFieldZoom: () => ({ zoomLevel: 1 }) },
    'services/gameService': { __esModule: true, default: { getGame: () => new Promise((resolve, reject) => requests.push({ resolve, reject })) } },
    'services/leagueManagerService': { leagueManagerService: {} },
    'services/statPlayerService': { statPlayerService: { getCurrentUserEnhancedWrapper: async () => null } },
    'utils/statPlayerCalculator': { createStatPlayerMap: () => new Map() },
    'utils/timezoneHelpers': { formatItalianDate: value => String(value) },
    '../providers/AuthProvider': { useAuth: () => ({ user: null }) },
    '../providers/AppProvider': { useApp: () => ({ currentGroup: null, superAdminMode: false }) },
    '../types/identity': { IdentityRole: {}, hasRole: () => false },
  }, { setInterval: () => 1, clearInterval: () => {} })
  return {
    requests,
    navigate: next => { params = { ...params, ...next } },
    render: () => {
      const page = GameScreen()
      if (page.key !== key) {
        for (const effect of effects) effect.cleanup?.()
        state = []; effects = []; pending = []; key = page.key
      }
      stateIndex = 0; effectIndex = 0
      return text(page.type())
    },
    effects: () => { const queue = pending; pending = []; for (const effect of queue) effect() },
  }
}

test('a fast calendar cannot display an empty match while the game request is pending', async () => {
  const app = screen()
  assert.match(app.render(), /Caricamento partita/)
  app.effects()
  assert.match(app.render(), /Caricamento partita/)
  assert.doesNotMatch(app.render(), /Nessuna partita/)
  app.requests[0].resolve(match('Home'))
  await settle()
  assert.match(app.render(), /Home/)
  assert.doesNotMatch(app.render(), /Caricamento partita|Nessuna partita/)
})

test('a slow or failed calendar cannot hide a loaded game', async () => {
  for (const calendar of [{ isLoading: true }, { error: 'Calendar error' }]) {
    const app = screen(calendar)
    app.render(); app.effects()
    app.requests[0].resolve(match('Loaded home'))
    await settle()
    assert.match(app.render(), /Loaded home/)
    assert.doesNotMatch(app.render(), /Caricamento partita|Calendar error|Nessuna partita/)
  }
})

test('game request errors are rendered instead of the empty-state message', async () => {
  const app = screen()
  app.render(); app.effects()
  app.requests[0].reject(new Error('Game unavailable'))
  await settle()
  assert.match(app.render(), /Game unavailable/)
  assert.doesNotMatch(app.render(), /Caricamento partita|Nessuna partita/)
})

test('empty state appears only after a successful empty response', async () => {
  const app = screen()
  assert.doesNotMatch(app.render(), /Nessuna partita/)
  app.effects()
  app.requests[0].resolve(null)
  await settle()
  assert.match(app.render(), /Nessuna partita disponibile/)
})

test('changing matches resets the loader and ignores the previous request response', async () => {
  const app = screen()
  app.render(); app.effects()
  app.navigate({ gameId: 'match-b' })
  assert.match(app.render(), /Caricamento partita/)
  app.effects()
  app.requests[0].resolve(match('Old match'))
  await settle()
  assert.match(app.render(), /Caricamento partita/)
  assert.doesNotMatch(app.render(), /Old match/)
  app.requests[1].resolve(match('New match'))
  await settle()
  assert.match(app.render(), /New match/)
})

test('incomplete routes show an explicit error and do not issue a game request', () => {
  const app = screen()
  app.navigate({ gameId: null })
  app.render(); app.effects()
  assert.match(app.render(), /Parametri della partita incompleti/)
  assert.equal(app.requests.length, 0)
})
