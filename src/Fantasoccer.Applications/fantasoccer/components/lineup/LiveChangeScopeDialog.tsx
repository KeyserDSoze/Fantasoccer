import { Button, Dialog, Paragraph, Text, XStack, YStack } from 'tamagui'

export type LiveChangeScope = 'all' | 'only' | 'cancel'

interface LiveChangeScopeDialogProps {
  // Names of the competitions where the team has the very same lineup; null keeps the dialog closed.
  leagues: string[] | null
  onChoose: (choice: LiveChangeScope) => void
}

// Separate lineups, but the same lineup in several competitions: the owner decides whether the live change goes everywhere.
export function LiveChangeScopeDialog({ leagues, onChoose }: LiveChangeScopeDialogProps) {
  return (
    <Dialog modal open={leagues !== null} onOpenChange={open => { if (!open) onChoose('cancel') }}>
      <Dialog.Portal>
        <Dialog.Overlay key="overlay" opacity={0.5} enterStyle={{ opacity: 0 }} exitStyle={{ opacity: 0 }} />
        <Dialog.Content bordered elevate key="content" gap="$3" width="90%" maxWidth={480}>
          <Dialog.Title size="$6">Stessa formazione in più leghe</Dialog.Title>
          <Dialog.Description>
            Hai la stessa formazione anche in {leagues && leagues.length === 1 ? 'questa lega' : 'queste leghe'}. Vuoi fare il cambio live anche lì?
          </Dialog.Description>
          <YStack gap="$1">
            {(leagues ?? []).map(name => <Text key={name} fontWeight="700">• {name}</Text>)}
          </YStack>
          <Paragraph color="$color10" size="$2">Il cambio conta nei cambi live disponibili di ogni lega in cui viene applicato; dove non è possibile ti avvisiamo.</Paragraph>
          <XStack gap="$2" justifyContent="flex-end" flexWrap="wrap">
            <Button size="$3" onPress={() => onChoose('cancel')}>Annulla</Button>
            <Button size="$3" onPress={() => onChoose('only')}>Solo qui</Button>
            <Button size="$3" theme="blue" onPress={() => onChoose('all')}>Anche nelle altre leghe</Button>
          </XStack>
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog>
  )
}
