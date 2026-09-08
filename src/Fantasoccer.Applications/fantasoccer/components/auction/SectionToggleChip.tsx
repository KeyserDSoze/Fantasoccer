import { type FC } from 'react'
import { Text, XStack } from 'tamagui'
import { Eye, EyeOff } from '@tamagui/lucide-icons-2'

export interface SectionToggleChipProps {
  label: string
  checked: boolean
  onCheckedChange: (checked: boolean) => void
}

/**
 * Pill-shaped toggle used to show/hide auction sections.
 * Provides a clear active/inactive state, an eye icon and press feedback,
 * replacing the raw Switch + label pairs that were hard to read.
 */
export const SectionToggleChip: FC<SectionToggleChipProps> = ({
  label,
  checked,
  onCheckedChange,
}) => {
  const EyeIcon = checked ? Eye : EyeOff

  return (
    <XStack
      alignItems="center"
      gap="$2"
      paddingVertical="$1.5"
      paddingHorizontal="$2.5"
      borderRadius="$10"
      borderWidth={1}
      backgroundColor={checked ? '$blue9' : '$blue2'}
      borderColor={checked ? '$blue9' : '$blue6'}
      cursor="pointer"
      userSelect="none"
      role="switch"
      aria-checked={checked}
      aria-label={label}
      pressStyle={{ scale: 0.97, opacity: 0.9 }}
      hoverStyle={{ borderColor: checked ? '$blue10' : '$blue8' }}
      transition="quick"
      onPress={() => onCheckedChange(!checked)}
    >
      <EyeIcon size={14} color={checked ? 'white' : '$blue10'} />
      <Text
        fontSize="$2"
        fontWeight={checked ? '700' : '500'}
        color={checked ? 'white' : '$blue11'}
      >
        {label}
      </Text>
    </XStack>
  )
}
