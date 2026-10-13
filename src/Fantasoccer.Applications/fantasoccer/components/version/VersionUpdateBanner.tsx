import { type FC, useEffect, useState } from 'react'
import { Platform } from 'react-native'
import { YStack, XStack, Card, Text, Button, Dialog } from 'tamagui'
import { X, FileText } from '@tamagui/lucide-icons-2'
import { APP_VERSION } from 'config/version'
import { useNavigation } from '@react-navigation/native'
import type { NativeStackNavigationProp } from '@react-navigation/native-stack'

const VERSION_STORAGE_KEY = 'fantasoccer_last_seen_version'
export const VERSION_BANNER_ENABLED_KEY = 'fantasoccer_version_banner_enabled'
export const VERSION_BANNER_CHANGED_EVENT = 'fantasoccer_version_banner_changed'

export const setVersionBannerEnabled = (enabled: boolean) => {
  localStorage.setItem(VERSION_BANNER_ENABLED_KEY, String(enabled))
  window.dispatchEvent(new Event(VERSION_BANNER_CHANGED_EVENT))
}

/**
 * Banner che mostra una notifica quando viene rilasciata una nuova versione.
 * Viene mostrato una sola volta per versione e salvato in localStorage.
 */
export const VersionUpdateBanner: FC = () => {
  const navigation = useNavigation<NativeStackNavigationProp<any>>()
  const [isVisible, setIsVisible] = useState(false)
  const [isDismissDialogOpen, setIsDismissDialogOpen] = useState(false)

  useEffect(() => {
    const updateVisibility = () => {
      const enabled = localStorage.getItem(VERSION_BANNER_ENABLED_KEY) !== 'false'
      const lastSeenVersion = localStorage.getItem(VERSION_STORAGE_KEY)
      setIsVisible(enabled && (!lastSeenVersion || lastSeenVersion !== APP_VERSION))
    }

    updateVisibility()
    window.addEventListener(VERSION_BANNER_CHANGED_EVENT, updateVisibility)
    return () => window.removeEventListener(VERSION_BANNER_CHANGED_EVENT, updateVisibility)
  }, [])

  const handleDismiss = () => {
    setIsDismissDialogOpen(true)
  }

  const dismissCurrentVersion = () => {
    localStorage.setItem(VERSION_STORAGE_KEY, APP_VERSION)
    setIsDismissDialogOpen(false)
    setIsVisible(false)
  }

  const disablePermanently = () => {
    setVersionBannerEnabled(false)
    setIsDismissDialogOpen(false)
    setIsVisible(false)
  }

  const handleViewPatchNotes = () => {
    localStorage.setItem(VERSION_STORAGE_KEY, APP_VERSION)
    setIsVisible(false)
    navigation.navigate('PatchNotes' as never)
  }

  if (!isVisible) return null

  return (
    <>
      <Card
        position="absolute"
        bottom={Platform.OS === 'web' ? 190 : 210}
        right={20}
        width={350}
        maxWidth="90%"
        backgroundColor="$blue3"
        borderWidth={2}
        borderColor="$blue8"
        padding="$4"
        zIndex={10001}
        elevation={10}
        style={Platform.OS === 'web' ? ({ position: 'fixed' } as const) : undefined}
      >
        <YStack gap="$3">
          <XStack justifyContent="space-between" alignItems="flex-start">
            <YStack flex={1} gap="$1">
              <Text fontSize="$5" fontWeight="700" color="$blue11">
                🎉 Nuova versione disponibile!
              </Text>
              <Text fontSize="$2" color="$blue10">
                Versione {APP_VERSION}
              </Text>
            </YStack>
            <Button size="$2" chromeless circular icon={X} onPress={handleDismiss} color="$blue10" />
          </XStack>
          <Text fontSize="$3" color="$blue11">
            Abbiamo rilasciato nuove funzionalità e miglioramenti. Scopri cosa è cambiato!
          </Text>
          <XStack gap="$2" justifyContent="flex-end">
            <Button size="$3" variant="outlined" onPress={handleDismiss} borderColor="$blue8" color="$blue11">
              Chiudi
            </Button>
            <Button size="$3" backgroundColor="$blue9" color="white" onPress={handleViewPatchNotes} icon={FileText}>
              Vedi novità
            </Button>
          </XStack>
        </YStack>
      </Card>

      <Dialog open={isDismissDialogOpen} onOpenChange={setIsDismissDialogOpen} modal>
        <Dialog.Portal>
          <Dialog.Overlay key="dismiss-overlay" />
          <Dialog.Content bordered elevate width={380} maxWidth="90%">
            <YStack gap="$4">
              <Dialog.Title>Chiudere il widget aggiornamenti?</Dialog.Title>
              <Dialog.Description>
                Puoi nasconderlo solo per questo momento oppure disattivarlo per sempre. Per riaprirlo dopo la
                disattivazione dovrai andare nelle Impostazioni e riattivarlo.
              </Dialog.Description>
              <XStack gap="$2" justifyContent="flex-end" flexWrap="wrap">
                <Button variant="outlined" onPress={() => setIsDismissDialogOpen(false)}>
                  Annulla
                </Button>
                <Button variant="outlined" onPress={dismissCurrentVersion}>
                  Chiudi solo per ora
                </Button>
                <Button theme="red" onPress={disablePermanently}>
                  Disattiva per sempre
                </Button>
              </XStack>
            </YStack>
          </Dialog.Content>
        </Dialog.Portal>
      </Dialog>
    </>
  )
}
