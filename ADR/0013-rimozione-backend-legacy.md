# ADR 0013 — Un solo backend PostgreSQL, dipendenze Rystem ridotte

- Data: 2026-10-30
- Stato: implementato; nessun deploy o cutover eseguito.
- Sostituisce le parti degli ADR precedenti che mantenevano un backend runtime Blob/Table alternativo.

## Decisioni

1. **PostgreSQL è l'unico backend.** Rimosso il selettore `PostgreSql:Enabled`, i controller/hub legacy, i repository Blob/Table e InMemory, i rispettivi middleware e le registrazioni condizionali. Senza connection string PostgreSQL l'API fallisce esplicitamente; non esiste un fallback. `Integrations:Infrastructure=InMemory` rimane soltanto per sostituire cache/file nei test, non il database.
2. **Eliminati `Fantasoccer.Business.BackgroundJobs` e `Fantasoccer.Infrastructure.InMemory`.** I job attivi hanno equivalenti SQL già implementati: calendario, catalogo, voti, probabilità, formazioni, risultati, hall, scadenze mercato e immagini. Il Live si costruisce su richiesta con cache condivisa; le push hanno worker/outbox propri. News e consigli AI restano su comando esplicito: anche le vecchie registrazioni automatiche erano commentate.
3. **Rystem ammesso solo per factory e lock.** Rimangono `Rystem.DependencyInjection` e `Rystem.Concurrency`, con la loro dipendenza transitiva `Rystem`. Nessun pacchetto RepositoryFramework, Content, BackgroundJob o Authentication.Social, né diretto né transitivo. Le factory `IFactory<ILeagueCalculator>` e `IFactory<IFormationParser>` non sono repository e non vengono riscritte.
4. **Foto giocatori senza Content.** `PlayerImageService` usa `IPlayerImageStore`; l'implementazione produttiva usa direttamente `BlobContainerClient`, quella di test un dizionario in memoria. Invariati container `images`, chiavi `players/{playerKey}.jpg`, tipo MIME, cache HTTP e sostituzione delle foto. `ILock` resta Rystem Concurrency.
5. **Modelli legacy soltanto nell'importatore.** Documenti/chiavi necessari ai dump spostati in `tools/Fantasoccer.LegacyModels`; envelope `k/v` e `Key/Value` letti da un convertitore dedicato senza Rystem. Nessun riferimento dall'API alla DLL di migrazione. I tipi realmente condivisi dal motore e dall'importatore restano nel Domain.
6. **Login applicativo proprietario.** `AddSocialLogin`, `MapSocialLogin` e gli URL `/api/Authentication/Social/*` sono codice nostro, non il vecchio pacchetto. Eliminato il fallback a un verifier PKCE costante: Microsoft richiede il verifier della specifica transazione (RFC 7636). Il frontend lo invia sempre nel body. Per decisione esplicita, il refresh resta nel formato esistente e dura 365 giorni; il firewall PostgreSQL non viene ristretto in questo intervento.
7. **MCP solo sulle API attuali.** Rimossi chiavi/comandi/guide dei repository legacy. I GET delle API relazionali sono classificati correttamente come letture; prima le regole del catalogo RepositoryFramework li etichettavano come scritture. Il catalogo dei modelli include i DTO di Application.Core. Restano OAuth, PKCE, consenso, revoca e limitazione del token alla risorsa MCP; test eseguiti sul backend PostgreSQL.
8. **Formazioni: copia automatica, non identità garantita.** Confermata la semantica best effort prepartita: ogni bersaglio applica regole, versioni e scadenze proprie; il salvataggio principale non viene annullato da una copia rifiutata. L'attivazione non riallinea retroattivamente le formazioni; i cambi live non si propagano. I testi dell'interfaccia lo dichiarano esplicitamente, senza promettere schieramenti sempre identici.

## Test e distribuzione

I test dei servizi eliminati sono rimossi, non mantenuti su un backend fantasma. Restano i test di regole e parser, ora registrati senza repository. OAuth/MCP e lo storage diretto delle foto sono verificati nella suite relazionale; i modelli di migrazione con fixture e integrazione PostgreSQL/Azurite. Dockerfile, compose e CI includono la DLL di modelli solo nelle immagini di migrazione/test.

Questa pulizia non certifica il dump reale: analisi, riconciliazione e prova integrale su target sacrificabile rimangono condizioni del runbook prima di qualunque cutover. La misura del carico Live SQL/Blob rimane una verifica operativa separata, non deducibile da una build verde.
