import { type FC, useEffect, useState } from 'react'
import { YStack, XStack, Card, Text, Button } from 'tamagui'
import { X, FileText } from '@tamagui/lucide-icons'
import { APP_VERSION } from 'config/version'
import { useNavigation } from '@react-navigation/native'
import type { NativeStackNavigationProp } from '@react-navigation/native-stack'

const VERSION_STORAGE_KEY = 'fantasoccer_last_seen_version'

/**
 * Banner che mostra una notifica quando viene rilasciata una nuova versione.
 * Viene mostrato una sola volta per versione e salvato in localStorage.
 */
export const VersionUpdateBanner: FC = () => {
  const navigation = useNavigation<NativeStackNavigationProp<any>>()
  const [isVisible, setIsVisible] = useState(false)

  useEffect(() => {
    const lastSeenVersion = localStorage.getItem(VERSION_STORAGE_KEY)
    
    // Mostra il banner solo se:
    // 1. Non c'è una versione salvata (primo accesso)
    // 2. La versione salvata è diversa da quella corrente (nuova versione)
    if (!lastSeenVersion || lastSeenVersion !== APP_VERSION) {
      setIsVisible(true)
    }
  }, [])

  const handleDismiss = () => {
    localStorage.setItem(VERSION_STORAGE_KEY, APP_VERSION)
    setIsVisible(false)
  }

  const handleViewPatchNotes = () => {
    localStorage.setItem(VERSION_STORAGE_KEY, APP_VERSION)
    setIsVisible(false)
    navigation.navigate('PatchNotes' as never)
  }

  if (!isVisible) return null

  return (
    <Card
      position="absolute"
      bottom={20}
      right={20}
      width={350}
      maxWidth="90%"
      backgroundColor="$blue3"
      borderWidth={2}
      borderColor="$blue8"
      padding="$4"
      zIndex={9999}
      elevation={10}
      animation="quick"
      enterStyle={{ opacity: 0, scale: 0.9, y: 20 }}
      exitStyle={{ opacity: 0, scale: 0.9, y: 20 }}
    >
      <YStack gap="$3">
        {/* Header con bottone chiudi */}
        <XStack justifyContent="space-between" alignItems="flex-start">
          <YStack flex={1} gap="$1">
            <Text fontSize="$5" fontWeight="700" color="$blue11">
              🎉 Nuova versione disponibile!
            </Text>
            <Text fontSize="$2" color="$blue10">
              Versione {APP_VERSION}
            </Text>
          </YStack>
          <Button
            size="$2"
            chromeless
            circular
            icon={X}
            onPress={handleDismiss}
            color="$blue10"
            hoverStyle={{ backgroundColor: '$blue4' }}
          />
        </XStack>

        {/* Messaggio */}
        <Text fontSize="$3" color="$blue11">
          Abbiamo rilasciato nuove funzionalità e miglioramenti. Scopri cosa è cambiato!
        </Text>

        {/* Azioni */}
        <XStack gap="$2" justifyContent="flex-end">
          <Button
            size="$3"
            variant="outlined"
            onPress={handleDismiss}
            borderColor="$blue8"
            color="$blue11"
            hoverStyle={{ backgroundColor: '$blue4' }}
          >
            Chiudi
          </Button>
          <Button
            size="$3"
            backgroundColor="$blue9"
            color="white"
            onPress={handleViewPatchNotes}
            icon={FileText}
            hoverStyle={{ backgroundColor: '$blue10' }}
            pressStyle={{ backgroundColor: '$blue11' }}
          >
            Vedi novità
          </Button>
        </XStack>
      </YStack>
    </Card>
  )
}
