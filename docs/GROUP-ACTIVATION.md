# Gruppi, super admin e attivazione con codice

## Ruoli distinti

- **Super admin di piattaforma**: per default `alessandro.rapiti44@gmail.com`, configurabile con `Authorization:GlobalAdministrators` oppure `GLOBAL_ADMIN_EMAIL` nel Compose. Il ruolo richiede il login Microsoft, un account SQL attivo e il riconoscimento server: non deriva dal nome profilo né da un ruolo in un gruppo. Dopo una modifica alla configurazione fare logout/login per rinnovare i claim.
- **Responsabile/amministratore del gruppo**: può configurare il proprio gruppo, non vedere utenti o gruppi di altri né generare codici della piattaforma.

`/api/accounts/me` espone `isGlobalAdministrator` per la navigazione frontend. La pagina `/platform`, accessibile dal menu anche senza gruppi, offre elenchi paginati e ricerca di **tutti** gli utenti (compresi i disabilitati) e di tutti i gruppi, con membri e leghe. Questa vista globale non aggiunge membership al super admin né modifica i permessi delle normali API di gioco.

## Come attivare un gruppo oggi

1. Il super admin apre **Utenti, gruppi e codici → Codici di attivazione** e genera un codice con una nota facoltativa.
2. Il codice completo è mostrato **una sola volta**. Copiarlo e consegnarlo privatamente al destinatario. Chi possiede il codice può usarlo con il proprio account autenticato; la nota non vincola un indirizzo email.
3. L'utente apre la home senza gruppi oppure **Crea un nuovo gruppo** nel menu e inserisce nome e codice in `/groups/new`.
4. Il server crea gruppo e membership del responsabile e consuma il codice in un'unica transazione PostgreSQL. Una seconda richiesta, anche contemporanea o da un altro utente, non può creare un altro gruppo. Gli errori annullano anche il consumo.
5. Il nuovo gruppo diventa la selezione dell'account. Da **Impostazioni → Gestisci i tuoi gruppi** il responsabile configura basket, leghe/competizioni, squadre e inviti. Non vengono inventati partecipanti, calendari o regole su gruppi esistenti.

Un codice abilita **un gruppo**, non una singola lega: il gruppo può avere più competizioni. Chi appartiene già a un gruppo può usare un altro codice per crearne un altro. Codici utilizzati o revocati non sono riattivabili; i codici ancora disponibili possono essere revocati dal super admin.

Gli account nuovi restano registrabili senza invito. Senza gruppi la home presenta prodotto, possibilità di invito e attivazione; il menu non espone funzioni di gioco inutilizzabili. L'amministrazione globale rimane visibile al super admin anche in questa situazione.

## API e sicurezza

Il creatore è responsabile/amministratore del proprio gruppo e può usare **Membri e inviti**. La ricerca richiede un'email completa e restituisce solo nome/email e presenza nel gruppo corrente, mai il catalogo utenti o le membership altrui. Può anche predisporre l'accesso a un'email non ancora registrata; non vengono spedite email automaticamente. La vista globale con tutti gli utenti e i rispettivi gruppi rimane separata in `/platform`. Vedere il [runbook](MIGRATION-RUNBOOK.md).

- `GET /api/platform/users`, `/groups`, `/group-codes`: riservati al super admin verificato, paginati (`offset`, `limit`, massimo 100). Ricerca per utenti e gruppi con `query`.
- `GET /api/platform/groups/{id}`: panoramica membri/leghe senza impersonazione.
- `POST /api/platform/group-codes`: `{ "label": "Nota facoltativa" }`, risposta con codice completo e metadati; `Cache-Control: no-store`.
- `POST /api/platform/group-codes/{id}/revoke`: revoca di un codice non ancora usato.
- `POST /api/workspace/groups/redeem-code`: `{ "name": "Nome gruppo", "code": "FZ-…" }`, disponibile a ogni account autenticato attivo.

I codici contengono 192 bit casuali crittografici. In `GroupCreationCodes` sono conservati soltanto l'hash SHA-256, la nota e l'audit emittente/destinatario/gruppo/date; gli elenchi non restituiscono codice o hash. Non inserire codici in URL, screenshot pubblici o log. La console di piattaforma non è pubblicata nel catalogo MCP. Il precedente comando di creazione diretta resta riservato al super admin come operazione amministrativa: non è un percorso gratuito per gli utenti ordinari.

La migration viene applicata all'avvio dell'API come le altre. L'emissione e il riscatto sono scritture applicative e chiudono la finestra di reimport dei dump: completare la migrazione del dataset prima di usarli.

## Acquisto annuale: non ancora attivo

La pagina anticipa l'acquisto di gruppi a costo annuale. **Prezzo, checkout, provider, addebiti, scadenze e rinnovi non sono implementati**. I codici attuali non effettuano pagamenti né introducono scadenze implicite nei gruppi; i gruppi esistenti/importati non vengono sospesi.

Stripe/PayPal verranno integrati come un'altra fonte di autorizzazione alla creazione, non come un flag scelto dal browser: serviranno ordine server, webhook verificato/idempotente, periodo di validità e gestione rinnovi/rimborsi. Nessuna finta conferma pagamento o prezzo viene introdotto in questo incremento.
