import { type FC } from 'react'
import { ScrollView, YStack, Card, Text, XStack, Separator, H2, H3 } from 'tamagui'
import { GitCommit, Calendar as CalendarIcon } from '@tamagui/lucide-icons-2'
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
    version: '2.0.1',
    date: '8 Settembre 2026',
    changes: [
      {
        category: 'Fix',
        description: 'Ripristinato il caricamento dei loghi delle squadre e delle immagini remote con Tamagui 2'
      },
      {
        category: 'Fix',
        description: 'Corretti gli endpoint per creare calendario e classifica e per randomizzare i giocatori dell’asta'
      },
      {
        category: 'Improvement',
        description: 'Migliorata la visibilità dei controlli toggle nelle schermate di amministrazione e asta'
      }
    ]
  },
  {
    version: '2.0.0',
    date: '29 Agosto 2026',
    changes: [
      {
        category: 'Breaking',
        description: 'Frontend aggiornato a Expo 57, React 19.2, React Native 0.86 e TypeScript 6'
      },
      {
        category: 'Breaking',
        description: 'Design system migrato a Tamagui 2 con Config v5, nuovi temi, token e animazioni cross-platform'
      },
      {
        category: 'Improvement',
        description: 'Deploy web trasferito da Azure Static Web Apps a GitHub Pages con dominio fanta.zone, HTTPS e fallback per le route SPA'
      },
      {
        category: 'Improvement',
        description: 'Aggiornati SignalR, MSAL, React Navigation, strumenti di sviluppo e workflow GitHub Actions alle versioni compatibili più recenti'
      },
      {
        category: 'Feature',
        description: 'Nuova gestione delle regole annuali di lega con configurazione completa, validazione e copia dalla stagione precedente'
      },
      {
        category: 'Feature',
        description: 'Nuovo mercato con proposte di scambio, votazioni, controllo budget, validazione dei ruoli e aggiornamenti in tempo reale'
      },
      {
        category: 'Improvement',
        description: 'Parser delle formazioni Sky e Mediaset aggiornati e protetti da test automatici e smoke test periodici non bloccanti'
      },
      {
        category: 'Fix',
        description: 'Corretto lo scambio del token Microsoft su fanta.zone allineando esattamente il redirect URI OAuth'
      },
      {
        category: 'Fix',
        description: 'Corrette violazioni delle Rules of Hooks, stringhe JSX e gestione del Service Worker rilevate dal nuovo lint'
      },
      {
        category: 'Improvement',
        description: 'Rimossi il precedente frontend Blazor, il migratore storico, ClientStorage e le configurazioni di deploy obsolete'
      }
    ]
  },
  {
    version: '1.4.0',
    date: '27 Agosto 2026',
    changes: [
      {
        category: 'Feature',
        description: 'Editor completo dei settings annuali della lega: classifica, soglie gol, bonus difesa, fair play, porta inviolata e crediti'
      },
      {
        category: 'Feature',
        description: 'Configurazione dei modificatori voto generali e override specifici per portieri, difensori, centrocampisti e attaccanti'
      },
      {
        category: 'Improvement',
        description: 'Copia completa dall\'anno precedente disponibile anche per stagioni già configurate, inclusi tipo annuale, modificatori voto e settings avanzati'
      },
      {
        category: 'Improvement',
        description: 'Validazione degli input e conferma prima di sostituire settings o abbandonare modifiche non salvate'
      },
      {
        category: 'Fix',
        description: 'Serializzazione allineata al backend per ruoli, formazione, Futsal League e configurazioni annuali avanzate'
      }
    ]
  },
  {
    version: '1.3.1',
    date: '27 Agosto 2026',
    changes: [
      {
        category: 'Fix',
        description: 'Corretta la copia dei team dal basket dell\'anno precedente, con aggiornamento immediato della lista e conferma solo dopo il salvataggio effettivo'
      },
      {
        category: 'Feature',
        description: 'Aggiunta la configurazione delle nuove stagioni nelle leghe tramite settings di default o copia completa dall\'anno precedente'
      },
      {
        category: 'Improvement',
        description: 'Aggiunta la selezione dei basket associati alla lega e la validazione di team, owner e calendario prima delle operazioni amministrative'
      },
      {
        category: 'Fix',
        description: 'Allineato il cambio stagione tra frontend e backend e preservati i settings avanzati durante il salvataggio della lega'
      }
    ]
  },
  {
    version: '1.3.0',
    date: '4 Febbraio 2026',
    changes: [
      {
        category: 'Feature',
        description: 'Pannello giocatori rimanenti - visualizza tutti i giocatori del ruolo corrente che non sono ancora usciti nell\'asta, ordinati per partite giocate e fantamedia'
      },
      {
        category: 'Feature',
        description: 'Pannello giocatori scartati nell\'asta - visualizza tutti i giocatori mostrati ma non acquistati e quelli rimossi dalle squadre'
      },
      {
        category: 'Feature',
        description: 'Badge team che ha scartato il giocatore - mostra quale squadra ha rimosso un giocatore dalla propria rosa'
      },
      {
        category: 'Feature',
        description: 'Visualizzazione eventi nei pannelli giocatori - mostra gol ⚽, assist 🎯, rigori, parate 🧤, cartellini 🟨🟥 in formato compatto'
      },
      {
        category: 'Feature',
        description: 'FloatingPlayerWidget - widget floating in basso a destra con immagine giocatore, timer, prezzo e squadra vincente durante l\'asta live'
      },
      {
        category: 'Feature',
        description: 'Offerta rapida +1 dal FloatingPlayerWidget - possibilità di rilanciare cliccando direttamente sul widget senza scorrere la pagina'
      },
      {
        category: 'Feature',
        description: 'Stato visivo widget disabilitato - il cerchio diventa grigio quando non si può fare offerta (budget insufficiente, non è il proprio turno, ecc.)'
      },
      {
        category: 'Improvement',
        description: 'Layout responsive asta live - su desktop il pannello del giocatore in asta e il pannello offerte sono affiancati orizzontalmente per non-banditori'
      },
      {
        category: 'Improvement',
        description: 'Layout pannelli giocatori migliorato - nome a sinistra con team reale tra parentesi per migliore leggibilità'
      },
      {
        category: 'Improvement',
        description: 'Sistema immagini giocatore unificato - FloatingPlayerWidget usa la stessa logica di caricamento immagini con fallback automatico'
      },
      {
        category: 'Improvement',
        description: 'Normalizzazione chiavi giocatori con getPlayerKey() - matching affidabile tra realPlayers e team players per calcolo giocatori rimanenti'
      },
      {
        category: 'Fix',
        description: 'Risolto aggiornamento status asta in tempo reale - ora l\'header mostra correttamente "In Pausa"/"In Corso" quando lo status cambia via SignalR'
      },
      {
        category: 'Fix',
        description: 'Risolto refresh giocatori nella schermata statistiche - il pulsante "Aggiorna" ora ricarica sia i giocatori che le squadre contemporaneamente'
      },
      {
        category: 'Fix',
        description: 'Mapping corretto dati giocatori scartati - risolto problema conversione JSON server con chiavi abbreviate (n, t, r) verso frontend TypeScript'
      }
    ]
  },
  {
    version: '1.2.0',
    date: '22 Novembre 2024',
    changes: [
      {
        category: 'Feature',
        description: 'Modalità Super Admin - permette agli amministratori di modificare le formazioni di entrambe le squadre durante le partite live'
      },
      {
        category: 'Feature',
        description: 'Toggle attivazione modalità Super Admin nel menu di navigazione con salvataggio persistente in localStorage'
      },
      {
        category: 'Feature',
        description: 'Selezione esplicita squadra da modificare (Home/Away) con bottoni dedicati e indicatore visivo dello stato attivo'
      },
      {
        category: 'Feature',
        description: 'Visualizzazione tribuna durante partite live - ora tutti i giocatori sono visibili anche quando la partita è iniziata'
      },
      {
        category: 'Feature',
        description: 'Badge prezzo giocatore visualizzato nelle modali di dettaglio e scambio (💰)'
      },
      {
        category: 'Improvement',
        description: 'Ordinamento corretto panchinari nella modal di scambio - primi panchinari prima dei secondi'
      },
      {
        category: 'Improvement',
        description: 'Polling automatico disabilitato durante modifica formazione in modalità Super Admin - previene sovrascrittura modifiche'
      },
      {
        category: 'Fix',
        description: 'Risolto problema sovrapposizione card giocatori in panchina/tribuna su mobile - implementato ScrollView orizzontale con zoom sincronizzato'
      },
      {
        category: 'Fix',
        description: 'Risolto problema apertura modal swap per Super Admin durante partite live - bypassato controllo hasGameStarted'
      },
      {
        category: 'Fix',
        description: 'Corretta logica ownership squadre in modalità admin - permette modifica indipendentemente da canEdit e proprietà'
      }
    ]
  },
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
        category: 'Fix',
        description: 'Risolto problema redirect dopo scadenza token - implementato sistema auto-refresh token con fallback e ritorno automatico alla pagina precedente dopo re-login'
      },
      {
        category: 'Fix',
        description: 'Risolto problema sovrapposizione card giocatori in panchina/tribuna su alcuni browser mobile - rimosso flexWrap in favore di ScrollView orizzontale'
      },
      {
        category: 'Improvement',
        description: 'Zoom panchina e tribuna ora sincronizzato con zoom campo - controlli zoom applicati uniformemente a tutto il SoccerField'
      },
      {
        category: 'Feature',
        description: 'Visualizzazione prezzo giocatore nelle modali di dettaglio e scambio - badge verde con icona 💰 accanto a nome e ruolo'
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
    <Card elevation="$2" borderWidth={1} borderColor="$borderColor" padding="$4" backgroundColor="$background">
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
              Tutte le modifiche, miglioramenti e nuove funzionalità dell&apos;app
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
