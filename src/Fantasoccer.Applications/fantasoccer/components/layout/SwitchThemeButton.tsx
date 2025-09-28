'use client'

import { Button, XStack, Text } from 'tamagui'
import { Moon, Sun } from '@tamagui/lucide-icons'
import { useAppTheme } from 'providers/ThemeProvider'

export const SwitchThemeButton = () => {
  const { theme, toggleTheme } = useAppTheme()
  const isDark = theme === 'dark'

  return (
    <Button onPress={toggleTheme} size="$3" variant="outlined">
      <XStack alignItems="center" gap="$2">
        {isDark ? <Moon size="$1" /> : <Sun size="$1" />}
        <Text textTransform="capitalize">{theme}</Text>
      </XStack>
    </Button>
  )
}
