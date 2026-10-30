import { useCallback, useEffect, useState } from 'react'
import { Shuffle } from '@tamagui/lucide-icons-2'
import { useToastController } from '@tamagui/toast'
import { Card, H3, Label, Separator, Spinner, Text, XStack, YStack } from 'tamagui'
import { ToggleChip } from '../ToggleChip'
import gameService, { type TeamLineupSettings } from '../../services/gameService'

interface LineupSettingsCardProps {
  groupId: string
  year: number
  teamId: string
}

// By default each pre-match save attempts to copy the lineup to the other competitions, respecting
// their rules/deadlines. With "different lineup per competition" the owner arranges each
// competition on its own and the game screen shows the competition switch and the copy button.
export function LineupSettingsCard({ groupId, year, teamId }: LineupSettingsCardProps) {
  const toast = useToastController()
  const [settings, setSettings] = useState<TeamLineupSettings | null>(null)
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    let active = true
    setSettings(null)
    gameService.getLineupSettings({ groupId, year, teamId }).then(value => { if (active) setSettings(value) }).catch(() => { if (active) setSettings(null) })
    return () => { active = false }
  }, [groupId, year, teamId])

  const toggle = useCallback(async (separate: boolean) => {
    setSaving(true)
    try {
      setSettings(await gameService.setLineupSettings({ groupId, year, teamId }, separate))
      toast.show(separate ? 'Ora puoi fare una formazione diversa per ogni competizione.' : 'Copia automatica attiva dai prossimi salvataggi prepartita.', { variant: 'success' })
    } catch (error: any) {
      toast.show(error?.message ?? 'Impossibile salvare l\'impostazione', { variant: 'error' })
    } finally { setSaving(false) }
  }, [groupId, year, teamId])

  if (!settings) return null
  return (
    <Card padding="$4" backgroundColor="$color2" borderRadius="$4" borderWidth="$0.5" borderColor="$borderColor">
      <YStack gap="$4">
        <XStack alignItems="center" gap="$2">
          <Shuffle size="$1.5" color="$blue10" />
          <H3 color="$color12">Formazioni</H3>
        </XStack>
        <Separator />
        <YStack gap="$2">
          <Label>Formazione diversa per ogni competizione</Label>
          <ToggleChip
            label={settings.separateLineups ? 'Attivo' : 'Disattivo'}
            accent="green"
            checked={settings.separateLineups}
            disabled={saving}
            onCheckedChange={checked => void toggle(checked)}
          />
          <Text fontSize="$2" color="$color10">
            {settings.separateLineups
              ? 'Ogni competizione ha la sua formazione. Nella schermata della partita trovi il cambio di competizione e "Copia formazione".'
              : `Ogni salvataggio prepartita tenta di copiare la formazione nelle altre competizioni${settings.competitions > 1 ? ` (${settings.competitions} in totale)` : ''}, rispettando regole e scadenze. Le copie rifiutate sono segnalate al salvataggio. I cambi live restano separati; attivare questa modalità non riallinea le formazioni già salvate.`}
          </Text>
        </YStack>
        {saving && <Spinner />}
      </YStack>
    </Card>
  )
}

export default LineupSettingsCard
