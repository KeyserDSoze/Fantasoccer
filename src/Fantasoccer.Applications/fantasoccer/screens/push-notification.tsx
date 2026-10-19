import { type ReactNode, useCallback, useEffect, useMemo, useState } from 'react'
import { Platform, View } from 'react-native'
import {
  Button,
  Card,
  H2,
  H3,
  Paragraph,
  Separator,
  Spinner,
  Text,
  XStack,
  YStack
} from 'tamagui'
import {
  Bell,
  BellRing,
  CheckCircle2,
  Clock,
  Loader2,
  RefreshCcw,
  ShieldOff,
  Smartphone,
  SquareCheck,
  Trash2,
  TriangleAlert
} from '@tamagui/lucide-icons-2'
import { ScreenContent } from 'components/layout/ScreenContent'
import { ToggleChip } from 'components/ToggleChip'
import {
  useCurrentUserPushNotifications
} from 'hooks/usePushNotifications'
import { PushNotificationSettings, PushSubscription } from 'types/pushNotification'
import { UpdatePushNotificationSettingsInput } from 'services/pushNotificationService'
import { useAuth } from '../providers/AuthProvider'
import { rememberPushDevice } from '../services/pushDeviceService'
import {
  PushSupportState,
  webPushSupport
} from 'services/webPushClient'

type PushToggleKey = 'matchEvents' | 'opponentMatchEvents' | 'marketEvents' | 'deploymentReminder' | 'endDay' | 'liveChangesReminder' | 'liveFormationChangeEvents'

const notificationToggles: Array<{
  key: PushToggleKey
  title: string
  description: string
  icon: ReactNode
}> = [
  {
    key: 'matchEvents',
    title: 'Partita in tempo reale',
    description: 'Ricevi aggiornamenti sulla tua partita principale.',
    icon: <Bell size={22} color="$blue10" />
  },
  {
    key: 'opponentMatchEvents',
    title: 'Partite avversari',
    description: 'Tieniti informato sui risultati dei tuoi rivali diretti.',
    icon: <BellRing size={22} color="$purple10" />
  },
  {
    key: 'marketEvents',
    title: 'Movimenti di mercato',
    description: 'Proposte, accettazioni, voti e risultati degli scambi della lega principale.',
    icon: <SquareCheck size={22} color="$green10" />
  },
  {
    key: 'deploymentReminder',
    title: 'Promemoria formazione',
    description: 'Ricevi un alert prima della deadline per schierare la tua squadra.',
    icon: <Clock size={22} color="$yellow10" />
  },
  {
    key: 'liveChangesReminder',
    title: 'Cambi finali',
    description: 'Avviso quando puoi ancora usare i cambi formazione prima della deadline live.',
    icon: <RefreshCcw size={22} color="$orange10" />
  },
  {
    key: 'liveFormationChangeEvents',
    title: 'Cambi formazione live',
    description: 'Ricevi chi è stato spostato e come cambia il punteggio nelle competizioni a formazione manuale.',
    icon: <RefreshCcw size={22} color="$blue10" />
  },
  {
    key: 'endDay',
    title: 'Fine giornata',
    description: 'Riepilogo risultati a chiusura dell’ultima partita della giornata.',
    icon: <CheckCircle2 size={22} color="$blue10" />
  }
]

const formatEndpoint = (endpoint: string): string => {
  if (endpoint.length <= 48) {
    return endpoint
  }
  return `${endpoint.slice(0, 24)}…${endpoint.slice(-12)}`
}

const formatTimestamp = (date: Date | null): string => {
  if (!date) {
    return 'Mai sincronizzato'
  }
  try {
    return new Intl.DateTimeFormat('it-IT', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    }).format(date)
  } catch (error) {
    console.warn('settings: failed to format date', error)
    return date.toISOString()
  }
}

const PushNotificationScreen = () => {
  const {
    settings,
    summary,
    isLoading,
    error,
    lastUpdated,
    refetch,
    updateSettings,
    addDevice,
    removeDevice,
    disableAll
  } = useCurrentUserPushNotifications({ autoRefresh: true })

  const [supportState, setSupportState] = useState<PushSupportState | null>(null)
  const [currentSubscription, setCurrentSubscription] = useState<PushSubscription | null>(null)
  const [isRegistering, setIsRegistering] = useState(false)
  const [isUnregistering, setIsUnregistering] = useState(false)
  const [isRefreshingSubscription, setIsRefreshingSubscription] = useState(false)
  const [deviceError, setDeviceError] = useState<string | null>(null)
  const { user } = useAuth()

  const isWeb = Platform.OS === 'web'

  const loadSupportState = useCallback(async () => {
    if (!isWeb) {
      setSupportState({ supported: false, reason: 'Registrazione push disponibile su browser/PWA compatibili, non nell’app nativa.' })
      return
    }

    const state = await webPushSupport.getSupportState()
    setSupportState(state)

    if (state.supported) {
      const subscription = await webPushSupport.getExistingSubscription()
      setCurrentSubscription(subscription)
    } else {
      setCurrentSubscription(null)
    }
  }, [isWeb])

  useEffect(() => {
    loadSupportState().catch((supportError) => {
      console.error('settings: unable to evaluate push support', supportError)
      setDeviceError(supportError instanceof Error ? supportError.message : 'Verifica del dispositivo non riuscita.')
    })
  }, [loadSupportState])

  const handleToggle = useCallback(async (key: PushToggleKey, value: boolean) => {
    const payload: UpdatePushNotificationSettingsInput = { [key]: value } as UpdatePushNotificationSettingsInput
    await updateSettings(payload)
  }, [updateSettings])

  const handleRegisterDevice = useCallback(async () => {
    if (!isWeb) {
      return
    }

    setIsRegistering(true)
    setDeviceError(null)
    try {
      const subscription = await webPushSupport.subscribe()
      if (!subscription) {
        setDeviceError('Attivazione non completata: controlla il permesso notifiche del browser.')
        return
      }

      await addDevice(subscription)
      if (user?.email) await rememberPushDevice(user.email, subscription.endpoint, true)
      setCurrentSubscription(subscription)
      await loadSupportState()
    } catch (registerError) {
      console.error('settings: failed to register push subscription', registerError)
      setDeviceError(registerError instanceof Error ? registerError.message : 'Attivazione push non riuscita.')
    } finally {
      setIsRegistering(false)
    }
  }, [addDevice, isWeb, loadSupportState, user?.email])

  const handleUnregisterDevice = useCallback(async () => {
    if (!isWeb) {
      return
    }

    setIsUnregistering(true)
    setDeviceError(null)
    try {
      if (user?.email) await rememberPushDevice(user.email, currentSubscription?.endpoint ?? '', false)
      const subscription = await webPushSupport.unsubscribe()
      if (!subscription) {
        if (currentSubscription?.endpoint) await removeDevice(currentSubscription.endpoint)
        await loadSupportState()
        return
      }

      await removeDevice(subscription.endpoint)
      setCurrentSubscription(null)
      await loadSupportState()
    } catch (unregisterError) {
      console.error('settings: failed to unregister push subscription', unregisterError)
      setDeviceError(unregisterError instanceof Error ? unregisterError.message : 'Disattivazione push non riuscita.')
    } finally {
      setIsUnregistering(false)
    }
  }, [isWeb, loadSupportState, removeDevice, user?.email, currentSubscription])

  const handleRefreshSubscription = useCallback(async () => {
    if (!isWeb) {
      return
    }
    setIsRefreshingSubscription(true)
    setDeviceError(null)
    try {
      await loadSupportState()
    } catch (refreshError) {
      console.error('settings: unable to refresh subscription state', refreshError)
      setDeviceError(refreshError instanceof Error ? refreshError.message : 'Verifica del dispositivo non riuscita.')
    } finally {
      setIsRefreshingSubscription(false)
    }
  }, [isWeb, loadSupportState])

  const thisDeviceEndpoint = currentSubscription?.endpoint
  const thisDeviceRegistered = useMemo(() => {
    if (!thisDeviceEndpoint) {
      return false
    }
    return settings.subscriptions.some((subscription) => subscription.endpoint === thisDeviceEndpoint)
  }, [settings.subscriptions, thisDeviceEndpoint])

  const pushStatusMessage = useMemo(() => {
    if (!supportState) {
      return 'Verifica compatibilità push in corso...'
    }

    if (!supportState.supported) {
      return supportState.reason
    }

    if (supportState.permission === 'denied') {
      return 'Consenti le notifiche dal browser per attivare le push.'
    }

    if (thisDeviceRegistered) {
      return 'Le push sono attive su questo dispositivo.'
    }

    return 'Attiva le push per ricevere aggiornamenti in tempo reale.'
  }, [supportState, thisDeviceRegistered])

  const isBusy = isLoading || isRegistering || isUnregistering

  const handleRemoveDevice = async (endpoint: string) => {
    if (endpoint === currentSubscription?.endpoint) return handleUnregisterDevice()
    await removeDevice(endpoint).catch(() => undefined) // Error is shown by the hook.
  }
  const handleDisableAll = async () => {
    setIsUnregistering(true)
    setDeviceError(null)
    try {
      if (user?.email) await rememberPushDevice(user.email, currentSubscription?.endpoint ?? '', false)
      await disableAll()
      await webPushSupport.unsubscribe()
      await loadSupportState()
    } catch (err) { setDeviceError(err instanceof Error ? err.message : 'Disattivazione non riuscita.') }
    finally { setIsUnregistering(false) }
  }

  return (
    <View style={{ flex: 1 }}>
      <ScreenContent title="Impostazioni">
        <YStack gap="$6" width="100%">
          <YStack gap="$2">
            <H2 color="$color12">Centro impostazioni</H2>
            <Paragraph color="$color10">
              Configura preferenze personali e notifiche push per restare aggiornato sui momenti chiave della tua lega.
            </Paragraph>
          </YStack>

          <Card padding="$5" gap="$4" backgroundColor="$color2" borderRadius="$5" borderWidth="$0.5" borderColor="$color4">
            <XStack alignItems="center" gap="$3">
              <Bell size="$2" color="$blue10" />
              <YStack>
                <H3 color="$color12">Notifiche push</H3>
                <Text color="$color10">Gestisci le notifiche e i dispositivi registrati</Text>
              </YStack>
            </XStack>

            <Separator />

            {(error || deviceError) && (
              <XStack backgroundColor="$red3" borderRadius="$3" padding="$3" gap="$3" alignItems="center">
                <TriangleAlert size="$1" color="$red10" />
                <Text color="$red11">{deviceError || error}</Text>
              </XStack>
            )}

            <YStack gap="$3">
              {notificationToggles.map((toggle) => {
                const value = Boolean(settings[toggle.key as keyof PushNotificationSettings])
                return (
                  <Card
                    key={String(toggle.key)}
                    padding="$3"
                    borderRadius="$4"
                    borderWidth="$0.5"
                    borderColor="$color4"
                    backgroundColor="$color1"
                  >
                    <XStack justifyContent="space-between" alignItems="center" gap="$3">
                      <XStack gap="$3" alignItems="center" flex={1}>
                        {toggle.icon}
                        <YStack gap="$1" flex={1}>
                          <Text fontWeight="600" color="$color12">{toggle.title}</Text>
                          <Paragraph color="$color10" fontSize="$2">
                            {toggle.description}
                          </Paragraph>
                        </YStack>
                      </XStack>
                      <ToggleChip
                        label={value ? 'Attiva' : 'Disattiva'}
                        accent="green"
                        checked={value}
                        disabled={isBusy}
                        onCheckedChange={(checked) => { void handleToggle(toggle.key, checked).catch(() => undefined) }}
                      />
                    </XStack>
                  </Card>
                )
              })}
            </YStack>

            <Separator />

            <YStack gap="$3">
              <XStack gap="$2" alignItems="center">
                <Smartphone size="$1.5" color="$color11" />
                <Text fontWeight="600" color="$color11">Dispositivi registrati ({summary.totalDevices})</Text>
              </XStack>

              <Paragraph color="$color10">{pushStatusMessage}</Paragraph>

              <XStack gap="$2" flexWrap="wrap">
                <Button
                  icon={isRegistering ? <Loader2 size="$1" color="$color1" /> : <CheckCircle2 size="$1" color="$color1" />}
                  onPress={handleRegisterDevice}
                  disabled={isBusy || !supportState?.supported || supportState?.permission === 'denied'}
                >
                  {isRegistering ? 'Attivazione in corso…' : 'Attiva su questo device'}
                </Button>
                <Button
                  icon={isUnregistering ? <Loader2 size="$1" color="$color1" /> : <ShieldOff size="$1" color="$color1" />}
                  variant="outlined"
                  onPress={handleUnregisterDevice}
                  disabled={isBusy || !thisDeviceRegistered}
                >
                  {isUnregistering ? 'Annullamento…' : 'Disattiva su questo device'}
                </Button>
                <Button
                  icon={isRefreshingSubscription ? <Loader2 size="$1" color="$color1" /> : <RefreshCcw size="$1" color="$color1" />}
                  variant="outlined"
                  onPress={handleRefreshSubscription}
                  disabled={isRefreshingSubscription}
                >
                  Aggiorna stato dispositivo
                </Button>
                <Button
                  icon={<Trash2 size="$1" color="$color1" />}
                  variant="outlined"
                  onPress={handleDisableAll}
                  disabled={isBusy || (summary.totalDevices === 0 && !summary.hasAnyEnabled)}
                >
                  Disattiva tutte le notifiche
                </Button>
              </XStack>

              <YStack gap="$2">
                {settings.subscriptions.length === 0 ? (
                  <Paragraph color="$color10">Nessun dispositivo registrato al momento.</Paragraph>
                ) : (
                  settings.subscriptions.map((subscription) => (
                    <Card key={subscription.endpoint} padding="$3" backgroundColor="$color1" borderRadius="$3" borderWidth="$0.5" borderColor="$color4">
                      <XStack justifyContent="space-between" alignItems="center" gap="$2">
                        <YStack gap="$1" flex={1}>
                          <Text fontWeight="600" color="$color12">{formatEndpoint(subscription.endpoint)}</Text>
                          <Paragraph color="$color10" fontSize="$2">
                            Chiave p256dh: {subscription.p256dh ? `${subscription.p256dh.slice(0, 16)}…` : 'n/d'}
                          </Paragraph>
                        </YStack>
                        <Button
                          size="$2"
                          variant="outlined"
                          icon={<Trash2 size="$1" color="$color1" />}
                          onPress={() => { void handleRemoveDevice(subscription.endpoint) }}
                          disabled={isBusy}
                        >
                          Rimuovi
                        </Button>
                      </XStack>
                    </Card>
                  ))
                )}
              </YStack>
            </YStack>

            <Separator />

            <XStack alignItems="center" justifyContent="space-between">
              <YStack gap="$1">
                <Text color="$color10">Ultimo aggiornamento</Text>
                <Text fontWeight="600" color="$color11">{formatTimestamp(lastUpdated)}</Text>
              </YStack>
              <Button
                icon={isLoading ? <Spinner size="small" color="$color1" /> : <RefreshCcw size="$1" color="$color1" />}
                onPress={() => refetch()}
                variant="outlined"
              >
                {isLoading ? 'Aggiornamento…' : 'Ricarica'}
              </Button>
            </XStack>
          </Card>

          <Card padding="$4" backgroundColor="$color2" borderRadius="$5" borderColor="$color4" borderWidth="$0.5">
            <H3 color="$color12">Sezioni in arrivo</H3>
            <Paragraph color="$color10">
              Qui aggiungeremo presto altre preferenze: personalizzazione tema, privacy, integrazione dispositivi mobili e molto altro.
            </Paragraph>
          </Card>
        </YStack>
      </ScreenContent>
    </View>
  )
}

export default PushNotificationScreen
