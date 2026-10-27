# Codici club verificati: compatibilità con i nomi legacy

## Fonte e versione

`FantacalcioClubCodes` contiene un elenco **finito ed esatto** di 39 nomi canonici/codici e un alias esplicito, per 40 etichette accettate: 35 coppie verificate sulla fonte fantacalcio.it, quattro integrazioni storiche e `Juve → Juventus` documentati sotto, versione `fantacalcio-2026-10-27-v4`. Non deriva sigle dai prefissi dei nomi e non effettua richieste HTTP durante analisi, import o ingestione. I codici sono salvati in minuscolo come PK/FK reali; anche il nome salvato è ora quello ufficiale determinato dal codice, indipendentemente da anno/casing/primo alias letto.

Verifica pubblica del 26 ottobre 2026: pagine della prima giornata delle stagioni da 2015/16 a 2026/27. Ogni pagina espone insieme il nome completo (`meta itemprop="name"`) e la sigla nel relativo link squadra. Il titolo della pagina è stato controllato contro la stagione richiesta: una URL storica che restituisce la stagione corrente non costituisce una prova per quella stagione. Le 240 coppie osservate sulle 12 pagine hanno prodotto 35 nomi/codici distinti, senza discrepanze tra anni.

URL delle fonti: `https://www.fantacalcio.it/voti-fantacalcio-serie-a/<stagione>/1`, dove la stagione usa il formato `2022-23`. La colonna finale indica una delle pagine su cui la coppia è stata verificata, non l'anno di fondazione o l'unico anno in cui il club è ammesso.

Integrazione del 27 ottobre 2026 per le stagioni iniziali dell'app (2012/13–2014/15): Catania, Siena, Livorno e Cesena. Queste quattro coppie non vengono attribuite alle pagine fantacalcio.it del periodo 2015–2026: le URL precedenti non espongono l'archivio richiesto e non sono una prova. Le fonti storiche esplicite sono:

- **H1**: [Siena–Catania, 9 dicembre 2012, StatMuse](https://www.statmuse.com/fc/match/12-9-2012-sie-vs-cat-82633), con `Siena SIE` e `Catania CAT`, confermati anche nella [pagina della stessa partita TNT Sports](https://www.tntsports.co.uk/football/serie-a-1/2012-2013/live-siena-catania_mtc535761/live.shtml).
- **H2**: [Livorno–Catania, 15 settembre 2013, StatMuse](https://www.statmuse.com/fc/match/9-15-2013-liv-vs-cat-82884), con `Livorno LIV` e `Catania CAT`.
- **H3**: [Classifica Serie A 2014/15, TNT Sports](https://www.tntsports.co.uk/football/serie-a/2014-2015/standings.shtml), con `Cesena CES`.

L'estensione copre i quattro nomi della Serie A delle prime tre stagioni assenti dalla tabella iniziale. È una scelta esplicita di codici documentati, non una deduzione dai prefissi né un'unificazione di sigle di provider differenti: per esempio `Siena/SNA` rimane un conflitto, non un alias automatico di `sie`. I nomi delle società successive o varianti non elencate non vengono accorpati per somiglianza.

| Nome esatto della fonte | Codice | Pagina verificata |
|---|---|---|
| Atalanta | `ata` | 2015-16/1 |
| Benevento | `ben` | 2017-18/1 |
| Bologna | `bol` | 2015-16/1 |
| Brescia | `bre` | 2019-20/1 |
| Cagliari | `cag` | 2016-17/1 |
| Carpi | `car` | 2015-16/1 |
| Catania | `cat` | H1, H2 |
| Cesena | `ces` | H3 |
| Chievo | `chi` | 2015-16/1 |
| Como | `com` | 2024-25/1 |
| Cremonese | `cre` | 2022-23/1 |
| Crotone | `cro` | 2016-17/1 |
| Empoli | `emp` | 2015-16/1 |
| Fiorentina | `fio` | 2022-23/1 |
| Frosinone | `fro` | 2015-16/1 |
| Genoa | `gen` | 2015-16/1 |
| Inter | `int` | 2015-16/1 |
| Juventus | `juv` | 2015-16/1 |
| Lazio | `laz` | 2015-16/1 |
| Lecce | `lec` | 2019-20/1 |
| Livorno | `liv` | H2 |
| Milan | `mil` | 2015-16/1 |
| Monza | `mon` | 2022-23/1 |
| Napoli | `nap` | 2015-16/1 |
| Palermo | `pal` | 2015-16/1 |
| Parma | `par` | 2018-19/1 |
| Pescara | `pes` | 2016-17/1 |
| Pisa | `pis` | 2025-26/1 |
| Roma | `rom` | 2015-16/1 |
| Salernitana | `sal` | 2021-22/1 |
| Sampdoria | `sam` | 2015-16/1 |
| Sassuolo | `sas` | 2015-16/1 |
| Siena | `sie` | H1 |
| SPAL | `spa` | 2017-18/1 |
| Spezia | `spe` | 2020-21/1 |
| Torino | `tor` | 2015-16/1 |
| Udinese | `udi` | 2015-16/1 |
| Venezia | `ven` | 2021-22/1 |
| Verona | `ver` | 2015-16/1 |

Esempi di verifica diretta: [2022/23](https://www.fantacalcio.it/voti-fantacalcio-serie-a/2022-23/1), [2015/16](https://www.fantacalcio.it/voti-fantacalcio-serie-a/2015-16/1), [2017/18](https://www.fantacalcio.it/voti-fantacalcio-serie-a/2017-18/1), [2019/20](https://www.fantacalcio.it/voti-fantacalcio-serie-a/2019-20/1), [2025/26](https://www.fantacalcio.it/voti-fantacalcio-serie-a/2025-26/1).

## Alias espliciti e copertura dei cataloghi di tutti gli anni

| Etichetta legacy esatta | Nome canonico del catalogo | Codice | Fonte |
|---|---|---|---|
| Juve | Juventus | `juv` | [Sito ufficiale del club: La Juve in cima al mondo / La storia di Juventus](https://www.juventus.com/it/club/la-storia/la-juve-in-cima-al-mondo); codice Juventus già verificato nelle fonti fantacalcio.it sopra |

`Aliases` è un dizionario immutabile separato da `Names`: l'alias non introduce un quarantesimo club o un'altra PK. La corrispondenza è esatta, non per prefisso: `Juve Next Gen`, `Juve Women` o un nome simile non vengono accorpati automaticamente. `Spal` non richiede un altro alias, perché il confronto di `SPAL` è già case-insensitive.

La regressione comprende l'intera unione pubblica dei cataloghi multi-anno: **62 coppie nome/abbreviazione, 40 etichette e 39 codici distinti**, comprese tutte le 23 coppie con sigla esplicita e le 39 senza sigla. Il test esegue analisi/import insieme per tutti i campioni, ripete con ordine invertito e verifica idempotenza e immutabilità della sorgente; una seconda prova collega rose, voti e anni del giocatore attraverso `Juve`/`Juventus`, preservando prezzi/ricavi e rifiutando una sigla discordante. Non si procede più verificando soltanto il catalogo di un singolo anno.

## Regole di risoluzione

1. Una sigla esplicita deve comunque avere tre lettere ASCII. Non viene corretta rimuovendo spazi interni, troncando caratteri o sostituendola con una deduzione.
2. Se il nome è nel catalogo, un codice esplicito discordante viene rifiutato con `club_code_name_mismatch`, non sovrascritto. Per esempio `Fiorentina/NAP` è un conflitto.
3. Soltanto se il codice è mancante/null/whitespace, un nome presente nella tabella può risolverlo: `{"n":"Fiorentina","a":null}` diventa il riferimento SQL `fio` senza modificare il documento originale.
4. Il confronto dei nomi e degli alias espliciti ignora soltanto maiuscole/minuscole e spazi esterni. Non usa prefissi, somiglianza, decodifica HTML, ordine degli array o associazioni di giocatori.
5. Un nome già composto da tre lettere rimane un codice leggibile come prima. Un'etichetta non catalogata, né elencata tra gli alias esatti, richiede un codice esplicito valido: l'elenco non pretende di coprire tutte le società storiche o ogni alias possibile.

Il report `resolvedClubCodes` registra documento SHA-256, container, archivio hash, posizione JSON e versione del catalogo per ogni riferimento recuperato, una sola volta per posizione. Non stampa nomi, codici, giocatori o proprietari. Nell'import la `MappingVersion` è 36; restano i controlli sulla sorgente, l'atomicità, l'idempotenza e il blocco dopo scritture applicative. La migration aggiuntiva `ClubSeasonsAndCanonicalNames` allinea i nomi già presenti e introduce le appartenenze annuali attestate, senza cambiare PK/FK testuali, sorgente o dump. `OptionalHistoricalVoteClubs` consente di preservare i [voti legacy senza club](LEGACY-VOTE-SNAPSHOTS.md) senza inventarne uno. Non occorre una nuova opzione env. Le label originali rimangono nel dump: riconoscere un alias non riscrive il documento. Vedere [modello e sicurezza dell'upgrade](CLUB-SEASONS.md).

Un'aggiunta alla tabella richiede una nuova corrispondenza verificata, aggiornamento della versione/documentazione e test; non si aggiungono alias soltanto per superare un errore dell'import.
