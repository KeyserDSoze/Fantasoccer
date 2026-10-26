import { useEffect, useRef, useState } from 'react'
import { Button, Card, H2, H3, Input, Paragraph, Spinner, Text, XStack, YStack } from 'tamagui'
import { ScreenContent } from '../components/layout/ScreenContent'
import { useAuth } from '../providers/AuthProvider'
import { platformService as api, type GroupCode, type IssuedGroupCode, type PlatformGroup, type PlatformGroupDetails, type PlatformPage, type PlatformUser } from '../services/platformService'

type Tab = 'groups' | 'users' | 'codes'
const codeStatus = { Available: 'Disponibile', Used: 'Utilizzato', Revoked: 'Revocato' }

export default function PlatformAdminScreen() {
  const { user } = useAuth()
  if (!user?.isGlobalAdministrator) return <ScreenContent><Paragraph>Questa pagina è riservata al super admin della piattaforma.</Paragraph></ScreenContent>
  return <PlatformAdminContent key={user.email} />
}

function PlatformAdminContent() {
  const [tab, setTab] = useState<Tab>('groups')
  const [search, setSearch] = useState('')
  const [query, setQuery] = useState('')
  const [offset, setOffset] = useState(0)
  const [revision, setRevision] = useState(0)
  const [page, setPage] = useState<PlatformPage<PlatformGroup | PlatformUser | GroupCode> | null>(null)
  const [details, setDetails] = useState<PlatformGroupDetails | null>(null)
  const [label, setLabel] = useState('')
  const [issued, setIssued] = useState<IssuedGroupCode | null>(null)
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const pending = useRef(false)
  const actions = useRef(new AbortController())
  const detailRequest = useRef<AbortController | null>(null)
  useEffect(() => {
    actions.current = new AbortController()
    return () => { actions.current.abort(); detailRequest.current?.abort() }
  }, [])

  useEffect(() => {
    const controller = new AbortController()
    setPage(null); setLoading(true); setError('')
    const request = tab === 'users' ? api.users(offset, query, controller.signal)
      : tab === 'groups' ? api.groups(offset, query, controller.signal) : api.codes(offset, controller.signal)
    request.then(result => { if (!controller.signal.aborted) setPage(result) })
      .catch(cause => { if (!controller.signal.aborted) setError(cause instanceof Error ? cause.message : 'Caricamento non riuscito.') })
      .finally(() => { if (!controller.signal.aborted) setLoading(false) })
    return () => controller.abort()
  }, [tab, query, offset, revision])

  async function issue() {
    if (pending.current) return
    pending.current = true; setBusy(true); setError('')
    const signal = actions.current.signal
    try {
      const result = await api.issueCode(label, signal)
      if (!signal.aborted) { setIssued(result); setLabel(''); setOffset(0); setRevision(value => value + 1) }
    } catch (cause) { if (!signal.aborted) setError(cause instanceof Error ? cause.message : 'Generazione non riuscita.') }
    finally { pending.current = false; if (!signal.aborted) setBusy(false) }
  }

  async function revoke(id: string) {
    if (pending.current) return
    pending.current = true; setBusy(true); setError('')
    const signal = actions.current.signal
    try {
      await api.revokeCode(id, signal)
      if (!signal.aborted) { if (issued?.details.id === id) setIssued(null); setRevision(value => value + 1) }
    } catch (cause) { if (!signal.aborted) setError(cause instanceof Error ? cause.message : 'Revoca non riuscita.') }
    finally { pending.current = false; if (!signal.aborted) setBusy(false) }
  }

  async function openGroup(id: string) {
    detailRequest.current?.abort()
    const controller = new AbortController()
    detailRequest.current = controller
    setDetails(null); setError('')
    try { const value = await api.group(id, controller.signal); if (!controller.signal.aborted) setDetails(value) }
    catch (cause) { if (!controller.signal.aborted) setError(cause instanceof Error ? cause.message : 'Caricamento non riuscito.') }
  }

  return <ScreenContent><YStack width="100%" maxWidth={1100} alignSelf="center" gap="$4">
    <H2>Super admin · Piattaforma</H2>
    <Paragraph>Consulta tutti gli utenti e tutti i gruppi, anche quelli di cui non sei membro. La gestione del gruppo resta distinta dall’amministrazione della piattaforma.</Paragraph>
    <XStack gap="$2" flexWrap="wrap">
      {([['groups', 'Tutti i gruppi'], ['users', 'Tutti gli utenti'], ['codes', 'Codici di attivazione']] as const).map(([value, text]) => <Button key={value}
        theme={tab === value ? 'blue' : undefined} onPress={() => {
          detailRequest.current?.abort(); setTab(value); setOffset(0); setQuery(''); setSearch(''); setPage(null); setDetails(null)
        }}>{text}</Button>)}
    </XStack>

    {tab !== 'codes' && <XStack gap="$2" flexWrap="wrap">
      <Input flex={1} minWidth={180} aria-label="Cerca nella piattaforma" value={search} onChangeText={setSearch} maxLength={200} placeholder={tab === 'users' ? 'Nome o email' : 'Nome gruppo'} />
      <Button onPress={() => { setQuery(search.trim()); setOffset(0); setDetails(null) }}>Cerca</Button>
    </XStack>}

    {tab === 'codes' && <Card padding="$4" gap="$3" borderWidth={1} borderColor="$blue7">
      <H3>Genera un codice monouso</H3>
      <Paragraph>Ogni codice permette a un account autenticato di creare un solo gruppo. Il codice completo è mostrato una sola volta: copialo e condividilo privatamente.</Paragraph>
      <Input aria-label="Nota del codice" placeholder="Nota per riconoscere il codice (facoltativa)" value={label} onChangeText={setLabel} maxLength={200} disabled={busy} />
      <Button alignSelf="flex-start" disabled={busy} onPress={() => void issue()}>{busy ? 'Attendi…' : 'Genera codice'}</Button>
      {issued && <YStack gap="$2" padding="$3" backgroundColor="$green2">
        <Text fontWeight="bold">Codice appena generato · Copialo ora</Text>
        <Text selectable style={{ overflowWrap: 'anywhere' }}>{issued.code}</Text>
        <Paragraph>Non sarà recuperabile dopo aver lasciato questa pagina. Se lo perdi, revocalo e generane un altro.</Paragraph>
        <Button alignSelf="flex-start" variant="outlined" onPress={() => setIssued(null)}>Ho salvato il codice · Nascondi</Button>
      </YStack>}
    </Card>}

    {error && <Text color="$red10" accessibilityRole="alert">{error}</Text>}
    <Button alignSelf="flex-start" variant="outlined" disabled={loading} onPress={() => setRevision(value => value + 1)}>Aggiorna elenco</Button>
    {loading && <Spinner />}
    {page && <>
      <Paragraph>{page.total} {tab === 'users' ? 'utenti' : tab === 'groups' ? 'gruppi' : 'codici'} · {page.items.length === 0 ? 'Nessun risultato' : `${page.offset + 1}–${page.offset + page.items.length}`}</Paragraph>
      {tab === 'users' && (page.items as PlatformUser[]).map(item => <Card key={item.id} padding="$4" gap="$2">
        <H3>{item.displayName}</H3><Text selectable>{item.email ?? 'Email di accesso non configurata'}</Text>
        <Paragraph>{item.isEnabled ? 'Attivo' : 'Disabilitato'}{item.isGlobalAdministrator ? ' · Super admin di piattaforma' : ''}</Paragraph>
        <Paragraph>{item.groups.map(group => group.name).join(', ') || 'Nessun gruppo'}</Paragraph>
      </Card>)}
      {tab === 'groups' && (page.items as PlatformGroup[]).map(item => <Card key={item.id} padding="$4" gap="$2">
        <H3>{item.name}</H3><Paragraph>{item.members} membri · {item.leagues} leghe</Paragraph>
        <Button alignSelf="flex-start" variant="outlined" onPress={() => void openGroup(item.id)}>Visualizza gruppo</Button>
      </Card>)}
      {tab === 'codes' && (page.items as GroupCode[]).map(item => <Card key={item.id} padding="$4" gap="$2">
        <H3>{item.label || 'Codice senza nota'}</H3><Text>{codeStatus[item.status]} · {new Date(item.createdAtUtc).toLocaleString('it-IT')}</Text>
        <Text selectable color="$color10">ID: {item.id}</Text>
        {item.redeemedAtUtc && <Paragraph>Usato il {new Date(item.redeemedAtUtc).toLocaleString('it-IT')} · Gruppo: {item.redeemedGroupId} · Utente: {item.redeemedByUserId}</Paragraph>}
        {item.status === 'Available' && <Button alignSelf="flex-start" variant="outlined" disabled={busy} onPress={() => void revoke(item.id)}>Revoca codice</Button>}
      </Card>)}
      <XStack gap="$2"><Button disabled={loading || offset === 0} onPress={() => setOffset(Math.max(0, offset - 25))}>Precedente</Button>
        <Button disabled={loading || offset + page.items.length >= page.total} onPress={() => setOffset(offset + 25)}>Successiva</Button></XStack>
    </>}
    {details && <Card padding="$4" gap="$3" borderWidth={1} borderColor="$blue7">
      <H3>{details.name} · Dettagli</H3>
      <Paragraph fontWeight="bold">Membri</Paragraph>{details.members.map(member => <Text key={member.userId}>{member.displayName} · {member.email ?? 'Senza email'} · {member.isActive ? 'Attivo' : 'Disattivo'}</Text>)}
      <Paragraph fontWeight="bold">Leghe</Paragraph>{details.leagues.map(league => <Text key={league.id}>{league.name}{league.isArchived ? ' (archiviata)' : ''}</Text>)}
      <Button alignSelf="flex-start" variant="outlined" onPress={() => setDetails(null)}>Chiudi dettagli</Button>
    </Card>}
  </YStack></ScreenContent>
}
