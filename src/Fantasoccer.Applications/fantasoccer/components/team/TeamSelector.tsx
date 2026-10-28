import { Check, ChevronDown, Users } from '@tamagui/lucide-icons-2'
import { Adapt, Label, Paragraph, Select, Sheet, Text, XStack, YStack } from 'tamagui'
import type { MyTeam } from '../../hooks/useMyTeams'

interface TeamSelectorProps {
  teams: MyTeam[]
  value: string | null
  onChange: (teamId: string) => void
  label?: string
  id?: string
}

// Explicit team choice for users with several teams in the group. Derived teams
// are labelled: their roster and credits belong to the origin team.
export function TeamSelector({ teams, value, onChange, label = 'Squadra', id = 'team-selector' }: TeamSelectorProps) {
  if (teams.length === 0) return null
  if (teams.length === 1) {
    const team = teams[0]
    return (
      <XStack alignItems="center" gap="$2">
        <Users size={16} />
        <Text fontWeight="600">{team.name}</Text>
        <Paragraph size="$2" color="$color10">{team.basketName}{team.isDerived ? ' · derivata' : ''}</Paragraph>
      </XStack>
    )
  }
  return (
    <YStack gap="$1" width="100%">
      <Label htmlFor={id} size="$3">{label}</Label>
      <Select id={id} value={value ?? undefined} onValueChange={onChange}>
        <Select.Trigger width="100%" iconAfter={ChevronDown}>
          <Select.Value placeholder="Seleziona la squadra" />
        </Select.Trigger>
        <Adapt when="sm" platform="touch">
          <Sheet modal dismissOnSnapToBottom transitionConfig={{ type: 'spring', damping: 20, mass: 1.2, stiffness: 250 }}>
            <Sheet.Frame>
              <Sheet.ScrollView>
                <Adapt.Contents />
              </Sheet.ScrollView>
            </Sheet.Frame>
            <Sheet.Overlay transition="lazy" enterStyle={{ opacity: 0 }} exitStyle={{ opacity: 0 }} />
          </Sheet>
        </Adapt>
        <Select.Content>
          <Select.ScrollUpButton />
          <Select.Viewport minWidth={240}>
            {teams.map((team, index) => (
              <Select.Item key={team.teamId} index={index} value={team.teamId}>
                <Select.ItemText>{`${team.name} — ${team.basketName}${team.isDerived ? ' (derivata)' : ''}`}</Select.ItemText>
                <Select.ItemIndicator marginLeft="auto"><Check size={16} /></Select.ItemIndicator>
              </Select.Item>
            ))}
          </Select.Viewport>
          <Select.ScrollDownButton />
        </Select.Content>
      </Select>
    </YStack>
  )
}

export default TeamSelector
