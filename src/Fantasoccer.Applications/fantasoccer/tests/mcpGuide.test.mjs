import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { createRequire } from 'node:module'
import test from 'node:test'
import { loadTypeScript } from './helpers/loadTypeScript.cjs'

const require = createRequire(import.meta.url)
const React = require('react')
const { renderToStaticMarkup } = require('react-dom/server')
const guide = loadTypeScript('content/mcpGuide.ts')
const apiDirectory = '../../Fantasoccer.Application.Api/Mcp/'

test('the guide documents the six real MCP tools, not a stale handwritten list', () => {
  const actual = ['McpAccountTools.cs', 'McpCatalogTools.cs', 'McpContextTools.cs'].flatMap(file => {
    const source = readFileSync(new URL(apiDirectory + file, import.meta.url), 'utf8')
    return [...source.matchAll(/McpServerTool\(Name = "([^"]+)"/g)].map(match => match[1])
  }).sort()
  assert.deepEqual(Array.from(guide.mcpTools, tool => tool.name).sort(), actual)
  assert.match(guide.mcpTools.find(tool => tool.name === 'execute_api_operation').description, /modificare dati/)
})

test('setup explains automatic OAuth, browser use and account consent without distributing credentials', () => {
  assert.equal(guide.MCP_GUIDE_PATH, 'mcp-guide')
  assert.equal(guide.mcpSetupSteps.length, 6)
  const text = Array.from(guide.mcpSetupSteps, step => step.description).join('\n')
  assert.match(text, /browser|web/)
  assert.match(text, /registrare automaticamente/)
  assert.match(text, /lasciali vuoti/)
  assert.match(text, /account Microsoft/)
  assert.match(text, /piano.*workspace/)
  for (const url of [guide.CHATGPT_PLUGINS_URL, guide.CHATGPT_MCP_HELP_URL]) assert.equal(new URL(url).protocol, 'https:')
})

test('starter prompts explicitly ask for reads or suggestions rather than unconfirmed writes', () => {
  assert.equal(guide.mcpPromptExamples.length, 4)
  for (const example of guide.mcpPromptExamples) {
    assert.match(example.text, /senza modificare|Non modificare|senza salvarla|Non accettare/)
  }
  const faq = Array.from(guide.mcpTroubleshooting, item => item.answer).join('\n')
  assert.match(faq, /401|token MCP valido/)
  assert.match(faq, /Aggiorna o scansiona/)
  assert.match(faq, /non esiste un secret universale/)
})

const icons = Object.fromEntries(['BookOpen', 'ChevronDown', 'ChevronUp', 'ExternalLink', 'Link2', 'ShieldCheck', 'Check', 'Copy', 'Share2'].map(name => [name, () => null]))
const element = tag => ({ children }) => React.createElement(tag, null, children)
const tamagui = {
  YStack: element('div'), XStack: element('div'), Card: element('section'),
  H1: element('h1'), H2: element('h2'), H3: element('h3'), Paragraph: element('p'),
  Text: element('span'), Button: element('button'), Separator: () => React.createElement('hr'),
}

test('documentation renders for an anonymous visitor, uses the configured production endpoint and requires no API reads', () => {
  let authenticated = false
  const copyable = loadTypeScript('components/mcp/CopyableGuideText.tsx', {
    react: React, 'react/jsx-runtime': require('react/jsx-runtime'),
    'react-native': { Platform: { OS: 'web' } }, '@tamagui/lucide-icons-2': icons, tamagui,
  })
  const page = loadTypeScript('screens/mcp-guide.tsx', {
    react: React, 'react/jsx-runtime': require('react/jsx-runtime'),
    'react-native': { Linking: { openURL: () => assert.fail('Rendering must not open links') } },
    '@react-navigation/native': { useNavigation: () => ({ navigate: () => assert.fail('Rendering must not navigate') }) },
    '@tamagui/lucide-icons-2': icons, tamagui,
    'components/layout/ScreenContent': { ScreenContent: element('main') },
    'components/mcp/CopyableGuideText': copyable,
    'configurations/apiConfig': { API_CONFIGS: { production: { baseUrl: 'https://api.production.example' } } },
    'providers/AuthProvider': { useAuth: () => ({ isAuthenticated: authenticated }) },
  })
  const html = renderToStaticMarkup(React.createElement(page.default))
  assert.match(html, /https:\/\/api.production.example\/mcp/)
  assert.match(html, /Leggerla non richiede login/)
  assert.match(html, /Accedi e gestisci gli accessi/)
  assert.match(html, /La revoca non cancella dati già letti/)
  assert.match(html, /25 ottobre 2026/)
  for (const tool of guide.mcpTools) assert.ok(html.includes(tool.name))
  authenticated = true
  const authenticatedHtml = renderToStaticMarkup(React.createElement(page.default))
  assert.match(authenticatedHtml, /Gestisci i collegamenti MCP/)
  assert.doesNotMatch(authenticatedHtml, /Accedi e gestisci gli accessi/)
})

function copyHarness({ clipboard, platform = 'web', share } = {}) {
  const cells = []
  let cursor = 0
  const state = {
    useState(initial) {
      const index = cursor++
      if (!(index in cells)) cells[index] = initial
      return [cells[index], value => { cells[index] = value }]
    },
  }
  const jsx = (type, props) => ({ type, props })
  const module = loadTypeScript('components/mcp/CopyableGuideText.tsx', {
    react: state, 'react/jsx-runtime': { jsx, jsxs: jsx },
    'react-native': { Platform: { OS: platform }, Share: { share } },
    '@tamagui/lucide-icons-2': icons,
    tamagui: { Button: 'button', Text: 'text', YStack: 'stack' },
  }, { navigator: { clipboard } })
  const render = () => {
    cursor = 0
    return module.CopyableGuideText({ value: 'https://api.example/mcp', label: 'URL MCP' })
  }
  const tree = render()
  return { click: () => tree.props.children[1].props.onPress(), render }
}

test('copy writes only the selected text and reports success after the browser accepts it', async () => {
  const writes = []
  const harness = copyHarness({ clipboard: { writeText: async value => writes.push(value) } })
  await harness.click()
  assert.deepEqual(writes, ['https://api.example/mcp'])
  assert.equal(harness.render().props.children[1].props.children, 'Copiato')
})

test('clipboard denial or absence keeps a selectable manual fallback and never reports success', async () => {
  for (const clipboard of [undefined, { writeText: async () => { throw new Error('denied') } }]) {
    const harness = copyHarness({ clipboard })
    await harness.click()
    const children = harness.render().props.children
    assert.equal(children[0].props.selectable, true)
    assert.equal(children[1].props.children, 'Copia URL MCP')
    assert.match(children[3].props.children, /manualmente/)
  }
})

test('native uses the share sheet without claiming to have copied to a missing clipboard', async () => {
  const shares = []
  const harness = copyHarness({ platform: 'ios', share: async value => shares.push(value.message) })
  await harness.click()
  assert.deepEqual(shares, ['https://api.example/mcp'])
  assert.equal(harness.render().props.children[1].props.children, 'Condividi URL MCP')
})
