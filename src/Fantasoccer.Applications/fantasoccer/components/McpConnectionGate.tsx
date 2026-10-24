import React, { ReactNode, useEffect, useState } from 'react'
import { Platform } from 'react-native'
import { Button, Spinner, Text, YStack } from 'tamagui'
import * as SplashScreen from 'expo-splash-screen'
import { useAuth } from '../providers/AuthProvider'
import { buildApiUrl, getApiConfig } from '../configurations/apiConfig'
import { tokenStorageService } from '../services/tokenStorageService'

const PENDING_KEY = 'fantasoccer_mcp_connection'

// Capture before AuthProvider handles the Microsoft callback and clears its URL.
function captureConnection(): string | null {
  if (Platform.OS !== 'web' || typeof window === 'undefined') return null
  try {
    const incoming = new URLSearchParams(window.location.search).get('mcp_request')
    if (incoming && /^[A-F0-9]{64}$/.test(incoming)) {
      window.sessionStorage.setItem(PENDING_KEY, incoming)
      window.history.replaceState({}, document.title, window.location.pathname)
    }
    const pending = window.sessionStorage.getItem(PENDING_KEY)
    return pending && /^[A-F0-9]{64}$/.test(pending) ? pending : null
  } catch { return null }
}

const initialConnection = captureConnection()

export function McpConnectionGate({ children }: { children: ReactNode }) {
  const [transactionId, setTransactionId] = useState(initialConnection)
  const [details, setDetails] = useState<{ clientName: string; description: string } | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const { isAuthenticated, isLoading, login, user } = useAuth()

  useEffect(() => {
    if (!transactionId) return
    void SplashScreen.hideAsync()
    const controller = new AbortController()
    fetch(buildApiUrl(`/connect/request/${transactionId}`), { signal: controller.signal, cache: 'no-store' })
      .then(async response => {
        if (!response.ok) throw new Error('Collegamento scaduto. Ricomincia dalla tua applicazione MCP.')
        setDetails(await response.json())
      })
      .catch(reason => { if (!controller.signal.aborted) setError(reason.message) })
    return () => controller.abort()
  }, [transactionId])

  const close = async () => {
    // Invalidate a pending request when the authenticated user explicitly denies it.
    try {
      const token = await tokenStorageService.getTokenString()
      if (token) await fetch(buildApiUrl('/connect/approve'), {
        method: 'POST', headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
        body: JSON.stringify({ transactionId, approved: false }),
      })
    } catch {
      // If offline, the browser transaction expires on the server after ten minutes.
    } finally {
      window.sessionStorage.removeItem(PENDING_KEY)
      setTransactionId(null)
    }
  }

  const approve = async () => {
    setBusy(true)
    setError(null)
    try {
      const token = await tokenStorageService.getTokenString()
      if (!token) throw new Error('Accedi nuovamente prima di autorizzare il collegamento.')
      const response = await fetch(buildApiUrl('/connect/approve'), {
        method: 'POST',
        headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
        body: JSON.stringify({ transactionId, approved: true }),
      })
      if (!response.ok) throw new Error('Autorizzazione non riuscita. Ricomincia il collegamento dalla tua applicazione MCP.')
      const { redirectUrl } = await response.json()
      const redirect = new URL(redirectUrl)
      if (redirect.origin !== new URL(getApiConfig().baseUrl).origin || redirect.pathname !== '/connect/complete')
        throw new Error('Destinazione di collegamento non valida.')
      window.sessionStorage.removeItem(PENDING_KEY)
      window.location.assign(redirect.href)
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Collegamento non riuscito.')
      setBusy(false)
    }
  }

  if (!transactionId) return <>{children}</>
  return (
    <YStack flex={1} justifyContent="center" alignItems="center" padding="$5" gap="$4" backgroundColor="$background">
      <Text fontSize="$7" fontWeight="bold">Collega Fantasoccer</Text>
      {!details && !error ? <Spinner /> : null}
      {details ? <>
        <Text>{details.clientName} richiede accesso al tuo account.</Text>
        <Text maxWidth={600}>Il nome è dichiarato dall'applicazione, non è un'identità verificata. Autorizza solo collegamenti avviati da te con un'app di cui ti fidi.</Text>
        <Text maxWidth={600}>{details.description}</Text>
        <Text maxWidth={600}>Potrà leggere dati e apportare modifiche quando glielo chiedi. I tuoi ruoli e permessi restano invariati.</Text>
      </> : null}
      {isAuthenticated ? <Text>Account: {user?.email}</Text> : null}
      {error ? <Text color="$red10">{error}</Text> : null}
      {isLoading || busy ? <Spinner /> : isAuthenticated
        ? <Button disabled={!details || !!error} onPress={approve}>Consenti accesso</Button>
        : <Button disabled={!details} onPress={() => login().catch(reason => setError(reason.message))}>Accedi con Microsoft</Button>}
      <Button disabled={busy} onPress={close}>Annulla</Button>
    </YStack>
  )
}
