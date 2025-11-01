import { type FC } from 'react'
import { XStack, Button, Text } from 'tamagui'
import { ZoomIn, ZoomOut, RotateCcw } from '@tamagui/lucide-icons'

interface ZoomControlsProps {
  zoomLevel: number
  onIncrease: () => void
  onDecrease: () => void
  onReset: () => void
  minZoom: number
  maxZoom: number
}

export const ZoomControls: FC<ZoomControlsProps> = ({
  zoomLevel,
  onIncrease,
  onDecrease,
  onReset,
  minZoom,
  maxZoom
}) => {
  const zoomPercent = Math.round(zoomLevel * 100)
  const canIncrease = zoomLevel < maxZoom
  const canDecrease = zoomLevel > minZoom

  return (
    <XStack 
      gap="$2" 
      alignItems="center" 
      backgroundColor="$background" 
      padding="$2" 
      borderRadius="$4"
      borderWidth={1}
      borderColor="$borderColor"
      elevation={2}
    >
      {/* Pulsante Zoom Out */}
      <Button
        size="$3"
        circular
        icon={ZoomOut}
        onPress={onDecrease}
        disabled={!canDecrease}
        backgroundColor={canDecrease ? '$blue9' : '$color5'}
        pressStyle={{ scale: 0.95 }}
        opacity={canDecrease ? 1 : 0.5}
      />

      {/* Display percentuale zoom */}
      <XStack minWidth={70} justifyContent="center" alignItems="center" gap="$1">
        <Text fontSize="$3" fontWeight="600" color="$color12">
          {zoomPercent}%
        </Text>
      </XStack>

      {/* Pulsante Zoom In */}
      <Button
        size="$3"
        circular
        icon={ZoomIn}
        onPress={onIncrease}
        disabled={!canIncrease}
        backgroundColor={canIncrease ? '$blue9' : '$color5'}
        pressStyle={{ scale: 0.95 }}
        opacity={canIncrease ? 1 : 0.5}
      />

      {/* Pulsante Reset */}
      <Button
        size="$3"
        circular
        icon={RotateCcw}
        onPress={onReset}
        backgroundColor="$color8"
        pressStyle={{ scale: 0.95 }}
      />
    </XStack>
  )
}
