import { useCallback, useEffect, useMemo, useState } from 'react'
import { useToastController } from '@tamagui/toast'
import { Button, Card, Paragraph, Spinner, Text, XStack, YStack } from 'tamagui'
import { ToggleChip } from 'components/ToggleChip'
import participationService from 'services/participationService'
import type { LeagueParticipants, QualificationCandidate } from 'types/participation'

interface LeagueParticipantsPanelProps {
  groupId: string
  leagueId: string
  year: number
  derived: boolean
  // Called after a confirmation so the group graph (participants, derived teams) is reloaded.
  onChanged: () => void
}

const sourceLabel = (source: number) => ['iscrizione dal basket', 'ammissione manuale', 'qualificazione', 'storico importato'][source] ?? 'altro'
const candidateKey = (candidate: QualificationCandidate) => `${candidate.sourceLeagueId}:${candidate.originTeamId}`

// Participants of one league season. For a derived league the manager reviews the
// qualification preview (first N of each source league) and confirms an explicit
// selection; confirmed participants stay stable when source standings change.
export function LeagueParticipantsPanel({ groupId, leagueId, year, derived, onChanged }: LeagueParticipantsPanelProps) {
  const toast = useToastController()
  const [participants, setParticipants] = useState<LeagueParticipants | null>(null)
  const [candidates, setCandidates] = useState<QualificationCandidate[]>([])
  const [selection, setSelection] = useState<Set<string>>(new Set())
  const [loading, setLoading] = useState(false)
  const [confirming, setConfirming] = useState(false)

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const current = await participationService.getParticipants(groupId, leagueId, year)
      setParticipants(current)
      if (derived) {
        const preview = await participationService.getQualification(groupId, leagueId, year)
        setCandidates(preview.candidates.filter(x => x.rank > 0))
        setSelection(new Set(preview.candidates.filter(x => x.qualified && !x.alreadyAdmitted && !x.problem).map(candidateKey)))
      }
    } catch (error: any) {
      toast.show(error?.message ?? 'Impossibile caricare i partecipanti', { variant: 'error' })
    } finally { setLoading(false) }
  }, [groupId, leagueId, year, derived])

  useEffect(() => { void load() }, [load])

  const selected = useMemo(() => candidates.filter(x => selection.has(candidateKey(x))), [candidates, selection])

  const confirm = useCallback(async () => {
    if (selected.length === 0) return
    setConfirming(true)
    try {
      setParticipants(await participationService.confirmQualification(groupId, leagueId, year,
        selected.map(x => ({ sourceLeagueId: x.sourceLeagueId, originTeamId: x.originTeamId }))))
      toast.show('Qualificazioni confermate', { variant: 'success' })
      onChanged()
      await load()
    } catch (error: any) {
      // e.g. league_owner_conflict: the same user would control two participants.
      toast.show(error?.message ?? 'Conferma non riuscita', { variant: 'error' })
    } finally { setConfirming(false) }
  }, [selected, groupId, leagueId, year, onChanged, load])

  return (
    <Card padding="$3" backgroundColor="$color1" borderRadius="$3" borderWidth="$0.5" borderColor="$borderColor">
      <YStack gap="$3">
        <XStack justifyContent="space-between" alignItems="center">
          <Text fontSize="$4" fontWeight="600" color="$color11">Partecipanti</Text>
          <Button size="$2" variant="outlined" onPress={() => void load()} disabled={loading}>Aggiorna</Button>
        </XStack>
        {loading && <Spinner />}
        {participants && participants.participants.length === 0 && !loading && (
          <Paragraph color="$color10" size="$2">
            {derived ? 'Nessuna squadra ammessa: conferma le qualificate dalle leghe di origine.' : 'Nessuna squadra iscritta.'}
          </Paragraph>
        )}
        {participants?.participants.map(participant => (
          <XStack key={participant.teamId} gap="$2" alignItems="center" flexWrap="wrap">
            <Text fontWeight="600" color={participant.isArchived ? '$color9' : '$color12'}>{participant.name}</Text>
            <Text fontSize="$2" color="$color10">
              {participant.owner}{participant.isDerived ? ' · derivata' : ''} · {sourceLabel(participant.source)}
              {participant.qualificationRank ? ` (${participant.qualificationRank}°)` : ''}
            </Text>
          </XStack>
        ))}
        {derived && candidates.length > 0 && (
          <YStack gap="$2">
            <Text fontSize="$3" fontWeight="600" color="$color11">Qualificazione</Text>
            <Paragraph size="$2" color="$color10">
              Le prime squadre di ogni lega di origine sono preselezionate. Puoi modificare la selezione: una volta confermate,
              le partecipazioni non cambiano con la classifica di origine. Un utente non può controllare due squadre della lega.
            </Paragraph>
            {candidates.map(candidate => (
              <XStack key={candidateKey(candidate)} gap="$2" alignItems="center" flexWrap="wrap">
                <ToggleChip
                  label={`${candidate.rank}° ${candidate.sourceLeagueName}: ${candidate.name}`}
                  accent={candidate.qualified ? 'green' : 'blue'}
                  checked={selection.has(candidateKey(candidate)) || candidate.alreadyAdmitted}
                  disabled={candidate.alreadyAdmitted || !!candidate.problem || confirming}
                  onCheckedChange={checked => setSelection(current => {
                    const next = new Set(current)
                    if (checked) next.add(candidateKey(candidate)); else next.delete(candidateKey(candidate))
                    return next
                  })}
                />
                <Text fontSize="$2" color="$color10">{candidate.owner}</Text>
                {candidate.alreadyAdmitted && <Text fontSize="$2" color="$green10">ammessa</Text>}
                {candidate.problem === 'derived_basket_missing' && (
                  <Text fontSize="$2" color="$orange10">collega un basket derivato dal suo basket</Text>
                )}
              </XStack>
            ))}
            <Button theme="blue" size="$3" disabled={selected.length === 0 || confirming} onPress={confirm}
              icon={confirming ? <Spinner size="small" /> : undefined}>
              {`Conferma ${selected.length} qualificate`}
            </Button>
          </YStack>
        )}
        {derived && candidates.length === 0 && !loading && (
          <Paragraph size="$2" color="$color10">
            Nessuna classifica disponibile per le leghe di origine di questa stagione.
          </Paragraph>
        )}
      </YStack>
    </Card>
  )
}

export default LeagueParticipantsPanel
