# Migrazione PostgreSQL, EF Core e contratti applicativi

Questa cartella raccoglie le decisioni e il piano di migrazione concordati il 25 ottobre 2026.
I documenti descrivono implementazione, decisioni e condizioni ancora necessarie per l'accettazione produttiva.
Sono disponibili schema EF Core, migration e test PostgreSQL/Docker: vedere [laboratorio locale](../ops/migration/README.md).
È disponibile anche l'[export Blob/Table con verifica e ripristino locale](../tools/Fantasoccer.StorageMigration/README.md), non un backup completo di tutti i servizi Azure.
L'[import relazionale](../tools/Fantasoccer.DataMigration/README.md) copre i domini censiti, inclusi mercato/aste, albo/carte, news/suggerimenti, notifiche/outbox, log e OAuth, con conservazione esplicita dei file/archivi.
Il runtime SQL comprende servizi applicativi e orchestrazione job; non espone percorsi dati Blob alternativi. Il cutover resta subordinato alla riconciliazione del dump reale, alle chiavi/configurazioni originali e all'accettazione UI/login.
SQL è il default dell'applicazione; connection string e amministratori globali vanno configurati esplicitamente. Il vecchio backend resta selezionato esplicitamente solo per caratterizzazione/regressione, non come ripiego automatico se manca PostgreSQL.
Il [laboratorio applicativo Docker Desktop](../scripts/local/README.md) include frontend, API PostgreSQL con schema applicato allo startup, Azurite per i file e telemetria locale OpenTelemetry/Aspire.
La scrittura di questi ADR non autorizza download di dati reali, modifiche Azure o il passaggio in produzione.

Branch dedicato al lavoro: `refactor/postgresql-multi-group`.

## Obiettivo del progetto

La migrazione serve a rendere Fantasoccer pronto ad accogliere altri amici e più gruppi nella stessa applicazione.
Gli utenti possono appartenere a più gruppi con ruoli differenti; i dati calcistici comuni sono condivisi, mentre dati di gioco e amministrazione restano isolati nel rispettivo gruppo.
L'obiettivo non è costruire una piattaforma enterprise: usare un database condiviso con relazioni e confini espliciti, senza istanze o database separati per ciascun gruppo.

## Documenti

1. [0001 — Architettura, API, DTO e autorizzazioni](0001-architettura-api-dto.md)
2. [0002 — Modello relazionale e proprietà dei dati](0002-modello-relazionale.md)
3. [0003 — Esportazione, migrazione e passaggio produttivo](0003-migrazione-dati.md)
4. [0004 — Ambiente locale e osservabilità](0004-ambiente-locale-e-osservabilita.md)
5. [Piano di azione e verifiche](PIANO-AZIONE.md)
6. [Inventario e copertura effettiva](INVENTARIO-MIGRAZIONE.md)
7. [0006 — Calcolo SQL e storico delle competizioni](0006-calcolo-sql-e-storico-competizioni.md)
8. [0007 — Mercato atomico e consensi](0007-mercato-atomico-e-consensi.md)
9. [0008 — Aste SQL e checkpoint crediti](0008-aste-sql-e-checkpoint-crediti.md)
10. [0009 — Albo, carte e notifiche SQL](0009-albo-carte-e-notifiche-sql.md)
11. [0010 — Ingestione SQL con sorgenti separate](0010-ingestione-sql-con-sorgenti-separate.md)
12. [0011 — Chiusura operativa, job/editoriale/archivi](0011-chiusura-operativa-job-editoriale-e-archivi.md)
13. [Matrice dei contratti e delle policy](MATRICE-CONTRATTI-E-POLICY.md)

## Decisioni confermate

- PostgreSQL per i dati strutturati; EF Core usato direttamente, senza repository generico sostitutivo.
- Servizi applicativi condivisi da controller, MCP, SignalR e job, senza duplicare la logica.
- Sostituzione di tutte le chiamate frontend alle vecchie API; non è richiesta compatibilità permanente con RepositoryFramework.
- DTO con proprietà e JSON leggibili; eliminazione dei mapper frontend che traducono nomi compatti.
- Modello relazionale normalizzato: distinguere dati globali, stagionali, di gruppo e di competizione.
- Chiavi primarie e straniere composite dove rappresentano correttamente l'identità della relazione; niente chiavi legacy concatenate nel nuovo modello.
- Accesso multi-gruppo verificato per API, MCP, SignalR, job e file specifici di gruppo.
- Migration dello schema allo startup API; importazione dei dati separata, esplicita e idempotente.
- Finestra di manutenzione per il passaggio produttivo; niente dual-write.
- Immagini e altri file rimangono su Azure Blob Storage.
- Script per scaricare lo Storage attuale e provare la migrazione localmente.
- Ambiente Docker con Azurite e PostgreSQL e test dell'importatore stesso.
- MCP deve restare semplice: riusare autenticazione e configurazione quando ragionevole; non è obbligatorio preservare ogni contratto o collegamento esistente.

## Principi di proporzionalità

È un progetto fra amici: niente event sourcing, framework CRUD proprietari, bus distribuiti o architetture complesse senza necessità dimostrata.
Semplicità non significa rinunciare a integrità dei dati, autorizzazioni, backup e verifiche della migrazione.
Le scelte ancora da validare sui dati reali sono indicate come tali; non vanno trasformate in assunzioni silenziose nell'importatore.
