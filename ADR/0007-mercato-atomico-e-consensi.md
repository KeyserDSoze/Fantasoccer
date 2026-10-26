# Mercato atomico, consensi e notifiche

## Decisione

Il mercato PostgreSQL non usa `IMarketManager` come wrapper generico dei vecchi repository. Un servizio applicativo EF specifico applica le stesse operazioni pubbliche, separando proposta immutabile, voti e trasferimenti.

- Le parti sono squadre annuali identificate da UUID, con FK composte che includono gruppo, basket, lega e stagione. Le email sono snapshot di presentazione/audit, non l'identità della squadra.
- Il client propone ID giocatore e importi di conguaglio, mai prezzi, stato, consenso o liste di approvatori. La creazione controlla entrambe le versioni rosa e copia i dati server.
- I co-owner operano per la squadra autorizzata; il quorum conta squadre, non account. Chi possiede più squadre deve scegliere esplicitamente quale rappresentare quando l'operazione è ambigua.
- Per eseguire lo scambio si rivalutano disponibilità, ruoli, prezzi, budget e consensi correnti. Un consenso di un proprietario non più autorizzato non viene attribuito automaticamente al suo successore.
- Stato/consensi, entrambe le rose, crediti e versioni sono aggiornati nella stessa transazione PostgreSQL. Un errore sulla seconda squadra non può lasciare eseguita solo la prima metà.
- Il trasferimento rimuove soltanto le righe attive selezionate e mantiene le vendite storiche dello stesso giocatore. Le righe immutabili della proposta e il timestamp di esecuzione costituiscono l'audit dello scambio.
- Gli eventi di mercato sono righe tipizzate con versione, attore, esito e timestamp, committate insieme alla proposta/rose. La consegna push sarà collegata all'outbox, non invocata dentro la transazione o durante un GET/import.
- I nuovi conguagli usano `MarketBalanceAdjustment`, separato da `MoneyFromRank`: le aste non potranno sovrascriverli aggiornando i premi classifica. Il valore legacy importato viene preservato senza tentare di scomporre retroattivamente movimenti che la sorgente non registrava separatamente.

## Verifiche

Fixture con envelope reale, doppio import e rimozioni. Test HTTP per prezzi/stati falsificati, utenti esterni, accettazione del venditore, voti concorrenti di co-owner, proposta rimasta senza giocatori, storico vendite e rollback su errore DB a metà trasferimento.

Il flusso notifiche non è dichiarato completo finché non è implementata e verificata la consegna dell'outbox.
