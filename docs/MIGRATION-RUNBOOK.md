# Windows: membri, reset locale e migrazione Azure Storage → PostgreSQL

## Permessi utenti

Chi crea un gruppo con un codice ne diventa **responsabile e amministratore**. Nel menu trova **Membri e inviti** e **Configura gruppo**. Può cercare un **indirizzo email completo**, aggiungere un account esistente o predisporre l'associazione per chi non ha ancora effettuato il primo login. Non vede l'elenco globale, lo stato privato dell'account o le appartenenze ad altri gruppi.

L'associazione non spedisce una mail: comunica tu all'amico il link del frontend e fagli usare Microsoft con quell'email. Un amministratore ordinario può invitare, ma solo un responsabile può assegnare/togliere il ruolo di responsabile; l'ultimo responsabile/amministratore è protetto dalla rimozione.

Il **super admin di piattaforma** ha la vista separata `/platform`: tutti gli utenti (anche senza gruppi o disabilitati), con le rispettive appartenenze, e tutti i gruppi. Le normali schermate di gruppo restano limitate alle membership reali.

## 1. Quale connection string impostare

Apri il `.env` **privato** nella root e compila:

```dotenv
# SORGENTE Azure: lettura/export soltanto. Non usare una SAS di un singolo Blob.
FANTASOCCER_EXPORT_STORAGE='DefaultEndpointsProtocol=https;AccountName=ACCOUNT_SORGENTE;AccountKey=CHIAVE_REALE;EndpointSuffix=core.windows.net'

# Per NON importare aste storiche e vecchie liste randomizzate:
IMPORT_LEGACY_AUCTIONS=false
# Import SQL dei log storici disattivato; distinto dall'opzione -IncludeTables.
IMPORT_LEGACY_LOGS=false
# Possono restare vuoti con la scelta precedente.
# Se invece IMPORT_LEGACY_AUCTIONS=true, servono le chiavi ORIGINALI per le liste cifrate.
LEGACY_ENCRYPTION_KEY=
LEGACY_ENCRYPTION_IV=

# Nessuno scrittore/notifica esterna sulla copia, specialmente durante l'import.
BACKGROUND_JOBS_ENABLED=false
EXTERNAL_HTTP_ENABLED=false
PUSH_ENABLED=false
AI_ENABLED=false
MCP_ENABLED=false
```

Sostituisci i placeholder con la tua connection string; preferisci una **SAS account di sola lettura**, con listing/lettura Blob. Query/listing Table servono solo se scegli `-IncludeTables`. Una SAS di un singolo container/Blob non basta a elencare tutti i container dell'account. Il tool legge soltanto dalla sorgente anche se la credenziale ha permessi superiori; non supporta ancora managed identity/Entra.

**Non confondere gli ambienti:** `FANTASOCCER_EXPORT_STORAGE` è la sorgente reale, passata solo al container di export. `ConnectionStrings:Storage` dell'API e il restore puntano ad **Azurite locale**; il restore remoto è vietato. Il target SQL è solo il servizio Compose `postgres`, DB/utente `fantasoccer_local`. Le tue porte personalizzate non cambiano questi indirizzi interni.

Non mettere connection string in argomenti, screenshot, `.env.test` o Git. Usa apici singoli nel `.env` per evitare l'espansione di `$`. Non stampare `docker compose config` completo: espanderebbe i segreti.

## 2. Scaricare e analizzare, senza importare nel database

Dalla root del repo, in PowerShell, il piano senza alcuna connessione:

```powershell
.\scripts\local\sync-storage.ps1 -EnvFile .env -DumpName azure-prova1 -WhatIf
```

Per eseguire export → verifica checksum → analisi mapping:

```powershell
.\scripts\local\sync-storage.ps1 -EnvFile .env -DumpName azure-prova1
```

Il dump è in `migration-data/azure-prova1/`, ignorato da Git e dai build Docker. Contiene **dati reali e possibili token/artefatti privati**: proteggi filesystem, ACL e backup.

Per default sono esportati **solo tutti i container Blob nello stato corrente**, compresi file, immagini e contenitori vuoti: Table Storage non viene nemmeno interrogato. Il manifest dichiara `scope=blob-current-state` e `Tables` tra le esclusioni. Non è un backup completo dell'account Azure: non include Queue, File Share, vecchie versioni/snapshot Blob, soft delete, tag o policy di accesso.

Solo se vuoi scaricare anche tutte le Table, aggiungi l'opzione esplicita:

```powershell
.\scripts\local\sync-storage.ps1 -EnvFile .env -DumpName azure-prova1 -Resume -IncludeTables
```

`-IncludeTables` abilita download e, con `-Apply`, restore locale delle tabelle; **non** abilita automaticamente l'import SQL dei log. Quest'ultimo ha il flag separato `IMPORT_LEGACY_LOGS=true` e richiede che il dump contenga le Table. La CLI .NET usa `--include-tables`; Bash usa `INCLUDE_TABLES=true`.

Ripresa/riacquisizione della **stessa** sorgente:

```powershell
.\scripts\local\sync-storage.ps1 -EnvFile .env -DumpName azure-prova1 -Resume
```

`-Resume` rifa il listing ma **non riscarica i Blob invariati** se ETag, dimensione e checksum locale coincidono. Riscarica file modificati, corrotti o non ancora registrati nell'ultimo checkpoint (al massimo 49 prima di un'interruzione brusca). Le Table, **solo con `-IncludeTables`**, vengono invece rilette dall'inizio: non c'è ripresa sicura di una scansione Table cambiata nel frattempo.

Un dump completato si verifica/analizza senza ricontattare Azure con `-UseExistingDump` al posto di `-Resume`. Puoi anche riprendere un vecchio export interrotto sulle Table usando `-Resume` **senza** `-IncludeTables`: riusa i Blob e completa un manifest Blob-only. Eventuali file Table già scaricati restano sul disco ma non sono referenziati dal nuovo manifest, né eliminati automaticamente.

### Se compare `invalid_email`

`Core import refused: invalid_email` è un errore di validazione nei **documenti Blob** (identità, membri, proprietari, preferenze, ecc.), non un errore di accesso a Table Storage. Escludere le tabelle non garantisce di risolverlo. Nessun dato di import viene committato parzialmente: non fare un reset solo per questo errore.

Il tool indica `Field` e `reader` (nome del file sorgente del lettore e riga), senza stampare indirizzi email, valori, Blob key o payload. Per gli errori di `appidentity` aggiunge anche `Document SHA-256`, corrispondente alla voce `sha256` nel manifest: permette di identificare localmente il documento senza pubblicarne il nome o il contenuto.

Un caso specifico è `Field: key` durante la lettura delle identità: l'email del profilo è già valida, ma la chiave interna dell'envelope non è un indirizzo. La chiave di storage **può essere un identificatore tecnico**, distinto dall'email usata per il login. Sono supportate queste forme, senza inventare indirizzi:

- chiave interna email, uguale all'email valida del profilo;
- chiave interna opaca, vuota o mancante, quando il nome del Blob è l'email valida del profilo (trim/case canonici);
- chiave tecnica non-email **non vuota e identica al nome del Blob**, confrontata esattamente con `Ordinal`: non si ignorano maiuscole o spazi per gli ID tecnici. L'email continua a provenire dal campo del profilo, non dall'ID.

Le chiavi contenenti un'altra email valida, i nomi Blob che dichiarano un'email discordante, i duplicati e gli ID tecnici non corrispondenti restano bloccati. Non si fondono automaticamente profili e un ID di storage non viene usato come subject Microsoft o credenziale.

I casi recuperati sono espliciti nel report (`reconciledIdentityDocuments`, hash SHA-256) e in `pendingFields`. La relazione documento sorgente → account viene conservata nel mapping SQL con riferimento al file di archivio e checksum, **entrambi hash**, senza duplicare in log/SQL il nome o la chiave tecnica originale. Gli originali restano nel dump. Gli ID account deterministici per email, i ruoli e i controlli di membership rimangono invariati.

Per ripetere soltanto l'analisi, senza download o import SQL:

```powershell
.\scripts\local\sync-storage.ps1 -EnvFile .env -DumpName azure-prova1 -UseExistingDump
```

Sostituisci `azure-prova1` con il **nome del dump che hai già scaricato**. Se il manifest è incompleto, usa prima `-Resume`. Non cancellare il dump o i checkpoint e non saltare automaticamente utenti: per i conflitti non recuperabili occorre riconciliare il campo indicato. Puoi condividere codice errore, `Field`, `reader` e `Document SHA-256`, non connection string, email, nomi dei Blob o l'intero dump.

### Se compare `invalid_player`

Riguarda un **calciatore**, non gli account o i ruoli dei membri. Il lettore continua a rifiutare una chiave giocatore vuota dopo la normalizzazione legacy oppure un ruolo fuori intervallo. Il ruolo ammesso è `-1` (non definito), `0` (portiere), `1` (difensore), `2` (centrocampista), `3` (attaccante).

Il messaggio indica ora `Document SHA-256` e `Player details`:

- `container`: il container del documento effettivamente in lettura;
- `archiveReference`: il percorso hash relativo al dump, per esempio `blobs/<hash>.bin`; non è il nome Blob originale;
- `jsonPath`: posizione esatta del record, a partire da `$.v` oppure `$.Value` nell'envelope storico. Per esempio `$.v.p[7]` è l'ottavo calciatore della lista, `$.v.wp[1].p` il giocatore della seconda voce dell'albo;
- `reasons`: `empty_player_key` se il nome non produce una chiave con la vecchia normalizzazione, `role_out_of_range` se il ruolo numerico non è ammesso; possono comparire entrambi. `null_player` identifica una voce calciatore `null`;
- `nameMissing`, `nameLength`, `role`: sola forma del nome e valore numerico del ruolo, mai nome o chiave del calciatore.

`Document SHA-256` è il checksum del contenuto registrato nel manifest; il nome del file in `archiveReference` deriva invece dall'hash container/nome Blob. Sono due riferimenti distinti. Il contesto segue il record effettivamente rifiutato anche se un altro documento ha già fornito una copia valida dello stesso giocatore: non viene cercato per nome in un file arbitrario.

La diagnostica copre cataloghi, rose, campioni voti/probabilità/statistiche, mercato, partite salvate, albo, baseline push e aste **solo se incluse**. Non decodifica i container esclusi per cercare altri errori e non corregge identità. Le sole regole specifiche per nomi assenti sono l'archiviazione dei cataloghi e l'esclusione approvata dei voti/baseline descritta sotto; non sono uno skip generico di `invalid_player`. Nomi, email dei proprietari, nomi Blob originali e payload non vengono stampati.

Riesegui soltanto l'analisi con `-UseExistingDump` e lo stesso env privato. Non aggiungere opzioni, fare reset, modificare il dump o usare `-Apply` per aggirare il controllo. Condividi il codice errore, `Document SHA-256` e `Player details`, non l'intero documento. Il record va poi esaminato localmente prima di scegliere un'eventuale riconciliazione.

#### Voti senza nome: esclusione scelta dal gestore

Gli oggetti senza nome (assente, null, vuoto o whitespace) in `live`/`notification`/`official` e nelle baseline push vengono esclusi dall'import SQL, anche con voto/bonus o `hasVote:true`. Non vengono creati giocatori o club fittizi, né un archivio SQL dei voti irrisolti: gli originali restano soltanto nel dump verificato. Il caso esaminato è `official` stagione 10/giornata 1, 331 voci di cui 35 senza nome; una contiene voto finale 6 e club `None`, quindi non è trattata come un segnaposto privo di dati.

Controlla `skippedUnnamedVotes`: riferimenti hash, anno/giornata, totale delle voci, numero escluso, quante avevano `hasVote:true` e prima posizione JSON. I voti nominati rimangono collegati al giocatore annuale con FK obbligatoria e conservano valori/flag; `None` nei record esclusi non diventa un club. Null array item, nomi non vuoti ma chiave invalida, ruoli/club invalidi dei nominati, voti mancanti e duplicati restano errori. Le rose non vengono scartate, salvo le voci vendute o rimosse senza alcun nome descritte sotto. Non servono nuove opzioni env, reset o migration di schema.

Questa esclusione non ricalcola calendari, risultati o classifiche fantasy: `calendar` e la classifica corrente `rank` vengono importati con i valori salvati. Con MappingVersion 36 o successiva le copie `dailyrank` restano soltanto nel dump verificato: lo storico della classifica viene ricostruito su richiesta dai risultati salvati fino alla giornata scelta e dal regolamento annuale, senza ricalcolare voti o accreditare denaro. La vista della partita può mostrare un'anteprima live senza sovrascrivere i risultati salvati. [Contratto delle classifiche](FANTASY-STANDINGS.md).

#### Probabili formazioni, statistiche e rose senza nome utilizzabile

Dalla MappingVersion 40, per decisione del gestore, una voce il cui nome manca o non contiene lettere che formino una chiave giocatore (per esempio `----`) viene esclusa da SQL anche fuori dai voti:
- **probabili formazioni e statistiche:** sono previsioni e dati derivati, e le voci senza nome non identificano nessuno. Nel dump reale sono 33 righe di probabili formazioni (2023/24 e 2024/25) e 514 righe di statistiche 2022/23 su 1194. Le righe con nome restano tutte. Il report le conta in `skippedUnusablePlayerRows` (documento, stagione, giornata, totale, escluse, prima posizione);
- **rose:** sono escluse solo le voci **vendute o rimosse** (stato da 1 a 4) senza nome e senza una chiave giocatore memorizzata. Nel dump reale sono 4 voci 2021/22 vendute a metà prezzo (prezzi 16, 3, 28, 28). Prezzo, ricavo, stato e posizione di ciascuna sono elencati in `excludedNamelessRosterEntries`. I costi ricalcolati da quelle rose storiche non le includono più; oggi i crediti si ricalcolano solo per mercato e aste della stagione corrente.

Una voce **attiva** senza nome resta un errore `invalid_player`, perché toglierla cambierebbe una rosa in uso. Se il nome manca ma la voce ha una chiave memorizzata di un giocatore noto, il nome viene recuperato come prima. Nessun giocatore viene creato da queste voci, e gli originali restano nel dump verificato.

Per trovarle tutte in una volta, `.\scripts\local\scan-player-names.ps1 -DumpName <nome>` (sola lettura) applica le stesse regole a tutti i contenitori con giocatori, conta quelle già gestite e mostra solo quelle che bloccherebbero l'import.

#### Catalogo con nomi realmente vuoti e recupero dalle rose

Se il confronto del JSON conferma righe con `n:""` nel catalogo `realplayerswrapper`, senza una chiave calciatore indipendente, non è possibile ricostruire l'identità da club, ruolo o ordine della lista. La `Key` numerica dell'envelope identifica la **stagione**, non un calciatore; la `RealPlayer.Key` del codice è derivata dal nome, quindi anch'essa vuota in questo caso.

Le righe senza nome non vengono importate come calciatori: sono conservate automaticamente come originali irrisolti in SQL, senza inventare nomi o chiavi. Non serve aggiungere opzioni per questa regola. Se vuoi mantenere anche un controllo su **un documento esatto** e sul conteggio verificato, il pin opzionale rimane supportato:

```dotenv
PLAYER_CATALOG_RESOLUTIONS_JSON='[{"documentSha256":"SHA256_CATALOGO_APPROVATO","expectedUnnamedPlayers":2}]'
```

Il checksum deve avere 64 caratteri esadecimali e il conteggio deve essere positivo e corrispondere alle righe senza nome, anche null/whitespace. Il pin è un'asserzione opzionale, non l'abilitazione di uno skip. Se lo conservi nell'env, hash cambiati/mancanti/ambigui e conteggi diversi rimangono errori. È passato soltanto al tool (`FANTASOCCER_PLAYER_CATALOG_RESOLUTIONS_JSON` con CLI diretta). Le risoluzioni delle identità utenti restano indipendenti e obbligatorie per le coppie in conflitto.

`PlayerKey` è il nome normalizzato globale; la chiave annuale è composta da `PlayerKey` e `SeasonIndex`, senza colonna YearlyPlayerKey. Club e riferimenti SQL usano codici di tre lettere. La migration converte le vecchie relazioni UUID tramite mappe verificate, conservando i GUID in `LegacyId`; chiavi o codici duplicati/ambigui fanno fallire tutto senza merge. Non usare un reset per aggirare una collisione. Il [contratto dei calciatori](PLAYER-IDENTITY.md) descrive le PK/FK naturali e il lookup.

I record nominati rimangono soggetti ai controlli su identità e ruoli; nomi non vuoti che non producono una chiave valida restano errori. I record di catalogo senza nome sono solo originali irrisolti: anche un loro eventuale identificatore rimane conservato nel payload, non usato per inventare un'anagrafica.

Gli originali irrisolti sono conservati **integralmente in SQL `UnresolvedPlayerCatalogEntries`**, con checksum, file hash, stagione, posizione e indice. Il testo mantiene campi sconosciuti, escape e proprietà duplicate. Righe identiche restano distinte; il loro ID d'archivio non è una PlayerKey. Sorgente, dump e flag originali rimangono invariati.

Se una rosa contiene un calciatore con nome valido assente dal catalogo, il lettore crea l'anagrafica e/o l'anno mancante e lo dichiara in `recoveredRosterPlayers`. Le proprietà esplicite `Key`/`PlayerKey` devono concordare con il nome; il compatto `k` è invece la posizione in formazione. Una chiave già nota può collegare un'anagrafica verificata; senza nome né chiave utilizzabile la rosa si ferma, non perde prezzi o righe storiche. Club/ruolo/ordine non vengono usati per indovinare identità, né vengono unite varianti HTML.

Rianalizza con `-UseExistingDump`. Controlla cataloghi irrisolti, recuperi, avvisi e riferimenti hash: il report non stampa i payload e mantiene `fullMigrationComplete:false`. Usa `-Apply` solo dopo analisi riuscita e revisione. Le migration e l'import sono atomici; un downgrade con dati calcistici presenti viene rifiutato anziché ricostruire UUID. Restano i blocchi su target non appartenente alla sorgente e reimport dopo scritture applicative.

### Se compare `duplicate_or_invalid_vote`

Il codice storico resta invariato, ma `Vote details` distingue ora due cause, eventualmente entrambe: `duplicate_player_vote` (due voci con la stessa PlayerKey/anno/giornata/fonte) e `non_finite_vote_value` (valore non rappresentabile come numero `float` finito). Non significa necessariamente che si possa scartare una riga: due nomi diversi possono convergere nella stessa chiave legacy, oppure la stessa identità può avere voti o flag discordanti.

La diagnostica riporta `Field: vote`, reader, checksum del documento, container, riferimento hash, anno/giornata e posizione originale del record. Per il duplicato aggiunge `otherDocumentSha256`, `otherArchiveReference` e `otherJsonPath` della prima voce, mantenendo gli indici sorgente anche quando precedono voti senza nome esclusi. Non stampa nomi/chiavi giocatore, club, nomi Blob, valori dei voti o payload.

`namesEqual`/`namesEqualIgnoringCase` confrontano i nomi originali senza stamparli; `votePayloadsEqual` confronta il JSON del voto completo, inclusi campi sconosciuti e flag; `playerPayloadsEqual` confronta tutta la voce e `playerPayloadsByteEqual` anche la rappresentazione testuale esatta del frammento. Questi confronti **non autorizzano deduplica, scelta del primo/ultimo voto, correzioni dei nomi o merge di identità**, nemmeno se tutto è identico. Prima va esaminata localmente la coppia precisa e concordata una politica sicura. I byte originali rimangono nel dump.

Per esaminare tutti i duplicati in una volta, e non uno per esecuzione, usa `.\scripts\local\scan-duplicate-votes.ps1 -DumpName <nome>` (sola lettura). Ripete la classificazione dei 6 d'ufficio dell'import e, per ogni duplicato ancora bloccante, mostra club, ruolo, voto, `hasVote`, `isFinal`, `isIn`, eventi, forma del 6 d'ufficio e rinvio nel calendario. Così si distinguono subito due omonimi di squadre diverse, un 6 d'ufficio non attestato e due voti reali discordanti. Vedere il [README degli script locali](../scripts/local/README.md).

Le baseline push mantengono i duplicati nominati e l'ordine per ordinale; i valori non finiti usano `invalid_push_baseline_vote` con la stessa diagnostica e il casing JSON effettivo. Nessuna nuova opzione env, scrittura Azure, reset o modifica delle risoluzioni utenti. L'import resta bloccato senza commit parziali. Rianalizza con `-UseExistingDump`, senza `-Apply`, e condividi soltanto la riga `Core import refused` con `Vote details`, non il dump o l'env.

#### Voto reale e 6 d'ufficio dello stesso giocatore

Dalla MappingVersion 37 il caso reale della stagione 10, giornata 26 (`p[33]` voto reale con `isIn:true`, `p[327]` 6 d'ufficio con `isIn:false`, stesso nome/ruolo/club) non è più un duplicato: il 6 d'ufficio viene conservato in `LegacyPoliticalVoteEntries` e la partita viene marcata come rinviata. Lo stesso vale per i 6 d'ufficio dei club la cui partita è attestata come rinviata dal calendario o da una voce accoppiata, avversario compreso. Controlla `legacyPoliticalVotes` nel report. Due voci reali per lo stesso giocatore restano bloccanti, comprese due copie del 6 prive di evidenza del rinvio.

Dalla MappingVersion 39, per decisione del gestore, il 6 d'ufficio dato a una voce **inattiva** (`a:false`) viene escluso da SQL senza creare giocatori o righe annuali, anche quando non ci sono prove del rinvio. È il caso reale della stagione 2024/25, giornata 14 (`p[139]` voto reale con assist, `p[335]` 6 d'ufficio senza club, ruolo 0 e inattivo, stesso nome): la vecchia app dava il 6 a tutta la squadra, inattivi compresi. Il report le conta in `skippedInactiveSubstitutes`. Il calendario di quella giornata segna come rinviate Fiorentina–Inter e Lecce–Juventus; per scelta del gestore il segnale di Lecce–Juventus resta quello del legacy, anche se la partita risulta giocata 1-1. Il voto politico effettivo viene calcolato dalle impostazioni della lega, non dal dump: vedere [partite rinviate](POSTPONED-MATCHES.md). La migration `PostponedMatchPoliciesAndLegacyVotes` va applicata al target prima dell'import.

### Se compare `invalid_club_code` o `missing_real_club`

Riguarda una squadra reale di calcio, non una squadra di fantacalcio, un account o i permessi Docker. Il codice naturale del club deve avere esattamente tre lettere ASCII. Un nome completo senza codice può usare soltanto il [catalogo di corrispondenze verificate della fonte](CLUB-CODES.md), mai le prime tre lettere del nome. Per esempio `Fiorentina` con `a:null` risolve `fio`; un nome sconosciuto resta bloccato. Il messaggio generico del wrapper PowerShell segnala solo l'uscita del tool: la causa da esaminare è `Core import refused`.

La diagnostica `Club details` indica:

- `Document SHA-256`, `container`, `archiveReference` e `jsonPath`: documento e posizione effettivamente rifiutati, per esempio `$.v.t[2]` nel catalogo club, `$.v.p[7].t` in una rosa oppure `$.Value.d[0].g[1].a` nel calendario;
- `reasons`: `missing_club_code`, `club_code_length`, `club_code_not_ascii_letters`, `club_code_name_mismatch`, `null_club` o `empty_club_name`;
- `nameMissing`, `nameLength`, `abbreviationMissing`, `abbreviationLength`: soltanto forma/lunghezza dei campi, mai nomi, codici, proprietari o payload.

Il contesto copre cataloghi, rose, voti/probabilità/statistiche, classifiche reali, calendario, mercato, albo, partite salvate, baseline push e aste incluse. Anche una copia con club invalido di un giocatore già noto riporta il proprio documento, non il primo catalogo in cui il giocatore era valido. Il casing storico delle baseline push viene mantenuto nella posizione JSON; i container esclusi non vengono decodificati per cercare club.

Rianalizza con `-UseExistingDump`, senza `-Apply`, e controlla `resolvedClubCodes`: contiene checksum, archivio, posizione e versione della tabella, non i valori. Il tool lavora offline e non richiede nuove opzioni env; la MappingVersion è 44. Il catalogo comprende le integrazioni storiche Catania, Siena, Livorno e Cesena, gli alias trovati nei dati legacy (`Juve`, `Hellas Verona`, `Como 1907`, `Venezia FC`), i nomi ufficiali dei club verificati sui loro siti (per esempio `AC Milan`, `SSC Napoli`, `Juventus FC`) e la variante di sigla `hel → ver`; con un nome verificato, un'etichetta che non è un codice di tre lettere (come `MONZ`) viene ignorata e dichiarata nel report. Le fonti sono documentate; `Spal` è già riconosciuta come `SPAL`. La copertura viene verificata su tutte le 62 coppie pubbliche nome/sigla dei cataloghi multi-anno, non un anno alla volta. Le sigle esplicite di tre lettere discordanti dal nome verificato, e quelle invalide accanto a un nome non verificato, non vengono sostituite. Se l'errore resta, condividi la riga `Core import refused` completa di checksum e `Club details`, non il dump o l'env. Queste coordinate permettono di esaminare localmente il solo record prima di scegliere una riconciliazione verificata. Non fare reset, non modificare gli originali e non aggiungere alias/sigle indovinate per far passare il controllo.

Per evitare di scoprire i problemi un errore alla volta, `.\scripts\local\scan-club-labels.ps1 -DumpName <nome>` elenca in sola lettura tutte le etichette che l'import rifiuterebbe, con le stesse regole e lo stesso catalogo del codice. Segnala anche, come "da verificare", i nomi non catalogati che portano la sigla di un club noto: l'import li unisce a quel club senza errore, quindi va confermato che siano la stessa squadra (per esempio `Como 1907/com`). Vedere il [README degli script locali](../scripts/local/README.md).

#### Nomi ufficiali e club annuali

La migration aggiuntiva `ClubSeasonsAndCanonicalNames` mantiene le chiavi testuali, allinea nome/abbreviazione dell'anagrafica globale e crea `ClubSeasons` con PK `(ClubCode, SeasonIndex)`. Nel report controlla anche `clubs`, `clubSeasons`, `clubCatalogueYears`. Le presenze annuali sono attestate solo da catalogo club, calendario reale o classifica reale; una rosa o un giocatore inattivo non le inventa. Il backfill SQL può recuperare calendario/classifica già presenti, non ricostruire il contenuto dei vecchi cataloghi da un solo marker. Non dichiara completi gli anni ricostruiti parzialmente.

Resta necessaria l'analisi riuscita e la revisione del report prima di `-Apply`, con backup verificato del target se contiene dati. La normalizzazione non cambia prezzi, voti o club salvati negli snapshot storici, né riscrive Azure/dump. Il downgrade con club/appartenenze presenti è bloccato; la finestra di reimport dopo scritture applicative non viene riaperta. Dettagli e letture API in [club e stagioni](CLUB-SEASONS.md).

#### Snapshot voti minimali con tutti i club null

Alcuni `live` storici hanno nomi/voti validi ma nessun oggetto club in tutti i record. Non sono voci di catalogo vuote e `isActive:false` non autorizza a perdere un `hasVote:true` con voto 6. L'import preserva voti `live`/`notification`/`official` e baseline push senza club con `ClubCode = NULL`, sempre collegati al giocatore annuale. La migration `OptionalHistoricalVoteClubs` rende nullable solo questi due campi snapshot e rifiuta un downgrade che richiederebbe di inventare club o scartare voti.

Una rosa **stagionale** dello stesso anno può completare il club annuale mancante se i riferimenti concordano su un solo codice, dichiarato con `recoveredRosterPlayers[].filledAnnualClub`. Il club storico assente dal voto resta invece null; più candidati, rose giornaliere e competizione iniziale non scelgono il club corrente per supposizione. Un club annuale già verificato non viene cancellato o disattivato dai flag del voto minimale.

Controlla `missingVoteClubSnapshots` (hash/anno/giornata/conteggio/prima posizione, non valori), `unresolvedAnnualPlayerClubs` e `ambiguousRosterClubRecoveries`. Il tool non richiede altre opzioni env o reset. Restano stretti nomi/ruoli, club non null ma invalidi e riferimenti di cataloghi/rose/probabilità/statistiche. Prima di Apply servono analisi riuscita e revisione del report; lo stato `fullMigrationComplete` resta falso. [Contratto dei voti legacy](LEGACY-VOTE-SNAPSHOTS.md).

#### Rose e albo senza club registrato

Le rose fino al 2021/22 e parte dell'albo salvavano i giocatori con `"t":{"n":""}`: 6.748 voci di rose stagionali, 132.193 di rose giornaliere e 508 dell'albo nel dump reale. Dalla MappingVersion 41, per scelta del gestore, il club resta NULL (migration `OptionalLegacyRosterClubs`), con prezzi e stati invariati; non viene mai preso dal catalogo annuale. Un club con nome o sigla resta validato come prima. L'app mostra questi giocatori con il club vuoto, come la vecchia app, e rifiuta uno scambio di mercato che li includa (`market_player_club_unknown`). Controlla `missingRosterClubSnapshots`. Dettagli nel [contratto dei voti e degli snapshot legacy](LEGACY-VOTE-SNAPSHOTS.md#rose-e-albo-legacy-senza-club).

#### Calendari reali con la stagione successiva e punteggi futuri

Il job legacy salvava il calendario del provider sotto la stagione "corrente" dell'app, che cambia il 10 agosto. A luglio e inizio agosto il provider pubblicava già la stagione successiva, che ha sovrascritto il calendario dell'anno precedente. Nel dump reale è successo due volte:
- il calendario 12 (2023/24) contiene le partite 2024/25, dal 18/08/2024 al 25/05/2025, con Como e Venezia;
- il calendario 14 (2025/26) contiene le partite 2026/27, dal 23/08/2026 al 30/05/2027, con 380 punteggi di riempimento.

I calendari veri del 2023/24 e del 2025/26 non sono nel dump.

Dalla MappingVersion 38, per scelta del gestore, un calendario reale le cui partite datate appartengono **tutte** a un'altra stagione (confine del 10 agosto) non entra in SQL:
- non genera partite, risultati, rinvii o appartenenze `ClubSeasons`, e non viene letto per cercare club;
- resta nel dump con i byte originali e viene dichiarato in `excludedRealCalendars` (checksum, archivio, stagione, giornate, partite, stagioni effettive, prima e ultima data) e in `preservedArtifacts`;
- per quelle stagioni non c'è un calendario reale in SQL. I risultati fantasy salvati non cambiano, e un ricalcolo di quelle giornate viene rifiutato invece di usare partite sbagliate.

Un calendario che mescola date di più stagioni viene rifiutato con `real_calendar_mixed_seasons` e il checksum del documento, senza import parziale.

Prima di queste regole l'import controlla che l'anno salvato dentro il calendario (`$.Value.y`) coincida con la chiave; altrimenti si ferma con `calendar_season_mismatch`, indicando checksum e campo. Dalla MappingVersion 43, per scelta del gestore, un anno interno 0 (mai salvato: nel dump reale i calendari dal 2023/24 al 2026/27) è accettato solo se **tutte** le giornate riportano la stagione della chiave; il calendario passa poi alle regole sopra (il 2023/24 e il 2025/26 restano esclusi perché contengono la stagione successiva). Questi calendari sono dichiarati in `realCalendarsWithoutYear` (checksum, archivio, stagione, giornate). Un anno interno diverso da 0 e dalla chiave, o una giornata con un altro anno, restano errori. Per vedere in una volta tutti i calendari con chiave, anno interno, date, stagioni effettive ed esito dell'import usa `.\scripts\local\scan-real-calendars.ps1 -DumpName <nome>` (sola lettura, senza club né punteggi). Dopo i calendari reali l'import legge mercati, competizioni fantacalcio, aste, albo, push, carte, news e consigli AI: `.\scripts\local\scan-competitions.ps1 -DumpName <nome>` elenca in una volta tutti i loro problemi strutturali con il codice d'errore che darebbe l'import.

Dalla MappingVersion 44, per scelta del gestore, i mercati (`marketwrapper`) salvati con i nomi lunghi del modello in uso dal 6 ottobre 2024 al 4 ottobre 2025 (`Markets`, `Buyer`, `Seller`, ... invece di `m`, `b`, `s`; nel dump reale 8 documenti delle stagioni 2024/25 e 2025/26) non vengono importati: quelle leghe e stagioni non hanno libro mercato né scambi in SQL. Il cambio di nomi non li aveva convertiti, e già la vecchia app non li leggeva più. Controlla `excludedLegacyMarkets` (checksum, archivio, stagione, numero di voci) e `preservedArtifacts`; i byte restano nel dump. Un mercato senza `m` né `Markets` resta `missing_market_trades`.

Un punteggio su una partita con orario successivo all'inizio dell'export (`StartedAtUtc` del manifest) non può essere un risultato. Le vecchie versioni del parser salvavano punteggi di riempimento, per esempio sul calendario corrente. Quelle partite vengono importate **senza risultato**, quindi l'app le mostra "da giocare" e non 0-0. Il risultato vero arriva dal job SQL del calendario. Il report le conta in `discardedFutureScores` (checksum, stagione, numero di partite, prima posizione JSON), senza nomi o valori.

### Classifica corrente e copie giornaliere legacy

Si salva una sola classifica per gruppo/competizione/anno, con `SnapshotDay=0` e `SerieADay` effettiva. `dailyrank` è escluso da SQL, dichiarato nel report e conservato nel dump con checksum verificati. La lettura `rank?day=N` è uno storico ricostruito (`isReconstructed:true`), non una copia persistita: eventuali correzioni di risultati/regole annuali si riflettono anche lì. Non ricalcola voti, formazioni o saldi; `plusMoney` è teorico e i saldi storici non ricostruibili (`money`/`valueAssets`) sono null.

La migration `CurrentOnlyFantasyStandings` impone il solo salvataggio corrente. Se trova già copie SQL giornaliere si ferma **senza cancellare nulla**: serve prima un backup verificato del target e la gestione esplicita di quelle copie, non un reset. La classifica corrente importata, i risultati e i checkpoint economici non vengono riscritti dall'upgrade. [Dettagli e limiti della ricostruzione](FANTASY-STANDINGS.md).

### Se compare `identity_collision`

Non significa necessariamente "due utenti duplicati": l'importatore usa questo blocco anche se una chiave email o il nome email del Blob contraddicono il profilo. La riga `Identity details` ora distingue:

- `blob_email_mismatch`: il nome Blob è un'email valida diversa da quella del profilo;
- `embedded_key_email_mismatch`: la chiave interna è un'email valida diversa da quella del profilo;
- `duplicate_profile_email`: due documenti hanno la stessa email canonica del profilo, anche se le chiavi tecniche sono diverse.

Per i duplicati indica checksum e riferimenti di archivio **hash** di entrambe le copie. I booleani `profilesIdentical`, `displayNameMatches`, `selectionMatches`, `groupRolesMatch` e `groupLabelsMatch` confrontano i dati senza mostrarli. I gruppi/ruoli sono confrontati indipendentemente dall'ordine; `profilesIdentical` confronta l'intero valore JSON, inclusi eventuali campi non modellati. I flag `currentBlobIsEmailLookup`/`otherBlobIsEmailLookup` indicano soltanto se il nome del Blob corrisponde all'email del profilo, non quale copia sia automaticamente autorevole.

**Per default le copie rimangono bloccate**, anche se identiche. Non usare un reset o cancellare una copia solo per far passare il controllo. Rilancia soltanto `analyze-core` sul dump esistente e condividi `Identity details`, non i valori originali dei documenti.

### Scelta esplicita e vincolata ai checksum

Dopo aver confrontato le copie, il gestore può approvare **una specifica coppia**, scegliendo il profilo del Blob il cui nome coincide con l'email, senza unire ruoli/selezioni e senza modificare la sorgente o il dump. Inserisci nel file env privato una sola riga, sostituendo i segnaposto con i due SHA-256 completi di 64 caratteri:

```dotenv
IDENTITY_RESOLUTIONS_JSON='[{"preferredDocumentSha256":"SHA256_COPIA_EMAIL_APPROVATA","otherDocumentSha256":"SHA256_ALTRA_COPIA_APPROVATA"}]'
```

Questa variabile è passata **solo ai tool di analisi/import**, non all'API o al frontend. Per CLI .NET diretta usa `FANTASOCCER_IDENTITY_RESOLUTIONS_JSON`. In caso di più coppie approvate, aggiungi oggetti all'array; uno stesso checksum non può comparire in più scelte. Non è una politica globale "preferisci sempre l'email".

La scelta è ammessa soltanto se i due documenti esistono, sono identificati univocamente dal contenuto, hanno la stessa email valida e sono **le sole due copie** di quell'email; la copia preferita deve essere un lookup email, l'altra un alias non-email. I metadati delle identità vengono validati prima della scelta e i normali controlli di membership/cache dei gruppi restano attivi. Una risoluzione non può scavalcare `membership_conflict`.

#### Due lookup email diversi solo per maiuscole

Se entrambe le copie hanno nome Blob email e differiscono soltanto per maiuscole/minuscole, la regola precedente resta rifiutata per default. Dopo confronto dei profili e approvazione del gestore, **solo l'oggetto di quella coppia** può aggiungere `"allowEmailLookupAlias":true`:

```dotenv
IDENTITY_RESOLUTIONS_JSON='[{"preferredDocumentSha256":"SHA256_COPIA_APPROVATA","otherDocumentSha256":"SHA256_ALIAS_NON_SCELTO","allowEmailLookupAlias":true}]'
```

I segnaposto vanno sostituiti con checksum completi di 64 caratteri. Questo flag richiede due lookup email della stessa email canonica e nomi Blob diversi **esclusivamente per case**; non permette di rimuovere spazi, risolvere altri ID o scegliere fra persone con email diverse. La copia viene mantenuta integralmente, non viene preferito automaticamente il profilo più ricco o il nome tutto minuscolo. Anche una copia vuota è conservata nel dump e non eliminata.

Se nell'array esistono altre risoluzioni approvate, **mantienile** e aggiungi il nuovo oggetto: il flag va solo sulla coppia di alias email, non sulle coppie email/ID tecnico. `resolvedIdentityConflicts` mostra `emailLookupAlias` per distinguere le due modalità; il mapping SQL registra la modalità con soli riferimenti hash. Tutti i vincoli su checksum, cardinalità, finestra d'import e membership rimangono invariati.

Se cambia un contenuto, compare una terza copia o mancano file, l'import si ferma (`identity_resolution_*`) e richiede una nuova revisione. Lo stesso vale per un checksum che identifica più Blob, perché non permetterebbe di scegliere in modo univoco. Nella modalità email/ID tecnico non è ammesso invertire la preferenza; fra alias email il checksum preferito identifica invece la copia scelta dal gestore e deve comunque superare i controlli di membership. Non usare la scelta per collegare email di persone diverse.

Prima esegui l'analisi sul dump esistente con lo stesso env privato e controlla `resolvedIdentityConflicts`, che indica entrambi i checksum/riferimenti hash; la copia non selezionata è dichiarata anche in `preservedArtifacts`. Solo dopo usa `-Apply`. Conserva la configurazione approvata per le reimportazioni di quel dump: rimuoverla ripristina il blocco sui duplicati. Restano validi idempotenza, transazione e rifiuto delle reimportazioni dopo scritture applicative.

### Se un proprietario storico non ha un'email

Le vecchie stagioni possono identificare alcuni proprietari con nomi/etichette invece che email. Dopo aver confermato questa forma del dataset, puoi aggiungere al tuo env privato (per esempio `.env.local`):

```dotenv
IMPORT_LEGACY_NAMED_OWNERS=true
```

Non sostituire `IDENTITY_RESOLUTIONS_JSON`: sono opzioni indipendenti e le scelte sulle copie d'identità rimangono necessarie. Il flag è passato soltanto al tool di migrazione e resta `false` per default. Con CLI diretta usa `FANTASOCCER_IMPORT_LEGACY_NAMED_OWNERS`.

Ogni riferimento esatto non-email viene conservato come **utente esistente**, senza inventare un indirizzo, con appartenenza al gruppo inizialmente disattiva. Lo stesso valore in un altro gruppo non viene collegato alla stessa persona; differenze di case/spazi non vengono normalizzate come fossero email. I collegamenti a squadre, rose, classifiche, calendari e albo rimangono tramite gli ID stabili, non attraverso un confronto del nome visualizzato. Valori vuoti, troppo lunghi, caratteri di controllo e indirizzi malformati contenenti `@` rimangono errori; nessuna regola sui profili di login o sulle membership attive viene allentata.

Rianalizza con `-UseExistingDump`, senza reset o nuovi download, e controlla `historicalNamedUsers` e gli avvisi. Solo dopo un'analisi riuscita e la revisione del report usa `-Apply`. Le migration SQL sono additive per questo caso: l'email può essere assente, le email note restano univoche; nessun utente esistente viene cancellato. Il downgrade viene rifiutato finché esistono utenti senza email, anziché inventare indirizzi o perdere lo storico.

Dopo aver completato l'import, in **Membri del gruppo** l'amministratore vede la colonna **Stato nel gruppo**, anche per i disattivi. Può riattivare/disattivare l'appartenenza e configurare l'email reale ancora mancante da **Modifica utente**. La riattivazione rapida assegna soltanto Partecipante; privilegi maggiori richiedono scelta e autorizzazione esplicite. Disattivare un gruppo non disabilita l'account globale o le altre appartenenze. Se l'email appartiene già a un altro account, l'operazione si ferma: non avviene un merge implicito. Queste sono scritture applicative: eseguirle solo dopo aver chiuso la migrazione, perché impediscono le reimportazioni successive.

### Se compare `unresolved_historical_team_identity`

Un documento cita un proprietario che, nel gruppo, non ha esattamente una squadra annuale principale dove l'import la cerca: per le rose nel basket e nell'anno della chiave (per le rose d'apertura l'anno precedente), per mercato, calendari, classifiche e aste fra i basket della lega in quell'anno, per le classifiche d'apertura fra i basket della lega nell'anno precedente. L'import non sceglie una squadra in base al nome, ai co-proprietari o ad altri anni. L'errore indica contenitore, percorso JSON, riga del codice e SHA-256 del documento, mai il proprietario.

Per vedere tutti i casi in una volta usa `.\scripts\local\scan-team-identities.ps1 -DumpName <nome>` (sola lettura): per ogni riferimento non risolto mostra il motivo, gli anni in cui il proprietario ha una squadra e il numero di giocatori della rosa, con pseudonimi al posto di email ed etichette. Vedere il [README degli script locali](../scripts/local/README.md).

Dalla MappingVersion 42, per scelta del gestore, una rosa salvata in un basket dove il proprietario non ha la squadra quell'anno viene esclusa da SQL in due soli casi: se è **identica in ogni campo** all'unica rosa dello stesso tipo, giornata e lega salvata per lo stesso proprietario nel basket della sua unica squadra di quell'anno (nel dump reale: 82 rose stagionali 2014/15–2022/23 di un gruppo); oppure se il proprietario **non ha nessuna squadra** nel gruppo quell'anno e la rosa è **vuota**, cioè senza giocatori, soldi, premio d'apertura né cambi di formazione (nel dump reale: una rosa 2025/26). Controlla `skippedRosterDocuments`: per ogni rosa esclusa riporta SHA-256, archivio, stagione, giornata e motivo (`identical_copy_in_team_basket` con la rosa tenuta, oppure `empty_roster_without_team`). I byte restano nel dump. Copie diverse, rose orfane non vuote e proprietari con più squadre nello stesso anno restano errori; la scansione conta le rose escluse a parte e mostra solo quelle che bloccano.

## 3. Importare esplicitamente la copia locale

Prima leggi il report: `pendingContainers` sono dati sconosciuti/non coperti; `pendingFields` richiedono riconciliazione; `excludedContainers` sono esclusioni deliberate (aste/liste con `IMPORT_LEGACY_AUCTIONS=false`, `table:logs` con `IMPORT_LEGACY_LOGS=false`); `preservedArtifacts` sono file/archivi preservati senza inventare dati SQL. `fullMigrationComplete=false` non è un errore del comando, ma **non autorizza il cutover**.

L'esclusione delle aste non tocca squadre, rose, prezzi già salvati, crediti, risultati o classifiche. Non elimina i Blob sorgente: i byte restano nel dump completo verificato come archivio, ma non sono caricati nelle tabelle SQL delle aste/ordini. Il default del tool rimane `true` per evitare di perdere dati senza una scelta esplicita.

### Log storici e logging attuale sono separati

I log della Table `logs` sono **esclusi da PostgreSQL per default** (`IMPORT_LEGACY_LOGS=false`). Restano nel dump solo se erano già presenti o vengono acquisiti con `-IncludeTables`; nessuna riga sorgente viene cancellata. Solo se necessario si può riattivare l'import SQL con `IMPORT_LEGACY_LOGS=true` (CLI diretta: `FANTASOCCER_IMPORT_LEGACY_LOGS=true`) su un dump che includa le tabelle. Le altre tabelle acquisite ma sconosciute restano segnalate come pendenti, non vengono ignorate.

Questa scelta non disabilita la telemetria corrente OpenTelemetry/Aspire né elimina ricevute, lease, outbox o audit SQL dell'app. La revisione futura dovrà distinguere log diagnostici da mantenere solo nella piattaforma di logging e dati operativi necessari per coordinamento/idempotenza. Non viene cambiata oggi la retention o la semantica dei job. Anche per cambiare opzione resta valido il blocco di reimport dopo scritture applicative.

Ferma anche eventuali processi `dotnet run`/debug avviati sull'host, poi:

```powershell
.\scripts\local\sync-storage.ps1 -EnvFile .env -DumpName azure-prova1 -UseExistingDump -Apply
```

Con `-Apply` lo script ferma API/frontend Compose, applica lo schema e importa SQL in transazione, quindi ripristina i Blob in Azurite (le Table solo con `-IncludeTables`, anche per un vecchio dump che le contenga). SQL viene importato **prima** del restore file: un DB che rifiuta la reimportazione non causa sovrascritture dei file. Un errore successivo del restore lascia l'app ferma: correggi e ripeti sullo stesso dump. SQL/file non sono una singola transazione distribuita.

**Nessun reset automatico, modifica di Azure o riavvio dell'app.** Dopo aver verificato i report, avvia deliberatamente con `local.ps1 up-https -EnvFile .env` (oppure `up` senza TLS).

## 4. Perché una nuova sincronizzazione può essere rifiutata

È una migrazione **snapshot sorgente → target**, non una replica continua/bidirezionale. Finché il target non riceve scritture applicative, un nuovo export/import riconcilia i dati della stessa sorgente, anche figli rimossi; uno snapshot invariato viene saltato.

La prima scrittura dell'app — collegamento Microsoft, gruppi/codici, OAuth MCP, job, ecc. — chiude la finestra di reimportazione. Non fondiamo automaticamente gruppi nuovi locali e dump legacy né scavalchiamo il controllo. Usa un laboratorio pulito separato o il reset esplicito, sapendo che perderai lo stato locale. Il restore Azurite non elimina file estranei al dump: per una replica esatta serve un emulatore dedicato pulito; il reset PostgreSQL non pulisce Azurite.

### Dump finale consistente

Una copia acquisita mentre la sorgente riceve scritture è **esplorativa**. Ferma prima **tutti gli scrittori sulla sorgente**, incluse API legacy, job, aste e altre istanze. Poi acquisisci un nuovo dump:

```powershell
.\scripts\local\sync-storage.ps1 -EnvFile .env -DumpName azure-finale1 -SourceQuiesced
# Dopo review del report, sul target pulito:
.\scripts\local\sync-storage.ps1 -EnvFile .env -DumpName azure-finale1 -UseExistingDump -Apply
```

`-SourceQuiesced` registra la tua dichiarazione nel manifest: **non ferma Azure e non verifica da solo la quiescenza**. Va specificato di nuovo quando riprendi un export, non è ereditato automaticamente.

## 5. Reset di TUTTI i dati PostgreSQL locali (distruttivo)

Solo per il PostgreSQL dedicato del Compose locale, **non per DB remoti o produzione**. Richiede un Docker context con socket locale/named pipe; TCP/SSH sono rifiutati. Non accetta connection string SQL arbitrarie.

Prima mostra il piano: nessuna operazione Docker/cancellazione.

```powershell
.\scripts\local\reset-postgres.ps1 -EnvFile .env -ConfirmDatabase fantasoccer_local -WhatIf
```

**Solo quando vuoi davvero perdere tutti i dati applicativi locali:**

```powershell
.\scripts\local\reset-postgres.ps1 -EnvFile .env -ConfirmDatabase fantasoccer_local
```

Lo script:

1. ferma API/frontend Compose e rifiuta tool di migrazione/ripristino attivi; ferma tu anche il debugger/API sull'host;
2. crea un backup custom `pg_dump` in `migration-data/postgres-backups/before-reset-….dump` e ne controlla l'indice; se fallisce, **non esegue il reset**;
3. svuota le tabelle applicative in transazione con advisory lock: **account, gruppi, membership, gioco, codici, OAuth, outbox e checkpoint import**;
4. conserva schema e `__EFMigrationsHistory`: niente downgrade o `down --volumes`;
5. lascia l'app ferma e conserva Azurite, dump sorgente, backup, telemetria, certificati e key ring.

Il backup contiene dati sensibili: proteggilo con ACL/cifratura. La verifica dell'indice non sostituisce un test completo di recovery. Un errore SQL annulla il reset e conserva il backup. Dopo il reset importa **prima** di riaprire login/MCP. Per ripartire senza legacy, avvia e fai nuovamente login: gli account saranno nuovi. Il reset non revoca credenziali presso Microsoft o altri provider.

## Bash / WSL e test

```bash
DUMP_NAME=azure-prova1 bash scripts/local/sync-storage.sh .env
DUMP_NAME=azure-prova1 RESUME=true INCLUDE_TABLES=true bash scripts/local/sync-storage.sh .env
DUMP_NAME=azure-prova1 USE_EXISTING_DUMP=true APPLY=true bash scripts/local/sync-storage.sh .env
CONFIRM_DATABASE=fantasoccer_local WHAT_IF=true bash scripts/local/reset-postgres.sh .env
```

Per il reset Bash effettivo ometti `WHAT_IF=true`, solo dopo averne verificato il piano.

Su Linux/WSL/CI, `python3 scripts/local/test-maintenance.py` usa Docker simulato (test Bash e PowerShell, se `pwsh` è installato). `--docker` aggiunge una fixture PostgreSQL temporanea **senza porte o volumi dell'app**: non resetta mai lo stack applicativo. Nessun reset dei tuoi dati reali viene effettuato per sviluppare/verificare gli script.
