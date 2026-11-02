import assert from 'node:assert/strict'
import { createRequire } from 'node:module'
import test from 'node:test'
const require = createRequire(import.meta.url)
const { loadTypeScript } = require('./helpers/loadTypeScript.cjs')

test('the shared login loader shows checkmarks only for completed tasks and keeps optional failures distinct', () => {
  const jsx = (type, props) => ({ type, props })
  const { SplashScreen } = loadTypeScript('components/layout/SplashScreen.tsx', {
    'react/jsx-runtime': { jsx, jsxs: jsx },
    tamagui: Object.fromEntries(['YStack', 'XStack', 'Text', 'Spinner'].map(name => [name, name])),
    '@tamagui/lucide-icons-2': { Check: 'Check', AlertTriangle: 'AlertTriangle' },
    './FantasoccerLogo': { FantasoccerLogo: 'FantasoccerLogo' },
  })
  const tree = SplashScreen({ compact: true, message: 'Partita', steps: [
    { id: 'match', label: 'Partita', status: 'pending' },
    { id: 'calendar', label: 'Calendario', status: 'done' },
    { id: 'stats', label: 'Statistiche', status: 'error' },
  ] })
  function nodes(node, type) {
    if (!node || typeof node !== 'object') return []
    if (Array.isArray(node)) return node.flatMap(item => nodes(item, type))
    return [...(node.type === type ? [node] : []), ...nodes(node.props?.children, type)]
  }
  assert.equal(nodes(tree, 'FantasoccerLogo').length, 1)
  assert.equal(nodes(tree, 'Spinner').length, 1)
  assert.equal(nodes(tree, 'Check').length, 1)
  assert.equal(nodes(tree, 'AlertTriangle').length, 1)
  assert.equal(nodes(tree, 'YStack').some(node => node.props.accessibilityLiveRegion === 'polite'), true)
})

test('live score animation respects reduced motion and stops on unmount or inactive matches', () => {
  for (const [active, reduceMotion, animate] of [[true, false, true], [true, true, false], [false, false, false]]) {
    const effects = [], timings = []
    let starts = 0, stops = 0, subscriptionsRemoved = 0
    const jsx = (type, props) => ({ type, props })
    const { LiveScore } = loadTypeScript('components/live/LiveScore.tsx', {
      react: { useRef: value => ({ current: value }), useState: () => [reduceMotion, () => {}], useEffect: effect => effects.push(effect) },
      'react/jsx-runtime': { jsx, jsxs: jsx },
      tamagui: { Text: 'Text', YStack: 'YStack' },
      'react-native': {
        Platform: { OS: 'web' },
        Easing: { ease: 'ease', inOut: value => value },
        AccessibilityInfo: { isReduceMotionEnabled: async () => reduceMotion,
          addEventListener: () => ({ remove: () => { subscriptionsRemoved++ } }) },
        Animated: { View: 'Animated.View', Value: class { setValue() {} },
          timing: (_opacity, options) => { timings.push(options); return options }, sequence: items => items,
          loop: () => ({ start: () => { starts++ }, stop: () => { stops++ } }) },
      },
    })
    const tree = LiveScore({ active, children: '1 - 0' })
    const cleanup = effects.map(effect => effect())
    assert.equal(starts, animate ? 1 : 0)
    assert.equal(JSON.stringify(tree).includes('LIVE'), active)
    if (animate) {
      assert.equal(timings[0].toValue, 0.5)
      assert.equal(timings[0].duration, 900)
    }
    cleanup.forEach(dispose => dispose?.())
    assert.equal(stops, animate ? 1 : 0)
    assert.equal(subscriptionsRemoved, 1)
  }
})
