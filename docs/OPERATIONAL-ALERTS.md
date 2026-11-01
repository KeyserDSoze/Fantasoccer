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
30 giorni dal job `job-retention`. Le run restano in `SqlJobRuns`.

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
