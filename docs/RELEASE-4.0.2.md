# Fantasoccer 4.0.2 — 1 novembre 2026

- Preferenza push **Superadmin · Errori di sistema**, inizialmente disattivata e
  riservata agli amministratori globali. Segnala errori e timeout dei job e import
  API falliti, con link ai log, senza messaggi di eccezione o payload sensibili.
- Deduplicazione per utente/sorgente/codice in finestre di 30 minuti, nessuno
  storico prima dell'attivazione. Account abilitato, privilegi e opt-out vengono
  ricontrollati prima dell'invio; non serve essere proprietario di una squadra.
- Ricevute degli errori degli import e dei worker in `OperationalFailures`,
  consultabili nei log amministrativi e conservate per 30 giorni.
- Retry dei job falliti, annullati o parzialmente falliti a un minuto. Restano
  invariati timeout di quattro minuti e lease di cinque minuti.
- Fasi del worker push isolate: un produttore in errore non blocca gli altri.
- Migrazione `OperationalSuperadminAlerts`: due colonne sulle preferenze,
  tabella delle ricevute e indice sulle run concluse. Le preferenze esistenti
  non vengono abilitate automaticamente.

Nessuna modifica al dominio `api.fanta.zone` della 4.0.1, alle regole di gioco,
alla Superlega o alla mutabilità del calendario. Campionato iniziale e Team
Helper AI restano rinviati nelle rispettive issue.

Dettagli e limiti: [OPERATIONAL-ALERTS.md](OPERATIONAL-ALERTS.md).
Tracciamento: [#14](https://github.com/KeyserDSoze/Fantasoccer/issues/14).
