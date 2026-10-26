import { useEffect, useState } from 'react'
import { Button, H2, Paragraph, Spinner, Text, YStack } from 'tamagui'
import { ScreenContent } from '../components/layout/ScreenContent'
import { GroupMembersPanel } from '../components/groups/GroupMembersPanel'
import { useApp } from '../providers/AppProvider'
import { useAuth } from '../providers/AuthProvider'
import { workspaceService as api } from '../services/workspaceService'
import type { WorkspaceDetails } from '../contracts/workspace'

export default function GroupUsersAdminScreen() {
  const { currentGroup } = useApp()
  const { user } = useAuth()
  return <ScreenContent>{!currentGroup ? <Paragraph>Seleziona un gruppo per gestire i membri.</Paragraph>
    : <GroupMembersContent key={`${currentGroup.id}:${user?.email}`} groupId={currentGroup.id} />}</ScreenContent>
}

function GroupMembersContent({ groupId }: { groupId: string }) {
  const { refreshGroups } = useApp()
  const [details, setDetails] = useState<WorkspaceDetails | null>(null)
  const [error, setError] = useState('')
  const [revision, setRevision] = useState(0)
  useEffect(() => {
    const controller = new AbortController()
    api.details(groupId, controller.signal).then(value => { if (!controller.signal.aborted) setDetails(value) })
      .catch(cause => { if (!controller.signal.aborted) setError(cause instanceof Error ? cause.message : 'Caricamento non riuscito.') })
    return () => controller.abort()
  }, [groupId, revision])
  return <YStack gap="$4" width="100%" maxWidth={1000} alignSelf="center">
    <H2>{details?.name ?? 'Membri del gruppo'}</H2>
    {error ? <><Text color="$red10" accessibilityRole="alert">{error}</Text><Button onPress={() => { setError(''); setRevision(value => value + 1) }}>Riprova</Button></> : !details ? <Spinner />
      : <GroupMembersPanel groupId={groupId} roles={details.roles} members={details.members} onChanged={async () => {
        setDetails(await api.details(groupId)); await refreshGroups()
      }} />}
  </YStack>
}
