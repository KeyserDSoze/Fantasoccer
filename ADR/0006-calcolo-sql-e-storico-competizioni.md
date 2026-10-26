# Calcolo SQL e storico delle competizioni

## Decisione

Il calcolo esistente resta caratterizzato e condiviso, con caricamenti specifici `IFootballMatchdayReader` e `IFormationStatisticsReader`. Gli adattatori SQL usano direttamente EF; l'adattatore repository è riservato ai test/regressioni legacy. Non esiste un repository generico sostitutivo né un passaggio SQL→Blob.

- Le identità trasmesse al client sono UUID; le chiavi normalizzate dei nomi restano solo alias di ricerca/immagini e input interno degli algoritmi esistenti.
- Calendario: stage, ordinale della giornata, numero del turno fantasy e giornata Serie A sono dimensioni distinte. I nomi/proprietari presenti nel risultato sono snapshot, non aggiornamenti alle anagrafiche attuali.
- Risultati importati e snapshot classifiche sono autoritativi; un GET può produrre un risultato live marcato `resultIsPreview`, mai persisterlo o sovrascrivere il risultato salvato.
- Non si usa la rosa attuale per far apparire completo uno storico mancante. Si usa lo snapshot giornaliero o quello della partita salvata; altrimenti la lettura segnala un conflitto esplicito.
- Le formazioni condivise fra leghe usano i limiti della lega principale. Le posizioni si salvano con versione della giornata e, quando la giornata non esiste ancora, versione della rosa di origine. I valori economici non vengono accettati dal client.
- Il ricalcolo esplicito è una scrittura distinta dall'import e richiede un responsabile del gruppo. Attende il termine della giornata e i voti ufficiali per tutte le squadre reali. Calendario, snapshot partita e classifiche si salvano atomicamente.
- L'algoritmo di classifica e i bonus esistenti vengono riusati. Il bonus casa è applicato anche nel ricalcolo manuale, come nel job storico; prima i due percorsi divergevano. Non si modifica retroattivamente alcun risultato senza un ricalcolo richiesto esplicitamente.
- La generazione non distrugge risultati esistenti. Le estensioni delle coppe possono aggiungere turni tramite il calcolatore esistente; il loro adattamento SQL deve restare transazionale.
- Le date HTTP sono istanti UTC ISO, non `Date` JavaScript fittizi convertiti due volte in ora italiana. La convenzione di ora locale è confinata al calcolatore legacy e all'import esplicito.

## Limiti attuali

Questo documento non dichiara portati ingestione/job, mercato/aste e notifiche. La classifica iniziale produce premi; l'assegnazione finanziaria nel flusso asta va completata insieme alla migrazione aste. `fullMigrationComplete` resta falso.
