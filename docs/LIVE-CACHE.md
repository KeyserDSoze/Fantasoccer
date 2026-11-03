# Live: lettura immediata e aggiornamento in background (4.0.3)

- Il job `live` acquisisce i voti ogni 5 minuti, nella finestra della giornata.
- Il job indipendente `live-projections` controlla ogni 5 minuti le proiezioni dei gruppi attivi, anche fuori dalla finestra dei voti. Confronta l'impronta delle sorgenti (voti, calendario, rose, formazioni, regole e risultati): ricostruisce solo se cambiano. Sorgenti invariate: conferma la freschezza senza ricalcolare le partite.
- `GET /api/groups/{groupId}/live` legge soltanto la cache: non acquisisce lease, non attende il job e non avvia ricalcoli. Il blob precedente resta disponibile durante l'aggiornamento, pubblicato solo a ricalcolo completato. Nessuna transazione SQL globale copre il ricalcolo della proiezione.
- Una copia vecchia viene restituita con `IsStale=true` e la data dell'ultima generazione: il frontend già avvisa che i dati sono in aggiornamento. Retention fisica: 3 giorni, estesa a ogni conferma del worker.
- Cache mancante (primo avvio, perdita del blob o nuova giornata): risposta immediata senza leghe, marcata stale. Il job la popola al prossimo giro. L'endpoint non deve ricostruire come ripiego.
- I due job hanno lease SQL separate e possono partire in parallelo: un voto arrivato dopo il controllo della proiezione sarà incluso nel giro successivo. Il frontend rilegge automaticamente la copia pubblicata ogni 15 secondi, senza forzare il ricalcolo. Dalla 4.0.5 i pulsanti di aggiornamento manuale ridondanti sono rimossi (vedi [verifica frontend](FRONTEND-REFRESH-AUDIT.md)); restano i «Riprova» in caso di errore.

## Classifica provvisoria (4.0.4)

La classifica del Live usa gli stessi risultati mostrati nelle partite: il job rilegge il calendario salvato e sostituisce in memoria i risultati della giornata con quelli della proiezione. Il calcolatore della competizione applica le regole annuali e le esclusioni delle fasi. Una partita già ufficializzata viene contata una sola volta, non aggiunta alla classifica ufficiale. Le identità di calcolo sono gli ID delle squadre, non il proprietario. I saldi disponibili vengono conservati dallo snapshot ufficiale; risultati, classifiche e saldi nel database non vengono modificati. L'impronta della cache cambia per invalidare gli snapshot con la vecchia classifica ufficiale-only (issue #29).

## Home, indicatori e caricamento (4.0.6)

- La finestra della home è calcolata dal server e condivisa con la selezione della cache: **primo calcio d'inizio meno 2 ore → ultimo calcio d'inizio + 135 minuti (fine prevista) + 14 ore**. Quando ci sono finestre sovrapposte ha priorità quella iniziata più di recente. I recuperi in corso mantengono la precedenza sulla giornata originale.
- La finestra è di visualizzazione, non riapre le formazioni né cambia l'orario dei job di ingestione. La home esce prima dal Live se tutte le partite della lega/giornata hanno risultati salvati e nessun turno è in attesa dei recuperi (`IsConsolidated`). I risultati **provvisori** della cache non sono un consolidamento.
- Home e pagina Live leggono la stessa proiezione (inclusa la classifica provvisoria), con polling di lettura ogni 15 secondi; il calendario e la classifica ufficiali restano il fallback dell'ultima giornata giocata. Si mantiene l'anno selezionato nella home, senza sostituirlo con la preferenza del profilo. Se la cache è fredda o la lega non gioca in quella giornata (es. coppa), la home mantiene l'ultima giornata disponibile, senza inventare risultati né svuotare la pagina.
- `IsPlaying` indica una gara Serie A effettivamente in corso, non l'intera finestra di visualizzazione: niente animazione nelle 2 ore pre-partita, tra gare già concluse o dopo l'ultimo match. `IsProvisional` distingue i punteggi di anteprima dagli ufficiali. Il calendario pulsa soltanto sui risultati provvisori non annullati mentre si gioca, con etichetta LIVE e supporto al movimento ridotto. Il vecchio riquadro informativo Live è rimosso; gli avvisi di dati vecchi/errori restano.
- Il loader condiviso `SplashScreen` mantiene logo e stile del login e accetta fasi con stato pending/done/error. La partita spunta solo richieste davvero completate: il server restituisce partita, formazioni e voti in **una risposta**, non vengono simulate fasi interne né percentuali. Calendario e statistiche sono facoltativi e non bloccano una partita già pronta. Il loader iniziale non sostituisce contenuti già disponibili durante il polling.

## Diagnostica query

In entrambe le modalità Application Insights e OpenTelemetry sono ascoltate le ActivitySource `Npgsql` (durata, operazione, correlazione delle query) e `Fantasoccer.Live` (`live.refresh`, `live.build`, `live.build.game`). Il processor rimuove `db.statement` e `db.query.text`: niente SQL completo o parametri utente nei trace.

Esempio Log Analytics:

```kusto
AppDependencies
| where TimeGenerated > ago(1h)
| summarize Calls=count(), P95=percentile(DurationMs,95), Max=max(DurationMs) by Type, Name
| order by Max desc
```

I test verificano letture con lease occupata, copia vecchia di un'ora, cache vuota, invalidazioni e isolamento dei gruppi. Riferimenti: issue #27 (Live) e #20 (CPU/lock dei job).

## Voti ufficiali: consolidamento partita per partita (4.1.6)

I voti provvisori (arancioni) diventano ufficiali (verdi) con il job `official`, ora **ogni 15 minuti, tutto il giorno**:

- **Giornata in corso:** ogni partita finita (fine prevista = calcio d'inizio + 135 minuti, o `IsFinished`) e non ancora consolidata viene importata appena il provider mostra voti finali (almeno un voto) per entrambe le squadre (`OfficialProgressAsync`, operazione `official-progress`). Una partita senza voti pubblicati non scrive nulla e conserva i voti live. Il provider si interroga una sola volta per passata, e solo se ci sono partite finite da consolidare.
- **Giornata finita:** l'import completo (tutte le squadre, allenatori, correzioni) gira nella finestra 03:00–10:45 (Europe/Rome), e nelle ore dopo la fine finché qualche partita non è consolidata; se il provider non è completo si ripiega sull'import partita per partita.
- Il risultato `Partial` con codice `official_votes_pending` è un'attesa prevista, non un incidente.

Prima della 4.1.6 il job girava solo 03:00–10:45 e solo per la giornata già conclusa: le partite del sabato restavano provvisorie fino al martedì mattina (giornata finita il lunedì sera).

Nota (4.1.7): `SqlJobStates.NextRunAtUtc` è calcolato con l'orario in vigore all'ultima esecuzione. Dopo un deploy che rende un orario più frequente, il job è dovuto al primo slot del nuovo orario successivo a `LastCompletedAtUtc` (`SqlJobSchedule.DueAt`), senza attendere lo slot vecchio. Con la 4.1.6 questo mancava: il primo controllo `official` era fissato alle 03:00.
