import { useState } from 'react'
import { Platform, Share } from 'react-native'
import { Check, Copy, Share2 } from '@tamagui/lucide-icons-2'
import { Button, Text, YStack } from 'tamagui'

export function CopyableGuideText({ value, label }: { value: string; label: string }) {
  const [copied, setCopied] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const isWeb = Platform.OS === 'web'

  const copy = async () => {
    setError(null)
    setCopied(false)
    try {
      if (isWeb) {
        if (typeof navigator === 'undefined' || !navigator.clipboard?.writeText)
          throw new Error('Clipboard unavailable')
        await navigator.clipboard.writeText(value)
        setCopied(true)
      } else {
        await Share.share({ message: value })
      }
    } catch {
      setError('Non riesco a copiare il testo. Puoi selezionarlo e copiarlo manualmente.')
    }
  }

  return <YStack gap="$2" minWidth={0}>
    <Text selectable color="$color12" fontSize="$3" lineHeight="$5">{value}</Text>
    <Button
      alignSelf="flex-start"
      size="$3"
      variant="outlined"
      icon={copied ? Check : isWeb ? Copy : Share2}
      aria-label={`${isWeb ? 'Copia' : 'Condividi'} ${label}`}
      onPress={copy}>
      {copied ? 'Copiato' : isWeb ? `Copia ${label}` : `Condividi ${label}`}
    </Button>
    {copied ? <Text aria-live="polite" color="$green11" fontSize="$2">Testo copiato negli appunti.</Text> : null}
    {error ? <Text role="alert" color="$red11" fontSize="$2">{error}</Text> : null}
  </YStack>
}
