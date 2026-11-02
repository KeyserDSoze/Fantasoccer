# Live: lettura immediata e aggiornamento in background (4.0.3)

- Il job `live` acquisisce i voti ogni 5 minuti, nella finestra della giornata.
- Il job indipendente `live-projections` controlla ogni 5 minuti le proiezioni dei gruppi attivi, anche fuori dalla finestra dei voti. Confronta l'impronta delle sorgenti (voti, calendario, rose, formazioni, regole e risultati): ricostruisce solo se cambiano. Sorgenti invariate: conferma la freschezza senza ricalcolare le partite.
- `GET /api/groups/{groupId}/live` legge soltanto la cache: non acquisisce lease, non attende il job e non avvia ricalcoli. Il blob precedente resta disponibile durante l'aggiornamento, pubblicato solo a ricalcolo completato. Nessuna transazione SQL globale copre il ricalcolo della proiezione.
- Una copia vecchia viene restituita con `IsStale=true` e la data dell'ultima generazione: il frontend già avvisa che i dati sono in aggiornamento. Retention fisica: 3 giorni, estesa a ogni conferma del worker.
- Cache mancante (primo avvio, perdita del blob o nuova giornata): risposta immediata senza leghe, marcata stale. Il job la popola al prossimo giro. L'endpoint non deve ricostruire come ripiego.
- I due job hanno lease SQL separate e possono partire in parallelo: un voto arrivato dopo il controllo della proiezione sarà incluso nel giro successivo. Il pulsante Aggiorna rilegge la copia pubblicata, non forza il ricalcolo.

## Classifica provvisoria (4.0.4)

La classifica del Live usa gli stessi risultati mostrati nelle partite: il job rilegge il calendario salvato e sostituisce in memoria i risultati della giornata con quelli della proiezione. Il calcolatore della competizione applica le regole annuali e le esclusioni delle fasi. Una partita già ufficializzata viene contata una sola volta, non aggiunta alla classifica ufficiale. Le identità di calcolo sono gli ID delle squadre, non il proprietario. I saldi disponibili vengono conservati dallo snapshot ufficiale; risultati, classifiche e saldi nel database non vengono modificati. L'impronta della cache cambia per invalidare gli snapshot con la vecchia classifica ufficiale-only (issue #29).

## Diagnostica

In entrambe le modalità Application Insights e OpenTelemetry sono ascoltate le ActivitySource `Npgsql` (durata, operazione, correlazione delle query) e `Fantasoccer.Live` (`live.refresh`, `live.build`, `live.build.game`). Il processor rimuove `db.statement` e `db.query.text`: niente SQL completo o parametri utente nei trace.

Esempio Log Analytics:

```kusto
AppDependencies
| where TimeGenerated > ago(1h)
| summarize Calls=count(), P95=percentile(DurationMs,95), Max=max(DurationMs) by Type, Name
| order by Max desc
```

I test verificano letture con lease occupata, copia vecchia di un'ora, cache vuota, invalidazioni e isolamento dei gruppi. Riferimenti: issue #27 (Live) e #20 (CPU/lock dei job).
