# Fantasoccer 3.0.0 — 24 ottobre 2026

## Novità

- Server MCP sul backend esistente: `https://apifc.azurewebsites.net/mcp`.
- OAuth Authorization Code con PKCE S256, login Microsoft esistente, consenso esplicito, refresh e revoca.
- Registrazione dinamica RFC 7591: gli utenti scelgono un client compatibile senza configurazioni GitHub per applicazione.
- Gestione e revoca dei collegamenti nelle impostazioni del sito.
- Persistenza OAuth nello storage Blob/Rystem esistente, senza nuovi database o hosting.

## Cambiamento intenzionale della major

Le scritture generiche delle rose richiedono un amministratore del gruppo. Proprietari e comproprietari usano i flussi formazione, mercato e aste; non possono aggirare questi flussi riscrivendo giocatori, prezzi o crediti via repository/API/MCP. Anche i documenti che determinano membership e ruoli sono protetti sulle API legacy, indipendentemente dall'abilitazione MCP.

Il formato dei bearer frontend, il login Microsoft e le route applicative esistenti non cambiano. Non viene azzerato né sostituito il key ring.

## Preparazione di produzione

Configurazione completa e dettagli tecnici: [MCP.md](MCP.md).

- GitHub environment `production`: `MCP_ENABLED=true`, issuer `https://apifc.azurewebsites.net`, frontend `https://fanta.zone/`.
- Secrets `MCP_CERTIFICATE_PFX_BASE64` e `MCP_CERTIFICATE_PASSWORD`, oltre ai secrets di produzione già esistenti.
- Conservare `MCP_DATA_PROTECTION_KEY_PATH=/home/ASP.NET/DataProtection-Keys` e confermare `MCP_KEY_RING_REVIEWED=true`. Il percorso è stato verificato in sola lettura su `apifc` Linux; non eliminare o spostare i file.
- Nessuna variabile/secret per client OAuth richiesta dalla pipeline: i precedenti `MCP_CLIENT_*` possono rimanere inutilizzati.
- Backup protetto delle chiavi e del PFX; non allegarli al repository, alla PR o ai log.

## Merge e verifica online

Il merge su `main` avvia **due pipeline distinte**, API e GitHub Pages. Il frontend e il backend non vengono pubblicati atomicamente: verificare il completamento di entrambe prima di collaudare il collegamento MCP.

1. Eseguire test backend, configurazione di deploy, frontend, TypeScript ed export web.
2. Prima del merge verificare i valori dell'environment `production`; il preflight blocca il deploy API se mancano certificato, password o key-ring review.
3. Dopo il merge controllare `Deploy API` e `Deploy GitHub Pages` sullo stesso commit.
4. Eseguire `python3 scripts/check-mcp.py https://apifc.azurewebsites.net` (sola lettura, nessun login o registrazione client).
5. Verificare una sessione browser già aperta, un nuovo login Microsoft, formazione e flussi amministrativi/mercato.
6. Collegare un client con registrazione automatica e provare consenso, lettura, rifiuto di operazioni non consentite, refresh e revoca dalle impostazioni.

I test locali e Azurite non certificano il collegamento con un account Microsoft reale o la compatibilità di ogni client MCP.

## Disabilitazione d'emergenza

Disabilitare MCP con `Mcp__Enabled=false` sull'App Service e allineare `MCP_ENABLED=false` su GitHub per i deploy successivi. **Conservare** percorso delle chiavi, certificato e password: anche con MCP disabilitato la versione 3.0.0 mantiene la configurazione Data Protection per leggere le sessioni emesse dopo il rilascio.

Non cancellare le chiavi, non rigenerare il PFX e non fare rollback alla cieca a un binario precedente: dopo la rotazione delle chiavi, il vecchio codice potrebbe non conoscere il certificato necessario a decifrarle. La disabilitazione della feature nella versione corrente è distinta dal rollback del binario.
