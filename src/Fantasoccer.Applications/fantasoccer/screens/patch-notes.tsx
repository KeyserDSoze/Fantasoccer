import { type FC } from 'react'
import { ScrollView, YStack, Card, Text, XStack, Separator, H2, H3 } from 'tamagui'
import { GitCommit, Calendar as CalendarIcon } from '@tamagui/lucide-icons'
import { ScreenContent } from 'components/layout/ScreenContent'
import { APP_VERSION } from 'config/version'

interface PatchNote {
  version: string
  date: string
  changes: {
    category: 'Feature' | 'Fix' | 'Improvement' | 'Breaking'
    description: string
  }[]
}

const patchNotes: PatchNote[] = [
  {
    version: '1.1.0',
    date: '2 Novembre 2024',
    changes: [
      {
        category: 'Feature',
        description: 'Sistema di statistiche giocatori dinamico basato su StatPlayer - sostituisce il vecchio sistema statico Chance'
      },
      {
        category: 'Feature',
        description: 'Badge trend dinamico nelle card giocatori con calcolo in tempo reale basato su somma ultime 5 partite'
      },
      {
        category: 'Feature',
        description: 'Timeline visiva partite nella modal dettaglio giocatore - pallini colorati con positiveness e giornata'
      },
      {
        category: 'Feature',
        description: 'Banner notifica nuova versione con link diretto alle patch notes - mostrato automaticamente solo una volta per versione'
      },
      {
        category: 'Feature',
        description: 'Sezione "Forma Recente" con trend calcolato su 5 soglie (Pessima, Negativa, Neutra, Positiva, Ottima) basato su somma ultime 5 partite'
      },
      {
        category: 'Feature',
        description: 'Visualizzazione "Ultima Partita" con gestione partite non giocate e calcolo automatico positiveness'
      },
      {
        category: 'Feature',
        description: 'Sistema caricamento logo squadra con supporto PNG e preview in tempo reale'
      },
      {
        category: 'Feature',
        description: 'Funzione "Usa Logo Anno Precedente" con modal di anteprima e conferma prima della copia'
      },
      {
        category: 'Improvement',
        description: 'Algoritmo formazione automatica migliorato - privilegia ultima partita giocata rispetto al trend per evitare penalizzazione giocatori tornati da infortunio'
      },
      {
        category: 'Improvement',
        description: 'Performance migliorate con Map lookup O(1) per accesso StatPlayer invece di array search'
      },
      {
        category: 'Improvement',
        description: 'Normalizzazione chiavi giocatori consistente tra frontend e backend per lookup affidabile'
      },
      {
        category: 'Improvement',
        description: 'Cache busting migliorato per loghi squadre - aggiornamento immediato dopo upload o copia con timestamp dinamico'
      },
      {
        category: 'Improvement',
        description: 'Logo squadre ora mostrato in formato quadrato/originale invece di circolare ritagliato'
      },
      {
        category: 'Fix',
        description: 'Risolto problema cambio nome squadra - ora funziona correttamente con validazione e feedback in tempo reale'
      },
      {
        category: 'Fix',
        description: 'Risolto problema cambio username - aggiornamento corretto con persistenza e validazione'
      },
      {
        category: 'Fix',
        description: 'Risolto errore 415 Unsupported Media Type durante upload logo - corretti attributi controller da [FromBody] a [FromForm]'
      },
      {
        category: 'Fix',
        description: 'Risolto problema cache browser per loghi - aggiunto delay propagazione blob storage e cache buster iniziale'
      },
      {
        category: 'Feature',
        description: 'Aggiunto parametro Fortuna (🍀) nella classifica - visualizza quanto sei stato fortunato o sfortunato durante la stagione'
      },
      {
        category: 'Feature',
        description: 'Modal di dettaglio fortuna con eventi partita per partita e spiegazione del calcolo'
      },
      {
        category: 'Feature',
        description: 'Sistema di calcolo fortuna con 9 tipi di eventi diversi (vittorie fortunate, sconfitte sfortunate, pareggi, ecc.)'
      },
      {
        category: 'Feature',
        description: 'Visualizzazione fortuna come percentuale nella classifica (+150%, -80%, ecc.)'
      },
      {
        category: 'Improvement',
        description: 'Aggiunte colonne PF (Punti Fatti) e PS (Punti Subiti) con medie per partita nella classifica'
      },
      {
        category: 'Improvement',
        description: 'Scrollbar orizzontale personalizzata per la tabella della classifica (miglior UX su mobile e PC)'
      },
      {
        category: 'Improvement',
        description: 'Aggiunti loghi delle squadre nel calendario delle partite'
      },
      {
        category: 'Fix',
        description: 'Risolto problema di allineamento dei nomi delle squadre nel calendario'
      },
      {
        category: 'Feature',
        description: 'Banner "Installa App" per PWA con dismissal intelligente (sessione/7 giorni/per sempre)'
      },
      {
        category: 'Feature',
        description: 'Pagina Patch Notes per tenere traccia di tutti i cambiamenti'
      },
      {
        category: 'Feature',
        description: 'Controlli Zoom per le card dei giocatori nel campo di gioco (10%-100%) con salvataggio preferenze'
      },
      {
        category: 'Improvement',
        description: 'Aggiunta visualizzazione versione app nella barra di navigazione'
      },
    ]
  },
  {
    version: '1.0.0',
    date: '15 Ottobre 2024',
    changes: [
      {
        category: 'Feature',
        description: 'Release iniziale dell\'app FantaZone'
      },
      {
        category: 'Feature',
        description: 'Classifica di lega con statistiche dettagliate'
      },
      {
        category: 'Feature',
        description: 'Calendario con risultati delle partite'
      },
      {
        category: 'Feature',
        description: 'Gestione formazione giocatori'
      },
      {
        category: 'Feature',
        description: 'Sistema di scambi tra utenti'
      },
      {
        category: 'Feature',
        description: 'Live score in tempo reale'
      },
      {
        category: 'Feature',
        description: 'Hall of Fame con statistiche storiche'
      },
    ]
  }
]

const getCategoryColor = (category: PatchNote['changes'][0]['category']) => {
  switch (category) {
    case 'Feature':
      return '$green10'
    case 'Fix':
      return '$red10'
    case 'Improvement':
      return '$blue10'
    case 'Breaking':
      return '$orange10'
  }
}

const getCategoryBgColor = (category: PatchNote['changes'][0]['category']) => {
  switch (category) {
    case 'Feature':
      return '$green3'
    case 'Fix':
      return '$red3'
    case 'Improvement':
      return '$blue3'
    case 'Breaking':
      return '$orange3'
  }
}

const PatchNoteCard: FC<{ note: PatchNote }> = ({ note }) => {
  return (
    <Card elevate bordered padding="$4" backgroundColor="$background">
      <YStack gap="$3">
        {/* Header */}
        <XStack justifyContent="space-between" alignItems="center" flexWrap="wrap" gap="$2">
          <XStack gap="$2" alignItems="center">
            <GitCommit size={20} color="$color11" />
            <H3 margin={0}>v{note.version}</H3>
          </XStack>
          <XStack gap="$2" alignItems="center">
            <CalendarIcon size={16} color="$color10" />
            <Text fontSize="$3" color="$color10">
              {note.date}
            </Text>
          </XStack>
        </XStack>

        <Separator />

        {/* Changes */}
        <YStack gap="$2">
          {note.changes.map((change, idx) => (
            <XStack key={idx} gap="$3" alignItems="flex-start">
              <YStack
                paddingHorizontal="$2"
                paddingVertical="$1"
                backgroundColor={getCategoryBgColor(change.category)}
                borderRadius="$2"
                minWidth={100}
                alignItems="center"
              >
                <Text
                  fontSize="$2"
                  fontWeight="600"
                  color={getCategoryColor(change.category)}
                >
                  {change.category}
                </Text>
              </YStack>
              <Text flex={1} fontSize="$3" color="$color11">
                {change.description}
              </Text>
            </XStack>
          ))}
        </YStack>
      </YStack>
    </Card>
  )
}

const PatchNotesScreen: FC = () => {
  return (
    <ScreenContent title="Patch Notes">
      <ScrollView>
        <YStack padding="$4" gap="$4" maxWidth={1200} width="100%" alignSelf="center">
          <YStack gap="$2">
            <H2>📝 Patch Notes</H2>
            <Text fontSize="$4" color="$color11">
              Tutte le modifiche, miglioramenti e nuove funzionalità dell'app
            </Text>
            <Text fontSize="$3" color="$color10">
              Versione attuale: v{APP_VERSION}
            </Text>
          </YStack>

          <Separator />

          <YStack gap="$4">
            {patchNotes.map((note) => (
              <PatchNoteCard key={note.version} note={note} />
            ))}
          </YStack>
        </YStack>
      </ScrollView>
    </ScreenContent>
  )
}

export default PatchNotesScreen
