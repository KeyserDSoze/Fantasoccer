# Club globali, nome ufficiale e appartenenza annuale alla Serie A

## Modello SQL

| Tabella/campo | Identità e significato |
|---|---|
| `Clubs.ClubCode` | PK globale `varchar(3)` minuscola, per esempio `juv`. Nessun ID numerico o GUID nelle FK del club. |
| `Clubs.Name` | Nome ufficiale unico per i codici del [catalogo verificato](CLUB-CODES.md), per esempio `Juventus`. Non dipende dal primo alias o anno letto. |
| `Clubs.Abbreviation` | Campo compatibile vincolato a essere uguale a `ClubCode`. |
| `Clubs.LegacyId` | GUID nullable conservato solo per tracciabilità delle vecchie importazioni. |
| `ClubSeasons.(ClubCode, SeasonIndex)` | PK composta della presenza annuale attestata, con FK al club globale e a `Seasons`. Nessuna stringa concatenata o nuova chiave tecnica. |
| `ClubSeasons.Sources` | Provenienza dell'appartenenza: catalogo club = 1, calendario reale = 2, classifica reale = 4; i valori si combinano come bitmask. |
| `PlayerSeasons.ClubCode` | FK testuale al club globale: club corrente del giocatore **per quella stagione**. |

Il modello CLR mantiene alcuni nomi compatibili `Id`/`ClubId`, ma i valori e le colonne SQL del dominio sono codici testuali. `SeasonIndex` usa l'indice dell'app (`1 = 2012/13`), non l'anno solare.

## Normalizzazione unica del nome

`Juve`, `JUVE`, `Juventus`, `JUVENTUS` e il codice `juv` convergono su `ClubCode=juv`, `Name=Juventus`, `Abbreviation=juv`. Il nome viene risolto **dal codice verificato**, non dall'ordine dei documenti. Gli alias e il confronto case-insensitive restano finiti ed espliciti; un nome/codice discordante continua a essere rifiutato dal lettore.

Import e ingestione usano la stessa tabella. Anche `SaveChanges`/`SaveChangesAsync` normalizzano i club aggiunti o modificati, senza cambiare la PK; i CHECK SQL impediscono che un codice conosciuto sia salvato con un alias invece del nome ufficiale o con un'abbreviazione diversa dal codice. I writer e la migration rifiutano una contraddizione già presente fra codice, nome verificato e sigla esplicita, anziché mascherarla con una rietichettatura. La migration aggiorna le anagrafiche coerenti già presenti, conservando codice e `LegacyId`. Per codici espliciti validi non ancora catalogati rimane il nome fornito dalla fonte, senza inventare un nome ufficiale o alias; nuove corrispondenze richiedono verifica.

Questo riguarda i **club reali**, non nomi delle squadre fantasy, utenti o giocatori. Sorgente Azure e byte del dump rimangono invariati, incluse le vecchie etichette; non si riscrivono i valori economici o i risultati.

## Presenza annuale: evidenza, non deduzione dai giocatori

Una riga in `ClubSeasons` è creata dall'import soltanto se il club compare per quell'anno in almeno una fonte pertinente:

- `realteamwrapper`: catalogo annuale dei club;
- `realcalendar`: casa/trasferta di una partita reale;
- `realrank`: classifica reale.

La stessa coppia codice/anno compare una sola volta e accumula le fonti. Le rose storiche, gli archivi senza anno e i giocatori inattivi non sono una prova che il loro vecchio club partecipasse alla Serie A in quell'anno: **non creano appartenenze annuali**. Per questo la FK del giocatore e degli snapshot punta al club globale, non obbliga a creare una falsa riga Serie A. Un'assenza dalla tabella significa appartenenza non attestata dai dati disponibili, non necessariamente retrocessione dimostrata.

L'ingestione aggiorna le appartenenze tramite calendario/classifica reali. Non cancella la presenza annuale di un club soltanto perché un giocatore si trasferisce.

Il marker interno `FootballDatasets.Kind=clubseasoncatalogue` identifica gli anni il cui catalogo club esplicito è stato completamente elaborato; il report espone `clubs`, `clubSeasons`, `clubCatalogueYears`. Il marker attesta il catalogo fornito, non un confronto indipendente con tutti i dati del provider. Gli anni ricostruiti solo da calendario/classifica possono essere parziali e non sono marcati completi.

## Trasferimenti e storico

Per un trasferimento nella stagione 15 basta aggiornare `PlayerSeasons.ClubCode` della coppia giocatore/stagione, per esempio `fio → nap`. La PlayerKey non cambia; la join punta subito all'anagrafica `Napoli`.

Non vengono cambiati il club delle stagioni precedenti, i voti già salvati o le formazioni di giornate passate. L'ingestione del catalogo aggiorna disponibilità della rosa corrente e snapshot futuri secondo le regole già esistenti, preservando prezzi/ricavi. I voti storici mantengono il proprio `ClubCode`: leggere il club corrente non deve sostituire quello dello snapshot.

Se il [voto legacy non conteneva il club](LEGACY-VOTE-SNAPSHOTS.md), il suo codice resta NULL anche quando è noto il club annuale. Quest'ultimo può essere completato da una rosa stagionale dello stesso anno solo se non esistono codici concorrenti; non viene così creata una presenza in Serie A o una falsa attribuzione storica.

## Letture autenticate

```http
GET /api/football/clubs
GET /api/football/15/clubs
GET /api/football/clubs/by-key/juv
GET /api/football/clubs/by-key/Juve
```

La prima elenca tutte le anagrafiche globali. La seconda restituisce `seasonIndex`, `teams` e `isComplete` (catalogo esplicito elaborato); non include club noti soltanto tramite una rosa o un giocatore storico. La lettura per chiave/alias restituisce il club canonico e gli anni attestati. Un club globale senza appartenenze note può avere `years: []`. Restano autenticazione e blocco degli account globalmente disabilitati.

```sql
SELECT s."SeasonIndex", c."ClubCode", c."Name", s."Sources"
FROM "ClubSeasons" s
JOIN "Clubs" c ON c."ClubCode" = s."ClubCode"
WHERE s."SeasonIndex" = 15
ORDER BY c."Name";
```

## Upgrade e sicurezza

La migration aggiuntiva `ClubSeasonsAndCanonicalNames` normalizza soltanto le anagrafiche dei club e crea la tabella annuale. Ricostruisce le appartenenze già verificabili da `FootballMatches` e `FootballClubRanks`; **non** incrocia tutti i club globali con tutti gli anni e non deduce appartenenze da `PlayerSeasons`. Il vecchio schema non conservava il contenuto annuale del catalogo club, quindi un marker `realteamwrapper` precedente da solo non consente di ricostruirlo o dichiararlo completo.

Una nuova analisi/import del dump verificato, con MappingVersion 43 e le risoluzioni utenti già approvate, ricostruisce i cataloghi esatti se la finestra di reimport è ancora aperta. Non si scavalca il blocco dopo scritture applicative e non si suggerisce un reset per aggirarlo. Il downgrade con club, giocatori o appartenenze presenti è rifiutato; per rollback di dati presenti occorre un backup verificato. Lo schema vuoto può essere smontato e ricreato. Nessuna migration pubblicata precedente viene modificata. Un futuro cambiamento ai nomi/codici canonici deve aggiornare anche i CHECK tramite una nuova migration, non modificare l'SQL congelato nelle migration pubblicate.
