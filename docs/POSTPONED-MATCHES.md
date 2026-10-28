# Partite rinviate: voto politico o attesa dei recuperi

## Regola

I voti salvati sono **soltanto quelli reali** del provider: ufficiali o live. Un voto politico non viene mai salvato come voto, né sovrascrive un voto reale.

Il calendario reale ricorda per sempre che una partita è stata rinviata (`WasPostponed`), anche dopo che il recupero è stato giocato e il provider ha aggiornato data e risultato. `Delayed` indica invece soltanto lo stato attuale (ancora senza data o non giocata). `OriginalKickoffUtc` conserva l'orario originale, fissato quando la partita risulta rinviata.

Ogni lega decide, nelle impostazioni annuali, come trattare i rinvii (`postponements`):

| Impostazione | Default | Effetto |
| --- | --- | --- |
| `waitForRecovery` | `false` | `false`: la giornata si chiude senza aspettare, con voto politico. `true`: la giornata resta aperta fino a tutti i recuperi. |
| `goalkeeper`, `defender`, `midfielder`, `forward` | `6` | Voto politico per ruolo (portiere, difensore, centrocampista, attaccante), da 0 a 10. |

Nel database sono le colonne `LeagueRules.WaitForPostponedMatches` e `PoliticalGoalkeeper/Defender/Midfielder/Forward`, con vincolo CHECK 0–10. Nel JSON legacy del regolamento sono sotto `pp`; un regolamento salvato prima di questa modifica usa i default.

## Lega con voto politico (default)

Al momento del calcolo il calcolatore controlla il calendario della giornata. Se la partita del club del giocatore è stata rinviata, **tutti i giocatori di entrambe le squadre** ricevono il voto politico del proprio ruolo:

- anche se nel frattempo il recupero è stato giocato e sono arrivati i voti reali;
- anche nei ricalcoli successivi, perché la memoria del rinvio resta nel calendario;
- il voto è piatto: niente bonus/malus, niente gol subiti e niente porta inviolata. Restano i modificatori di squadra (fair play, difesa) calcolati come per un voto normale;
- nella vista partita il voto è marcato `isPolitical: true`.

Il club usato per individuare la partita è quello del voto reale della giornata, se c'è, altrimenti quello della rosa salvata per quella giornata. Non si usa il club attuale del giocatore.

La giornata è pronta quando le partite **non** rinviate sono finite e hanno voti ufficiali. Le scadenze delle formazioni e le finestre live usano gli orari originali: un recupero non riapre le formazioni e non allunga per mesi la giornata live originale.

## Lega che attende i recuperi

Nessuno riceve il voto politico. Finché manca anche un solo recupero o i suoi voti ufficiali reali:

- la giornata resta senza risultato e `FantasyMatchday.AwaitingPostponedMatches` vale `true`, anche per mesi;
- il ricalcolo manuale risponde `{"changed":…,"awaitingRecovery":true}` e non inventa punteggi;
- un risultato già salvato non viene mai cancellato o sostituito: il ricalcolo risponde `409 postponed_recovery_not_finished`;
- la competizione iniziale salva una classifica provvisoria (`IsComplete=false`) senza premi; l'asta rifiuta i crediti provvisori con `opening_waiting_for_recovery`.

Quando il recupero è finito e i voti reali sono disponibili, la giornata viene calcolata con i voti reali, come se si fosse giocata regolarmente. Anche in questa modalità un giocatore che non ha giocato resta senza voto.

## Job `postponed-results`

Gira ogni 5 minuti (ora di Roma) e considera tutte le stagioni, non soltanto l'ultima giornata:

1. cerca le giornate senza risultato delle leghe attive che attendono i recuperi, più le competizioni iniziali provvisorie; le leghe con voto politico non vengono mai selezionate;
2. procede a rotazione in base a `FootballMatchday.LastRecoveryCheckUtc`;
3. se un recupero non risulta finito, rilegge il calendario di quella giornata dal provider; la risposta deve confermare stagione, giornata e coppie di club già note;
4. durante il recupero aggiorna i voti live dei soli club coinvolti; a recupero finito scarica i voti ufficiali **solo dei club recuperati**, senza riscrivere gli altri voti storici della giornata, e imposta `ActualOfficialVotesReady`;
5. ricalcola lega per lega soltanto le giornate ora chiudibili.

Il provider attuale non verifica il calendario delle stagioni passate: in quel caso la giornata resta in attesa con codice `recovery_calendar_season_unavailable` e usa i dati già importati. Gli altri codici di attesa sono `recovery_calendar_scope_mismatch`, `recovery_source_unavailable`, `football_provider_incomplete`, `official_matchday_incomplete` e `postponed_recovery_not_pending`. Ogni esecuzione tratta al massimo 25 giornate, così una stagione non verificabile non blocca le altre. Ogni aggiornamento e ogni ricalcolo di lega usa una propria transazione: un errore non previsto fa fallire il job senza annullare le leghe già chiuse correttamente.

## Import legacy: i 6 d'ufficio

I vecchi documenti `official` potevano contenere, oltre al voto reale, una copia del giocatore con il 6 d'ufficio. Il caso reale esaminato è la stagione 10, giornata 26: `p[33]` è il voto reale (`isIn:true`, un gol subito) e `p[327]` il 6 d'ufficio, con stessi nome, ruolo e club `cag`.

Una voce ha la forma del 6 d'ufficio se: voto 6, `hasVote:true`, `isFinal:true`, `isIn:false`, nessun evento o cartellino, ruolo coerente e soltanto campi noti senza duplicati. La forma da sola **non basta**. La voce viene separata dai voti reali soltanto se:

- esiste una voce reale (`isIn:true`) dello stesso giocatore, ruolo e club nello stesso documento; oppure
- il calendario legacy indica come rinviata la partita di quel club in quella giornata.

Queste evidenze valgono per l'intera partita: i 6 d'ufficio dei compagni e dell'avversario dello stesso match vengono separati anche senza una voce reale accoppiata. Un 6 con `isIn:false` senza evidenze resta un voto reale. Due voci reali per lo stesso giocatore restano un errore `duplicate_or_invalid_vote`: non si sceglie nessuna delle due.

Le voci separate vengono salvate in `LegacyPoliticalVoteEntries` con JSON originale esatto, checksum, riferimento d'archivio, posizione JSON e ordinale sorgente. Non vengono mai lette come voti. Il report le riassume in `legacyPoliticalVotes` (documento, giornata, numero di voci, quante hanno una voce reale accoppiata, prima posizione), senza stampare nomi o valori.

L'import marca `WasPostponed` sulla partita se il calendario la indica come rinviata o se un 6 d'ufficio la attesta. `ActualOfficialVotesReady` viene valorizzato solo per le partite rinviate: `true` se il recupero è giocato e entrambi i club hanno voti ufficiali reali, `false` altrimenti. Per le altre partite resta `null` e vale il controllo sui voti in fase di calcolo.

## Schema, versione e rollback

- Migration `20261028112843_PostponedMatchPoliciesAndLegacyVotes`: nuove colonne su partite, giornate, regolamenti e classifica iniziale, più la tabella `LegacyPoliticalVoteEntries`. Il backfill copia `Delayed` in `WasPostponed` e `KickoffUtc` in `OriginalKickoffUtc`. `ActualOfficialVotesReady` è `false` per le partite rinviate, `null` per le altre.
- Il downgrade è rifiutato se perderebbe informazioni: 6 d'ufficio salvati, impostazioni non default, attese o classifiche provvisorie, recuperi giocati o orari originali diversi. Senza queste informazioni lo schema può essere smontato. Per tornare indietro con dati presenti serve un backup verificato.
- MappingVersion 37. Nessuna nuova opzione env; risoluzioni utenti e byte del dump restano invariati.

Per l'import reale rianalizza il dump verificato con `-UseExistingDump`, senza reset e senza `-Apply`; usa `-Apply` solo dopo un'analisi riuscita e la revisione del report.
