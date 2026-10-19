import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useAuth } from '../providers/AuthProvider'
import { UpdatePushNotificationSettingsInput, pushNotificationService } from '../services/pushNotificationService'
import {
  PushNotificationSettings,
  PushNotificationSummary,
  PushSubscription,
  EMPTY_PUSH_NOTIFICATION_SETTINGS,
  buildPushNotificationSummary
} from 'types/pushNotification'

interface BaseState {
  settings: PushNotificationSettings
  isLoading: boolean
  error: string | null
  lastUpdated: Date | null
}

const createInitialState = (): BaseState => ({
  settings: { ...EMPTY_PUSH_NOTIFICATION_SETTINGS, subscriptions: [] },
  isLoading: true,
  error: null,
  lastUpdated: null
})

export interface UsePushNotificationsOptions {
  /** Optional explicit email; if omitted the current user email is used */
  email?: string | null
  /** Enable automatic polling */
  autoRefresh?: boolean
  /** Polling interval in milliseconds (default 30s) */
  refreshInterval?: number
  /** Allow fetching (default true) */
  enabled?: boolean
}

export interface UsePushNotificationsResult {
  settings: PushNotificationSettings
  summary: PushNotificationSummary
  isLoading: boolean
  error: string | null
  lastUpdated: Date | null
  refetch: () => Promise<void>
  updateSettings: (updates: UpdatePushNotificationSettingsInput) => Promise<void>
  addDevice: (subscription: PushSubscription) => Promise<void>
  removeDevice: (endpoint: string) => Promise<void>
  disableAll: () => Promise<void>
}

export function usePushNotifications(options: UsePushNotificationsOptions = {}): UsePushNotificationsResult {
  const {
    email,
    autoRefresh = false,
    refreshInterval = 30000,
    enabled = true,
  } = options

  const useCurrentUser = useMemo(() => !email, [email])
  const [state, setState] = useState<BaseState>(createInitialState)
  const mountedRef = useRef(true)
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null)
  const requestRef = useRef(0)
  const mutationCount = useRef(0)
  const mutationTail = useRef<Promise<unknown>>(Promise.resolve())
  const scopeRef = useRef({ email, enabled })
  if (scopeRef.current.email !== email || scopeRef.current.enabled !== enabled) {
    scopeRef.current = { email, enabled }
    requestRef.current++
    mutationCount.current = 0
    mutationTail.current = Promise.resolve()
  }
  const mutationScope = scopeRef.current

  const handleError = useCallback((message: string, err: unknown) => {
    console.error(message, err)
    if (!mountedRef.current) {
      return
    }
    setState(prev => ({
      ...prev,
      isLoading: false,
      error: err instanceof Error ? err.message : message,
      lastUpdated: new Date()
    }))
  }, [])

  const fetchSettings = useCallback(async (isRefresh = false) => {
    if (!enabled || mutationCount.current > 0) {
      return
    }
    const request = ++requestRef.current

    if (!isRefresh) {
      setState(prev => ({ ...prev, isLoading: true, error: null }))
    }

    try {
      const settings = useCurrentUser
        ? await pushNotificationService.getCurrentUserSettings()
        : await pushNotificationService.getSettings(email!)

      if (!mountedRef.current || request !== requestRef.current) {
        return
      }

      if (!settings) {
        throw new Error('Impossibile recuperare le impostazioni di notifica')
      }

      setState({
        settings,
        isLoading: false,
        error: null,
        lastUpdated: new Date()
      })
    } catch (err) {
      if (request === requestRef.current) handleError('Errore nel caricamento delle impostazioni push', err)
    }
  }, [email, enabled, handleError, useCurrentUser])

  const runMutation = useCallback(
    (operation: () => Promise<PushNotificationSettings | null | undefined>, fallbackMessage: string): Promise<void> => {
      const scope = mutationScope
      if (scope !== scopeRef.current || !scope.enabled || !mountedRef.current) {
        return Promise.reject(new Error('Account cambiato durante il salvataggio.'))
      }
      ++requestRef.current // Invalidate any polling request already in flight.
      mutationCount.current++
      setState(prev => ({ ...prev, isLoading: true, error: null }))
      const task = mutationTail.current.catch(() => undefined).then(async () => {
        try {
          if (scope !== scopeRef.current || !scope.enabled || !mountedRef.current) throw new Error('Account cambiato durante il salvataggio.')
          const result = await operation()
          if (!mountedRef.current || scope !== scopeRef.current) throw new Error('Account cambiato durante il salvataggio.')
          if (!result) throw new Error(fallbackMessage)
          setState({
            settings: result,
            isLoading: mutationCount.current > 1,
            error: null,
            lastUpdated: new Date()
          })
        } catch (err) {
          if (scope === scopeRef.current) handleError(fallbackMessage, err)
          throw err
        } finally {
          if (scope === scopeRef.current) {
            mutationCount.current--
            if (mountedRef.current) setState(prev => ({ ...prev, isLoading: mutationCount.current > 0 }))
          }
        }
      })
      mutationTail.current = task
      return task
    },
    [mutationScope, handleError]
  )

  useEffect(() => {
    setState({ ...createInitialState(), isLoading: enabled })
  }, [email, useCurrentUser, enabled])

  useEffect(() => {
    mountedRef.current = true
    if (enabled) {
      fetchSettings()
    }

    return () => {
      mountedRef.current = false
      requestRef.current++
      if (intervalRef.current) {
        clearInterval(intervalRef.current)
      }
    }
  }, [enabled, fetchSettings])

  useEffect(() => {
    if (!autoRefresh || !enabled) {
      return undefined
    }

    if (intervalRef.current) {
      clearInterval(intervalRef.current)
    }

    intervalRef.current = setInterval(() => {
      if (!mountedRef.current) {
        return
      }
      fetchSettings(true)
    }, refreshInterval)

    return () => {
      if (intervalRef.current) {
        clearInterval(intervalRef.current)
      }
    }
  }, [autoRefresh, enabled, refreshInterval, fetchSettings])

  const refetch = useCallback(() => fetchSettings(false), [fetchSettings])

  const updateSettings = useCallback(
    (updates: UpdatePushNotificationSettingsInput) => runMutation(
      () => useCurrentUser
        ? pushNotificationService.updateCurrentUserSettings(updates)
        : pushNotificationService.updateSettings(email!, updates),
      'Errore durante il salvataggio delle impostazioni'
    ),
    [email, runMutation, useCurrentUser]
  )

  const addDevice = useCallback(
    (subscription: PushSubscription) => runMutation(
      () => useCurrentUser
        ? pushNotificationService.addDeviceForCurrentUser(subscription)
        : pushNotificationService.addDevice(email!, subscription),
      'Errore durante la registrazione del dispositivo'
    ),
    [email, runMutation, useCurrentUser]
  )

  const removeDevice = useCallback(
    (endpoint: string) => runMutation(
      () => useCurrentUser
        ? pushNotificationService.removeDeviceForCurrentUser(endpoint)
        : pushNotificationService.removeDevice(email!, endpoint),
      'Errore durante la rimozione del dispositivo'
    ),
    [email, runMutation, useCurrentUser]
  )

  const disableAll = useCallback(
    () => runMutation(
      () => useCurrentUser
        ? pushNotificationService.disableAllForCurrentUser()
        : pushNotificationService.disableAll(email!),
      'Errore durante la disattivazione delle notifiche'
    ),
    [email, runMutation, useCurrentUser]
  )

  const summary = useMemo(() => buildPushNotificationSummary(state.settings), [state.settings])

  return {
    settings: state.settings,
    summary,
    isLoading: state.isLoading,
    error: state.error,
    lastUpdated: state.lastUpdated,
    refetch,
    updateSettings,
    addDevice,
    removeDevice,
    disableAll
  }
}

export function useCurrentUserPushNotifications(options: Omit<UsePushNotificationsOptions, 'email'> = {}): UsePushNotificationsResult {
  const { user, isAuthenticated } = useAuth()
  return usePushNotifications({ ...options, email: user?.email, enabled: (options.enabled ?? true) && isAuthenticated })
}
