# Chiavi naturali di calciatori, stagioni e club

## Contratto

La lista di fantacalcio.it è la fonte dei nomi disambiguati. La PlayerKey è il nome normalizzato, senza anno. La riga annuale è identificata dalla coppia anno/PlayerKey: **non esiste un campo YearlyPlayerKey persistito**. Le PK e le FK SQL sono realmente naturali, non stringhe aggiunte a relazioni UUID.

| Campo | Scopo |
|---|---|
| `Players.PlayerKey` | PK globale testuale, per esempio `osimhen`, prodotta dalla normalizzazione legacy. |
| `PlayerSeasons.SeasonIndex` | Anno nel formato già usato dall'app: `1` = 2012/13, `11` = 2022/23. Non è l'anno solare. |
| `PlayerSeasons.(PlayerKey, SeasonIndex)` | PK composta della stagione, con FK `PlayerKey → Players.PlayerKey`. |
| `Clubs.ClubCode` | PK/FK testuale: abbreviazione della fonte di esattamente tre lettere minuscole, per esempio `nap`. |
| `Players.LegacyId`, `Clubs.LegacyId` | Vecchi GUID, nullable e conservati solo per tracciare la conversione. Non sono le FK del dominio. |

Le PK impongono unicità globale e annuale; i vincoli rifiutano PlayerKey vuote/non canoniche e codici club non validi. Nei modelli C# e in alcuni DTO restano i nomi compatibili `Id`, `PlayerId` e `ClubId`, ma **i loro valori sono le chiavi testuali**, non UUID. Le colonne SQL si chiamano `PlayerKey`, `ClubCode`, `HomeClubCode`, ecc. La notazione `11/osimhen` può essere composta per visualizzazione: l'informazione SQL resta nei due campi separati.

Il codice club deve essere esplicitamente presente nella fonte, oppure il nome deve essere già un codice di tre lettere. Il fallback legacy che mostra le prime tre lettere di un nome completo non viene usato per inventare una PK: un club con solo `n:"Napoli"` e senza codice si ferma con `invalid_club_code`, mentre `n:"nap"` è già identificabile. La visualizzazione legacy rimane invariata.

Le classifiche legacy hanno soltanto il nome: possono collegarsi a un'etichetta esatta e univoca di un club il cui codice è già verificato nel piano. Se più codici hanno quell'etichetta l'import si ferma con `ambiguous_real_rank_club`; se il club non è noto, un nome completo senza codice non permette di crearne uno.

## Dipendenze

`RosterPlayers`, voti, probabilità, statistiche e gli altri snapshot annuali hanno FK verso `(PlayerKey, SeasonIndex)`; i riferimenti globali senza anno usano `PlayerKey`. I club sono collegati tramite `ClubCode`. L'anno delle rose è già nello scope dello snapshot/squadra annuale: non viene duplicato dentro una stringa. La FK impedisce di usare un calciatore globale per un anno privo della sua riga stagionale.

Gli archivi che non dichiarano una stagione non ricevono un anno inventato. Le snapshot iniziali mantengono l'anno calcistico della competizione separato da `TeamOriginYear` della squadra.

Per tracciare in sola lettura le rose collegate a una chiave:

```sql
SELECT s."PlayerKey", s."SeasonIndex", p."Name", r."GroupId", r."TeamId", r."Kind", r."Day"
FROM "PlayerSeasons" s
JOIN "Players" p ON p."PlayerKey" = s."PlayerKey"
LEFT JOIN "RosterPlayers" r ON r."PlayerKey" = s."PlayerKey" AND r."SeasonIndex" = s."SeasonIndex"
WHERE s."PlayerKey" = 'osimhen';
```

## API e lookup leggibile

Catalogo, voti, probabilità, statistiche e rose espongono `playerKey` e `seasonIndex`. Il compatibile `playerId` contiene la stessa PlayerKey. I club espongono `clubCode`; il compatibile `clubId` contiene quel codice. Le letture richiedono un account autenticato:

```http
GET /api/football/players/by-key/osimhen
GET /api/football/11/players/by-key/osimhen
```

La prima restituisce l'anagrafica e gli anni disponibili; la seconda la riga dell'anno preciso. I comandi ricevono chiavi canoniche nel contesto autorizzato di gruppo/anno: il client non può assegnare un nome libero o modificare prezzi/ruoli per cambiare l'identità.

Il vecchio alias `key` coincide con la PlayerKey. La normalizzazione legacy resta invariata: nessuna decodifica HTML o unione automatica di alias.

## Import e aggiornamenti

Una riga di catalogo priva di nome **non crea un calciatore**, anche senza regole JSON: è conservata come originale irrisolto in `UnresolvedPlayerCatalogEntries`. I pin opzionali continuano a controllare checksum e conteggio se configurati. Le righe identiche non vengono collassate, i flag originali non sono alterati e sorgente/dump non vengono riscritti.

Quando una rosa contiene un nome valido assente dal catalogo, l'import crea la sua anagrafica globale e/o la riga dell'anno mancante, usando i metadati della rosa; `recoveredRosterPlayers` ne registra sorgente, posizione e tipo di recupero senza loggare i valori. Le eventuali proprietà esplicite `Key`/`PlayerKey` devono concordare con il nome. Nel JSON compatto delle rose **`k` è la posizione in formazione**, non la PlayerKey. Una chiave esplicita già nota può recuperare il nome dall'anagrafica; senza nome né chiave verificabile l'import della rosa resta bloccato, senza perdere righe economiche o inventare identità da club/ruolo/ordine.

La migration costruisce prima mappe verificate dai vecchi GUID a PlayerKey/codici, conserva i GUID in `LegacyId`, poi converte PK e FK nella stessa transazione. Chiavi vuote/ambigue, collisioni e codici club mancanti/duplicati fermano tutto senza merge o cast di UUID a testo. Il downgrade è consentito soltanto con grafo calcistico vuoto; per dati presenti serve un backup verificato, non GUID ricostruiti. Utenti, ruoli e confini di autorizzazione non vengono cambiati.
