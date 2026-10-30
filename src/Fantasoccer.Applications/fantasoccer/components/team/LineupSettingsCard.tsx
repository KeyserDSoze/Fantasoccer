import { useCallback, useEffect, useState } from 'react'
import { Shuffle } from '@tamagui/lucide-icons-2'
import { useToastController } from '@tamagui/toast'
import { Card, H3, Separator, Spinner, Switch, Text, XStack, YStack } from 'tamagui'
import gameService, { type TeamLineupSettings } from '../../services/gameService'

interface LineupSettingsCardProps {
  groupId: string
  year: number
  teamId: string
}

// By default the lineup of a team is the same in every competition it plays: a save in any of
// them is copied to the others. With "different lineup per competition" the owner arranges each
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
      toast.show(separate ? 'Ora puoi fare una formazione diversa per ogni competizione.' : 'La formazione sarà la stessa in tutte le competizioni.', { variant: 'success' })
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
        <XStack gap="$3" alignItems="center" justifyContent="space-between">
          <YStack flex={1} gap="$1">
            <Text fontWeight="600">Formazione diversa per ogni competizione</Text>
            <Text fontSize="$2" color="$color10">
              {settings.separateLineups
                ? 'Attivo: ogni competizione ha la sua formazione. Nella schermata della partita trovi il cambio di competizione e "Copia formazione".'
                : `Disattivo: la formazione è la stessa in tutte le competizioni di questa squadra${settings.competitions > 1 ? ` (${settings.competitions})` : ''} e ogni salvataggio viene copiato nelle altre.`}
            </Text>
          </YStack>
          <Switch size="$3" checked={settings.separateLineups} disabled={saving} onCheckedChange={value => void toggle(value)}>
            <Switch.Thumb />
          </Switch>
        </XStack>
        {saving && <Spinner />}
      </YStack>
    </Card>
  )
}

export default LineupSettingsCard
