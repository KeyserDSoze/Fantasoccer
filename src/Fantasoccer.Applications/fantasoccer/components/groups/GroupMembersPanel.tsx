import { useEffect, useRef, useState } from 'react'
import { Button, Card, H3, Input, Paragraph, Spinner, Text, XStack, YStack } from 'tamagui'
import { workspaceService as api } from '../../services/workspaceService'
import type { WorkspaceMember, WorkspaceMemberLookup } from '../../contracts/workspace'

const rolesList = [{ value: 1, label: 'Lettura' }, { value: 2, label: 'Partecipante' }, { value: 4, label: 'Amministratore' }, { value: 8, label: 'Responsabile' }]
const roleLabel = (value: number) => rolesList.filter(role => (value & role.value) !== 0).map(role => role.label).join(', ') || 'Rimosso'
const validEmail = (value: string) => /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value.trim()) && value.trim().length <= 320

export function GroupMembersPanel({ groupId, roles, members, onChanged }: {
  groupId: string; roles: number; members: WorkspaceMember[]; onChanged: () => Promise<void>
}) {
  const [email, setEmail] = useState('')
  const [name, setName] = useState('')
  const [memberRoles, setMemberRoles] = useState(2)
  const [lookup, setLookup] = useState<WorkspaceMemberLookup | null>(null)
  const [searching, setSearching] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const searchRequest = useRef<AbortController | null>(null)
  const pending = useRef(false)
  const mounted = useRef(true)
  useEffect(() => { mounted.current = true; return () => { mounted.current = false; searchRequest.current?.abort() } }, [])
  const canManage = (roles & 12) !== 0
  const canManageOwners = (roles & 8) !== 0
  const editingOwner = lookup && members.some(member => member.email.toLowerCase() === lookup.email.toLowerCase() && (member.roles & 8) !== 0)

  function changeEmail(value: string) {
    searchRequest.current?.abort(); setSearching(false); setEmail(value); setLookup(null); setName(''); setMemberRoles(2); setError(''); setNotice('')
  }
  async function search() {
    if (!canManage || !validEmail(email)) return
    searchRequest.current?.abort()
    const controller = new AbortController(); searchRequest.current = controller
    setSearching(true); setError(''); setNotice(''); setLookup(null)
    try {
      const result = await api.lookupMember(groupId, email, controller.signal)
      if (!controller.signal.aborted) {
        setLookup(result); setName(result.displayName ?? result.email.split('@')[0].slice(0, 200));
        setMemberRoles(members.find(member => member.email.toLowerCase() === result.email.toLowerCase())?.roles ?? 2)
      }
    } catch (cause) { if (!controller.signal.aborted) setError(cause instanceof Error ? cause.message : 'Ricerca non riuscita.') }
    finally { if (!controller.signal.aborted) setSearching(false) }
  }
  async function save() {
    if (!lookup || pending.current || !canManage || (editingOwner && !canManageOwners) || (!lookup.isMember && memberRoles === 0)) return
    pending.current = true; setBusy(true); setError(''); setNotice('')
    try {
      await api.setMember(groupId, { email: lookup.email, displayName: name.trim(), roles: memberRoles })
      if (!mounted.current) return
      await onChanged()
      if (mounted.current) { setNotice(memberRoles === 0 ? 'Membro rimosso dal gruppo.' : 'Associazione al gruppo salvata.'); setLookup(null); setEmail(''); setName('') }
    } catch (cause) { if (mounted.current) setError(cause instanceof Error ? cause.message : 'Modifica non riuscita.') }
    finally { pending.current = false; if (mounted.current) setBusy(false) }
  }

  return <YStack gap="$4">
    <Card padding="$4" gap="$3"><H3>Membri del gruppo</H3>
      {members.filter(member => member.roles !== 0).map(member => <XStack key={member.userId} gap="$2" alignItems="center" flexWrap="wrap">
        <YStack flex={1}><Text>{member.displayName} · {member.email}</Text><Text color="$color10">{roleLabel(member.roles)}</Text></YStack>
        {canManage && <Button size="$2" disabled={busy || (!canManageOwners && (member.roles & 8) !== 0)} onPress={() => {
          searchRequest.current?.abort(); setSearching(false); setEmail(member.email); setName(member.displayName); setMemberRoles(member.roles)
          setLookup({ email: member.email, displayName: member.displayName, found: true, isMember: true }); setError(''); setNotice('')
        }}>Modifica membro</Button>}
      </XStack>)}
    </Card>
    {canManage && <Card padding="$4" gap="$3">
      <H3>Aggiungi o invita tramite email</H3>
      <Paragraph>Puoi cercare un indirizzo email completo, non sfogliare tutti gli utenti della piattaforma. Le appartenenze ad altri gruppi restano private.</Paragraph>
      <Input aria-label="Email da cercare nel gruppo" placeholder="nome@example.com" value={email} onChangeText={changeEmail} disabled={busy} keyboardType="email-address" autoCapitalize="none" autoCorrect={false} maxLength={320} />
      <Button alignSelf="flex-start" variant="outlined" disabled={busy || searching || !validEmail(email)} onPress={() => void search()}>{searching ? <Spinner /> : 'Cerca email'}</Button>
      {lookup && <YStack gap="$3">
        <Paragraph>{lookup.isMember ? 'Questa persona è già nel gruppo: puoi modificarne il ruolo.' : lookup.found
          ? 'Account trovato. Puoi aggiungerlo a questo gruppo.'
          : 'Account non trovato: puoi predisporre l’accesso per questa email. La persona potrà entrare al primo login Microsoft con lo stesso indirizzo.'}</Paragraph>
        <Input aria-label="Nome del membro" value={name} onChangeText={setName} disabled={busy} placeholder="Nome del membro" maxLength={200} />
        <XStack gap="$2" flexWrap="wrap">{rolesList.map(role => <Button key={role.value} size="$2" disabled={busy || (role.value === 8 && !canManageOwners)}
          theme={(memberRoles & role.value) !== 0 ? 'blue' : undefined} onPress={() => setMemberRoles(value => value ^ role.value)}>{role.label}</Button>)}</XStack>
        {lookup.isMember && <Button alignSelf="flex-start" variant="outlined" disabled={busy || (!!editingOwner && !canManageOwners)} onPress={() => setMemberRoles(0)}>Rimuovi dal gruppo</Button>}
        <Button alignSelf="flex-start" disabled={busy || name.trim().length < 2 || (!lookup.isMember && memberRoles === 0) || (!canManageOwners && (!!editingOwner || (memberRoles & 8) !== 0))} onPress={() => void save()}>{busy ? 'Salvataggio…' : 'Salva associazione'}</Button>
        <Paragraph color="$color10">L’associazione non invia email automaticamente. Nessun ruolo del gruppo concede privilegi di piattaforma. La rimozione non cancella lo storico.</Paragraph>
      </YStack>}
    </Card>}
    {error && <Text color="$red10" accessibilityRole="alert">{error}</Text>}
    {notice && <Text color="$green10" accessibilityRole="alert">{notice}</Text>}
  </YStack>
}
