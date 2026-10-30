# MCP Fantasoccer — backend PostgreSQL

L'applicazione espone MCP su `/mcp`, usando gli stessi servizi applicativi e gli stessi controlli di appartenenza, proprietà e ruolo delle API HTTP. Non esiste più un backend runtime RepositoryFramework/Blob. Le descrizioni dei vecchi endpoint `api/Group/Get`, `api/Team/day/Get`, `Settings/*` e `McpAuction/*` non sono valide.

## Collegamento e autenticazione

- `Mcp:Enabled` attiva endpoint OAuth e MCP; richiede issuer HTTPS, origine frontend consentita e client configurati o registrati dinamicamente.
- Discovery: `/.well-known/oauth-protected-resource/mcp`, `/.well-known/oauth-authorization-server`, `/.well-known/openid-configuration`.
- Endpoint OAuth: `/connect/authorize`, `/connect/token`, `/connect/revoke`, `/connect/register`; il frontend gestisce consenso e connessioni tramite `/connect/approve` e `/connect/disconnect`.
- Authorization Code con PKCE S256, codici monouso, refresh con rotazione e revoca delle connessioni. Access token MCP 24 ore; refresh OAuth MCP 30 giorni.
- Il bearer dell'applicazione non è un token MCP. Un token MCP non è utilizzabile sulle API HTTP ordinarie né sull'hub aste.
- L'autenticazione Microsoft del frontend è una nostra implementazione: verifier casuale per ogni login, `state` monouso e verifier obbligatorio nello scambio. Nessuna libreria Rystem Social. Il refresh applicativo resta di 365 giorni e nel formato di richiesta esistente (distinto dal refresh OAuth MCP).

## Navigazione degli strumenti

1. `get_my_account`: account, gruppi, selezioni salvate e stagione corrente. Le selezioni non concedono permessi.
2. `get_current_season`: `year` è un indice di stagione (15 = 2026/27), non l'anno solare.
3. `list_my_relational_groups`, `get_relational_group`: gruppi autorizzati e relativi identificatori.
4. `get_fantasoccer_guide(topic='lookups')`: percorsi verso rose, calendario, voti, partite e classifiche.
5. `list_api_operations`: catalogo paginato degli endpoint applicativi con schema di input e modelli di risposta. Non include login, storage OAuth o amministrazione globale della piattaforma.
6. `describe_api_model`: significato e schema JSON dei DTO esposti. Nessuna chiave composta Blob da costruire.
7. `execute_api_operation`: richiama l'operazione scoperta con `route`, `query`, `body` ed eventualmente `files`. La chiamata esegue il vero endpoint in processo, senza URL scelti dal chiamante e senza saltare i controlli HTTP.

Gruppi, leghe, basket, squadre e partite hanno ID UUID. I giocatori hanno chiavi naturali normalizzate: usare quelle restituite dall'API, senza inventarle dai nomi visualizzati.

## Scritture

Le operazioni di modifica richiedono un'istruzione esplicita dell'utente; non ritentarle automaticamente quando l'esito è incerto. I GET del backend relazionale non modificano il gioco. Generazione calendari, ricalcoli e preparazione della competizione iniziale sono comandi POST espliciti.

Per una formazione inviare solo squadra, posizioni, versioni e flag previsti dal DTO; mai prezzi, crediti o l'intero documento Team. I risultati salvati restano autorevoli; le proiezioni Live non salvano risultati o classifiche durante una lettura. La copia automatica tra competizioni è best effort prepartita, non un vincolo di identità permanente.

## Persistenza e configurazione

- Documenti OAuth su PostgreSQL con compare-and-swap e retention; nessun repository Rystem.
- Chiavi Data Protection persistenti sul container privato `mcp-keys` oppure su percorso condiviso configurato; mai entrambi.
- In produzione usare certificato persistente (file o base64, non entrambi) e chiavi Data Protection persistenti. Le configurazioni incomplete falliscono all'avvio.
- `Mcp:PublishAllTools=false` limita l'elenco iniziale agli strumenti di navigazione; il catalogo e l'esecuzione delle operazioni rimangono disponibili.
- Rate limit e controlli Origin restano attivi; il facade non accetta credenziali o header di impersonazione e non segue redirect.

Vedere [ADR 0013](../ADR/0013-rimozione-backend-legacy.md) per la rimozione del backend precedente e `src/Fantasoccer.RelationalApi.Tests/McpOAuthApiTests.cs` per i test end-to-end.
