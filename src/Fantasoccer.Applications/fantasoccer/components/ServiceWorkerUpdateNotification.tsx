import { useEffect, useState } from 'react'
import { Button, Card, Text, XStack, YStack } from 'tamagui'
import { RefreshCw } from '@tamagui/lucide-icons-2'
import { Platform } from 'react-native'

export function ServiceWorkerUpdateNotification() {
  const [showUpdate, setShowUpdate] = useState(false)
  const [newWorker, setNewWorker] = useState<ServiceWorker | null>(null)

  useEffect(() => {
    if (Platform.OS !== 'web' || !('serviceWorker' in navigator)) {
      return
    }

    const handleUpdate = (registration: ServiceWorkerRegistration) => {
      registration.addEventListener('updatefound', () => {
        const installingWorker = registration.installing
        
        if (!installingWorker) return

        installingWorker.addEventListener('statechange', () => {
          if (installingWorker.state === 'installed' && navigator.serviceWorker.controller) {
            console.log('[ServiceWorker] New version available')
            setNewWorker(installingWorker)
            setShowUpdate(true)
          }
        })
      })
    }

    // Get existing registration or wait for it
    navigator.serviceWorker.getRegistration().then((registration) => {
      if (registration) {
        handleUpdate(registration)
      }
    })

    // Also listen for new registrations
    navigator.serviceWorker.ready.then((registration) => {
      handleUpdate(registration)
    })

    // The first activation must not reload away an in-progress registration.
    const hadController = Boolean(navigator.serviceWorker.controller)
    // Listen for controller change (when new SW takes over)
    let refreshing = false
    const handleControllerChange = () => {
      if (refreshing || !hadController) return
      console.log('[ServiceWorker] Controller changed, reloading page...')
      refreshing = true
      window.location.reload()
    }

    navigator.serviceWorker.addEventListener('controllerchange', handleControllerChange)

    return () => {
      navigator.serviceWorker.removeEventListener('controllerchange', handleControllerChange)
    }
  }, [])

  const handleUpdate = () => {
    if (newWorker) {
      newWorker.postMessage({ type: 'SKIP_WAITING' })
    }
    
    // Reload after a short delay to let the new SW activate
    setTimeout(() => {
      window.location.reload()
    }, 100)
  }

  const handleDismiss = () => {
    setShowUpdate(false)
  }

  if (!showUpdate) return null

  return (
    <Card
      position="absolute"
      bottom="$4"
      left="$4"
      right="$4"
      maxWidth={400}
      margin="auto"
      zIndex={9999}
      padding="$4"
      backgroundColor="$blue9"
      borderRadius="$4"
      borderWidth="$0.5"
      borderColor="$blue10"
      shadowColor="$shadowColor"
      shadowOffset={{ width: 0, height: 4 }}
      shadowOpacity={0.3}
      shadowRadius={8}
      elevation={5}
      transition="quick"
      enterStyle={{
        opacity: 0,
        y: 20,
      }}
    >
      <YStack gap="$3">
        <XStack gap="$2" alignItems="center">
          <RefreshCw size={20} color="white" />
          <Text fontSize="$5" fontWeight="700" color="white">
            Aggiornamento Disponibile
          </Text>
        </XStack>
        
        <Text fontSize="$3" color="rgba(255, 255, 255, 0.9)">
          È disponibile una nuova versione dell&apos;app. Aggiorna ora per ottenere le ultime funzionalità e correzioni.
        </Text>

        <XStack gap="$2" justifyContent="flex-end">
          <Button
            size="$3"
            variant="outlined"
            borderColor="white"
            color="white"
            onPress={handleDismiss}
          >
            Dopo
          </Button>
          <Button
            size="$3"
            backgroundColor="white"
            color="$blue9"
            fontWeight="700"
            onPress={handleUpdate}
            icon={RefreshCw}
          >
            Aggiorna Ora
          </Button>
        </XStack>
      </YStack>
    </Card>
  )
}
