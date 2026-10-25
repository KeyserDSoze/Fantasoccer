import { useState } from 'react'
import { Linking } from 'react-native'
import { useNavigation } from '@react-navigation/native'
import { BookOpen, ChevronDown, ChevronUp, ExternalLink, Link2, ShieldCheck } from '@tamagui/lucide-icons-2'
import { Button, Card, H1, H2, H3, Paragraph, Separator, Text, XStack, YStack } from 'tamagui'
import type { NavigationProp } from 'navigation'
import { ScreenContent } from 'components/layout/ScreenContent'
import { CopyableGuideText } from 'components/mcp/CopyableGuideText'
import { API_CONFIGS } from 'configurations/apiConfig'
import {
  CHATGPT_MCP_HELP_URL, CHATGPT_PLUGINS_URL, MCP_GUIDE_REVIEWED_ON,
  mcpPromptExamples, mcpSetupSteps, mcpTools, mcpTroubleshooting,
} from '../content/mcpGuide'
import { useAuth } from 'providers/AuthProvider'

// This page documents the PUBLIC production endpoint even when previewed locally.
const serverUrl = `${API_CONFIGS.production.baseUrl}/mcp`

function TroubleshootingItem({ question, answer }: { question: string; answer: string }) {
  const [open, setOpen] = useState(false)
  return <YStack borderWidth={1} borderColor="$borderColor" borderRadius="$3" overflow="hidden">
    <Button
      unstyled
      padding="$3"
      backgroundColor="$color2"
      hoverStyle={{ backgroundColor: '$color3' }}
      aria-expanded={open}
      onPress={() => setOpen(!open)}>
      <XStack gap="$3" alignItems="center">
        <Text flex={1} color="$color12" fontWeight="600">{question}</Text>
        {open ? <ChevronUp size={18} color="$color10" /> : <ChevronDown size={18} color="$color10" />}
      </XStack>
    </Button>
    {open ? <Paragraph padding="$3" color="$color11" lineHeight="$5">{answer}</Paragraph> : null}
  </YStack>
}

export default function McpGuideScreen() {
  const navigation = useNavigation<NavigationProp>()
  const { isAuthenticated } = useAuth()
  const [linkError, setLinkError] = useState<string | null>(null)
  const openLink = async (url: string) => {
    setLinkError(null)
    try { await Linking.openURL(url) }
    catch { setLinkError('Non riesco ad aprire il collegamento. Puoi aprirlo manualmente nel browser.') }
  }

  return <ScreenContent path="screens/mcp-guide.tsx" title="Guida MCP e ChatGPT">
    <YStack width="100%" maxWidth={960} alignSelf="center" gap="$5" paddingBottom="$6" minWidth={0}>
      <YStack gap="$3">
        <Button size="$3" variant="outlined" alignSelf="flex-start" onPress={() => navigation.navigate('Home')}>Torna alla Home</Button>
        <XStack gap="$2" alignItems="center">
          <BookOpen size={20} color="$blue10" />
          <Text color="$blue11" fontSize="$3" fontWeight="700">GUIDA AL COLLEGAMENTO MCP</Text>
        </XStack>
        <H1 role="heading" aria-level={1} color="$color12" fontSize="$9">Il tuo fantacalcio, anche nella chat.</H1>
        <Paragraph color="$color11" fontSize="$4" lineHeight="$6">
          Collega Fantasoccer a ChatGPT o a un’altra applicazione MCP compatibile per consultare la tua lega e usare le funzioni consentite dal tuo account, in linguaggio naturale.
        </Paragraph>
        <Text color="$color10" fontSize="$2">Questa guida è pubblica. Leggerla non richiede login e non autorizza nessuna applicazione.</Text>
      </YStack>

      <Card padding="$4" backgroundColor="$blue2" borderColor="$blue7" borderWidth={1} borderRadius="$4">
        <YStack gap="$3" minWidth={0}>
          <XStack gap="$2" alignItems="center"><Link2 size={20} color="$blue11" /><H2 role="heading" aria-level={2} fontSize="$6" color="$blue11">Indirizzo del server MCP</H2></XStack>
          <Text color="$blue11" fontSize="$3">Incolla questo URL nel client, non aprirlo come una normale pagina web.</Text>
          <CopyableGuideText value={serverUrl} label="URL MCP" />
          <Text color="$blue11" fontSize="$3">Autenticazione: OAuth con registrazione automatica. Non servono secrets da copiare o callback da configurare a mano.</Text>
        </YStack>
      </Card>

      <YStack gap="$3">
        <H2 role="heading" aria-level={2} color="$color12" fontSize="$7">Che cosa puoi fare</H2>
        <XStack gap="$3" flexWrap="wrap">
          <Card flex={1} minWidth={220} padding="$4" backgroundColor="$color2" borderRadius="$4" gap="$2">
            <H3 role="heading" aria-level={3} fontSize="$5">Leggere e capire</H3>
            <Paragraph color="$color11">Gruppi, rose, classifiche, risultati, voti e proposte di mercato. Puoi chiedere analisi e suggerimenti senza salvare modifiche.</Paragraph>
          </Card>
          <Card flex={1} minWidth={220} padding="$4" backgroundColor="$color2" borderRadius="$4" gap="$2">
            <H3 role="heading" aria-level={3} fontSize="$5">Agire, se autorizzato</H3>
            <Paragraph color="$color11">Formazioni, mercato e altre funzioni passano dagli stessi controlli del sito. Azioni disponibili e conferme dipendono anche dal client e dal piano.</Paragraph>
          </Card>
        </XStack>
        <Paragraph color="$color10">MCP è il protocollo che consente all’assistente di chiamare gli strumenti di Fantasoccer. Non è un account condiviso e non pubblica i dati della tua lega a chiunque conosca l’URL.</Paragraph>
      </YStack>

      <Card padding="$4" backgroundColor="$color2" borderColor="$borderColor" borderWidth={1} borderRadius="$4">
        <YStack gap="$4">
          <H2 role="heading" aria-level={2} color="$color12" fontSize="$7">Collegalo a ChatGPT, passo dopo passo</H2>
          <Paragraph color="$color11">La configurazione descritta è per ChatGPT sul web. Menu, disponibilità dei connettori e funzioni di modifica possono cambiare in base a piano e workspace; la creazione o pubblicazione può richiedere un amministratore.</Paragraph>
          <Button alignSelf="flex-start" icon={ExternalLink} variant="outlined" onPress={() => void openLink(CHATGPT_PLUGINS_URL)}>Apri Plugins in ChatGPT</Button>
          {mcpSetupSteps.map((step, index) => <YStack key={step.title} gap="$2">
            <Text color="$blue11" fontSize="$4" fontWeight="700">{index + 1}. {step.title}</Text>
            <Paragraph color="$color11" lineHeight="$5">{step.description}</Paragraph>
          </YStack>)}
          <Separator />
          <Text color="$color10" fontSize="$2">Percorsi verificati il {MCP_GUIDE_REVIEWED_ON}. Se la tua interfaccia è diversa, consulta le istruzioni aggiornate di OpenAI.</Text>
          <Button alignSelf="flex-start" icon={ExternalLink} variant="outlined" onPress={() => void openLink(CHATGPT_MCP_HELP_URL)}>Guida ufficiale OpenAI</Button>
          {linkError ? <Text role="alert" color="$red11">{linkError}</Text> : null}
        </YStack>
      </Card>

      <YStack gap="$3">
        <H2 role="heading" aria-level={2} color="$color12" fontSize="$7">Le prime richieste da provare</H2>
        <Paragraph color="$color11">Seleziona prima Fantasoccer nella chat. Questi esempi richiedono solo letture o suggerimenti, non modificano la lega.</Paragraph>
        {mcpPromptExamples.map(example => <Card key={example.title} padding="$4" backgroundColor="$color2" borderRadius="$4" gap="$3">
          <H3 role="heading" aria-level={3} fontSize="$5" color="$color12">{example.title}</H3>
          <CopyableGuideText value={example.text} label="richiesta" />
        </Card>)}
      </YStack>

      <Card padding="$4" backgroundColor="$yellow2" borderColor="$yellow7" borderWidth={1} borderRadius="$4">
        <YStack gap="$3">
          <XStack gap="$2" alignItems="center"><ShieldCheck size={20} color="$yellow11" /><H2 role="heading" aria-level={2} fontSize="$6" color="$yellow11">Il controllo resta tuo</H2></XStack>
          <Paragraph color="$yellow11">Collegare un’app significa consentirle di ricevere e trattare i dati che legge con il tuo account. Autorizza soltanto applicazioni fidate e collegamenti avviati da te; il nome mostrato nel consenso è dichiarato dal client, non verificato.</Paragraph>
          <Paragraph color="$yellow11">I ruoli restano quelli della lega: un partecipante non diventa admin. Non può aggirare il mercato o cancellare giocatori riscrivendo la rosa; formazione, scambi e aste seguono i flussi autorizzati.</Paragraph>
          <Paragraph color="$yellow11">L’assistente può sbagliare. Controlla le proposte e chiedi esplicitamente prima di salvare, votare, accettare scambi o compiere azioni amministrative. Non presumere che ogni client chieda sempre un’ulteriore conferma e non ripetere alla cieca una modifica dall’esito incerto.</Paragraph>
          <Paragraph color="$yellow11">Non incollare password, token del sito, certificati o secrets GitHub nella chat o nella configurazione dell’app.</Paragraph>
        </YStack>
      </Card>

      <YStack gap="$3">
        <H2 role="heading" aria-level={2} color="$color12" fontSize="$7">Come l’assistente capisce Fantasoccer</H2>
        <Paragraph color="$color11">Il server invia istruzioni iniziali e mette a disposizione una guida e un dizionario dei payload. Non è un addestramento né una garanzia di correttezza: l’assistente deve consultarli prima di usare le API.</Paragraph>
        <Paragraph color="$color11">I dati sono documenti NoSQL: l’assistente parte dal tuo account, legge il gruppo e trova gli ID di lega e basket, la stagione e l’email dell’owner. Da lì legge direttamente rosa, formazione o classifica. Non usa ricerche generiche su tutte le squadre; il gruppo è l’indice dei collegamenti, non contiene già tutti i calciatori.</Paragraph>
        <Paragraph color="$color11">Una stagione non è un anno solare: 2012/13 è la numero 1, 2026/27 è la numero 15. La stagione corrente è calcolata dal server con cambio il 10 agosto UTC; le tue selezioni sul sito possono essere storiche. Non significa che una partita sia in corso.</Paragraph>
        <YStack backgroundColor="$color2" borderRadius="$4" padding="$4" gap="$3">
          {mcpTools.map(tool => <YStack key={tool.name} gap="$1">
            <Text selectable fontSize="$3" fontWeight="700" color="$color12">{tool.name}</Text>
            <Paragraph color="$color11">{tool.description}</Paragraph>
          </YStack>)}
        </YStack>
      </YStack>

      <Card padding="$4" backgroundColor="$color2" borderColor="$borderColor" borderWidth={1} borderRadius="$4">
        <YStack gap="$3">
          <H2 role="heading" aria-level={2} color="$color12" fontSize="$7">Gestisci o revoca il collegamento</H2>
          <Paragraph color="$color11">Sul sito apri Impostazioni → Collegamenti MCP. Seleziona l’app, premi “Revoca accesso” e conferma. La revoca blocca l’accesso di quel client al tuo account, non quello degli altri utenti, e non ti disconnette da Fantasoccer.</Paragraph>
          <Paragraph color="$color11">Il logout dal sito non revoca automaticamente le app MCP. La revoca non cancella dati già letti o salvati dal client o nelle chat: gestiscili anche nelle impostazioni del servizio collegato.</Paragraph>
          <Button alignSelf="flex-start" variant="outlined" onPress={() => navigation.navigate(isAuthenticated ? 'Settings' : 'Login')}>
            {isAuthenticated ? 'Gestisci i collegamenti MCP' : 'Accedi e gestisci gli accessi'}
          </Button>
        </YStack>
      </Card>

      <YStack gap="$3">
        <H2 role="heading" aria-level={2} color="$color12" fontSize="$7">Se qualcosa non funziona</H2>
        {mcpTroubleshooting.map(item => <TroubleshootingItem key={item.question} {...item} />)}
      </YStack>
    </YStack>
  </ScreenContent>
}
