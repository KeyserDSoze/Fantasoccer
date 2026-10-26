import { useCallback, useEffect, useRef, useState } from 'react'
import { Button, Card, H2, H3, Input, Paragraph, Spinner, Text, XStack, YStack } from 'tamagui'
import { ScreenContent } from '../components/layout/ScreenContent'
import { useAuth } from '../providers/AuthProvider'
import { workspaceService as api } from '../services/workspaceService'
import type { WorkspaceAccount, WorkspaceGroup, WorkspaceDetails } from '../contracts/workspace'
import { getActualFantacalcioYear } from '../utils/timezoneHelpers'
import { useApp } from '../providers/AppProvider'
import { useNavigation } from '@react-navigation/native'
import type { NavigationProp } from '../navigation'

const roleOptions = [{ value: 1, label: 'Lettura' }, { value: 2, label: 'Partecipante' }, { value: 4, label: 'Amministratore' }, { value: 8, label: 'Responsabile' }]
const roleLabel = (roles: number) => roleOptions.filter(role => (roles & role.value) !== 0).map(role => role.label).join(', ') || 'Rimosso'

export default function GroupWorkspaceScreen() {
  const { isAuthenticated } = useAuth()
  const { refreshGroups } = useApp()
  const navigation = useNavigation<NavigationProp>()
  const [account, setAccount] = useState<WorkspaceAccount | null>(null)
  const [groups, setGroups] = useState<WorkspaceGroup[]>([])
  const [groupId, setGroupId] = useState('')
  const [details, setDetails] = useState<WorkspaceDetails | null>(null)
  const [loading, setLoading] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [rename, setRename] = useState('')
  const [email, setEmail] = useState('')
  const [displayName, setDisplayName] = useState('')
  const [roles, setRoles] = useState(2)
  const [season, setSeason] = useState(String(getActualFantacalcioYear()))
  const [basketName, setBasketName] = useState('')
  const [leagueName, setLeagueName] = useState('')
  const [leagueBaskets, setLeagueBaskets] = useState<string[]>([])
  const [basketId, setBasketId] = useState('')
  const [teamName, setTeamName] = useState('')
  const [ownerId, setOwnerId] = useState('')
  const [coOwners, setCoOwners] = useState<string[]>([])
  const sequence = useRef(0)

  const loadGroups = useCallback(async (signal?: AbortSignal) => {
    const [profile, list] = await Promise.all([api.account(signal), api.groups(signal)])
    if (signal?.aborted) return
    setAccount(profile); setGroups(list)
    setGroupId(previous => list.some(group => group.groupId === previous) ? previous : list[0]?.groupId || '')
  }, [])
  useEffect(() => {
    if (!isAuthenticated) { setDetails(null); setAccount(null); setGroups([]); return }
    const controller = new AbortController()
    setLoading(true)
    loadGroups(controller.signal).catch(cause => { if (!controller.signal.aborted) setError(cause instanceof Error ? cause.message : 'Caricamento non riuscito.') })
      .finally(() => { if (!controller.signal.aborted) setLoading(false) })
    return () => controller.abort()
  }, [isAuthenticated, loadGroups])

  useEffect(() => {
    const current = ++sequence.current
    setDetails(null); setError(''); setNotice(''); setLeagueBaskets([]); setBasketId(''); setOwnerId(''); setCoOwners([])
    setEmail(''); setDisplayName(''); setRoles(2)
    if (!groupId || !isAuthenticated) return
    const controller = new AbortController()
    setLoading(true)
    api.details(groupId, controller.signal).then(value => {
      if (current !== sequence.current || controller.signal.aborted) return
      setDetails(value); setRename(value.name)
    }).catch(cause => { if (!controller.signal.aborted) setError(cause instanceof Error ? cause.message : 'Caricamento non riuscito.') })
      .finally(() => { if (current === sequence.current && !controller.signal.aborted) setLoading(false) })
    return () => controller.abort()
  }, [groupId, isAuthenticated])

  async function execute(operation: () => Promise<unknown>) {
    const current = sequence.current
    setBusy(true); setError(''); setNotice('')
    try {
      await operation()
      await loadGroups()
      await refreshGroups()
      if (groupId) {
        const updated = await api.details(groupId)
        if (current === sequence.current) setDetails(updated)
      }
      if (current === sequence.current) setNotice('Modifica salvata in PostgreSQL.')
    } catch (cause) { if (current === sequence.current) setError(cause instanceof Error ? cause.message : 'Operazione non riuscita.') }
    finally { setBusy(false) }
  }
  const index = Number(season)
  const validSeason = Number.isInteger(index) && index >= 1 && index <= 200
  const baskets = details?.baskets.filter(basket => basket.seasonIndices.includes(index)) || []
  const activeMembers = details?.members.filter(member => member.roles !== 0) || []
  const canManage = !!details && (details.roles & 12) !== 0
  const canManageOwners = !!details && (details.roles & 8) !== 0
  const toggle = (values: string[], value: string) => values.includes(value) ? values.filter(item => item !== value) : [...values, value]

  return <ScreenContent><YStack gap="$4" maxWidth={1000} width="100%" alignSelf="center">
    <H2>{account && groups.length === 0 ? 'Nessun gruppo associato' : 'Gruppi'}</H2>
    {account && groups.length === 0 ? <Card padding="$4" gap="$2">
      <Paragraph>Chiedi a un amministratore di invitarti oppure crea il tuo gruppo con un codice di attivazione monouso.</Paragraph>
      <Button alignSelf="flex-start" onPress={() => navigation.navigate('CreateGroup')}>Scopri e crea un gruppo</Button>
    </Card> : <Card padding="$4" gap="$2">
      <Paragraph>Gestisci membri, basket, partecipazioni e proprietà delle squadre del gruppo selezionato.</Paragraph>
      <Paragraph>Completare l&apos;import prima di modificare i dati: la prima scrittura interattiva blocca le successive reimportazioni sullo stesso database.</Paragraph>
    </Card>}
    {!isAuthenticated && <Text>Accedi per visualizzare i tuoi gruppi.</Text>}
    {error && <Card padding="$3"><Text color="$red10" accessibilityRole="alert">{error}</Text></Card>}
    {notice && <Text accessibilityRole="alert">{notice}</Text>}
    {(loading || busy) && <Spinner />}
    {account && <Paragraph>Account: {account.displayName} ({account.email})</Paragraph>}
    <XStack gap="$2" flexWrap="wrap">{groups.map(group => <Button key={group.groupId} disabled={busy} theme={group.groupId === groupId ? 'blue' : undefined}
      onPress={() => setGroupId(group.groupId)}>{group.name}</Button>)}</XStack>
    {account && groups.length > 0 && <Button alignSelf="flex-start" onPress={() => navigation.navigate('CreateGroup')}>Crea un nuovo gruppo</Button>}
    {details && <>
      <H3>{details.name}</H3><Paragraph>I tuoi ruoli: {roleLabel(details.roles)}</Paragraph>
      <Card padding="$4" gap="$2"><H3>Membri</H3>{details.members.map(member => <XStack key={member.userId} gap="$2" alignItems="center" flexWrap="wrap">
        <Text>{member.displayName} · {member.email} · {roleLabel(member.roles)}</Text>
        {canManage && <Button size="$2" disabled={busy} onPress={() => { setEmail(member.email); setDisplayName(member.displayName); setRoles(member.roles) }}>Modifica membro</Button>}
      </XStack>)}</Card>
      {canManage && <Card padding="$4" gap="$2"><H3>Amministrazione gruppo</H3>
        <Input aria-label="Nome gruppo" value={rename} onChangeText={setRename} />
        <Button disabled={busy || rename.trim().length < 2} onPress={() => execute(() => api.renameGroup(groupId, rename))}>Salva nome gruppo</Button>
        <Input aria-label="Email membro" placeholder="Email membro" value={email} onChangeText={setEmail} autoCapitalize="none" keyboardType="email-address" />
        <Input aria-label="Nome membro" placeholder="Nome membro" value={displayName} onChangeText={setDisplayName} />
        <XStack gap="$2" flexWrap="wrap">{roleOptions.map(role => <Button key={role.value} size="$2" disabled={busy || (role.value === 8 && !canManageOwners)}
          theme={(roles & role.value) !== 0 ? 'blue' : undefined} onPress={() => setRoles(value => value ^ role.value)}>{role.label}</Button>)}</XStack>
        <Paragraph>Nessun ruolo = membro rimosso; lo storico non viene cancellato. Le modifiche non cambiano il profilo globale degli altri utenti.</Paragraph>
        <Button disabled={busy || !email.includes('@') || displayName.trim().length < 2} onPress={() => execute(() => api.setMember(groupId, { email, displayName, roles }))}>Salva membro</Button>
      </Card>}
      <Card padding="$4" gap="$2"><H3>Basket e competizioni</H3>
        {details.baskets.map(basket => <Paragraph key={basket.basketId}>{basket.name} · stagioni {basket.seasonIndices.join(', ')}</Paragraph>)}
        {details.leagues.map(league => <Paragraph key={league.leagueId}>{league.name} · {league.seasons.map(value => `${value.seasonIndex}: ${value.basketIds.map(id => details.baskets.find(b => b.basketId === id)?.name || id).join(', ')}`).join('; ')}</Paragraph>)}
      </Card>
      {canManage && <Card padding="$4" gap="$2"><H3>Configura stagione</H3>
        <Input aria-label="Indice stagione" value={season} onChangeText={value => { setSeason(value); setBasketId(''); setLeagueBaskets([]) }} keyboardType="number-pad" />
        <Paragraph>Indice stagione, non anno solare: 2012/13 = 1.</Paragraph>
        <Input aria-label="Nome basket" placeholder="Nome basket" value={basketName} onChangeText={setBasketName} />
        <Button disabled={busy || !validSeason || basketName.trim().length < 2} onPress={() => execute(async () => { await api.createBasket(groupId, basketName, index); setBasketName('') })}>Crea basket stagionale</Button>
        <Input aria-label="Nome competizione" placeholder="Nome competizione" value={leagueName} onChangeText={setLeagueName} />
        <Paragraph>Seleziona i basket condivisi dalla competizione:</Paragraph>
        <XStack gap="$2" flexWrap="wrap">{baskets.map(basket => <Button key={basket.basketId} size="$2" disabled={busy}
          theme={leagueBaskets.includes(basket.basketId) ? 'blue' : undefined} onPress={() => setLeagueBaskets(value => toggle(value, basket.basketId))}>{basket.name}</Button>)}</XStack>
        <Button disabled={busy || !validSeason || leagueName.trim().length < 2 || leagueBaskets.length === 0} onPress={() => execute(async () => {
          await api.createLeague(groupId, leagueName, index, leagueBaskets); setLeagueName('')
        })}>Crea partecipazione della lega</Button>
      </Card>}
      <Card padding="$4" gap="$2"><H3>Squadre annuali</H3>{details.teams.map(team => <Paragraph key={team.teamId}>
        {team.name} · stagione {team.seasonIndex} · {details.baskets.find(b => b.basketId === team.basketId)?.name} · {team.owners.map(owner => `${owner.displayName}${owner.isPrimary ? ' (proprietario)' : ''}`).join(', ')}
      </Paragraph>)}</Card>
      {canManage && <Card padding="$4" gap="$2"><H3>Nuova squadra per la stagione selezionata</H3>
        <Input aria-label="Nome squadra" placeholder="Nome squadra" value={teamName} onChangeText={setTeamName} />
        <Paragraph>Basket:</Paragraph><XStack gap="$2" flexWrap="wrap">{baskets.map(basket => <Button key={basket.basketId} size="$2" disabled={busy}
          theme={basketId === basket.basketId ? 'blue' : undefined} onPress={() => setBasketId(basket.basketId)}>{basket.name}</Button>)}</XStack>
        <Paragraph>Proprietario:</Paragraph><XStack gap="$2" flexWrap="wrap">{activeMembers.map(member => <Button key={member.userId} size="$2" disabled={busy}
          theme={ownerId === member.userId ? 'blue' : undefined} onPress={() => { setOwnerId(member.userId); setCoOwners(values => values.filter(id => id !== member.userId)) }}>{member.displayName}</Button>)}</XStack>
        <Paragraph>Comproprietari facoltativi:</Paragraph><XStack gap="$2" flexWrap="wrap">{activeMembers.filter(member => member.userId !== ownerId).map(member => <Button key={member.userId} size="$2" disabled={busy}
          theme={coOwners.includes(member.userId) ? 'blue' : undefined} onPress={() => setCoOwners(value => toggle(value, member.userId))}>{member.displayName}</Button>)}</XStack>
        <Button disabled={busy || !validSeason || !basketId || !ownerId || teamName.trim().length < 2} onPress={() => execute(async () => {
          await api.createTeam(groupId, { basketId, seasonIndex: index, name: teamName, ownerUserId: ownerId, additionalOwnerUserIds: coOwners }); setTeamName('')
        })}>Crea squadra</Button>
      </Card>}
    </>}
  </YStack></ScreenContent>
}
