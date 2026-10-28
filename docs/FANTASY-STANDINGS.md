# Classifica corrente persistita e storico ricostruito

## Contratto

Per ogni `(GroupId, LeagueId, SeasonIndex)` si salva **una sola classifica**, aggiornata all'ultima giornata elaborata. Calendario, risultati delle partite e regolamento annuale rimangono persistiti. Il ricalcolo esplicito o il job dei risultati aggiorna la classifica corrente; una lettura non calcola di nuovo voti/formazioni, non genera calendari e non scrive risultati o crediti.

Per compatibilità lo schema mantiene `FantasyStandingSnapshots` e la componente di chiave `SnapshotDay`, ma il CHECK SQL impone `SnapshotDay = 0`. `SerieADay` resta la giornata effettiva della classifica, non viene azzerata. Le righe di classifica mantengono punteggi, etichette e valori economici salvati; aste e albo continuano a usare questa classifica corrente.

## Letture

```http
GET /api/competitions/{group}/{league}/{year}/rank
GET /api/competitions/{group}/{league}/{year}/rank?day=7
```

La prima legge la classifica corrente SQL (`isReconstructed:false`, `snapshotDay:0`). La seconda ricostruisce la classifica usando soltanto le giornate del calendario con **SerieADay <= 7**, non il numero del turno fantasy, e il regolamento/tipo di competizione di quell'anno. Usa il medesimo motore di classifica per campionato, coppa, nuova coppa e superlega, incluse le esclusioni delle fasi a eliminazione. Non ricalcola i gol dai fantapunti: usa gol e punteggi dei risultati salvati.

Lo storico risponde con `isReconstructed:true`; il compatibile `snapshotDay` indica il limite richiesto, **non una riga persistita**. `serieADay` indica l'ultima giornata effettivamente elaborata dal motore entro quel limite: può essere precedente se ci sono giornate non ancora giocate. Le partite annullate mantengono la semantica esistente del motore, senza aggiungere statistiche sportive.

Le identità di calcolo sono gli ID delle squadre annuali già collegati alle partite, non nomi o casing dei proprietari. I nomi/proprietari restituiti provengono dal calendario storico, non dalla rosa corrente. Le fasi future non compaiono prima della giornata richiesta; i partecipanti che compaiono in seguito non sono inventati nelle giornate precedenti. Il motore inizializza i partecipanti da tutte le partite della porzione di calendario fornita, senza assumere che la prima giornata li contenga tutti.

Senza calendario non si usa la classifica corrente come finto storico. Una giornata di una lega che [attende i recuperi](POSTPONED-MATCHES.md) resta senza risultato e non entra in classifica finché non viene calcolata con i voti reali; nelle leghe con voto politico la giornata si chiude con il voto politico. Bracket non assegnati senza risultato rimangono vuoti; risultati giocati senza identità o punteggi completi producono un errore esplicito, non squadre/punteggi indovinati. Le notizie possono continuare a descrivere risultati legacy salvati con collegamenti incompleti: in quel caso omettono il contesto della classifica, senza fingere uno storico con la classifica corrente e senza allentare autorizzazioni o controlli di concorrenza. Restano autorizzazione di gruppo e scope di competizione/anno. Il frontend esegue una sola richiesta per lo storico e ne deriva localmente le proprietà di presentazione.

## Non è una fotografia immutabile del passato

Questa scelta è stata approvata esplicitamente dal gestore: una correzione di risultati o del regolamento annuale si riflette anche sulla classifica storica ricostruita. Si mostra la classifica fino alla giornata N **secondo i dati attualmente salvati per quell'anno**, non ciò che l'interfaccia mostrava esattamente in una vecchia data di calendario. Non viene introdotto un sistema di versionamento dei regolamenti o degli snapshot.

`plusMoney` ricostruito è il premio teorico derivato da gol e regole, non un accredito. I vecchi saldi `money` non sono ricostruibili dal solo calendario: nello storico `money` e `valueAssets` sono **null**, non zero o il saldo attuale. I DTO/client propagano questa indisponibilità; somme e ordinamenti non trasformano un saldo ignoto in uno noto. I valori correnti e dell'albo rimangono numerici quando disponibili. Nessuna lettura modifica `MoneyFromRank`, `RankCreditGranted`, premi, saldi o checkpoint economici.

## Import e upgrade sicuro

Dalla MappingVersion 36 (corrente: 43). Il container legacy `rank` continua a fornire la classifica corrente, senza ricalcolarne o correggerne i valori all'import. `calendar` e `playedgame` mantengono risultati e snapshot salvati. `dailyrank` è invece **escluso dall'import SQL** e dichiarato in `excludedContainers`/`preservedArtifacts`. Tutti i suoi byte e checksum rimangono nel dump verificato: vengono verificati anche se il contenuto non viene decodificato. Non si promuove una copia giornaliera a classifica corrente se manca il Blob `rank` e non si inventa uno storico per calendari incompleti.

La migration aggiuntiva `20261027214331_CurrentOnlyFantasyStandings` modifica soltanto il CHECK sulla giornata della classifica. Se il target contiene già righe con `SnapshotDay != 0`, **l'upgrade viene rifiutato senza cancellarle**, poiché possono contenere valori storici non ricostruibili. Prima occorrono backup verificato del target e una gestione esplicita di quelle copie da parte dell'operatore. Non usare reset o cancellazioni automatiche per aggirare il rifiuto. Un target con sole classifiche correnti viene aggiornato senza ricalcolare punteggi o cambiare saldi. Il downgrade ripristina il vecchio intervallo ammesso, senza creare snapshot mancanti.

Non vengono modificate migration pubblicate, sorgente Azure, originali del dump, risoluzioni utenti, appartenenze o permessi. Nessuna nuova opzione env. Analizzare prima con `-UseExistingDump`; `-Apply` resta subordinato a revisione del report e alla sicurezza del target. Non vengono inviate notifiche o riaperte finestre di reimport dopo scritture applicative.
