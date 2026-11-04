# Avvisi operativi del superadmin

## Attivazione

In **Notifiche push**, un amministratore globale vede l'interruttore
**Superadmin · Errori di sistema**. Serve anche un dispositivo push attivato.
La preferenza è disattivata inizialmente: non viene abilitata per tutti gli
amministratori né quando si registra semplicemente un dispositivo.

Il controllo è server-side: ruolo `super_admin` e account nella configurazione
degli amministratori globali. Il solo ruolo di amministratore di gruppo non
autorizza gli avvisi. Il server ricontrolla account abilitato, configurazione e
opt-out prima di ciascun invio. La rimozione del ruolo globale o la disattivazione
della preferenza sopprime anche le consegne già in coda.

## Errori coperti

- run dei job `Failed`, `LeaseLost`, timeout e risultati parziali con un errore;
- errori del runner prima/dopo la registrazione di una run;
- import manuali eseguiti tramite `/api/football/refresh` da un amministratore
  autorizzato, quando la sorgente fallisce o risulta incompleta;
- errori dei produttori push e del dispatcher (senza notificare ricorsivamente
  gli errori del produttore degli avvisi operativi).

Le attese normali dei voti o dei recuperi e i batch ancora da completare non sono
incidenti. La cancellazione normale per spegnimento/deploy non genera una push.
Richieste non autorizzate o con contesto non valido non sono errori di import.

Gli import manuali e gli errori dei worker hanno ricevute minime in
`OperationalFailures`, consultabili nei log amministrativi e conservate per
30 giorni dal job `job-retention`. Le run restano in `SqlJobRuns` (anch'esse
cancellate dopo 30 giorni dallo stesso job, alle 04:30 di Roma).

Dalla 4.1.8 run e ricevute hanno anche un `Detail` (max 1000 caratteri) mostrato
nei log: il nostro messaggio con il contesto (giornata, partite o squadre
mancanti, motivo), oppure, per un errore imprevisto, solo il tipo di eccezione
(e lo stato PostgreSQL), mai il testo dell'eccezione né il contenuto del
provider. URL, segreti ed e-mail vengono mascherati. Esiti come
`live_votes_closed` (feed live rimosso dal provider a giornata finita) e
`official_votes_pending` sono attese previste, non incidenti. Lo è anche
`chances_lineups_pending`: il job `chances` legge tre sorgenti (Fantagazzetta,
Gazzetta, Indisponibili); se mancano solo le probabili formazioni e il primo
calcio d'inizio è oltre 48 ore, attende il turno successivo. Altrimenti il log
nomina la sorgente vuota o non leggibile e il numero di giocatori di ognuna.

## Deduplicazione e riservatezza

Un avviso per destinatario, sorgente e codice d'errore in ogni finestra UTC di
30 minuti. Errori diversi possono quindi generare avvisi diversi. Sono considerate
soltanto ricevute delle ultime 24 ore, successive all'attivazione della preferenza:
nessun invio massivo dello storico. Il produttore gira nel ciclo push di 30 secondi;
la consegna è best effort e dipende dalla rete e dal servizio del browser.

La push contiene soltanto nome operativo, codice normalizzato e link `/logs`.
Non contiene messaggi di eccezione, stack trace, URL dei provider, token o payload
importati. Usa l'outbox e le ricevute dispositivo già esistenti, con tipo 7 e senza
richiedere l'ownership di una squadra. Il payload scade dopo un'ora.

Gli errori di un produttore push non impediscono l'esecuzione delle altre fasi e
la spedizione dei messaggi già accodati: ogni fase usa uno scope indipendente.

## Retry e calendario

I job falliti, annullati o parzialmente falliti ritentano dopo **un minuto**, senza
backoff crescente. I batch con altro lavoro continuano dopo un minuto; le normali
attese tornano alla schedulazione ordinaria. Le lease restano di cinque minuti e
il timeout di lavoro di quattro: il retry non consente due esecuzioni simultanee
dello stesso job. Il backoff di consegna Web Push, distinto dal retry dei job,
resta quello già esistente.

Il calendario rimane modificabile e viene riletto ogni cinque minuti dalle
12:00 alle 23:55, Europe/Rome. Nessun congelamento dei kickoff introdotto qui.

## Limiti

Queste notifiche non sostituiscono un monitor esterno: se API, PostgreSQL, worker
push o servizio di consegna sono indisponibili, la push può non arrivare. Gli
import CLI/offline non sono automaticamente collegati a questa funzione. Una
risposta di accettazione del provider non certifica lettura o ricezione sul telefono.

Tracciamento: [#14](https://github.com/KeyserDSoze/Fantasoccer/issues/14).
Attività future rinviate: [#15](https://github.com/KeyserDSoze/Fantasoccer/issues/15),
Campionato iniziale [#4](https://github.com/KeyserDSoze/Fantasoccer/issues/4),
Team Helper AI [#1](https://github.com/KeyserDSoze/Fantasoccer/issues/1).

## Timeout dopo un deploy

Un deploy può spegnere la vecchia API senza chiudere le connessioni. PostgreSQL
tiene la sessione morta, e il lock globale delle scritture (`pg_advisory_xact_lock`
in `CoreMigrationLock`) che eventualmente teneva, finché il keepalive TCP non si
arrende: con i valori predefiniti (120 s + 9 sonde da 30 s) circa 6,5 minuti, in cui
ogni altra scrittura (`formations`, `hall`, ricevute dei job) scade dopo 120 s con
`NpgsqlException ← TimeoutException`, e il job che non riesce a scrivere la ricevuta
resta `Running` fino a `LeaseLost` / `previous_lease_expired`. `infra/postgresql.tf`
porta i keepalive a 30 s + 3 sonde da 10 s (circa un minuto). Il log lo spiega nel
dettaglio. `hall_baseline_reconciliation_required` richiede una persona: il job
viene ricontrollato ogni ora, non ogni minuto.

Dalla 4.1.12 ogni processo dell'API dà alle proprie sessioni PostgreSQL un
`Application Name` unico (`fantasoccer-api:<id>`). Il nuovo processo (non in
sviluppo né nei test) cerca per i primi 15 minuti, ogni 20 secondi, le sessioni
di altri processi dell'API con una transazione aperta da oltre 60 secondi e le
termina con `pg_terminate_backend`: il blocco globale torna libero subito, senza
aspettare il keepalive del server. Una transazione sana dura pochi secondi; il
messaggio "Released N database session(s)…" nei log indica che è servito.

### Cosa mostrano i log del database (4.1.17)

I log di PostgreSQL (Log Analytics `log-fcv2`, categoria `PostgreSQLLogs`) mostrano che il vecchio
processo chiude le sue connessioni in modo pulito. Il blocco riguarda il nuovo container: le
connessioni aperte subito dopo lo stop del vecchio (spesso sulle stesse porte SNAT appena liberate)
impiegano ~30 s ad aprirsi e poi restano appese fino al timeout di 120 s; quelle aperte dopo
funzionano. Successo in 4 deploy su 7 il 3/11, per 2–6 minuti. Per questo i job in background e il
produttore push partono `JobMaintenance:StartDelaySeconds` (300) secondi dopo l'avvio e recuperano
subito dopo, e le connessioni usano il keepalive TCP (10 s, poi ogni 5 s) per accorgersi prima di una
connessione appesa.

## Quante esecuzioni mostra la pagina dei log

Ogni job ha il suo spazio (`SqlJobLogPolicy`): 20 esecuzioni per i job frequenti (`formations`,
`live`, `live-projections`, `postponed-results`, `calendar`, `official`, `statistics`, `results`),
30 per `chances` e `hall`, 10 per gli altri (giornalieri o annuali). In più, a parte, gli ultimi
errori di ciascun job, nello stesso numero. Le ricevute di errore sono 20 per sorgente. Una
ricerca (`query`) invece usa il limite generale della richiesta.
