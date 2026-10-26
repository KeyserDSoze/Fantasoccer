# Identità dei calciatori: ID e PlayerKey stagionale

## Contratto

La lista di fantacalcio.it è la fonte dei nomi disambiguati. L'unicità richiesta viene garantita anche da PostgreSQL: **un nome normalizzato identifica un solo calciatore nella stessa stagione**. Una collisione viene rifiutata, non risolta unendo righe.

| Campo | Scopo |
|---|---|
| `Players.Id` / `playerId` | GUID tecnico stabile del calciatore; è usato dalle foreign key. |
| `PlayerSeasons.SeasonIndex` | Anno nel formato già usato dall'app: `1` = 2012/13, `11` = 2022/23. Non è l'anno solare. |
| `PlayerSeasons.NormalizedName` | Chiave del nome prodotta dalla normalizzazione legacy `KeyUtils.GetPlayerKey`. Non dipende da club o ruolo. |
| `PlayerSeasons.PlayerKey` | Colonna generata e persistita da SQL: `<SeasonIndex>:<NormalizedName>`, per esempio `11:osimhen`. |

`IX_PlayerSeasons_PlayerKey` è un **indice UNIQUE**. La PK della riga stagionale rimane `(PlayerId, SeasonIndex)`; non vengono ricreati GUID o spostate dipendenze. Un vincolo rifiuta chiavi normalizzate vuote o non canoniche. La stessa parte nome in due anni produce due PlayerKey differenti.

## Dipendenze tramite ID

La rosa non deve copiare il nome come foreign key. `RosterPlayers`, voti, probabilità, statistiche e gli altri snapshot stagionali sono già collegati alla riga dell'anno attraverso `PlayerId` e `SeasonIndex` (anche quando l'anno è una colonna di scope ereditata dallo snapshot). La FK impedisce di usare un giocatore di un altro anno solo perché esiste in `Players`.

Gli archivi che non dichiarano una stagione continuano a riferirsi all'ID del calciatore senza inventare un anno. Le snapshot iniziali hanno l'anno della competizione/voti e mantengono separato `TeamOriginYear`: la PlayerKey segue l'anno calcistico dello snapshot, non quello della vecchia squadra.

Per tracciare in sola lettura le rose collegate a una chiave:

```sql
SELECT s."PlayerKey", s."PlayerId", p."Name", r."GroupId", r."TeamId", r."Kind", r."Day"
FROM "PlayerSeasons" s
JOIN "Players" p ON p."Id" = s."PlayerId"
LEFT JOIN "RosterPlayers" r ON r."PlayerId" = s."PlayerId" AND r."SeasonIndex" = s."SeasonIndex"
WHERE s."PlayerKey" = '11:osimhen';
```

## API e lookup leggibile

Catalogo, voti, probabilità, statistiche e rose espongono `playerId`, `seasonIndex` e `playerKey`. La ricerca della chiave restituisce lo stesso ID usato dai comandi:

```http
GET /api/football/11/players/by-key/11%3Aosimhen
```

La query usa la colonna indicizzata e richiede un account autenticato come le altre letture condivise. L'anno della URL e quello della chiave devono coincidere. I comandi di formazione/mercato/aste continuano a ricevere gli ID nel contesto del gruppo e della stagione, non nomi scelti dal client.

Il vecchio campo `key` senza anno rimane un alias per ricerca, calcoli legacy e nomi delle immagini. Anche gli alias nome-only di alcuni flussi immagini/ottimizzazione restano compatibili: non sono foreign key né sostituiscono `playerId` e anno. Nessuna normalizzazione HTML o unione automatica di alias viene introdotta.

## Import e aggiornamenti

L'import e i writer SQL valorizzano il nome normalizzato; SQL compone la PlayerKey e ne impone l'unicità. Ingestione catalogo/voti cerca prima l'identità della stagione, conserva gli ID e non aggira la validazione del provider quando trova già una chiave.

La migration riusa le chiavi già registrate in `LegacyIdentityMaps` per il backfill. Senza una chiave registrata accetta solo un backfill ASCII verificabile; ambiguità, normalizzazioni non verificabili, chiavi vuote e collisioni fanno fallire l'intera migration senza unire, cancellare o riassegnare ID. Il downgrade rifiuta di eliminare le chiavi con righe stagionali presenti.

I record di catalogo senza nome e senza chiave indipendente **non possono avere una PlayerKey**. La conservazione approvata in `UnresolvedPlayerCatalogEntries` li mantiene separati dai calciatori utilizzabili; il GUID di una riga d'archivio non è un PlayerId e non può essere usato per collegare una rosa.
