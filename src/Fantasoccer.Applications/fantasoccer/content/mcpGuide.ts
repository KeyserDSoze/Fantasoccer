// Public documentation only: no tokens, client secrets or account data belong here.
export const MCP_GUIDE_PATH = 'mcp-guide'
export const CHATGPT_PLUGINS_URL = 'https://chatgpt.com/plugins'
export const CHATGPT_MCP_HELP_URL = 'https://help.openai.com/en/articles/12584461-developer-mode-and-mcp-apps-in-chatgpt'
export const MCP_GUIDE_REVIEWED_ON = '25 ottobre 2026'

export const mcpSetupSteps = [
  {
    title: 'Apri ChatGPT dal browser',
    description: 'Usa ChatGPT sul web, preferibilmente da computer. Apri Plugins, premi + e scegli “Add custom MCP server”. Se hai ancora l’interfaccia precedente, cerca Impostazioni → App → Crea. Il tuo piano e il workspace devono consentire le connessioni MCP personalizzate.',
  },
  {
    title: 'Inserisci nome e indirizzo',
    description: 'Come nome scegli “Fantasoccer”. Nel campo Connection / MCP server URL incolla l’indirizzo completo qui sopra, compreso /mcp. Non usare l’indirizzo del sito fanta.zone e non scegliere un tunnel: il server è già pubblico su HTTPS.',
  },
  {
    title: 'Scegli OAuth con registrazione automatica',
    description: 'Seleziona OAuth, non “No authentication”. Se i campi Client ID e Client secret sono facoltativi, lasciali vuoti: ChatGPT può registrare automaticamente il proprio client. Non devi copiare callback, password Microsoft o secrets di GitHub. Se la schermata impone credenziali manuali, non inventarle: controlla le opzioni di registrazione o chiedi assistenza.',
  },
  {
    title: 'Avvia la connessione e la scansione',
    description: 'Leggi l’avviso di rischio e confermalo solo se riconosci il server. Nella nuova interfaccia premi “Create as a plugin”; in quella precedente “Scan Tools”. ChatGPT avvia la scoperta degli strumenti e può aprire il login OAuth: completa il passaggio successivo senza chiudere la pagina.',
  },
  {
    title: 'Accedi a Fantasoccer e dai il consenso',
    description: 'Quando si apre Fantasoccer, usa lo stesso account Microsoft con cui accedi al sito. Se sei già autenticato, potresti vedere direttamente il consenso: controlla l’email e autorizza soltanto il collegamento appena avviato. Torna a ChatGPT, attendi la scansione e, se richiesto dall’interfaccia precedente, premi “Create”.',
  },
  {
    title: 'Seleziona Fantasoccer nella chat',
    description: 'Apri una nuova conversazione e seleziona l’app/plugin Fantasoccer, ad esempio digitando @ nel messaggio. Inizia con una richiesta di sola lettura. Se una richiesta successiva ha bisogno di nuovi dati, seleziona di nuovo l’app quando richiesto dall’interfaccia.',
  },
] as const

export const mcpPromptExamples = [
  {
    title: 'Primo collegamento',
    text: 'Leggi la guida di Fantasoccer e la stagione corrente, poi mostrami i miei gruppi senza modificare dati.',
  },
  {
    title: 'Classifica',
    text: 'Mostrami la classifica della mia lega per la stagione corrente. Se ho più leghe, chiedimi quale scegliere. Non modificare dati.',
  },
  {
    title: 'Suggerimento formazione',
    text: 'Ricava dal mio gruppo owner e basket della mia squadra, leggi la rosa con una lettura puntuale e suggeriscimi una formazione per la prossima giornata. Spiegami le scelte, senza salvarla.',
  },
  {
    title: 'Mercato',
    text: 'Mostrami le proposte di mercato aperte nella mia lega e spiegami quali richiedono la mia risposta. Non accettare, rifiutare o votare nulla.',
  },
] as const

export const mcpTools = [
  { name: 'get_current_season', description: 'Legge data server e stagione corrente: 2026/27 corrisponde all’indice 15.' },
  { name: 'get_my_account', description: 'Identifica il tuo account, i gruppi e le selezioni salvate, che possono riferirsi anche a stagioni storiche.' },
  { name: 'get_fantasoccer_guide', description: 'Spiega regole d’uso e funzionalità. Il tema lookups mostra come seguire account → gruppo → lega, basket e owner → rosa o formazione della partita.' },
  { name: 'describe_api_model', description: 'Decodifica nomi JSON abbreviati, tipi e valori enum. La stessa lettera può significare cose diverse nei diversi modelli.' },
  { name: 'list_api_operations', description: 'Cerca nel catalogo delle operazioni, non nei dati degli utenti. Descrive letture puntuali, ricette per le chiavi e flussi autorizzati; non espone ricerche o aggregazioni generiche sui repository.' },
  { name: 'execute_api_operation', description: 'Esegue le operazioni applicative con i tuoi permessi. Può modificare dati: non è uno strumento di sola lettura.' },
] as const

export const mcpTroubleshooting = [
  {
    question: 'Vedo calendario e classifica, ma l’assistente non recupera la rosa.',
    answer: 'Chiedigli di leggere la guida con il tema lookups. Dal gruppo deve risolvere basket, stagione e owner effettivo, poi usare Team/Get; per una formazione salvata usa Team/day/Get e la giornata Serie A della partita. Il calendario contiene le email degli owner, distinte dai nomi squadra. Le ricerche generiche come Team/Query non sono disponibili su MCP. Se manca lo snapshot giornaliero può leggere la rosa stagionale, spiegando che non è la formazione salvata di quella partita.',
  },
  {
    question: 'Non vedo Plugins, Crea o il pulsante per aggiungere il server.',
    answer: 'La disponibilità dipende dal piano ChatGPT, dai permessi del workspace e dalla versione dell’interfaccia. Controlla la guida ufficiale OpenAI o chiedi all’amministratore del workspace. Nella nuova interfaccia Plugins non serve abilitare prima la modalità sviluppatore; in alcune interfacce precedenti l’opzione può essere nelle impostazioni avanzate delle App.',
  },
  {
    question: 'ChatGPT mi chiede un Client ID o un Client secret.',
    answer: 'Fantasoccer supporta la registrazione dinamica OAuth. Lascia vuoti i campi facoltativi e usa la registrazione automatica, se disponibile. Non usare i secrets del deploy, il certificato o la password Microsoft. Se il client richiede soltanto una configurazione manuale, chiedi assistenza: non esiste un secret universale da distribuire agli utenti.',
  },
  {
    question: 'Aprendo /mcp nel browser vedo 401 o un errore: è normale?',
    answer: 'Sì, /mcp non è una pagina web e rifiuta le richieste senza un token MCP valido. Incolla quell’indirizzo nella configurazione del client e completa OAuth. Non scegliere un collegamento senza autenticazione e non copiare il bearer del sito nella chat.',
  },
  {
    question: 'Il consenso è scaduto o il collegamento non si completa.',
    answer: 'Ricomincia il collegamento dal client MCP nello stesso browser e completa login e consenso senza attendere troppo. Verifica di usare l’account Microsoft giusto. Non creare altri utenti o modificare gruppi per risolvere un errore di connessione; se persiste, comunica il messaggio di errore senza inviare token o password.',
  },
  {
    question: 'Non vedo la guida, la stagione o i nuovi strumenti in ChatGPT.',
    answer: 'Aggiorna o scansiona nuovamente gli strumenti dalla gestione dell’app/plugin. Alcuni workspace conservano una copia del catalogo che deve essere aggiornata da chi gestisce l’app. Poi apri una nuova chat e seleziona Fantasoccer. Se non c’è un comando di aggiornamento, può essere necessario ricreare la connessione.',
  },
  {
    question: 'Il gruppo, la lega o la stagione mostrati non sono quelli che volevo.',
    answer: 'Specifica la lega e la stagione nella richiesta. La selezione salvata sul sito può essere storica e non coincide necessariamente con la stagione corrente. Chiedi all’assistente di leggere prima account, gruppi e stagione server; per operazioni ambigue deve chiederti di scegliere.',
  },
  {
    question: 'Una modifica viene rifiutata, anche se riesco a leggere i dati.',
    answer: 'Il client ha gli stessi limiti del tuo account. Contano ruolo nel gruppo, ownership, scadenze, regole della lega e capacità del piano/client. Il collegamento non ti rende amministratore: mercato, formazione e aste devono passare dai flussi previsti. Controlla il messaggio applicativo, non soltanto l’esito HTTP.',
  },
  {
    question: 'Posso usare un’app diversa da ChatGPT?',
    answer: 'Sì: usa lo stesso URL con un client che supporti MCP remoto via Streamable HTTP e il flusso OAuth con registrazione dinamica e PKCE S256. Non tutti i client supportano gli stessi protocolli. Ogni persona fa il proprio login Microsoft e può revocare i propri collegamenti dal sito.',
  },
] as const
