import { useNavigation } from '@react-navigation/native'
import { BookOpen } from '@tamagui/lucide-icons-2'
import { Button, Card, H3, Paragraph, YStack } from 'tamagui'
import type { NavigationProp } from 'navigation'
import { MCP_ENTRY_POINTS } from '../../configurations/features'

export function McpGuideCard() {
  const navigation = useNavigation<NavigationProp>()
  if (!MCP_ENTRY_POINTS) return null
  return <Card padding="$4" backgroundColor="$blue2" borderWidth={1} borderColor="$blue7" borderRadius="$4">
    <YStack gap="$3">
      <H3 color="$blue11">Fantasoccer su ChatGPT e altre app</H3>
      <Paragraph color="$blue11">Scopri come collegare un assistente AI, cosa può fare e come mantenere il controllo degli accessi.</Paragraph>
      <Button alignSelf="flex-start" icon={BookOpen} variant="outlined" onPress={() => navigation.navigate('McpGuide')}>
        Guida MCP e ChatGPT
      </Button>
    </YStack>
  </Card>
}
