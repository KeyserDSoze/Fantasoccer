import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { createRequire } from 'node:module'
import test from 'node:test'
import { loadTypeScript } from './helpers/loadTypeScript.cjs'

const require = createRequire(import.meta.url)
const React = require('react')
const { renderToStaticMarkup } = require('react-dom/server')

const element = tag => ({ children }) => React.createElement(tag, null, children)
const boundaries = {
  react: React,
  'react/jsx-runtime': require('react/jsx-runtime'),
  '@react-navigation/native': { useNavigation: () => ({ navigate: () => assert.fail('Rendering must not navigate') }) },
  '@tamagui/lucide-icons-2': { BookOpen: () => null },
  tamagui: { Button: element('button'), Card: element('section'), H3: element('h3'), Paragraph: element('p'), YStack: element('div') },
}
const environment = value => ({ process: { env: value === undefined ? {} : { EXPO_PUBLIC_MCP_ENABLED: value } } })
const render = value => {
  const { McpGuideCard } = loadTypeScript('components/mcp/McpGuideCard.tsx', boundaries, environment(value))
  return renderToStaticMarkup(React.createElement(McpGuideCard))
}

test('MCP entry points stay hidden unless the build explicitly enables them', () => {
  for (const value of [undefined, '', 'false', 'TRUE', '1']) {
    assert.equal(loadTypeScript('configurations/features.ts', {}, environment(value)).MCP_ENTRY_POINTS, false, String(value))
  }
  assert.equal(loadTypeScript('configurations/features.ts', {}, environment('true')).MCP_ENTRY_POINTS, true)
})

test('the guide card is invisible on fcv2 (MCP off) and visible where MCP is enabled', () => {
  assert.equal(render(undefined), '')
  assert.equal(render('false'), '')
  assert.match(render('true'), /Guida MCP e ChatGPT/)
})

test('menu and home link to the guide only behind the same flag', () => {
  for (const file of ['components/layout/NavigationBar.tsx', 'screens/home.tsx']) {
    const lines = readFileSync(new URL(`../${file}`, import.meta.url), 'utf8').split('\n')
    assert.ok(lines.some(line => line.startsWith('import { MCP_ENTRY_POINTS }')), `${file} imports the flag`)
    lines.forEach((line, index) => {
      if (!line.includes("'McpGuide'")) return
      const nearby = lines.slice(Math.max(0, index - 3), index + 1).join('\n')
      assert.match(nearby, /MCP_ENTRY_POINTS/, `${file}:${index + 1} links to the MCP guide without the flag`)
    })
  }
})
