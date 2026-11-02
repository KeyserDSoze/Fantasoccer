import { useEffect, useRef, useState, type ReactNode } from 'react'
import { AccessibilityInfo, Animated, Easing, Platform } from 'react-native'
import { Text, YStack } from 'tamagui'

export function LiveScore({ active, children }: { active: boolean; children: ReactNode }) {
  const opacity = useRef(new Animated.Value(1)).current
  const [reduceMotion, setReduceMotion] = useState(true)
  useEffect(() => {
    let mounted = true
    void AccessibilityInfo.isReduceMotionEnabled().then(value => { if (mounted) setReduceMotion(value) }).catch(() => {})
    const subscription = AccessibilityInfo.addEventListener('reduceMotionChanged', setReduceMotion)
    return () => { mounted = false; subscription.remove() }
  }, [])
  useEffect(() => {
    if (!active || reduceMotion) { opacity.setValue(1); return }
    const animation = Animated.loop(Animated.sequence([
      Animated.timing(opacity, { toValue: 0.5, duration: 900, easing: Easing.inOut(Easing.ease), useNativeDriver: Platform.OS !== 'web' }),
      Animated.timing(opacity, { toValue: 1, duration: 900, easing: Easing.inOut(Easing.ease), useNativeDriver: Platform.OS !== 'web' }),
    ]))
    animation.start()
    return () => { animation.stop(); opacity.setValue(1) }
  }, [active, reduceMotion, opacity])
  return <YStack alignItems="center" gap="$1">
    <Animated.View style={{ opacity, alignItems: 'center' }}>{children}</Animated.View>
    {active && <Text fontSize="$1" color="$green10" accessibilityLabel="Risultato provvisorio, partita in corso">LIVE</Text>}
  </YStack>
}
