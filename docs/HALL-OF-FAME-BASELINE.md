# Hall of Fame: punto di partenza dell'aggiornamento automatico

I totali storici importati (`HallCareerRank`) contengono anche anni che non hanno una
classifica salvata. L'aggiornamento incrementale (job `hall`) somma solo le differenze delle
classifiche attuali rispetto a un punto di partenza per proprietario e stagione
(`HallRankContribution`), per non contare due volte quello che è già nei totali.

All'import il punto di partenza è fissato solo se i totali coincidono con la somma delle
stagioni disponibili. Dalla 4.1.16, quando non coincidono, il job lo fissa da solo se, per ogni
proprietario che ha una riga nell'albo, ogni totale (punti, vittorie, pareggi, sconfitte, gol,
punti valore) è almeno pari alla somma delle sue stagioni disponibili: gli anni più vecchi, le
leghe nate dopo e le stagioni non giocate sono solo nei totali, quindi le stagioni che abbiamo
sono già dentro. Un proprietario senza riga nell'albo non viene fissato: il primo aggiornamento
aggiunge le sue stagioni. La conferma automatica è registrata in `HallBaselineConfirmations`
senza utente (`ActorUserId` nullo) e non modifica i totali.

I proprietari delle classifiche sono riconosciuti tra tutti gli utenti (anche chi non è più
nel gruppo), i riferimenti `user:<id>` e i partecipanti storici del gruppo.

Resta bloccata (`hall_baseline_reconciliation_required`, riprovata ogni ora) solo la lega in cui
un totale è inferiore alle stagioni disponibili, un proprietario non è riconoscibile o l'albo ha
righe doppie: il log del job dice quale proprietario e quali totali. In quel caso un
amministratore può usare `POST .../hall-of-fame/confirm-baseline`.
