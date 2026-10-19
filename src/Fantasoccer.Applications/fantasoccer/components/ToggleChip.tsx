import { type FC } from 'react'
import { Check, EyeOff } from '@tamagui/lucide-icons-2'
import { Text, XStack } from 'tamagui'

export interface ToggleChipProps {
  label: string
  checked: boolean
  onCheckedChange: (checked: boolean) => void
  accent?: 'blue' | 'green' | 'orange'
  disabled?: boolean
}

const accents = {
  blue: {
    active: '$blue9',
    border: '$blue6',
    hover: '$blue8',
    foreground: '$blue11',
  },
  green: {
    active: '$green9',
    border: '$green6',
    hover: '$green8',
    foreground: '$green11',
  },
  orange: {
    active: '$orange9',
    border: '$orange6',
    hover: '$orange8',
    foreground: '$orange11',
  },
} as const

export const ToggleChip: FC<ToggleChipProps> = ({
  label,
  checked,
  onCheckedChange,
  accent = 'blue',
  disabled = false,
}) => {
  const Icon = checked ? Check : EyeOff
  const colors = accents[accent]

  return (
    <XStack
      alignItems="center"
      gap="$2"
      paddingVertical="$1.5"
      paddingHorizontal="$2.5"
      borderRadius="$10"
      borderWidth={1}
      backgroundColor={checked ? colors.active : '$color2'}
      borderColor={checked ? colors.active : colors.border}
      cursor={disabled ? 'not-allowed' : 'pointer'}
      opacity={disabled ? 0.5 : 1}
      userSelect="none"
      role="switch"
      aria-checked={checked}
      aria-label={label}
      aria-disabled={disabled}
      pressStyle={disabled ? undefined : { scale: 0.97, opacity: 0.9 }}
      hoverStyle={disabled ? undefined : { borderColor: checked ? colors.active : colors.hover }}
      transition="quick"
      onPress={disabled ? undefined : () => onCheckedChange(!checked)}
    >
      <Icon size={14} color={checked ? 'white' : colors.foreground} />
      <Text
        fontSize="$2"
        fontWeight={checked ? '700' : '500'}
        color={checked ? 'white' : colors.foreground}
      >
        {label}
      </Text>
    </XStack>
  )
}
