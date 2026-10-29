import { useCallback, useEffect, useState } from 'react'
import { Copy } from '@tamagui/lucide-icons-2'
import { useToastController } from '@tamagui/toast'
import { Button, Dialog, Paragraph, Spinner, Switch, Text, XStack, YStack } from 'tamagui'
import gameService, { type GameContext, type LineupAutoCopy, type LineupCopySource } from '../../services/gameService'
import type { GameWrapper, LiveTeam } from '../../types/game'

interface CopyFormationDialogProps {
  gameWrapper: GameWrapper
  context: GameContext
  asAdmin: boolean
  // In SuperAdmin mode the side being edited; otherwise the caller's editable team.
  preferredSide?: 'home' | 'away' | null
  onCopied: () => void
}

// Copies the lineup of the same roster from another competition of the same day.
// It is a one-off transfer: later edits of either lineup stay independent, and the
// server re-validates the target (rules, roles, bench, deadline, version, live).
export function CopyFormationDialog({ gameWrapper, context, asAdmin, preferredSide, onCopied }: CopyFormationDialogProps) {
  const toast = useToastController()
  const [open, setOpen] = useState(false)
  const [loading, setLoading] = useState(false)
  const [copying, setCopying] = useState(false)
  const [sources, setSources] = useState<LineupCopySource[]>([])
  const [checked, setChecked] = useState(false)
  const [autoCopy, setAutoCopy] = useState<LineupAutoCopy | null>(null)
  const [savingAuto, setSavingAuto] = useState(false)
  const [selected, setSelected] = useState<LineupCopySource | null>(null)
  const editable = gameWrapper.teams.filter(team => team.canEdit)
  const target: LiveTeam | undefined = preferredSide === 'home' ? gameWrapper.teams[0] : preferredSide === 'away' ? gameWrapper.teams[1] : editable.length === 1 ? editable[0] : undefined

  const load = useCallback(async () => {
    if (!target) return
    setLoading(true); setSelected(null)
    try {
      setSources(await gameService.getCopySources({ groupId: context.groupId, leagueId: context.leagueId, year: context.year, day: gameWrapper.serieADay, teamId: target.teamId }))
      // Only the team's own owner can set it (not a SuperAdmin acting for someone else).
      setAutoCopy(asAdmin ? null : await gameService.getAutoCopy({ groupId: context.groupId, leagueId: context.leagueId, year: context.year, teamId: target.teamId }).catch(() => null))
    } catch (error: any) {
      toast.show(error?.message ?? 'Impossibile caricare le formazioni', { variant: 'error' })
      setSources([])
    } finally { setLoading(false); setChecked(true) }
  }, [context.groupId, context.leagueId, context.year, gameWrapper.serieADay, target?.teamId])

  // The button exists only when another competition shares this roster.
  useEffect(() => {
    setChecked(false); setSources([])
    if (target && context.editMode !== 'view') void load()
  }, [load, context.editMode])

  const toggleAutoCopy = useCallback(async (enabled: boolean) => {
    if (!target) return
    setSavingAuto(true)
    try {
      setAutoCopy(await gameService.setAutoCopy({ groupId: context.groupId, leagueId: context.leagueId, year: context.year }, target.teamId, enabled))
      toast.show(enabled ? 'Da ora ogni formazione salvata qui viene copiata anche nelle altre competizioni.' : 'Copia automatica disattivata.', { variant: 'success' })
    } catch (error: any) {
      toast.show(error?.message ?? 'Impossibile cambiare l\'impostazione', { variant: 'error' })
    } finally { setSavingAuto(false) }
  }, [context.groupId, context.leagueId, context.year, target?.teamId])

  const copy = useCallback(async () => {
    if (!target || !selected) return
    setCopying(true)
    try {
      const result = await gameService.copyFormation(context, target, { leagueId: selected.leagueId, teamId: selected.teamId }, asAdmin)
      toast.show(result.message ?? 'Formazione copiata.', { variant: 'success' })
      setOpen(false); onCopied()
    } catch (error: any) {
      toast.show(error?.message ?? 'Copia non riuscita', { variant: 'error' })
    } finally { setCopying(false) }
  }, [context, target, selected, asAdmin, onCopied])

  if (!target || context.editMode === 'view' || (checked && sources.length === 0 && !open)) return null
  return (
    <Dialog modal open={open} onOpenChange={value => { setOpen(value); if (value) void load() }}>
      <Dialog.Trigger asChild>
        <Button size="$3" icon={Copy} variant="outlined" borderColor="$blue8">Copia formazione</Button>
      </Dialog.Trigger>
      <Dialog.Portal>
        <Dialog.Overlay key="overlay" opacity={0.5} enterStyle={{ opacity: 0 }} exitStyle={{ opacity: 0 }} />
        <Dialog.Content bordered elevate key="content" gap="$3" width="90%" maxWidth={480}>
          <Dialog.Title size="$6">Copia formazione</Dialog.Title>
          <Dialog.Description>
            Destinazione: {target.name}. Scegli la competizione di origine con la stessa rosa: la copia non collega le due formazioni.
          </Dialog.Description>
          {loading ? <Spinner /> : sources.length === 0 ? (
            <Paragraph color="$color10">Nessun'altra competizione condivide questa rosa per la giornata {gameWrapper.serieADay}.</Paragraph>
          ) : (
            <YStack gap="$2">
              {sources.map(source => (
                <Button key={`${source.leagueId}:${source.teamId}`} justifyContent="flex-start" variant="outlined"
                  borderColor={selected === source ? '$blue8' : '$borderColor'} backgroundColor={selected === source ? '$blue2' : undefined}
                  onPress={() => setSelected(source)}>
                  <YStack>
                    <Text fontWeight="600">{source.leagueName}</Text>
                    <Text fontSize="$2" color="$color10">{source.teamName} · {source.hasLineup ? `formazione v${source.version}` : 'formazione predefinita'}</Text>
                  </YStack>
                </Button>
              ))}
            </YStack>
          )}
          {autoCopy && (
            <XStack gap="$3" alignItems="center" justifyContent="space-between" paddingVertical="$2" borderTopWidth={1} borderColor="$borderColor">
              <YStack flex={1}>
                <Text fontWeight="600">Usa questa come formazione principale</Text>
                <Text fontSize="$2" color="$color10">
                  Ogni volta che salvi la formazione qui, viene copiata anche in: {autoCopy.targets.map(x => x.leagueName).join(', ')}. Le modifiche fatte nelle altre competizioni non tornano qui.
                </Text>
              </YStack>
              <Switch size="$3" checked={autoCopy.enabled} disabled={savingAuto} onCheckedChange={value => void toggleAutoCopy(value)}>
                <Switch.Thumb />
              </Switch>
            </XStack>
          )}
          <XStack gap="$2" justifyContent="flex-end">
            <Dialog.Close asChild><Button size="$3">Annulla</Button></Dialog.Close>
            <Button size="$3" theme="blue" disabled={!selected || copying} onPress={copy} icon={copying ? <Spinner size="small" /> : undefined}>Copia</Button>
          </XStack>
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog>
  )
}

export default CopyFormationDialog
