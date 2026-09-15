import { type FC } from 'react';
import { ScrollView, YStack, Card, Text, XStack, Separator, H2, H3 } from 'tamagui';
import { GitCommit, Calendar as CalendarIcon } from '@tamagui/lucide-icons-2';
import { ScreenContent } from 'components/layout/ScreenContent';
import { APP_VERSION } from 'config/version';

interface PatchNote {
  version: string;
  date: string;
  changes: {
    category: 'Feature' | 'Fix' | 'Improvement' | 'Breaking';
    description: string;
  }[];
}

const patchNotes: PatchNote[] = [
  {
    version: '2.0.52',
    date: '15 Settembre 2026',
    changes: [
      {
        category: 'Feature',
        description:
          'Aggiunta nella Home la tua ultima partita di fantacampionato, con filtri per lega e stagione e supporto alla partita live',
      },
      {
        category: 'Improvement',
        description:
          'Il widget globale viene nascosto durante il live e nella visualizzazione delle formazioni o delle altre partite',
      },
    ],
  },
  {
    version: '2.0.51',
    date: '15 Settembre 2026',
    changes: [
      {
        category: 'Feature',
        description:
          'Aggiunto il widget globale per risultati live, competizioni dell\'utente e conto alla rovescia della prossima formazione, con gestione dalle Impostazioni',
      },
      {
        category: 'Improvement',
        description:
          'Aggiunti strumenti Super Admin per modificare formazioni storiche e ricalcolare una giornata completa',
      },
      {
        category: 'Fix',
        description:
          'Rese piu affidabili la raccolta dei voti ufficiali, la gestione delle rinviate e il calcolo con voti incompleti',
      },
    ],
  },
  {
    version: '2.0.50',
    date: '14 Settembre 2026',
    changes: [
      {
        category: 'Fix',
        description:
          'Le probabilità pre-partita vengono rimosse dal backend e dalla UI non appena la partita del giocatore è iniziata o è disponibile un voto',
      },
    ],
  },
  {
    version: '2.0.49',
    date: '14 Settembre 2026',
    changes: [
      {
        category: 'Fix',
        description:
          'Nascosti correttamente gli indicatori di probabilità quando il voto o il risultato della partita arrivano dopo il primo caricamento',
      },
    ],
  },
  {
    version: '2.0.48',
    date: '14 Settembre 2026',
    changes: [
      {
        category: 'Fix',
        description:
          'Il salvataggio live usa sempre e solo Game/Switch con le chiavi generate dai nomi dei due giocatori, senza fallback a SaveTeam',
      },
    ],
  },
  {
    version: '2.0.47',
    date: '14 Settembre 2026',
    changes: [
      {
        category: 'Fix',
        description:
          'Corretto il riconoscimento dei due giocatori selezionati nel cambio live usando la chiave effettiva del giocatore',
      },
    ],
  },
  {
    version: '2.0.46',
    date: '14 Settembre 2026',
    changes: [
      {
        category: 'Fix',
        description:
          'Ripristinata la formazione effettiva con sostituzioni automatiche nella visualizzazione live senza usarla per il salvataggio degli switch',
      },
    ],
  },
  {
    version: '2.0.45',
    date: '14 Settembre 2026',
    changes: [
      {
        category: 'Fix',
        description:
          'Introdotto un endpoint dedicato per gli scambi live: il server riceve solo i due giocatori, verifica ruolo e disponibilità e registra un solo cambio',
      },
    ],
  },
  {
    version: '2.0.44',
    date: '12 Settembre 2026',
    changes: [
      {
        category: 'Fix',
        description:
          'Le sostituzioni automatiche mostrate nel live non vengono più conteggiate come cambi manuali al salvataggio della formazione',
      },
    ],
  },
  {
    version: '2.0.43',
    date: '12 Settembre 2026',
    changes: [
      {
        category: 'Fix',
        description:
          'Ripristinate nel live le sostituzioni automatiche dei panchinari mantenendo i colori originali del primo e del secondo panchinaro',
      },
    ],
  },
  {
    version: '2.0.42',
    date: '12 Settembre 2026',
    changes: [
      {
        category: 'Fix',
        description:
          'Corretta la finestra dei cambi live quando due giornate di Serie A si sovrappongono; il server accetta un solo scambio per ogni cambio disponibile',
      },
    ],
  },
  {
    version: '2.0.41',
    date: '11 Settembre 2026',
    changes: [
      {
        category: 'Fix',
        description:
          'Il numero dei cambi live ora usa il setting della lega selezionata senza essere azzerato da altre leghe dello stesso basket',
      },
    ],
  },
  {
    version: '2.0.40',
    date: '11 Settembre 2026',
    changes: [
      {
        category: 'Fix',
        description:
          'Mostrato nel titolo dei Controlli partita il numero di cambi live disponibili anche quando il valore arriva a livello partita',
      },
    ],
  },
  {
    version: '2.0.39',
    date: '11 Settembre 2026',
    changes: [
      {
        category: 'Improvement',
        description:
          'Rendere sempre visibile il banner dei cambi live disponibili e aprire automaticamente i controlli durante la modalità live',
      },
    ],
  },
  {
    version: '2.0.38',
    date: '11 Settembre 2026',
    changes: [
      {
        category: 'Fix',
        description:
          'Ripristinata la visualizzazione del risultato live in alto e del pannello con i cambi live disponibili durante la partita',
      },
    ],
  },
  {
    version: '2.0.37',
    date: '11 Settembre 2026',
    changes: [
      {
        category: 'Fix',
        description:
          'Ripristinato il caricamento dei voti e dei risultati nella formazione durante le partite live, mantenendo visibili anche i cambi live disponibili',
      },
    ],
  },
  {
    version: '2.0.36',
    date: '11 Settembre 2026',
    changes: [
      {
        category: 'Improvement',
        description:
          'Completata la Guida al sito con istruzioni dettagliate per la Vista rapida mobile e le colonne T, 1, 2 e F',
      },
    ],
  },
  {
    version: '2.0.35',
    date: '11 Settembre 2026',
    changes: [
      {
        category: 'Improvement',
        description:
          'Compattata la Vista rapida mobile con colonne T, 1, 2 e F per eliminare lo scroll orizzontale',
      },
    ],
  },
  {
    version: '2.0.34',
    date: '11 Settembre 2026',
    changes: [
      {
        category: 'Feature',
        description:
          "Aggiunta su mobile la Vista rapida tabellare per modificare titolari, panchinari e tribuna prima dell'inizio della partita",
      },
    ],
  },
  {
    version: '2.0.33',
    date: '11 Settembre 2026',
    changes: [
      {
        category: 'Improvement',
        description:
          'Aggiornata la numerazione delle scorciatoie desktop in base al ruolo: difensori e centrocampisti fino a 5 titolari, portieri con sequenza 1-3',
      },
    ],
  },
  {
    version: '2.0.32',
    date: '11 Settembre 2026',
    changes: [
      {
        category: 'Feature',
        description: 'Aggiunto Ctrl + S per salvare rapidamente la formazione desktop modificata',
      },
    ],
  },
  {
    version: '2.0.31',
    date: '11 Settembre 2026',
    changes: [
      {
        category: 'Improvement',
        description:
          'Semplificate le scorciatoie della formazione desktop: basta premere ruolo e numero, con Esc per annullare la selezione e cambio ruolo diretto',
      },
    ],
  },
  {
    version: '2.0.30',
    date: '11 Settembre 2026',
    changes: [
      {
        category: 'Improvement',
        description:
          'Aggiunta nella Guida al sito una legenda desktop con istruzioni per il click destro e per le scorciatoie Ctrl e Shift della formazione',
      },
    ],
  },
  {
    version: '2.0.29',
    date: '11 Settembre 2026',
    changes: [
      {
        category: 'Improvement',
        description:
          'Il menu rapido desktop si apre sopra il punto del click destro e supporta la selezione e lo spostamento dei giocatori tramite scorciatoie Ctrl e Shift',
      },
      {
        category: 'Feature',
        description:
          'Aggiunti i comandi rapidi A, D, C e P con numeri 1-6 per titolari, panchina e tribuna, con scambio automatico delle posizioni occupate',
      },
    ],
  },
  {
    version: '2.0.28',
    date: '11 Settembre 2026',
    changes: [
      {
        category: 'Feature',
        description:
          'Aggiunto su desktop il menu rapido con click destro per spostare i giocatori tra titolari, panchina e tribuna, effettuando automaticamente lo scambio quando la posizione è occupata',
      },
      {
        category: 'Improvement',
        description:
          'Le formazioni incomplete continuano a essere rifiutate dal salvataggio con un errore esplicito di validazione della formazione',
      },
    ],
  },
  {
    version: '2.0.27',
    date: '11 Settembre 2026',
    changes: [
      {
        category: 'Fix',
        description:
          'Corretto il calcolo della formazione ottimale in modalità SuperAdmin inviando all’API l’autorizzazione amministrativa necessaria',
      },
    ],
  },
  {
    version: '2.0.26',
    date: '11 Settembre 2026',
    changes: [
      {
        category: 'Feature',
        description:
          'Il SuperAdmin può modificare le formazioni delle partite future e live direttamente dal calendario, finché la giornata non è conclusa',
      },
      {
        category: 'Improvement',
        description:
          'La modalità Live mostra i cambi disponibili per entrambe le squadre, consente la modifica pre-partita e apre i controlli in base allo stato della partita',
      },
    ],
  },
  {
    version: '2.0.25',
    date: '11 Settembre 2026',
    changes: [
      {
        category: 'Feature',
        description:
          'Aggiunte all’asta le descrizioni PRO e CONTRO dei giocatori provenienti dalle schede Fantacalcio.it, visibili sia nella pulsantiera sia nella modalità TV',
      },
      {
        category: 'Improvement',
        description:
          'Migliorata la modalità TV dell’asta con statistiche giocatore, countdown live, budget dopo l’acquisto, ordinamento delle squadre per disponibilità economica e layout responsive per TV e mobile',
      },
    ],
  },
  {
    version: '2.0.24',
    date: '11 Settembre 2026',
    changes: [
      {
        category: 'Fix',
        description:
          'Corretta la panchina della formazione ottimale: è previsto un solo portiere, assegnato esclusivamente come primo panchinaro; gli altri portieri vanno in tribuna e vengono mantenuti i due slot per gli attaccanti',
      },
    ],
  },
  {
    version: '2.0.23',
    date: '11 Settembre 2026',
    changes: [
      {
        category: 'Improvement',
        description:
          'La formazione ottimale privilegia i moduli offensivi, in particolare il 3-4-3, solo quando ogni reparto mantiene almeno una riserva indicata come probabile titolare',
      },
      {
        category: 'Improvement',
        description:
          'La panchina viene ora ordinata dando priorità ai giocatori con maggiore probabilità di giocare',
      },
    ],
  },
  {
    version: '2.0.22',
    date: '11 Settembre 2026',
    changes: [
      {
        category: 'Fix',
        description:
          'Aggiunta nei settings una spiegazione apribile del funzionamento della formazione ottimale, inclusi pesi, filtri e tier',
      },
      {
        category: 'Improvement',
        description:
          'I tier della formazione ora vengono calcolati dinamicamente dalla classifica ufficiale di Serie A e il matchup casa/trasferta contro squadre di tier superiore ha un peso maggiore',
      },
    ],
  },
  {
    version: '2.0.21',
    date: '11 Settembre 2026',
    changes: [
      {
        category: 'Fix',
        description:
          'La formazione ottimale ora esclude dall’undici titolare i giocatori indicati fuori da Fantagazzetta e Gazzetta, mantenendoli disponibili per panchina o tribuna',
      },
    ],
  },
  {
    version: '2.0.20',
    date: '11 Settembre 2026',
    changes: [
      {
        category: 'Fix',
        description:
          'Risolto definitivamente l’errore 500 della formazione AI quando un giocatore non ha ancora un voto finale: il valore viene ora gestito come dato opzionale',
      },
    ],
  },
  {
    version: '2.0.19',
    date: '11 Settembre 2026',
    changes: [
      {
        category: 'Fix',
        description:
          'Aggiunte ulteriori protezioni nel calcolo OptimalFormation per dati annidati mancanti, liste di voti/calendario assenti e chiavi giocatore duplicate',
      },
      {
        category: 'Fix',
        description:
          'Regolata a 28 la distanza della fascia partita dal fondo della scheda giocatore per ridurre lo spazio con il nome mantenendo visibili partita e orario',
      },
    ],
  },
  {
    version: '2.0.18',
    date: '11 Settembre 2026',
    changes: [
      {
        category: 'Fix',
        description:
          'Rafforzato il calcolo della formazione AI contro dati incompleti, liste mancanti e giocatori duplicati che potevano causare un errore 500',
      },
      {
        category: 'Fix',
        description:
          'Spostata più in alto la fascia partita nella scheda giocatore per mantenere sempre visibili avversari e orario sopra il nome',
      },
    ],
  },
  {
    version: '2.0.17',
    date: '11 Settembre 2026',
    changes: [
      {
        category: 'Fix',
        description:
          'Risolto l’errore 500 del calcolo formazione AI quando calendario, rosa o impostazioni voto sono incompleti o non disponibili',
      },
      {
        category: 'Improvement',
        description:
          'Aggiunto logging dettagliato dell’endpoint OptimalFormation con contesto di gruppo, lega, giornata e proprietario per la diagnosi in Application Insights',
      },
      {
        category: 'Fix',
        description:
          'Ripristinata la posizione della fascia partita nella scheda giocatore: partita e orario restano visibili sopra il nome anche con il testo ridotto',
      },
    ],
  },
  {
    version: '2.0.16',
    date: '11 Settembre 2026',
    changes: [
      {
        category: 'Feature',
        description:
          'Aggiunta telemetria Application Insights per richieste API, dipendenze, eccezioni e log business dei job, con dettagli utili per diagnosticare gli errori in produzione',
      },
      {
        category: 'Fix',
        description:
          'Migliorata la gestione degli errori nel calcolo della formazione AI: risposte vuote o non JSON e Problem Details vengono ora gestiti senza errori secondari nel client',
      },
      {
        category: 'Improvement',
        description:
          'Ridotta la fascia con partita e orario nella scheda giocatore per lasciare più spazio all’immagine e al nome',
      },
      {
        category: 'Improvement',
        description:
          'Riorganizzati i controlli della formazione in un accordion compatto e chiuso inizialmente, mantenendo sempre disponibile il calcolo AI manuale',
      },
    ],
  },
  {
    version: '2.0.15',
    date: '11 Settembre 2026',
    changes: [
      {
        category: 'Feature',
        description:
          'Aggiunto un motore backend deterministico per calcolare la formazione ottimale usando voti, statistiche, forma recente, chance, calendario, casa/trasferta e classifica reale',
      },
      {
        category: 'Feature',
        description:
          'Il pulsante AIgoritmo usa ora il calcolo server-side e mostra modulo, titolari, panchina e tribuna in modo coerente e ripetibile',
      },
      {
        category: 'Feature',
        description:
          'Aggiunta l’impostazione utente Formazione ottimale automatica: quando attiva, la nuova giornata viene preparata automaticamente senza sovrascrivere una formazione già esistente',
      },
      {
        category: 'Improvement',
        description:
          'Rimossa la precedente implementazione duplicata dell’algoritmo dal frontend e aggiunti fallback deterministici per dati mancanti',
      },
    ],
  },
  {
    version: '2.0.14',
    date: '11 Settembre 2026',
    changes: [
      {
        category: 'Fix',
        description:
          'Ripristinati in alto i nomi delle squadre avversarie, la giornata e la data di inizio quando il risultato non è ancora disponibile',
      },
      {
        category: 'Improvement',
        description:
          'La mia formazione mostra sempre la prossima giornata non iniziata, anche quando una giornata è già live',
      },
      {
        category: 'Feature',
        description:
          'Nel live sono visibili per entrambe le squadre cambi usati e disponibili, scadenza, autore e giocatori coinvolti in ogni cambio',
      },
      {
        category: 'Fix',
        description:
          'Le modifiche live sono consentite e salvabili soltanto dalla partita aperta tramite Live e vengono bloccate dal server alla scadenza',
      },
    ],
  },
  {
    version: '2.0.13',
    date: '10 Settembre 2026',
    changes: [
      {
        category: 'Fix',
        description:
          'Corretto il riconoscimento del banditore quando l’admin o super admin non possiede una squadra partecipante all’asta',
      },
    ],
  },
  {
    version: '2.0.12',
    date: '10 Settembre 2026',
    changes: [
      {
        category: 'Improvement',
        description:
          'Nella modalità TV i contatori dei giocatori mancanti mostrano ora i titolari Fantacalcio.it come numero principale e il totale dei mancanti tra parentesi',
      },
    ],
  },
  {
    version: '2.0.11',
    date: '10 Settembre 2026',
    changes: [
      {
        category: 'Feature',
        description:
          'Aggiunta la modalità TV read-only per seguire un’asta in tempo reale con giocatore corrente, timer, offerte, squadre e stato della sessione',
      },
      {
        category: 'Feature',
        description:
          'La dashboard TV mostra giocatori rimasti per ruolo, tier 1-4, titolari Fantacalcio.it disponibili, crediti e posti mancanti per ogni squadra',
      },
      {
        category: 'Improvement',
        description:
          'Aggiunti link, copia link e apertura diretta della modalità TV dopo la creazione e durante l’asta live',
      },
      {
        category: 'Fix',
        description:
          'Corretta la risoluzione del deep link /auction/{auctionId}/display e limitato il caricamento dell’asta agli utenti appartenenti al gruppo',
      },
    ],
  },
  {
    version: '2.0.10',
    date: '9 Settembre 2026',
    changes: [
      {
        category: 'Fix',
        description:
          'Rafforzate le autorizzazioni owner/co-owner e rimossa la possibilità di autorizzare una squadra usando dati modificati dal client',
      },
      {
        category: 'Fix',
        description:
          'Protette le formazioni iniziali contro giocatori estranei o duplicati e aggiunta la posizione esplicita per la gestione corretta dei pari merito',
      },
      {
        category: 'Fix',
        description:
          'Reso atomico il salvataggio delle modifiche live tramite lo stesso lock Rystem utilizzato dalle operazioni d’asta',
      },
      {
        category: 'Improvement',
        description:
          'Centralizzato il calcolo della finestra live e aggiunta la validazione dei settings del Campionato iniziale',
      },
    ],
  },
  {
    version: '2.0.9',
    date: '9 Settembre 2026',
    changes: [
      {
        category: 'Feature',
        description:
          'Aggiunto il Campionato iniziale per le leghe principali, con rose della stagione precedente, giornate configurabili e classifica a fantapunteggio',
      },
      {
        category: 'Feature',
        description:
          'Aggiunti premi configurabili per posizione, gestione dei pari merito e accredito separato dei premi nell’asta iniziale',
      },
      {
        category: 'Feature',
        description:
          'Aggiunta la gestione delle formazioni iniziali per giornata, con modifica, salvataggio e ricalcolo automatico della classifica',
      },
      {
        category: 'Fix',
        description:
          'Corretta la gestione dei pari merito nel calcolo dei premi e aggiunti test automatici dedicati',
      },
    ],
  },
  {
    version: '2.0.8',
    date: '9 Settembre 2026',
    changes: [
      {
        category: 'Fix',
        description:
          'Uniformata la gestione di owner e co-owner in formazione, asta, impostazioni e servizi condivisi',
      },
      {
        category: 'Fix',
        description:
          'Risolti i confronti email sensibili a maiuscole e minuscole per evitare che un co-owner perda l’accesso alla squadra',
      },
    ],
  },
  {
    version: '2.0.7',
    date: '9 Settembre 2026',
    changes: [
      {
        category: 'Fix',
        description:
          'Corretta la schermata La mia formazione: ora anche i co-owner vedono e possono aprire la squadra condivisa',
      },
    ],
  },
  {
    version: '2.0.6',
    date: '9 Settembre 2026',
    changes: [
      {
        category: 'Feature',
        description:
          'Aggiunte le modifiche alla formazione durante la giornata live, configurabili per ogni lega annuale con numero massimo di cambi e abilitazione del cambio modulo',
      },
      {
        category: 'Improvement',
        description:
          'Durante il salvataggio live vengono mostrati scadenza precisa, cambi residui e avviso sul consumo del cambio',
      },
    ],
  },
  {
    version: '2.0.3',
    date: '8 Settembre 2026',
    changes: [
      {
        category: 'Improvement',
        description:
          'Uniformati tutti i controlli booleani del sito con nuovi toggle a chip leggibili, accessibili e coerenti',
      },
    ],
  },
  {
    version: '2.0.2',
    date: '8 Settembre 2026',
    changes: [
      {
        category: 'Improvement',
        description:
          'Ridisegnati i controlli Mostra/Nascondi della sala asta con chip compatti, leggibili e accessibili',
      },
    ],
  },
  {
    version: '2.0.1',
    date: '8 Settembre 2026',
    changes: [
      {
        category: 'Fix',
        description:
          'Ripristinato il caricamento dei loghi delle squadre e delle immagini remote con Tamagui 2',
      },
      {
        category: 'Fix',
        description:
          'Corretti gli endpoint per creare calendario e classifica e per randomizzare i giocatori dell’asta',
      },
      {
        category: 'Improvement',
        description:
          'Migliorata la visibilità dei controlli toggle nelle schermate di amministrazione e asta',
      },
    ],
  },
  {
    version: '2.0.0',
    date: '29 Agosto 2026',
    changes: [
      {
        category: 'Breaking',
        description: 'Frontend aggiornato a Expo 57, React 19.2, React Native 0.86 e TypeScript 6',
      },
      {
        category: 'Breaking',
        description:
          'Design system migrato a Tamagui 2 con Config v5, nuovi temi, token e animazioni cross-platform',
      },
      {
        category: 'Improvement',
        description:
          'Deploy web trasferito da Azure Static Web Apps a GitHub Pages con dominio fanta.zone, HTTPS e fallback per le route SPA',
      },
      {
        category: 'Improvement',
        description:
          'Aggiornati SignalR, MSAL, React Navigation, strumenti di sviluppo e workflow GitHub Actions alle versioni compatibili più recenti',
      },
      {
        category: 'Feature',
        description:
          'Nuova gestione delle regole annuali di lega con configurazione completa, validazione e copia dalla stagione precedente',
      },
      {
        category: 'Feature',
        description:
          'Nuovo mercato con proposte di scambio, votazioni, controllo budget, validazione dei ruoli e aggiornamenti in tempo reale',
      },
      {
        category: 'Improvement',
        description:
          'Parser delle formazioni Sky e Mediaset aggiornati e protetti da test automatici e smoke test periodici non bloccanti',
      },
      {
        category: 'Fix',
        description:
          'Corretto lo scambio del token Microsoft su fanta.zone allineando esattamente il redirect URI OAuth',
      },
      {
        category: 'Fix',
        description:
          'Corrette violazioni delle Rules of Hooks, stringhe JSX e gestione del Service Worker rilevate dal nuovo lint',
      },
      {
        category: 'Improvement',
        description:
          'Rimossi il precedente frontend Blazor, il migratore storico, ClientStorage e le configurazioni di deploy obsolete',
      },
    ],
  },
  {
    version: '1.4.0',
    date: '27 Agosto 2026',
    changes: [
      {
        category: 'Feature',
        description:
          'Editor completo dei settings annuali della lega: classifica, soglie gol, bonus difesa, fair play, porta inviolata e crediti',
      },
      {
        category: 'Feature',
        description:
          'Configurazione dei modificatori voto generali e override specifici per portieri, difensori, centrocampisti e attaccanti',
      },
      {
        category: 'Improvement',
        description:
          "Copia completa dall'anno precedente disponibile anche per stagioni già configurate, inclusi tipo annuale, modificatori voto e settings avanzati",
      },
      {
        category: 'Improvement',
        description:
          'Validazione degli input e conferma prima di sostituire settings o abbandonare modifiche non salvate',
      },
      {
        category: 'Fix',
        description:
          'Serializzazione allineata al backend per ruoli, formazione, Futsal League e configurazioni annuali avanzate',
      },
    ],
  },
  {
    version: '1.3.1',
    date: '27 Agosto 2026',
    changes: [
      {
        category: 'Fix',
        description:
          "Corretta la copia dei team dal basket dell'anno precedente, con aggiornamento immediato della lista e conferma solo dopo il salvataggio effettivo",
      },
      {
        category: 'Feature',
        description:
          "Aggiunta la configurazione delle nuove stagioni nelle leghe tramite settings di default o copia completa dall'anno precedente",
      },
      {
        category: 'Improvement',
        description:
          'Aggiunta la selezione dei basket associati alla lega e la validazione di team, owner e calendario prima delle operazioni amministrative',
      },
      {
        category: 'Fix',
        description:
          'Allineato il cambio stagione tra frontend e backend e preservati i settings avanzati durante il salvataggio della lega',
      },
    ],
  },
  {
    version: '1.3.0',
    date: '4 Febbraio 2026',
    changes: [
      {
        category: 'Feature',
        description:
          "Pannello giocatori rimanenti - visualizza tutti i giocatori del ruolo corrente che non sono ancora usciti nell'asta, ordinati per partite giocate e fantamedia",
      },
      {
        category: 'Feature',
        description:
          "Pannello giocatori scartati nell'asta - visualizza tutti i giocatori mostrati ma non acquistati e quelli rimossi dalle squadre",
      },
      {
        category: 'Feature',
        description:
          'Badge team che ha scartato il giocatore - mostra quale squadra ha rimosso un giocatore dalla propria rosa',
      },
      {
        category: 'Feature',
        description:
          'Visualizzazione eventi nei pannelli giocatori - mostra gol ⚽, assist 🎯, rigori, parate 🧤, cartellini 🟨🟥 in formato compatto',
      },
      {
        category: 'Feature',
        description:
          "FloatingPlayerWidget - widget floating in basso a destra con immagine giocatore, timer, prezzo e squadra vincente durante l'asta live",
      },
      {
        category: 'Feature',
        description:
          'Offerta rapida +1 dal FloatingPlayerWidget - possibilità di rilanciare cliccando direttamente sul widget senza scorrere la pagina',
      },
      {
        category: 'Feature',
        description:
          'Stato visivo widget disabilitato - il cerchio diventa grigio quando non si può fare offerta (budget insufficiente, non è il proprio turno, ecc.)',
      },
      {
        category: 'Improvement',
        description:
          'Layout responsive asta live - su desktop il pannello del giocatore in asta e il pannello offerte sono affiancati orizzontalmente per non-banditori',
      },
      {
        category: 'Improvement',
        description:
          'Layout pannelli giocatori migliorato - nome a sinistra con team reale tra parentesi per migliore leggibilità',
      },
      {
        category: 'Improvement',
        description:
          'Sistema immagini giocatore unificato - FloatingPlayerWidget usa la stessa logica di caricamento immagini con fallback automatico',
      },
      {
        category: 'Improvement',
        description:
          'Normalizzazione chiavi giocatori con getPlayerKey() - matching affidabile tra realPlayers e team players per calcolo giocatori rimanenti',
      },
      {
        category: 'Fix',
        description:
          'Risolto aggiornamento status asta in tempo reale - ora l\'header mostra correttamente "In Pausa"/"In Corso" quando lo status cambia via SignalR',
      },
      {
        category: 'Fix',
        description:
          'Risolto refresh giocatori nella schermata statistiche - il pulsante "Aggiorna" ora ricarica sia i giocatori che le squadre contemporaneamente',
      },
      {
        category: 'Fix',
        description:
          'Mapping corretto dati giocatori scartati - risolto problema conversione JSON server con chiavi abbreviate (n, t, r) verso frontend TypeScript',
      },
    ],
  },
  {
    version: '1.2.0',
    date: '22 Novembre 2024',
    changes: [
      {
        category: 'Feature',
        description:
          'Modalità Super Admin - permette agli amministratori di modificare le formazioni di entrambe le squadre durante le partite live',
      },
      {
        category: 'Feature',
        description:
          'Toggle attivazione modalità Super Admin nel menu di navigazione con salvataggio persistente in localStorage',
      },
      {
        category: 'Feature',
        description:
          'Selezione esplicita squadra da modificare (Home/Away) con bottoni dedicati e indicatore visivo dello stato attivo',
      },
      {
        category: 'Feature',
        description:
          'Visualizzazione tribuna durante partite live - ora tutti i giocatori sono visibili anche quando la partita è iniziata',
      },
      {
        category: 'Feature',
        description: 'Badge prezzo giocatore visualizzato nelle modali di dettaglio e scambio (💰)',
      },
      {
        category: 'Improvement',
        description:
          'Ordinamento corretto panchinari nella modal di scambio - primi panchinari prima dei secondi',
      },
      {
        category: 'Improvement',
        description:
          'Polling automatico disabilitato durante modifica formazione in modalità Super Admin - previene sovrascrittura modifiche',
      },
      {
        category: 'Fix',
        description:
          'Risolto problema sovrapposizione card giocatori in panchina/tribuna su mobile - implementato ScrollView orizzontale con zoom sincronizzato',
      },
      {
        category: 'Fix',
        description:
          'Risolto problema apertura modal swap per Super Admin durante partite live - bypassato controllo hasGameStarted',
      },
      {
        category: 'Fix',
        description:
          'Corretta logica ownership squadre in modalità admin - permette modifica indipendentemente da canEdit e proprietà',
      },
    ],
  },
  {
    version: '1.1.0',
    date: '2 Novembre 2024',
    changes: [
      {
        category: 'Feature',
        description:
          'Sistema di statistiche giocatori dinamico basato su StatPlayer - sostituisce il vecchio sistema statico Chance',
      },
      {
        category: 'Feature',
        description:
          'Badge trend dinamico nelle card giocatori con calcolo in tempo reale basato su somma ultime 5 partite',
      },
      {
        category: 'Feature',
        description:
          'Timeline visiva partite nella modal dettaglio giocatore - pallini colorati con positiveness e giornata',
      },
      {
        category: 'Feature',
        description:
          'Banner notifica nuova versione con link diretto alle patch notes - mostrato automaticamente solo una volta per versione',
      },
      {
        category: 'Feature',
        description:
          'Sezione "Forma Recente" con trend calcolato su 5 soglie (Pessima, Negativa, Neutra, Positiva, Ottima) basato su somma ultime 5 partite',
      },
      {
        category: 'Feature',
        description:
          'Visualizzazione "Ultima Partita" con gestione partite non giocate e calcolo automatico positiveness',
      },
      {
        category: 'Feature',
        description: 'Sistema caricamento logo squadra con supporto PNG e preview in tempo reale',
      },
      {
        category: 'Feature',
        description:
          'Funzione "Usa Logo Anno Precedente" con modal di anteprima e conferma prima della copia',
      },
      {
        category: 'Improvement',
        description:
          'Algoritmo formazione automatica migliorato - privilegia ultima partita giocata rispetto al trend per evitare penalizzazione giocatori tornati da infortunio',
      },
      {
        category: 'Improvement',
        description:
          'Performance migliorate con Map lookup O(1) per accesso StatPlayer invece di array search',
      },
      {
        category: 'Improvement',
        description:
          'Normalizzazione chiavi giocatori consistente tra frontend e backend per lookup affidabile',
      },
      {
        category: 'Improvement',
        description:
          'Cache busting migliorato per loghi squadre - aggiornamento immediato dopo upload o copia con timestamp dinamico',
      },
      {
        category: 'Improvement',
        description:
          'Logo squadre ora mostrato in formato quadrato/originale invece di circolare ritagliato',
      },
      {
        category: 'Fix',
        description:
          'Risolto problema cambio nome squadra - ora funziona correttamente con validazione e feedback in tempo reale',
      },
      {
        category: 'Fix',
        description:
          'Risolto problema cambio username - aggiornamento corretto con persistenza e validazione',
      },
      {
        category: 'Fix',
        description:
          'Risolto errore 415 Unsupported Media Type durante upload logo - corretti attributi controller da [FromBody] a [FromForm]',
      },
      {
        category: 'Fix',
        description:
          'Risolto problema cache browser per loghi - aggiunto delay propagazione blob storage e cache buster iniziale',
      },
      {
        category: 'Fix',
        description:
          'Risolto problema redirect dopo scadenza token - implementato sistema auto-refresh token con fallback e ritorno automatico alla pagina precedente dopo re-login',
      },
      {
        category: 'Fix',
        description:
          'Risolto problema sovrapposizione card giocatori in panchina/tribuna su alcuni browser mobile - rimosso flexWrap in favore di ScrollView orizzontale',
      },
      {
        category: 'Improvement',
        description:
          'Zoom panchina e tribuna ora sincronizzato con zoom campo - controlli zoom applicati uniformemente a tutto il SoccerField',
      },
      {
        category: 'Feature',
        description:
          'Visualizzazione prezzo giocatore nelle modali di dettaglio e scambio - badge verde con icona 💰 accanto a nome e ruolo',
      },
      {
        category: 'Feature',
        description:
          'Aggiunto parametro Fortuna (🍀) nella classifica - visualizza quanto sei stato fortunato o sfortunato durante la stagione',
      },
      {
        category: 'Feature',
        description:
          'Modal di dettaglio fortuna con eventi partita per partita e spiegazione del calcolo',
      },
      {
        category: 'Feature',
        description:
          'Sistema di calcolo fortuna con 9 tipi di eventi diversi (vittorie fortunate, sconfitte sfortunate, pareggi, ecc.)',
      },
      {
        category: 'Feature',
        description:
          'Visualizzazione fortuna come percentuale nella classifica (+150%, -80%, ecc.)',
      },
      {
        category: 'Improvement',
        description:
          'Aggiunte colonne PF (Punti Fatti) e PS (Punti Subiti) con medie per partita nella classifica',
      },
      {
        category: 'Improvement',
        description:
          'Scrollbar orizzontale personalizzata per la tabella della classifica (miglior UX su mobile e PC)',
      },
      {
        category: 'Improvement',
        description: 'Aggiunti loghi delle squadre nel calendario delle partite',
      },
      {
        category: 'Fix',
        description: 'Risolto problema di allineamento dei nomi delle squadre nel calendario',
      },
      {
        category: 'Feature',
        description:
          'Banner "Installa App" per PWA con dismissal intelligente (sessione/7 giorni/per sempre)',
      },
      {
        category: 'Feature',
        description: 'Pagina Patch Notes per tenere traccia di tutti i cambiamenti',
      },
      {
        category: 'Feature',
        description:
          'Controlli Zoom per le card dei giocatori nel campo di gioco (10%-100%) con salvataggio preferenze',
      },
      {
        category: 'Improvement',
        description: 'Aggiunta visualizzazione versione app nella barra di navigazione',
      },
    ],
  },
  {
    version: '1.0.0',
    date: '15 Ottobre 2024',
    changes: [
      {
        category: 'Feature',
        description: "Release iniziale dell'app FantaZone",
      },
      {
        category: 'Feature',
        description: 'Classifica di lega con statistiche dettagliate',
      },
      {
        category: 'Feature',
        description: 'Calendario con risultati delle partite',
      },
      {
        category: 'Feature',
        description: 'Gestione formazione giocatori',
      },
      {
        category: 'Feature',
        description: 'Sistema di scambi tra utenti',
      },
      {
        category: 'Feature',
        description: 'Live score in tempo reale',
      },
      {
        category: 'Feature',
        description: 'Hall of Fame con statistiche storiche',
      },
    ],
  },
];

const getCategoryColor = (category: PatchNote['changes'][0]['category']) => {
  switch (category) {
    case 'Feature':
      return '$green10';
    case 'Fix':
      return '$red10';
    case 'Improvement':
      return '$blue10';
    case 'Breaking':
      return '$orange10';
  }
};

const getCategoryBgColor = (category: PatchNote['changes'][0]['category']) => {
  switch (category) {
    case 'Feature':
      return '$green3';
    case 'Fix':
      return '$red3';
    case 'Improvement':
      return '$blue3';
    case 'Breaking':
      return '$orange3';
  }
};

const PatchNoteCard: FC<{ note: PatchNote }> = ({ note }) => {
  return (
    <Card
      elevation="$2"
      borderWidth={1}
      borderColor="$borderColor"
      padding="$4"
      backgroundColor="$background">
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
                alignItems="center">
                <Text fontSize="$2" fontWeight="600" color={getCategoryColor(change.category)}>
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
  );
};

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
  );
};

export default PatchNotesScreen;
