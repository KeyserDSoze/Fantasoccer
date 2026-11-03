# Hall of Fame: punto di partenza dell'aggiornamento automatico

I totali storici importati (`HallCareerRank`) vengono dal vecchio job, che a ogni esecuzione
sommava tutte le classifiche, compresa quella della stagione in corso così com'era in quel
momento. L'aggiornamento incrementale (job `hall`) somma solo le differenze delle classifiche
attuali rispetto a un punto di partenza per proprietario e stagione (`HallRankContribution`),
per non contare due volte quello che è già nei totali.

All'import il punto di partenza è fissato solo se i totali coincidono esattamente con la somma
delle stagioni salvate. Non succede quasi mai: la stagione in corso è andata avanti dopo l'import,
le somme di numeri decimali su molte stagioni differiscono di qualche millesimo, e l'import teneva
una riga per ogni etichetta storica (la stessa persona o lo stesso giocatore poteva comparire due
volte). Dalla 4.1.17 il job lo fissa da solo, in quest'ordine:

1. **Righe doppie**: righe dello stesso proprietario (totali sommati) o dello stesso giocatore o
   squadra vincitrice (vittorie unite) diventano una.
2. **Stagione in corso contata in parte**: se per ogni proprietario con una riga nell'albo i totali
   valgono le stagioni precedenti più una parte (da zero a tutta) dell'ultima, le precedenti sono
   fissate così come sono e l'ultima con la parte già contata; l'aggiornamento aggiunge il resto.
3. **Anni senza classifica**: se i totali contengono anche anni di cui non abbiamo la classifica,
   l'ultima stagione Y le cui classifiche stanno tutte nei totali è considerata già inclusa, insieme
   alle precedenti; quelle dopo Y vengono aggiunte.

Le somme decimali sono confrontate con una tolleranza (0,05 o due centomillesimi del valore). Un
proprietario senza riga nell'albo non viene fissato: il primo aggiornamento aggiunge le sue stagioni.
La conferma automatica è registrata in `HallBaselineConfirmations` senza utente (`ActorUserId`
nullo) e il log del job dice cosa è stato deciso ("stagioni fino al 2025/26 già nei totali, la
2026/27 in parte...").

I proprietari sono riconosciuti tra tutti gli utenti (anche chi non è più nel gruppo), i riferimenti
`user:<id>` e i partecipanti storici del gruppo. Resta bloccata (`hall_baseline_reconciliation_required`,
riprovata ogni ora) solo una lega con un proprietario non riconoscibile (il log indica squadra e
stagione) o con totali che non rientrano in nessuno dei casi (il log indica squadra e valori). In quel
caso un amministratore può usare `POST .../hall-of-fame/confirm-baseline`.
