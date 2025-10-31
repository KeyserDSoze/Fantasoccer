import React, { useState, useEffect } from 'react'
import { Platform } from 'react-native'
import { YStack, XStack, Text, Button, Card } from 'tamagui'
import { Home, X, Share } from '@tamagui/lucide-icons'

// Definizione del tipo per l'evento beforeinstallprompt
interface BeforeInstallPromptEvent extends Event {
  prompt: () => Promise<void>
  userChoice: Promise<{ outcome: 'accepted' | 'dismissed' }>
}

export const AddToHomeScreenBanner: React.FC = () => {
  const [deferredPrompt, setDeferredPrompt] = useState<BeforeInstallPromptEvent | null>(null)
  const [showBanner, setShowBanner] = useState(false)
  const [isIOS, setIsIOS] = useState(false)
  const [isStandalone, setIsStandalone] = useState(false)

  useEffect(() => {
    // Verifica se siamo in una build nativa (React Native)
    if (Platform.OS === 'ios' || Platform.OS === 'android') {
      // Siamo in una build nativa, non mostrare il banner
      return
    }

    // Verifica se l'app è già installata (modalità standalone)
    const isInStandaloneMode = () => {
      if (typeof window === 'undefined') return false
      
      return (
        window.matchMedia('(display-mode: standalone)').matches ||
        (window.navigator as any).standalone === true ||
        document.referrer.includes('android-app://')
      )
    }

    // Verifica se siamo su iOS
    const checkIsIOS = () => {
      if (typeof window === 'undefined') return false
      
      return /iPad|iPhone|iPod/.test(navigator.userAgent) && !(window as any).MSStream
    }

    const standalone = isInStandaloneMode()
    const iOS = checkIsIOS()
    
    setIsStandalone(standalone)
    setIsIOS(iOS)

    // Se è già installata, non mostrare nulla
    if (standalone) {
      return
    }

    // Verifica se l'utente ha già chiuso il banner in precedenza
    const bannerDismissedTime = localStorage.getItem('a2hs-banner-dismissed')
    if (bannerDismissedTime) {
      // Verifica se è stato nascosto per sempre
      if (bannerDismissedTime === 'forever') {
        return
      }
      
      // Altrimenti calcola i giorni passati
      const dismissedDate = parseInt(bannerDismissedTime)
      if (!isNaN(dismissedDate)) {
        const daysSinceDismissed = (Date.now() - dismissedDate) / (1000 * 60 * 60 * 24)
        
        // Se sono passati meno di 7 giorni, non mostrare il banner
        if (daysSinceDismissed < 7) {
          return
        }
      }
    }

    // Per iOS, mostra il banner se non è standalone
    if (iOS) {
      setShowBanner(true)
      return
    }

    // Per Android/Desktop, intercetta l'evento beforeinstallprompt
    const handleBeforeInstallPrompt = (e: Event) => {
      // Previeni il prompt automatico
      e.preventDefault()
      // Salva l'evento per usarlo dopo
      setDeferredPrompt(e as BeforeInstallPromptEvent)
      setShowBanner(true)
    }

    window.addEventListener('beforeinstallprompt', handleBeforeInstallPrompt)

    return () => {
      window.removeEventListener('beforeinstallprompt', handleBeforeInstallPrompt)
    }
  }, [])

  const handleInstallClick = async () => {
    if (!deferredPrompt) return

    // Mostra il prompt di installazione
    await deferredPrompt.prompt()
    
    // Aspetta la scelta dell'utente
    const { outcome } = await deferredPrompt.userChoice
    
    if (outcome === 'accepted') {
      console.log('User accepted the A2HS prompt')
    }
    
    // Reset del prompt
    setDeferredPrompt(null)
    setShowBanner(false)
  }

  const handleDismiss = () => {
    setShowBanner(false)
    // Salva il timestamp corrente per calcolare i 7 giorni
    localStorage.setItem('a2hs-banner-dismissed', Date.now().toString())
  }

  const handleDismissUntilNextSession = () => {
    setShowBanner(false)
    // Non salva nulla in localStorage, quindi riapparirà alla prossima sessione
  }

  const handleDismissForever = () => {
    const confirmed = window.confirm(
      '⚠️ Sei sicuro di voler nascondere questo messaggio per sempre?\n\n' +
      'Non comparirà mai più e dovrai installare l\'app manualmente tramite il menu del browser.\n\n' +
      'Vuoi continuare?'
    )
    
    if (confirmed) {
      setShowBanner(false)
      // Usa il valore speciale 'forever' per indicare la dismissione permanente
      localStorage.setItem('a2hs-banner-dismissed', 'forever')
    }
  }

  if (!showBanner) return null

  return (
    <Card
      position="absolute"
      bottom={0}
      left={0}
      right={0}
      margin="$4"
      padding="$4"
      backgroundColor="$blue3"
      borderRadius="$4"
      borderWidth={1}
      borderColor="$blue7"
      elevation={10}
      zIndex={9999}
      // @ts-ignore - Web-only styles
      style={{ position: 'fixed' }}
    >
      <YStack gap="$3">
        <XStack justifyContent="space-between" alignItems="flex-start">
          <XStack gap="$3" alignItems="center" flex={1}>
            <Home size={24} color="$blue10" />
            <YStack flex={1}>
              <Text fontSize="$5" fontWeight="bold" color="$blue11">
                Installa l'App
              </Text>
              <Text fontSize="$3" color="$blue10" marginTop="$1">
                {isIOS 
                  ? "Aggiungi Fantasoccer alla tua schermata Home per un accesso rapido"
                  : "Installa l'app per un'esperienza migliore e accesso offline"}
              </Text>
            </YStack>
          </XStack>
          
          <Button
            size="$2"
            circular
            chromeless
            onPress={handleDismissUntilNextSession}
            icon={<X size={16} />}
          />
        </XStack>

        {isIOS ? (
          // Istruzioni per iOS
          <>
            <Card padding="$3" backgroundColor="$blue2" borderRadius="$3">
              <YStack gap="$2">
                <Text fontSize="$3" color="$blue11" fontWeight="600">
                  Per installare su iOS:
                </Text>
                <XStack gap="$2" alignItems="center">
                  <Share size={16} color="$blue10" />
                  <Text fontSize="$2" color="$blue10">
                    1. Tocca il pulsante di condivisione
                  </Text>
                </XStack>
                <XStack gap="$2" alignItems="center">
                  <Home size={16} color="$blue10" />
                  <Text fontSize="$2" color="$blue10">
                    2. Scorri e seleziona "Aggiungi a Home"
                  </Text>
                </XStack>
              </YStack>
            </Card>
            <XStack gap="$2">
              <Button
                flex={1}
                variant="outlined"
                onPress={handleDismiss}
              >
                Posponi a 7 giorni
              </Button>
              <Button
                variant="outlined"
                chromeless
                onPress={handleDismissForever}
                fontSize="$2"
              >
                Mai Più
              </Button>
            </XStack>
          </>
        ) : (
          // Pulsante per Android/Desktop
          <YStack gap="$2">
            <Button
              backgroundColor="$blue9"
              color="white"
              onPress={handleInstallClick}
              icon={<Home size={16} />}
            >
              Installa Ora
            </Button>
            <XStack gap="$2">
              <Button
                flex={0.75}
                backgroundColor="$orange9"
                color="white"
                onPress={handleDismiss}
                pressStyle={{ backgroundColor: '$orange10' }}
              >
                Posponi a 7 giorni
              </Button>
              <Button
                flex={0.25}
                backgroundColor="$red9"
                color="white"
                onPress={handleDismissForever}
                pressStyle={{ backgroundColor: '$red10' }}
              >
                Non mostrare più
              </Button>
            </XStack>
          </YStack>
        )}
      </YStack>
    </Card>
  )
}
