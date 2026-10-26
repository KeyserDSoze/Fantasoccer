# ADR 0003 — Export Storage, importazione idempotente e manutenzione

- Data: 2026-10-25
- Stato: strategia concordata; comandi e script sotto indicati sono deliverable da implementare

## Decisione

Separare quattro operazioni: export della sorgente, caricamento locale della copia legacy, importazione relazionale e riconciliazione.
La migrazione produttiva avviene in manutenzione senza dual-write. Azure resta per i file, non per i documenti applicativi.

## 1. Script di esportazione dello Storage attuale

Predisporre uno script documentato, indicativamente `scripts/migration/export-storage`, con queste proprietà:

- Legge l'account sorgente senza modificarlo; nessuna creazione/eliminazione di container, modifica ACL o scrittura in produzione.
- Usa autenticazione Azure/identità con permessi minimi ove possibile; eventuali SAS/connection string sono input segreti, mai nel repository, negli argomenti stampati o nei manifest.
- Esporta tutti i container Blob e le tabelle Azure presenti, non solo la lista nota dei repository. Include immagini/file per poter ottenere una copia completa.
- Inventaria anche eventuali Queue/File Share: se presenti, segnala un percorso di export dedicato o blocca la dichiarazione di export completo. Non promettere che Azurite emuli tutti i servizi Azure.
- Modalità completa per backup/test e modalità selettiva esplicita per iterazioni veloci; gli elementi esclusi sono elencati nel report.
- Conserva nomi e percorsi originali, byte dei payload e metadati rilevanti. Per le Table conserva tipi, PartitionKey e RowKey; ETag/timestamp sono metadati di provenienza, non necessariamente ripristinabili identici.
- Listing paginato, retry limitati, ripresa del download, scrittura atomica dei file e checksum di verifica.
- Manifest versionato con sorgente non segreta, periodo di acquisizione, conteggi, dimensioni, checksum, metadati e lista degli errori/esclusioni.
- Gestisce esplicitamente eventuali versioni/snapshot/soft-delete: la copia dello stato corrente non equivale al backup della cronologia Azure. Indica quale ambito è stato acquisito.
- Un errore o un servizio non esportato impedisce di presentare la copia come completa.

Un export mentre l'app scrive è una copia esplorativa, **non uno snapshot consistente**. Per la prova conclusiva e il passaggio finale fermare tutti gli scrittori, inclusi job, hub/aste ed eventuali processi esterni.
Non affidarsi al solo blocco delle pagine frontend.

### Protezione della copia

Il dump può contenere email, profili, credenziali push, concessioni OAuth e dati di sicurezza.
Destinazione esterna alle directory pubblicate, permessi restrittivi, cifratura/retention concordate, esclusione da Git, Docker build context e artefatti CI.
Niente caricamento automatico dei dump su servizi esterni. Per CI e sviluppo ordinario usare fixture sintetiche o una copia anonimizzata in modo coerente fra tutte le relazioni.
Una copia anonimizzata non sostituisce l'originale protetto per la prova finale di integrità.

## 2. Ripristino locale e ambiente Docker

Preparare un Compose dedicato ai test, con versioni fissate, health check e porte esposte solo in loopback:

- PostgreSQL con database/credenziali esclusivamente locali e volumi di test.
- Azurite per Blob/Table e, se necessario, Queue.
- Tool di caricamento del dump in Azurite preservando il formato/envelope legacy.
- Importatore e test runner configurabili senza dipendere da servizi di produzione.

La connessione al DB locale e ad Azurite deve essere esplicita. Gli script di test rifiutano endpoint di produzione per default e non ereditano automaticamente segreti dell'hosting.
Non inviare notifiche, chiamate OAuth o aggiornamenti esterni usando i dati copiati; disabilitare job/integrazioni reali nel profilo di migrazione.
La pulizia può eliminare soltanto volumi e risorse locali dedicate, con comando esplicito; non cancellare dump o volumi generici.

Verificare che il percorso `Azure export -> Azurite -> lettore legacy -> PostgreSQL` usi gli stessi contratti della sorgente reale.
Gli ETag ricreati da Azurite possono cambiare: l'identità dell'importazione deriva dalle chiavi e dai checksum del manifest, non dagli ETag assegnati al ripristino.
Azurite non certifica identità gestite, networking, ACL e tutte le caratteristiche del servizio Azure: serve anche una prova su risorse Azure isolate.

## 3. Importatore idempotente

Tool dedicato, indicativamente `tools/Fantasoccer.DataMigration`, indipendente dallo startup API.

### Contratto operativo

- Comandi distinti per analisi/validazione, importazione, verifica e report; analisi senza scritture nel target.
- Sorgente configurabile: dump verificato oppure Azure/Azurite tramite lettore legacy. Nessuna modifica alla sorgente.
- Target protetto da identificazione esplicita dell'ambiente; nessuna importazione produttiva per semplice uso dei default.
- Schema creato dalle migration EF, non da un secondo insieme di DDL divergenti.
- Report con versione del tool, versione del mapping, manifest, migration/schema target e stato di ogni fase.

### Idempotenza

- Registro di mapping con identità stabile composta da sorgente logica, container/tabella, factory e chiave legacy. Non basta la chiave del documento senza tipo/factory.
- Identità della sorgente originale preservata anche quando il dump viene ripristinato in Azurite.
- Mapping separato per documenti che producono più righe, con chiavi deterministiche anche per i figli.
- Vincoli univoci di business nel DB oltre al registro tecnico.
- Checksum/versione della trasformazione: record già importati e invariati vengono saltati; modifiche gestite con regole esplicite.
- Transazione per batch/aggregato coerente: dati e checkpoint si salvano insieme. Riprendere dopo un crash non deve lasciare figli duplicati o aggregati parziali.
- Riconciliare anche i figli rimossi da un documento aggiornato; non limitarsi ad aggiungere righe. Cancellazioni di interi documenti gestite solo da un confronto con manifest completo e con modalità esplicita.
- Non usare il nome del file del dump o l'ordine di esecuzione come identità.
- Non sovrascrivere dati già modificati dall'applicazione dopo il passaggio produttivo: le riesecuzioni sono consentite solo sul target di migrazione in manutenzione, con guardia che blocchi l'import dopo l'apertura alle scritture.
- Retry solo per errori transitori; collisioni, riferimenti mancanti e ambiguità producono errori strutturati e richiedono risoluzione.
- Nessuna fusione arbitraria di account/giocatori o incremento implicito dei privilegi.

L'importatore non genera eventi applicativi, notifiche o email e non richiama i normali comandi utente con effetti collaterali.

### Ordine indicativo

1. Validazione formati/envelope legacy, manifest, duplicati e chiavi.
2. Identità, stagioni, club e giocatori reali condivisi.
3. Gruppi, membership, basket, leghe, configurazioni e partecipazioni annuali.
4. Squadre annuali, proprietà/comproprietà, rose e movimenti economici storici.
5. Calendari, voti, formazioni giornaliere, risultati e classifiche.
6. Mercato, aste, competizione iniziale, carte, news, statistiche e altri dati inventariati.
7. Preferenze, notifiche e OAuth secondo la strategia deliberata.
8. Verifica finale, report e approvazione del passaggio.

## 4. Riconciliazione

Non confrontare soltanto il numero di documenti con il numero di righe: una trasformazione relazionale cambia le cardinalità.
Verificare conteggi attesi per categoria, mapping completi, relazioni, duplicati, orfani e invarianti:

- utenti e membership, senza privilegi acquisiti dalla fusione di copie;
- giocatori stagionali unici e riutilizzati fra gruppi/leghe;
- squadre condivise correttamente, comproprietà e storico dei proprietari;
- rose attive/vendute, prezzi, premi e bilanci equivalenti ai calcoli legacy;
- giornate Serie A, turni fantasy e stagioni senza conversioni ambigue;
- formazioni storiche e risultati preservati, classifiche riconciliate;
- riferimenti ai blob validi, senza spostare inutilmente i file;
- ogni categoria inventariata importata o esclusa tramite decisione documentata.

Errore bloccante se rimangono dati mancanti o discrepanze non approvate. Il report non stampa token, segreti o payload personali completi.

## 5. Passaggio produttivo

1. Predisporre PostgreSQL Azure con accesso di rete, TLS, identità/credenziali, backup/PITR e prova di ripristino. Dimensionare dopo aver misurato i dati.
2. Provare export e import completo in ambiente isolato; misurare durata e concordare la manutenzione.
3. Fermare tutti gli scrittori legacy; acquisire e verificare l'export finale consistente.
4. Applicare lo schema e importare sul target di migrazione non aperto al traffico applicativo.
5. Riconciliare e provare API/frontend/MCP con job ed effetti esterni disabilitati.
6. Distribuire backend e frontend coordinati. Lo startup API verifica/applica lo schema prima della disponibilità.
7. Eseguire smoke test, abilitare job e aprire alle scritture; registrare la chiusura della fase d'importazione.
8. Conservare sorgente/dump protetti e backup PostgreSQL per il periodo concordato; dismettere i documenti legacy solo dopo approvazione.

Prima delle nuove scritture è possibile tornare ai binari/dati legacy. Dopo nuove scritture PostgreSQL il rollback richiede riconciliazione inversa o recupero delle modifiche: non basta cambiare una connection string.
Conservare i file su Blob; rimuovere dalla futura identità applicativa i permessi legacy non più necessari senza compromettere immagini, loghi o chiavi ancora in uso.
