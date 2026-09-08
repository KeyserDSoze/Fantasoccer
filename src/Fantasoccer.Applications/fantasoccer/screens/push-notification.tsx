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
import {
  PushSupportState,
  webPushSupport
} from 'services/webPushClient'

type PushToggleKey = 'matchEvents' | 'opponentMatchEvents' | 'marketEvents' | 'deploymentReminder' | 'endDay'

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
    description: 'Notifiche sulle offerte e sugli scambi della tua lega.',
    icon: <SquareCheck size={22} color="$green10" />
  },
  {
    key: 'deploymentReminder',
    title: 'Promemoria formazione',
    description: 'Ricevi un alert prima della deadline per schierare la tua squadra.',
    icon: <Clock size={22} color="$yellow10" />
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

  const isWeb = Platform.OS === 'web'

  const loadSupportState = useCallback(async () => {
    if (!isWeb) {
      setSupportState({ supported: false, reason: 'Registrazione push disponibile solo su Web desktop.' })
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
    try {
      const subscription = await webPushSupport.subscribe()
      if (!subscription) {
        return
      }

      await addDevice(subscription)
      setCurrentSubscription(subscription)
      await loadSupportState()
    } catch (registerError) {
      console.error('settings: failed to register push subscription', registerError)
    } finally {
      setIsRegistering(false)
    }
  }, [addDevice, isWeb, loadSupportState])

  const handleUnregisterDevice = useCallback(async () => {
    if (!isWeb) {
      return
    }

    setIsUnregistering(true)
    try {
      const subscription = await webPushSupport.unsubscribe()
      if (!subscription) {
        return
      }

      await removeDevice(subscription.endpoint)
      setCurrentSubscription(null)
      await loadSupportState()
    } catch (unregisterError) {
      console.error('settings: failed to unregister push subscription', unregisterError)
    } finally {
      setIsUnregistering(false)
    }
  }, [isWeb, loadSupportState, removeDevice])

  const handleRefreshSubscription = useCallback(async () => {
    if (!isWeb) {
      return
    }
    setIsRefreshingSubscription(true)
    try {
      await loadSupportState()
    } catch (refreshError) {
      console.error('settings: unable to refresh subscription state', refreshError)
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

            {error && (
              <XStack backgroundColor="$red3" borderRadius="$3" padding="$3" gap="$3" alignItems="center">
                <TriangleAlert size="$1" color="$red10" />
                <Text color="$red11">{error}</Text>
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
                        onCheckedChange={(checked) => handleToggle(toggle.key, checked)}
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
                  disabled={isRegistering || !supportState?.supported || supportState?.permission === 'denied'}
                >
                  {isRegistering ? 'Attivazione in corso…' : 'Attiva su questo device'}
                </Button>
                <Button
                  icon={isUnregistering ? <Loader2 size="$1" color="$color1" /> : <ShieldOff size="$1" color="$color1" />}
                  variant="outlined"
                  onPress={handleUnregisterDevice}
                  disabled={isUnregistering || !thisDeviceRegistered}
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
                  onPress={() => disableAll()}
                  disabled={summary.totalDevices === 0 && !settings.matchEvents && !settings.opponentMatchEvents && !settings.marketEvents && !settings.deploymentReminder && !settings.endDay}
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
                          onPress={() => removeDevice(subscription.endpoint)}
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
