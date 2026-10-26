# Aste SQL, presenze e checkpoint crediti

## Decisione

Le aste non risiedono più in una cache singleton aggiornata su Blob a intervalli. Ogni comando legge uno stato SQL versionato e applica le modifiche nella transazione condivisa con rose e crediti. HTTP, MCP e SignalR invocano il medesimo servizio.

- L'ordine è privato nel DB. Si espongono solo conteggi, giocatori mostrati/scartati e vincite; non occorre cifrare gli ID da inviare al browser.
- L'import decifra le vecchie liste solo con chiave/IV originali forniti esplicitamente. Con chiavi mancanti/errate si ferma con un codice privo di payload o segreti. Non interpreta un ciphertext come nome o ID giocatore.
- Il lotto conserva lo snapshot delle statistiche e descrizioni mostrate, oltre a importi, sostituito e timestamp. L'import non riparte con timer nuovi e non assegna nuovamente i giocatori.
- Le vecchie liste di partecipanti restano audit. Le connessioni attive hanno una tabella distinta; la presenza non concede mai diritto di offerta.
- Si rileggono ogni volta appartenenza, ruolo e proprietà. Offerte per conto di altre squadre sono consentite solo al banditore autorizzato; un co-owner offre per la squadra scelta tramite ID.
- I token query necessari al WebSocket/SSE sono accettati soltanto sotto `/auctionhub`. I token MCP rimangono vincolati alla loro risorsa. Query string e credenziali non vengono esportate nella telemetria.
- Gli aggiornamenti SignalR trasportano ID/versione/operazione, non uno stato con autorizzazioni personali del chiamante. In caso di evento perso il client rilegge lo stato; non si ripete un'assegnazione finanziaria.
- Le modifiche stato/rimozioni della rosa sono versionate e conservano un audit normalizzato. Le formazioni future sono sincronizzate mantenendo gli slot; quelle iniziate/storiche non vengono riscritte.

## Crediti

`MoneyFromRank` importato viene conservato. I nuovi conguagli sono in `MarketBalanceAdjustment`; gli accrediti successivi usano `RankCreditGranted` per applicare soltanto la differenza, non sovrascrivere il saldo.

Il legacy non registrava separatamente il checkpoint dei premi già pagati. Non è corretto dedurlo dal solo saldo, che poteva includere altri movimenti. L'import lo lascia `null` e segnala una riconciliazione; prima di un nuovo accredito un responsabile conferma i crediti già pagati tramite `/api/teams/credits/confirm-rank-checkpoint`, con versione e saldo attesi. La conferma è tracciata e non cambia il saldo. Le nuove squadre SQL partono invece da checkpoint zero.

Il premio iniziale proviene dalla classifica della lega/stagione corretta e dalla squadra dell'anno precedente, collegata per basket e proprietario autorevole, non per nome squadra.
