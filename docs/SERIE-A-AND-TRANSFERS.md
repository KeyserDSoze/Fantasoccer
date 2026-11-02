# Serie A, trasferimenti dei giocatori e partite reali

Design concordato il 2 novembre 2026. Tre fasi, una PR ciascuna.

## Fase 1 — Trasferimenti (4.0.7)

Una sola mossa per giocatore e stagione (scelta del 02/11: niente tabella di storico). Sul giocatore stagionale (`PlayerSeasons`):

| Campo | Significato |
| --- | --- |
| `ClubId` (squadra attuale) | Vuoto quando il giocatore ha lasciato la Serie A |
| `PreviousClubCode` (squadra precedente) | Squadra prima dell'ultimo movimento; vuoto per chi arriva da fuori Serie A |
| `TransferredAtUtc` (data di trasferimento) | Run notturna che ha scoperto il movimento, non la data ufficiale del trasferimento |
| `MissingSinceUtc` | Prima run in cui il giocatore non era nel listone (serve per la conferma) |

Job `catalogue`, **ogni notte alle 02:30** (Europe/Rome), avviabile anche a mano:

| Caso | Squadra attuale | Squadra precedente | Data |
| --- | --- | --- | --- |
| Prima run della stagione | squadra trovata | — | — (fotografia di partenza) |
| Nuovo giocatore | squadra trovata | — | run |
| Squadra diversa | nuova | vecchia | run |
| Assente una notte | invariata | invariata | `MissingSinceUtc` = run |
| Assente anche la notte dopo (≥ 20 ore) | vuota, `IsActive = false` | ultima squadra | prima assenza |
| Rientro in Serie A | squadra trovata | ultima squadra di Serie A (vuota se torna nella stessa) | run |

- Una run ripetuta la stessa sera non conferma l'uscita; un giocatore che ricompare la notte dopo non esce mai.
- Protezioni: listone sotto l'80% dei giocatori attivi, oppure una squadra con almeno 5 giocatori attivi del tutto assente → la run si ferma senza modificare nulla.
- I giocatori già disattivati prima del tracciamento diventano "fuori Serie A" con data sconosciuta.
- Un giocatore visto per la prima volta nei voti (dopo la fotografia iniziale) conta come arrivo.
- **Chi lascia la Serie A** non è più proponibile in aste e ordini dei giocatori (servono una squadra attuale), ma resta nelle rose fanta con l'ultima squadra e l'icona "Ha lasciato la Serie A", fino a svincolo o asta di riparazione.
- **Chi cambia squadra** resta in Serie A: la rosa fanta stagionale segue la squadra nuova; le formazioni usano sempre la squadra attuale.
- Lo storico non viene riscritto: i voti salvano la squadra del giorno (`FootballVote.ClubId`) e le rose giornaliere la loro.
- Lista giocatori: colonne "Squadra attuale" ("Fuori Serie A"), "Squadra precedente", "Data di trasferimento", anche nel CSV. Carta e dettaglio del giocatore mostrano il movimento.

## Fase 2 — Sezione Serie A e partite reali (4.1.0)

- Nuova sezione "Serie A" (menu, `/serie-a`): calendario per giornata e stagione (anche passate) e classifica ricalcolata alla giornata scelta (`rankAtDay`, dal calendario: solo partite giocate, poi differenza reti, gol fatti, ordine ufficiale). Si apre sulla giornata in corso, altrimenti sulla prossima, altrimenti sull'ultima.
- Ogni partita è cliccabile dalla sezione e dal widget della home; il risultato pulsa con "LIVE" solo mentre si gioca (stessa regola del server: `FootballLiveTiming.IsPlaying`).
- Pagina partita `/serie-a/match/{stagione}/{giornata}/{partita}` in sola lettura, stessa grafica del nostro campo. API: `GET /api/football/{year}/days/{day}/matches/{ordinal}` (opzionali `groupId` e `leagueId`).
- Le formazioni vengono ricostruite da `SerieALineupBuilder` (funzione pura) per ciascuna squadra:

| Fonte | Quando | Contenuto |
| --- | --- | --- |
| `official` | voti ufficiali del club in `FootballVotes` | titolari = con voto/s.v. non subentrati; panchina = subentrati e convocati senza voto; panchina stimata (1 P, 2 D, 2 C, 2 A) con i più utilizzati nelle giornate precedenti; il resto in tribuna |
| `live` | partita iniziata, voti provvisori nella cache condivisa | come sopra; con meno di 11 titolari (inizio gara) si completa con le probabili formazioni |
| `probable` | partita non iniziata, almeno 8 giocatori della squadra segnalati dalle fonti | un portiere + 10 di movimento per numero di fonti, poi presenze; infortunati e squalificati esclusi |
| `unavailable` | nessun dato | "Formazione non disponibile" |

- Rosa a quella data: giocatore con la squadra attuale, salvo arrivo dopo il calcio d'inizio; oppure con la squadra precedente se l'uscita è successiva al calcio d'inizio (data ignota = escluso). I voti con club nullo non vengono mai attribuiti.
- Ogni giocatore porta `lineupKind` (`starter`, `estimated-starter`, `substitute`, `bench`, `estimated-bench`, `stand`) e, con una lega scelta, `fantasyTeams` (tutte le squadre fanta proprietarie, una per basket: un giocatore può averne più d'una; una squadra derivata non si ripete se c'è la sua origine. Dalle rose del giorno: ultima rosa di giornata ≤ giornata, altrimenti rosa stagionale). Serve l'accesso al gruppo (`workspace-member`).
- Cache in memoria per partita: 15 s vicino alla partita, 5 minuti altrimenti (`FootballMatchOptions`, 0 la disattiva). Nessun pulsante di aggiornamento: la pagina riesegue la richiesta da sola (15 s vicino alla partita, 2 minuti il giorno dopo, 5 minuti nei due giorni precedenti, mai per partite vecchie).
- Infortunati e squalificati della giornata (snapshot probabili formazioni, valido 30 giorni) sono segnalati sulle carte senza voto; non entrano mai nella formazione probabile né nella panchina stimata.
- Sul campo: la banda sotto la carta elenca le squadre fanta; prezzo e info partita non compaiono. Il selettore di lega sta dentro la partita.

## Fase 3 — Eventi live (4.1.0)

Componente `MatchEvents` (widget home e pagina partita): una riga per giocatore, voto colorato per fascia (≥7 verde, ≥6,5 azzurro, ≥6 blu, ≥5,5 ambra, sotto rosso, s.v. grigio), badge con icona, conteggio ed etichetta ("Gol ×2", "Assist", "Autogol", "Ammonito"…), colori traslucidi leggibili in dark mode. Ordine per importanza: gol, rigori, assist, rigori parati, espulsioni, rigori sbagliati, autogol, gol subiti, ammonizioni (sulla pagina partita anche uscito/entrato). Nessun minuto inventato: il feed non li fornisce.
