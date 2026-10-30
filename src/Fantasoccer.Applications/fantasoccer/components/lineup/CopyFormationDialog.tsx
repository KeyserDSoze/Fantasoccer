import { useCallback, useEffect, useState } from 'react'
import { ArrowLeftRight, Copy } from '@tamagui/lucide-icons-2'
import { useToastController } from '@tamagui/toast'
import { Button, Dialog, Paragraph, Spinner, Text, XStack, YStack } from 'tamagui'
import gameService, { type GameContext, type LineupCopySource } from '../../services/gameService'
import type { GameWrapper, LiveTeam } from '../../types/game'

interface CopyFormationDialogProps {
  gameWrapper: GameWrapper
  context: GameContext
  asAdmin: boolean
  // In SuperAdmin mode the side being edited; otherwise the caller's editable team.
  preferredSide?: 'home' | 'away' | null
  onCopied: () => void
  // Opens the same day of another competition of the same team.
  onSwitchCompetition: (source: LineupCopySource) => void
}

// Shown only when the owner chose separate lineups (team settings): by default the lineup is shared
// and copied automatically. Switches to another competition of the same team, or copies the lineup of
// the same roster from it for the same day. The copy is a one-off transfer: later edits of either lineup stay independent, and the
// server re-validates the target (rules, roles, bench, deadline, version, live).
export function CopyFormationDialog({ gameWrapper, context, asAdmin, preferredSide, onCopied, onSwitchCompetition }: CopyFormationDialogProps) {
  const toast = useToastController()
  const [open, setOpen] = useState(false)
  const [loading, setLoading] = useState(false)
  const [copying, setCopying] = useState(false)
  const [sources, setSources] = useState<LineupCopySource[]>([])
  const [checked, setChecked] = useState(false)
  const [selected, setSelected] = useState<LineupCopySource | null>(null)
  const editable = gameWrapper.teams.filter(team => team.canEdit)
  const target: LiveTeam | undefined = preferredSide === 'home' ? gameWrapper.teams[0] : preferredSide === 'away' ? gameWrapper.teams[1] : editable.length === 1 ? editable[0] : undefined

  const load = useCallback(async () => {
    if (!target) return
    setLoading(true); setSelected(null)
    try {
      setSources(await gameService.getCopySources({ groupId: context.groupId, leagueId: context.leagueId, year: context.year, day: gameWrapper.serieADay, teamId: target.teamId }))
    } catch (error: any) {
      toast.show(error?.message ?? 'Impossibile caricare le formazioni', { variant: 'error' })
      setSources([])
    } finally { setLoading(false); setChecked(true) }
  }, [context.groupId, context.leagueId, context.year, gameWrapper.serieADay, target?.teamId])

  // The button exists only when another competition shares this roster.
  useEffect(() => {
    setChecked(false); setSources([])
    if (target?.separateLineups && context.editMode !== 'view') void load()
  }, [load, context.editMode, target?.separateLineups])

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

  if (!target || !target.separateLineups || context.editMode === 'view' || (checked && sources.length === 0 && !open)) return null
  const switchable = sources.filter(source => source.gameId)
  return (
    <>
    {switchable.map(source => (
      <Button key={`switch:${source.leagueId}`} size="$3" icon={ArrowLeftRight} variant="outlined" borderColor="$purple8" onPress={() => onSwitchCompetition(source)}>
        {source.leagueName}
      </Button>
    ))}
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
          <XStack gap="$2" justifyContent="flex-end">
            <Dialog.Close asChild><Button size="$3">Annulla</Button></Dialog.Close>
            <Button size="$3" theme="blue" disabled={!selected || copying} onPress={copy} icon={copying ? <Spinner size="small" /> : undefined}>Copia</Button>
          </XStack>
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog>
    </>
  )
}

export default CopyFormationDialog
