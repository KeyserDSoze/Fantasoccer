import assert from 'node:assert/strict'
import { createRequire } from 'node:module'
import test from 'node:test'
import { loadTypeScript } from './helpers/loadTypeScript.cjs'

const require = createRequire(import.meta.url)
const React = require('react')
const runtime = require('react/jsx-runtime')
const { renderToStaticMarkup } = require('react-dom/server')
const element = tag => ({ children }) => React.createElement(tag, null, children)
const tags = { YStack: 'div', XStack: 'div', Card: 'section', H1: 'h1', H2: 'h2', H3: 'h3', Paragraph: 'p', Text: 'span', Button: 'button', Input: 'input', Label: 'label', Spinner: 'span' }
const tamagui = Object.fromEntries(Object.entries(tags).map(([name, tag]) => [name, element(tag)]))
const icons = Object.fromEntries(['Trophy', 'SlidersHorizontal', 'Users', 'Gavel'].map(name => [name, () => null]))
const user = { email: 'friend@example.test', groups: [], isGlobalAdministrator: false }
const componentMocks = {
  react: React, 'react/jsx-runtime': runtime, tamagui, '@tamagui/lucide-icons-2': icons,
  '@react-navigation/native': { useNavigation: () => ({ navigate() {} }) },
  '../../providers/AuthProvider': { useAuth: () => ({ user, isAuthenticated: true }) },
  '../../providers/AppProvider': { useApp: () => ({ refreshGroups() {} }) },
  '../../services/workspaceService': { workspaceService: { redeemGroupCode: () => assert.fail('Rendering must not redeem a code') } },
  '../../features/LoginButton': { LoginButton: element('button') },
}

test('the no-group landing explains leagues, cups, customizable rules and the real code-only activation without pretending to charge', () => {
  const { GroupCreationContent } = loadTypeScript('components/groups/GroupCreationContent.tsx', componentMocks)
  const html = renderToStaticMarkup(React.createElement(GroupCreationContent))
  assert.match(html, /Il tuo fantacalcio\. Le tue regole\./)
  assert.match(html, /più leghe, campionati e coppe/)
  assert.match(html, /bonus, malus, moduli, cambi/)
  assert.match(html, /costo annuale/)
  assert.match(html, /non sono ancora disponibili/)
  assert.match(html, /senza addebiti o rinnovi automatici/)
  assert.match(html, /non può essere riutilizzato/)
  assert.doesNotMatch(html, /Super admin · Genera codici/)
})

test('a global administrator without groups gets the code management shortcut; ordinary visitors never get the platform console', () => {
  const { GroupCreationContent } = loadTypeScript('components/groups/GroupCreationContent.tsx', {
    ...componentMocks,
    '../../providers/AuthProvider': { useAuth: () => ({ user: { ...user, isGlobalAdministrator: true }, isAuthenticated: true }) },
  })
  assert.match(renderToStaticMarkup(React.createElement(GroupCreationContent)), /Super admin · Genera codici/)
  const page = loadTypeScript('screens/platform-admin.tsx', {
    react: React, 'react/jsx-runtime': runtime, tamagui,
    '../components/layout/ScreenContent': { ScreenContent: element('main') },
    '../providers/AuthProvider': { useAuth: () => ({ user }) },
    '../services/platformService': { platformService: new Proxy({}, { get: () => assert.fail('No API calls for non-admins') }) },
  })
  const html = renderToStaticMarkup(React.createElement(page.default))
  assert.match(html, /riservata al super admin/)
  assert.doesNotMatch(html, /Genera un codice/)
})

test('home presentation points at the creation page instead of redeeming while the home widgets are being replaced', () => {
  const { GroupCreationContent } = loadTypeScript('components/groups/GroupCreationContent.tsx', componentMocks)
  const html = renderToStaticMarkup(React.createElement(GroupCreationContent, { showForm: false }))
  assert.match(html, /Ho un codice · Crea il gruppo/)
  assert.doesNotMatch(html, /Nome del gruppo<\/label>/)
})

test('platform menu is independent of memberships and the group owner role never reveals platform management', () => {
  const Sheet = element('div')
  Sheet.Overlay = () => null
  Sheet.Frame = element('div')
  let globalAdmin = false, inGroup = false
  const { NavigationBar } = loadTypeScript('components/layout/NavigationBar.tsx', {
    react: React, 'react/jsx-runtime': runtime,
    'react-native': { Platform: { OS: 'web' } },
    tamagui: { ...tamagui, Separator: element('hr'), ScrollView: element('div'), Sheet, useMedia: () => ({ sm: false }) },
    '@tamagui/lucide-icons-2': new Proxy({}, { get: () => () => null }),
    '@react-navigation/native': { useNavigation: () => ({ navigate() {} }) },
    'providers/AuthProvider': { useAuth: () => ({ user: { ...user, isGlobalAdministrator: globalAdmin }, logout() {} }) },
    'providers/AppProvider': { useApp: () => ({ currentGroup: inGroup ? { name: 'Group', users: [{ email: user.email, role: 8 }], leagues: [] } : null,
      userGroups: inGroup ? [{}] : [], hasLoadedGroups: true, isGroupsLoading: false, groupsError: null,
      getAvailableLeagues: () => [], getAvailableYears: () => [] }) },
    './SwitchThemeButton': { SwitchThemeButton: () => null },
    'config/version': { APP_VERSION: 'fixture' },
    'utils/season': { formatSeasonFromYear: () => '' },
    'providers/MarketActivityProvider': { useMainLeagueMarketActivity: () => ({ activeMarketCount: 0 }) },
  }, { process: { env: {} } })
  let html = renderToStaticMarkup(React.createElement(NavigationBar))
  assert.doesNotMatch(html, /Classifica della tua lega|Utenti, gruppi e codici/)
  globalAdmin = true
  html = renderToStaticMarkup(React.createElement(NavigationBar))
  assert.match(html, /Utenti, gruppi e codici/)
  assert.doesNotMatch(html, /Classifica della tua lega/)
  globalAdmin = false; inGroup = true
  html = renderToStaticMarkup(React.createElement(NavigationBar))
  assert.match(html, /Crea un nuovo gruppo/)
  assert.doesNotMatch(html, /Utenti, gruppi e codici/)
})

test('activation rejects double clicks locally and refreshes group membership only after successful creation', async () => {
  const cells = [], refs = []
  let cursor = 0, refCursor = 0, calls = 0, refreshes = 0, finish
  const state = {
    useState(initial) { const index = cursor++; if (!(index in cells)) cells[index] = initial; return [cells[index], value => { cells[index] = value }] },
    useRef(initial) { const index = refCursor++; return refs[index] ??= { current: initial } },
    useEffect() {},
  }
  const jsx = (type, props) => ({ type, props })
  const module = loadTypeScript('components/groups/GroupCreationContent.tsx', {
    ...componentMocks,
    react: state, 'react/jsx-runtime': { jsx, jsxs: jsx }, tamagui: Object.fromEntries(Object.keys(tags).map(name => [name, name])),
    '../../providers/AppProvider': { useApp: () => ({ refreshGroups: async () => { refreshes++ } }) },
    '../../services/workspaceService': { workspaceService: { redeemGroupCode: async (name, code) => {
      calls++; assert.equal(name, 'Friends'); assert.equal(code, 'FZ-private-code');
      return new Promise(resolve => { finish = resolve })
    } } },
  }, { AbortController })
  function render() { cursor = 0; refCursor = 0; return module.GroupCreationContent({}) }
  function find(tree, predicate) {
    if (!tree || typeof tree !== 'object') return undefined
    if (predicate(tree)) return tree
    for (const item of [tree.props?.children].flat(Infinity)) { const found = find(item, predicate); if (found) return found }
  }
  render()
  cells[0] = ' Friends '; cells[1] = ' FZ-private-code '
  const button = find(render(), node => node.type === 'Button' && node.props.children === 'Crea il mio gruppo')
  button.props.onPress(); button.props.onPress()
  assert.equal(calls, 1); assert.equal(refreshes, 0)
  finish({ groupId: 'new-group' })
  await new Promise(resolve => setImmediate(resolve))
  assert.equal(refreshes, 1); assert.equal(cells[1], ''); assert.equal(cells[4], true)
})
