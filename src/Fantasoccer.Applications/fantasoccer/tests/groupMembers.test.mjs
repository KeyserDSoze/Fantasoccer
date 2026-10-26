import assert from 'node:assert/strict'
import { createRequire } from 'node:module'
import test from 'node:test'
import { loadTypeScript } from './helpers/loadTypeScript.cjs'

const require = createRequire(import.meta.url)
const React = require('react')
const runtime = require('react/jsx-runtime')
const { renderToStaticMarkup } = require('react-dom/server')
const tags = { YStack: 'div', XStack: 'div', Card: 'section', H3: 'h3', Paragraph: 'p', Text: 'span', Button: 'button', Input: 'input', Spinner: 'span' }
const element = tag => ({ children }) => React.createElement(tag, null, children)
const member = { userId: 'owner', email: 'owner@example.test', displayName: 'Owner', roles: 12 }

test('readers see only group members; administrators get the exact-email form, not a global user list', () => {
  const { GroupMembersPanel } = loadTypeScript('components/groups/GroupMembersPanel.tsx', {
    react: React, 'react/jsx-runtime': runtime,
    tamagui: Object.fromEntries(Object.entries(tags).map(([name, tag]) => [name, element(tag)])),
    '../../services/workspaceService': { workspaceService: new Proxy({}, { get: () => assert.fail('Rendering must not search or list accounts') }) },
  })
  let html = renderToStaticMarkup(React.createElement(GroupMembersPanel, { groupId: 'group', roles: 2, members: [member], onChanged() {} }))
  assert.match(html, /owner@example.test/)
  assert.doesNotMatch(html, /Cerca email/)
  html = renderToStaticMarkup(React.createElement(GroupMembersPanel, { groupId: 'group', roles: 4, members: [member], onChanged() {} }))
  assert.match(html, /Cerca email/)
  assert.match(html, /non sfogliare tutti gli utenti/)
})

test('email lookup adds a participant only to the chosen group and ignores stale lookup responses after the email changes', async () => {
  const cells = [], refs = []
  let cursor = 0, refCursor = 0, searches = 0, changes = 0, saves = [], resolveSearch
  const state = {
    useState(initial) { const index = cursor++; if (!(index in cells)) cells[index] = initial; return [cells[index], value => { cells[index] = typeof value === 'function' ? value(cells[index]) : value }] },
    useRef(initial) { const index = refCursor++; return refs[index] ??= { current: initial } },
    useEffect() {},
  }
  const jsx = (type, props) => ({ type, props })
  const { GroupMembersPanel } = loadTypeScript('components/groups/GroupMembersPanel.tsx', {
    react: state, 'react/jsx-runtime': { jsx, jsxs: jsx }, tamagui: Object.fromEntries(Object.keys(tags).map(name => [name, name])),
    '../../services/workspaceService': { workspaceService: {
      lookupMember: async (group, email) => { searches++; assert.equal(group, 'group'); assert.equal(email, 'friend@example.test'); return new Promise(resolve => { resolveSearch = resolve }) },
      setMember: async (group, body) => saves.push({ group, body: JSON.parse(JSON.stringify(body)) }),
    } },
  }, { AbortController })
  function render() { cursor = 0; refCursor = 0; return GroupMembersPanel({ groupId: 'group', roles: 12, members: [member], onChanged: async () => { changes++ } }) }
  function find(tree, predicate) {
    if (!tree || typeof tree !== 'object') return undefined
    if (predicate(tree)) return tree
    for (const child of [tree.props?.children].flat(Infinity)) { const found = find(child, predicate); if (found) return found }
  }
  const field = () => find(render(), node => node.props?.['aria-label'] === 'Email da cercare nel gruppo')
  const button = label => find(render(), node => node.type === 'Button' && node.props.children === label)
  field().props.onChangeText('friend@example.test'); button('Cerca email').props.onPress()
  assert.equal(searches, 1)
  field().props.onChangeText('other@example.test')
  resolveSearch({ email: 'friend@example.test', displayName: 'Friend', found: true, isMember: false })
  await new Promise(resolve => setImmediate(resolve))
  assert.equal(cells[3], null)
  field().props.onChangeText('friend@example.test'); button('Cerca email').props.onPress()
  resolveSearch({ email: 'friend@example.test', displayName: 'Friend', found: true, isMember: false })
  await new Promise(resolve => setImmediate(resolve))
  const save = button('Salva associazione')
  save.props.onPress(); save.props.onPress()
  await new Promise(resolve => setImmediate(resolve))
  assert.equal(changes, 1)
  assert.deepEqual(saves, [{ group: 'group', body: { email: 'friend@example.test', displayName: 'Friend', roles: 2 } }])
})
