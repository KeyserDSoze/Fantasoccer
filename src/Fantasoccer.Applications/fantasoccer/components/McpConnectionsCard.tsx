import { useCallback, useEffect, useState } from 'react'
import { Button, Card, H3, Spinner, Text, YStack } from 'tamagui'
import { McpConnection, mcpConnectionService } from '../services/mcpConnectionService'

export function McpConnectionsCard({ email }: { email: string | null }) {
  const [connections, setConnections] = useState<McpConnection[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [pending, setPending] = useState<string | null>(null)
  const [confirmation, setConfirmation] = useState<string | null>(null)
  const refresh = useCallback(async () => {
    try {
      setConnections(await mcpConnectionService.list())
      setError(null)
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Errore di caricamento.') }
  }, [])

  useEffect(() => {
    if (!email) return
    let active = true
    mcpConnectionService.list().then(items => {
      if (active) { setConnections(items); setError(null) }
    }).catch(reason => {
      if (active) setError(reason instanceof Error ? reason.message : 'Errore di caricamento.')
    })
    return () => { active = false }
  }, [email])

  const disconnect = async (clientId: string) => {
    setPending(clientId)
    try {
      await mcpConnectionService.disconnect(clientId)
      setConfirmation(null)
      await refresh()
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Revoca non riuscita.') }
    finally { setPending(null) }
  }

  if (!email || (connections === null && !error)) return null
  return <Card padding="$4" backgroundColor="$color2" borderRadius="$4" borderWidth="$0.5" borderColor="$borderColor">
    <YStack gap="$3">
      <H3>Collegamenti MCP</H3>
      <Text>La revoca blocca subito l’accesso del collegamento, senza disconnetterti da Fantasoccer.</Text>
      {error ? <Text color="$red10">{error}</Text> : null}
      {connections?.length === 0 ? <Text>Nessun collegamento attivo.</Text> : null}
      {connections?.map(connection => <YStack key={connection.clientId} gap="$2">
        <Text fontWeight="600">{connection.displayName || connection.clientId}</Text>
        {confirmation === connection.clientId ? <>
          <Text>Vuoi revocare tutti gli accessi di questo client al tuo account?</Text>
          <Button disabled={!!pending} onPress={() => disconnect(connection.clientId)}>Conferma revoca</Button>
          <Button disabled={!!pending} onPress={() => setConfirmation(null)}>Annulla</Button>
        </> : <Button disabled={!!pending} onPress={() => setConfirmation(connection.clientId)}>Revoca accesso</Button>}
      </YStack>)}
      {pending ? <Spinner /> : null}
      <Button disabled={!!pending} onPress={refresh}>Aggiorna collegamenti</Button>
    </YStack>
  </Card>
}
