# ADR 0001 — EF Core diretto, servizi condivisi e JSON leggibile

- Data: 2026-10-25
- Stato: decisione concordata; implementazione da realizzare

## Contesto verificato

La persistenza attuale è registrata in `src/Fantasoccer.Infrastructure.Storage/ServiceCollectionExtensions.cs`.
RepositoryFramework non è soltanto un accesso allo Storage: genera API, applica policy e autorizzazioni specifiche e supporta hook business.

In particolare:

- `Program.cs` registra `AddApiFromRepositoryFramework()` e pubblica gli endpoint con `UseApiFromRepositoryFramework().SetPolicyForAll().With("authenticated")`.
- `Fantasoccer.Business/ServiceCollectionExtensions.cs` richiama `ScanBusinessForRepositoryFramework()` e `ScanAuthorizationForRepositoryFramework()`.
- `TeamAuthorization.cs` implementa autorizzazioni degli endpoint repository distinte dalle chiamate interne fidate.
- Controller e middleware contengono ulteriori controlli e logica.
- Il frontend usa `rystem.repository.client`, modelli con proprietà compatte e conversioni verso modelli leggibili.
- MCP ricostruisce attualmente la pipeline HTTP tramite `McpApiFacade`.

La presenza di una scansione business non dimostra quali hook siano attivi: occorre censire implementazioni, registrazioni e comportamento effettivo.

## Decisione

```text
Controller HTTP ─┐
Tool MCP ────────┼─> Servizi applicativi ─> DbContext EF Core ─> PostgreSQL
Hub SignalR ────┤          │
Background job ─┘          ├─> Calcolatori e regole di dominio
                          └─> Servizio file ─> Azure Blob Storage
```

- Usare EF Core e Npgsql compatibili con .NET 10, verificando versioni supportate all'implementazione.
- Introdurre l'infrastruttura PostgreSQL e un contesto applicativo, senza `IRepository<T>` o Unit of Work aggiuntivi.
- Tenere EF e i dettagli SQL fuori dagli adattatori HTTP/MCP. I servizi applicativi usano direttamente il contesto.
- Separare le regole di calcolo pure dall'orchestrazione che carica e salva dati.
- Scegliere servizi per funzionalità, non creare un'interfaccia e una classe per ogni singolo CRUD se non serve.
- Gestire correttamente scope e durata del contesto: nessun `DbContext` catturato da singleton; job e hub usano scope o factory appropriati.
- Non rimuovere indiscriminatamente ogni pacchetto Rystem: valutare separatamente login social, contenuti e altre utilità.

### Sostituzione della superficie automatica

Prima di eliminare RepositoryFramework produrre una matrice con:

| Campo | Contenuto richiesto |
| --- | --- |
| Origine | Entità, tipo chiave, factory, repository esposto o interno |
| Endpoint | Route, verbo, input, risposta e uso effettivo frontend/MCP |
| Autorizzazione | Policy, handler repository, middleware e controlli business |
| Pipeline | Hook prima/dopo, validazioni, effetti collaterali, gestione errori |
| Destinazione | Nuovo endpoint, servizio e tool MCP, oppure dismissione motivata |
| Verifica | Test che prova il comportamento e il rifiuto degli accessi non autorizzati |

Includere letture/scritture semplici, `Exist`, query, conteggi, batch, bootstrap e factory storiche.
Non replicare automaticamente operazioni inutilizzate o pericolose; non perdere quelle necessarie solo perché non hanno un controller scritto a mano.
Un repository non esponibile non deve diventare pubblico durante la migrazione.

### API e DTO

- Riscrivere le API frontend insieme al backend, senza livello permanente di compatibilità con le vecchie route.
- Contratti espliciti per letture e comandi; mai binding diretto delle entità EF in scrittura.
- Proprietà C# descrittive e JSON `camelCase` completo: `groupId`, `seasonId`, `ownerUserId`, `players`, non `g`, `y`, `e`, `p`.
- Preferire nomi non ambigui: `seasonIndex`, `serieAMatchday`, `fantasyRound`, `globalMatchdayId` dove corrispondono davvero a concetti diversi.
- DTO di input con i soli campi modificabili; prezzi, crediti, membership e ruoli non devono essere modificabili tramite aggiornamenti generici del profilo o della formazione.
- Risposte tipizzate, codici HTTP coerenti e `ProblemDetails` per gli errori. GET non deve produrre modifiche.
- Dichiarare nei contratti nullabilità, date UTC/offset, valori enum, paginazione e unità di misura. Verificarne l'uso nel client.
- OpenAPI come fonte dei contratti TypeScript, preferibilmente con generazione ripetibile e controllo di allineamento in CI.
- Il frontend usa direttamente i DTO leggibili: eliminare modelli `Raw` e mapper di espansione delle abbreviazioni. Le trasformazioni puramente di presentazione restano ammesse.
- La lettura dei vecchi JSON compatti rimane confinata all'importatore e alle fixture legacy, non ai nuovi contratti pubblici.

Esempio indicativo, non contratto definitivo:

```json
{
  "teamId": "team-id",
  "groupId": "group-id",
  "seasonIndex": 15,
  "name": "Nome squadra",
  "ownerUserId": "user-id",
  "additionalOwnerUserIds": [],
  "players": []
}
```

### Autorizzazioni

- Policy esplicite sugli endpoint, con autenticazione effettiva (`RequireAuthenticatedUser`), non sola presenza di una claim email.
- Handler condivisi per membership attiva, amministrazione del gruppo, proprietà/comproprietà, gestione asta e amministrazione globale.
- Verifica della risorsa nei servizi applicativi, affinché anche MCP sia protetto senza attraversare MVC.
- Fonte autorevole dei ruoli nel database; non fidarsi di ruoli, `owner` o `asAdmin` forniti dal client.
- Distinguere amministratore globale e ruoli amministrativi nel gruppo.
- Policy di default restrittiva e anonimato esplicito soltanto dove necessario, ad esempio discovery e login.
- Ogni operazione specifica se ammette un utente o un'esecuzione di sistema: i job non ottengono privilegi tramite un finto principal HTTP.
- Rimuovere i middleware di sicurezza legacy solo dopo aver coperto e verificato tutti i comportamenti sostitutivi.

### MCP e SignalR

- Controller e tool MCP chiamano gli stessi servizi e ricevono gli stessi contratti leggibili.
- Sostituire la dipendenza dalla simulazione HTTP senza ricostruire un altro framework generico.
- Riutilizzare SDK MCP, login, OAuth e configurazione esistenti quando semplice; aggiornare guida e catalogo ai nuovi contratti, senza glossario delle abbreviazioni.
- Nessuna esposizione generica di SQL, `IQueryable` o CRUD sulle entità EF.
- Conservare discovery/capacità utili; non imporre compatibilità degli identificatori delle vecchie operazioni se complica inutilmente il progetto.
- Estrarre la logica d'asta da hub/controller verso servizi condivisi, mantenendo i broadcast SignalR.
- Valutare gli store EF ufficiali OpenIddict. Migrare client, concessioni e token attivi solo se ragionevole; in alternativa documentare il nuovo collegamento richiesto.
- Decidere esplicitamente se mantenere le sessioni frontend. Non ruotare o eliminare chiavi Data Protection/certificati accidentalmente.

### Transazioni e schema allo startup

- Operazioni su crediti, mercato, rose e aste atomiche; vincoli DB e controllo della concorrenza verificati anche con chiamate simultanee.
- Per gli effetti esterni evitare invii prima del commit e retry che duplicano notifiche. Introdurre un outbox solo se il requisito di consegna lo giustifica.
- Applicare `Database.MigrateAsync()` prima di warm-up, avvio dei job e disponibilità al traffico.
- Coordinare migration concorrenti fra repliche con meccanismo verificato sul provider, timeout e rilascio sicuro del lock.
- Fallire l'avvio se lo schema non è pronto. Non usare `EnsureCreated` come sostituto delle migration.
- Esplicitare i privilegi DDL necessari e la gestione delle credenziali in Azure.
- L'importazione legacy non viene eseguita automaticamente allo startup.

## Conseguenze e accettazione

Il rilascio richiede aggiornamento coordinato del frontend e manutenzione. I contratti diventano più chiari e il livello repository automatico scompare.
La sostituzione è completa quando ogni comportamento della matrice è coperto o dismesso esplicitamente, e frontend/MCP non dipendono più da route, chiavi o alias RepositoryFramework.
