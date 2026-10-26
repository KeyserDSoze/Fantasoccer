# ADR 0004 — Docker Desktop, file env e osservabilità locale

- Data: 2026-10-25
- Stato: decisione concordata; primo stack implementato

## Decisione

- Un `compose.yml` nella root avvia frontend web, API, PostgreSQL, Azurite e Aspire Dashboard usando Linux containers, anche da Docker Desktop su Windows/WSL2.
- Build e tool in container: non richiedere SDK .NET, Node o Azure CLI sull'host per avvio, export, ripristino e migration dello schema.
- Comandi raccolti in `scripts/local/`, con PowerShell per Windows e Bash per gli altri ambienti.
- `.env.test` è versionato e contiene esclusivamente dati sintetici locali; `.env` e le altre varianti private sono escluse da Git e build Docker.
- Generare un `.env` privato senza sovrascrivere quelli esistenti. Ogni servizio riceve solo le variabili necessarie, non l'intero file.
- La credenziale di export Azure è assegnata solo al tool di export. Il backend locale punta esplicitamente ad Azurite, non alla sorgente reale.
- Log, tracce e metriche usano OpenTelemetry e Aspire Dashboard; Application Insights rimane un provider alternativo, non una dipendenza necessaria all'avvio.
- Rimuovere query string e SQL dagli span; non raccogliere automaticamente body/header sensibili. Considerare sensibile anche la telemetria di business legacy.
- Porte pubblicate solo in loopback, dashboard con token da env e OTLP non pubblicato. Nessun bypass di autenticazione dell'app.
- Volumi persistenti distinti per PostgreSQL, Azurite, sessioni locali e telemetria. La pulizia distruttiva è sempre esplicita.
- I test automatici usano un PostgreSQL e un Azurite separati dal dataset dell'app: non devono leggere/copiarne o eliminarne i dati.
- Job, warm-up, fetch dati esterni e push sono disabilitati di default quando si lavora con copie locali.

## Limiti dichiarati

- Il database applicativo riceve lo schema EF all'avvio. In modalità PostgreSQL gli adattatori Blob e gli endpoint automatici non sono esposti; le funzioni non ancora portate sono indisponibili, non servite da una seconda fonte.
- `migrate` non equivale all'importazione NoSQL -> PostgreSQL, ancora da implementare.
- Il login Microsoft è reale e richiede configurazione/account SQL; gli inviti SQL sono supportati. Un seed demo completo del gioco non è ancora disponibile.
- MCP richiede HTTPS: è disponibile `compose.https.yml` e un generatore certificati in container, senza indebolire i controlli di produzione o modificare automaticamente la fiducia del PC.
- Aspire standalone è diagnostica di sviluppo, non sostituisce retention/monitoraggio produttivi Azure.
- I file env sono testo e i segreti dei container sono visibili agli utenti autorizzati a Docker: proteggere ACL e macchina. Non scambiare questa soluzione con un secret vault.
- Il dump Blob/Table non è un backup completo di tutti i servizi/configurazioni Azure; l'ambito è documentato nell'ADR 0003 e nel tool.

Istruzioni, variabili, percorsi e comandi: [scripts/local/README.md](../scripts/local/README.md).
