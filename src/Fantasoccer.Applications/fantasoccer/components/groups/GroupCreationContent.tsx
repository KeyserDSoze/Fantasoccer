import { useEffect, useRef, useState } from 'react'
import { useNavigation } from '@react-navigation/native'
import { Button, Card, H1, H2, H3, Input, Label, Paragraph, Spinner, Text, XStack, YStack } from 'tamagui'
import { Trophy, SlidersHorizontal, Users, Gavel } from '@tamagui/lucide-icons-2'
import type { NavigationProp } from '../../navigation'
import { useAuth } from '../../providers/AuthProvider'
import { useApp } from '../../providers/AppProvider'
import { workspaceService } from '../../services/workspaceService'
import { LoginButton } from '../../features/LoginButton'

const features = [
  { icon: Trophy, title: 'Un gruppo, più competizioni', text: 'Organizza più leghe, campionati e coppe con i tuoi amici. Riutilizza le squadre tra competizioni e tieni separati calendari e classifiche.' },
  { icon: SlidersHorizontal, title: 'Il regolamento lo decidi tu', text: 'Personalizza bonus, malus, moduli, cambi, fasce gol e impostazioni di lega: costruisci il fantacalcio che piace al tuo gruppo.' },
  { icon: Gavel, title: 'Dall’asta alla giornata live', text: 'Gestisci aste, rose, formazioni e scambi. Segui risultati e classifiche senza passare da fogli di calcolo e chat.' },
  { icon: Users, title: 'Tutti insieme, ognuno con il suo ruolo', text: 'Invita partecipanti e amministratori, condividi la proprietà delle squadre e conserva lo storico del tuo gruppo.' },
]

// Shared by the no-group home and the "new group" route for existing members.
export function GroupCreationContent({ showForm = true }: { showForm?: boolean }) {
  const { user, isAuthenticated } = useAuth()
  const { refreshGroups } = useApp()
  const navigation = useNavigation<NavigationProp>()
  const [name, setName] = useState('')
  const [code, setCode] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [created, setCreated] = useState(false)
  const pending = useRef(false)
  const request = useRef<AbortController | null>(null)
  useEffect(() => () => request.current?.abort(), [])

  async function create() {
    if (!isAuthenticated || !user || pending.current) return
    pending.current = true
    const controller = new AbortController()
    request.current = controller
    setBusy(true); setError('')
    try {
      await workspaceService.redeemGroupCode(name.trim(), code.trim(), controller.signal)
      if (controller.signal.aborted) return
      setCode(''); setCreated(true)
      await refreshGroups()
    } catch (cause) {
      if (!controller.signal.aborted) setError(cause instanceof Error ? cause.message : 'Non è stato possibile creare il gruppo.')
    } finally {
      pending.current = false
      if (!controller.signal.aborted) setBusy(false)
    }
  }

  return <YStack width="100%" maxWidth={1100} alignSelf="center" gap="$5">
    <Card backgroundColor="$blue2" borderColor="$blue6" borderWidth={1} padding="$5" gap="$4">
      <Text color="$blue11" fontWeight="700" letterSpacing={2}>FANTASOCCER · IL TUO GRUPPO</Text>
      <H1 color="$color12" fontSize="$10">Il tuo fantacalcio. Le tue regole.</H1>
      <Paragraph fontSize="$5" color="$color11">
        Una stagione da vivere insieme, non solo una classifica da guardare. Crea il tuo gruppo, invita gli amici e dai forma alle vostre sfide.
      </Paragraph>
      {user?.groups.length === 0 && <Paragraph color="$color10">
        Il tuo account è pronto. Puoi farti aggiungere a un gruppo esistente oppure crearne uno tuo con un codice di attivazione.
      </Paragraph>}
      {!showForm && (isAuthenticated
        ? <Button alignSelf="flex-start" onPress={() => navigation.navigate('CreateGroup')}>Crea il tuo gruppo</Button>
        : <LoginButton variant="primary" />)}
    </Card>

    <XStack gap="$3" flexWrap="wrap">
      {features.map(({ icon: Icon, title, text }) => <Card key={title} flexGrow={1} flexBasis={240} padding="$4" gap="$3" borderColor="$borderColor" borderWidth={1}>
        <Icon size={26} color="$blue10" />
        <H3 fontSize="$6">{title}</H3>
        <Paragraph color="$color10">{text}</Paragraph>
      </Card>)}
    </XStack>

    <Card padding="$5" gap="$4" borderWidth={1} borderColor="$green6" backgroundColor="$green2">
      <H2>Da un codice al vostro primo campionato</H2>
      <Paragraph>1. Ricevi un codice dal super admin di Fantasoccer.</Paragraph>
      <Paragraph>2. Scegli il nome del gruppo e attivalo qui sotto. Ogni codice crea un solo gruppo e non può essere riutilizzato.</Paragraph>
      <Paragraph>3. Invita gli amici, configura squadre e competizioni e scegli le vostre regole.</Paragraph>
      <Paragraph color="$color10">
        Stiamo preparando l’acquisto di gruppi con un costo annuale. Prezzi e pagamenti online non sono ancora disponibili: per ora l’attivazione avviene solo tramite codice, senza addebiti o rinnovi automatici.
      </Paragraph>
    </Card>

    <Card padding="$5" gap="$3" borderWidth={1} borderColor="$borderColor">
      <H2>{created ? 'Il tuo gruppo è pronto!' : 'Attiva il tuo gruppo'}</H2>
      {!isAuthenticated ? <><Paragraph>Accedi con Microsoft per usare il tuo codice. La registrazione dell’account è gratuita.</Paragraph><LoginButton variant="primary" /></>
        : !showForm ? <>
          <Paragraph>Hai ricevuto un codice dal super admin? Scegli il nome del gruppo e inizia a organizzare la tua stagione.</Paragraph>
          <Button alignSelf="flex-start" onPress={() => navigation.navigate('CreateGroup')}>Ho un codice · Crea il gruppo</Button>
        </>
        : created ? <>
          <Paragraph>Codice utilizzato con successo. Ora sei il responsabile del nuovo gruppo: configura le competizioni e invita i partecipanti.</Paragraph>
          <Button alignSelf="flex-start" onPress={() => navigation.navigate('GroupWorkspace')}>Configura il gruppo</Button>
        </> : <>
          <Label htmlFor="new-group-name">Nome del gruppo</Label>
          <Input id="new-group-name" aria-label="Nome del nuovo gruppo" value={name} onChangeText={setName} maxLength={200} disabled={busy} placeholder="Es. Gli amici del lunedì" />
          <Label htmlFor="new-group-code">Codice di attivazione</Label>
          <Input id="new-group-code" aria-label="Codice di attivazione" value={code} onChangeText={setCode} maxLength={80} autoCapitalize="characters" autoCorrect={false} disabled={busy} placeholder="FZ-…" />
          <Paragraph color="$color10">Non hai un codice? Richiedilo al super admin. Se un amministratore ti ha già invitato, aggiorna i tuoi gruppi dalla home.</Paragraph>
          {error && <Text color="$red10" accessibilityRole="alert">{error}</Text>}
          <Button alignSelf="flex-start" disabled={busy || name.trim().length < 2 || !code.trim()} onPress={() => void create()}>
            {busy ? <Spinner /> : 'Crea il mio gruppo'}
          </Button>
        </>}
      {user?.isGlobalAdministrator && <Button alignSelf="flex-start" variant="outlined" onPress={() => navigation.navigate('PlatformAdmin')}>Super admin · Genera codici</Button>}
    </Card>
  </YStack>
}
