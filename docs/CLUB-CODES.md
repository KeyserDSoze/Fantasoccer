# Codici club verificati: compatibilità con i nomi legacy

## Fonte e versione

`FantacalcioClubCodes` contiene un elenco **finito ed esatto** di 39 nomi canonici/codici e 45 alias espliciti, per 84 etichette accettate: 35 coppie verificate sulla fonte fantacalcio.it, quattro integrazioni storiche, gli alias trovati nei dati legacy (`Juve`, `Hellas Verona`, `Como 1907`, `Venezia FC`) e i nomi ufficiali dei club verificati sui loro siti, documentati sotto, più una variante di sigla del provider (`hel → ver`). Versione `fantacalcio-2026-10-28-v8`. Non deriva sigle dai prefissi dei nomi e non effettua richieste HTTP durante analisi, import o ingestione. I codici sono salvati in minuscolo come PK/FK reali; anche il nome salvato è ora quello ufficiale determinato dal codice, indipendentemente da anno/casing/primo alias letto.

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

### Alias trovati nei dati legacy

| Etichetta legacy esatta | Nome canonico del catalogo | Codice | Fonte |
|---|---|---|---|
| Juve | Juventus | `juv` | [Sito ufficiale del club: La Juve in cima al mondo / La storia di Juventus](https://www.juventus.com/it/club/la-storia/la-juve-in-cima-al-mondo); codice Juventus già verificato nelle fonti fantacalcio.it sopra |
| Hellas Verona | Verona | `ver` | [Sito ufficiale del club: Hellas Verona FC](https://www.hellasverona.it/) (Hellas Verona Football Club S.p.A.); codice Verona già verificato nelle fonti fantacalcio.it sopra. Etichetta trovata nel dump nel calendario 2024/25 e nelle classifiche reali 2024/25 e 2025/26 (anche in maiuscolo). |
| Como 1907 | Como | `com` | [Sito ufficiale del club: Como 1907](https://www.comofootball.com/); etichetta del provider nel calendario legacy 2024/25. |
| Venezia FC | Venezia | `ven` | [Sito ufficiale del club: Venezia FC](https://www.veneziafc.it/); etichetta del provider nel calendario legacy 2024/25. |

### Nomi ufficiali dei club (per i dati futuri)

Per decisione del gestore, il catalogo v8 accetta anche i nomi ufficiali dei club, così un cambio di formato del provider non blocca l'ingestione. Ogni etichetta è stata letta **testualmente** sul sito ufficiale del club il 28 ottobre 2026 (titolo della pagina o testo della home); le varianti con o senza punti sono incluse solo se compaiono sul sito. I codici erano già verificati nelle fonti sopra.

| Nome canonico | Codice | Alias esatti | Sito ufficiale |
|---|---|---|---|
| Atalanta | `ata` | Atalanta Bergamasca Calcio | https://www.atalanta.it/ |
| Benevento | `ben` | Benevento Calcio | https://www.beneventocalcio.club/ |
| Bologna | `bol` | Bologna Football Club 1909, Bologna FC | https://www.bolognafc.it/ |
| Cagliari | `cag` | Cagliari Calcio | https://cagliaricalcio.com/ |
| Catania | `cat` | Catania FC | https://www.cataniafc.it/ |
| Cesena | `ces` | Cesena FC | https://www.cesenafc.com/ |
| Cremonese | `cre` | US Cremonese, U.S. Cremonese | https://uscremonese.it/ |
| Crotone | `cro` | FC Crotone, F.C. Crotone | https://www.fccrotone.it/ |
| Fiorentina | `fio` | ACF Fiorentina | https://www.acffiorentina.com/ |
| Frosinone | `fro` | Frosinone Calcio | https://www.frosinonecalcio.com/ |
| Genoa | `gen` | Genoa CFC, Genoa Cricket and Football Club | https://genoacfc.it/ |
| Inter | `int` | FC Internazionale Milano | https://www.inter.it/ |
| Juventus | `juv` | Juventus FC, Juventus Football Club | https://www.juventus.com/ |
| Lazio | `laz` | S.S. Lazio | https://www.sslazio.it/ |
| Lecce | `lec` | US Lecce, U.S. Lecce, Unione Sportiva Lecce | https://uslecce.it/ |
| Milan | `mil` | AC Milan | https://www.acmilan.com/ |
| Monza | `mon` | AC Monza, A.C. Monza, Associazione Calcio Monza | https://www.acmonza.com/ |
| Napoli | `nap` | SSC Napoli | https://sscnapoli.it/ |
| Palermo | `pal` | Palermo FC, Palermo F.C. | https://www.palermofc.com/ |
| Parma | `par` | Parma Calcio 1913 | https://www.parmacalcio1913.com/ |
| Pescara | `pes` | Pescara Calcio 1936 | https://www.pescaracalcio.com/ |
| Roma | `rom` | AS Roma | https://www.asroma.com/ |
| Salernitana | `sal` | U.S. Salernitana 1919 | https://salernitana.it/ |
| Sampdoria | `sam` | U.C. Sampdoria | https://www.sampdoria.it/ |
| Sassuolo | `sas` | U.S. Sassuolo, U.S. Sassuolo Calcio | https://www.sassuolocalcio.it/ |
| Spezia | `spe` | Spezia Calcio | https://www.speziacalcio.com/ |
| Torino | `tor` | Torino FC, Torino FC 1906 | https://www.torinofc.it/ |
| Udinese | `udi` | Udinese Calcio | https://www.udinese.it/ |
| Verona | `ver` | Hellas Verona FC | https://www.hellasverona.it/ |

Non sono stati aggiunti alias per Brescia, Carpi, Chievo, Empoli, Livorno, Pisa, Siena e SPAL: alla data della verifica i loro siti non rispondevano, non mostravano il nome o (per SPAL) il dominio ospitava un altro sito. Per questi club valgono il nome canonico e il codice; un alias si aggiunge solo dopo una nuova verifica.

In totale il catalogo accetta 39 nomi canonici e 45 alias. Nel database si salvano sempre e soltanto il codice e il nome canonico (per esempio `com`/"Como", `ven`/"Venezia"): il vincolo `CK_Clubs_CanonicalName` impedisce di salvare un alias come nome di un codice noto. Gli alias servono solo a riconoscere l'etichetta originale, anche quando manca la sigla; le etichette originali restano soltanto nel dump.

`Aliases` è un dizionario immutabile separato da `Names`: un alias non introduce un altro club o un'altra PK. La corrispondenza è esatta, non per prefisso o somiglianza: `Juve Next Gen`, `Juve Women`, `Milan Futuro`, `Hellas Verona Primavera`, `Como 1907 Women`, `SS Lazio` (senza punti, non presente sul sito) o un nome simile non vengono accorpati automaticamente. `Spal` non richiede un altro alias, perché il confronto di `SPAL` è già case-insensitive.

### Sigle del provider

| Sigla esplicita | Codice verificato | Accettata solo con | Evidenza |
|---|---|---|---|
| `hel` | `ver` | il nome verificato del Verona (`Verona` o un suo alias) | Il calendario legacy 2024/25 (archivio `blobs/9f2852d838ac1529e5aecbb31df22a73cb67568e9cc616b23b11ac602d5923d2.bin`) usa per lo stesso club sia `Hellas verona/hel` sia `Hellas verona/ver`. |

`CodeVariants` contiene solo le sigle che **sembrano** codici validi di tre lettere ma differiscono da quello verificato: vanno documentate una per una. Insieme a un altro club verificato (per esempio `Juventus/hel`) restano un conflitto; con un nome non catalogato `hel` resta il codice esplicito `hel` come prima. Senza `hel` il calendario 2024/25 avrebbe creato in silenzio un secondo club, e la classifica non avrebbe potuto scegliere fra le due sigle.

Per decisione del gestore, quando il nome è verificato (canonico o alias), un'etichetta esplicita che **non è** un codice di tre lettere ASCII viene ignorata, perché non può contraddire il nome: per esempio `MONZ`, che il provider Gazzetta usa per il Monza in tutte le 38 giornate del 2026/27 (API `https://api2-mtc.gazzetta.it/api/v1/sports/calendar?day=<1-38>&sportId=1&competitionId=21`, verificata il 28 ottobre 2026; le altre 19 squadre hanno sigle di tre lettere coerenti). Non serve più una variante per ciascuna di queste etichette. I recuperi vengono dichiarati in `resolvedClubCodes`.

La regressione comprende l'intera unione pubblica dei cataloghi multi-anno: **62 coppie nome/abbreviazione, 40 etichette e 39 codici distinti**, comprese tutte le 23 coppie con sigla esplicita e le 39 senza sigla. Il test esegue analisi/import insieme per tutti i campioni, ripete con ordine invertito e verifica idempotenza e immutabilità della sorgente; una seconda prova collega rose, voti e anni del giocatore attraverso `Juve`/`Juventus`, preservando prezzi/ricavi e rifiutando una sigla discordante. Non si procede più verificando soltanto il catalogo di un singolo anno.

## Regole di risoluzione

1. Con un nome verificato (canonico o alias), il club è deciso dal nome. Una sigla esplicita che non è un codice di tre lettere ASCII (per esempio `MONZ`, `FIO!`, `fi`) viene ignorata e il recupero dichiarato nel report.
2. Con un nome verificato, un codice esplicito **di tre lettere** discordante viene rifiutato con `club_code_name_mismatch`, non sovrascritto: per esempio `Fiorentina/NAP` è un conflitto. L'unica eccezione è una variante di sigla documentata sopra.
3. Senza sigla, un nome presente nella tabella risolve il codice: `{"n":"Fiorentina","a":null}` diventa il riferimento SQL `fio` senza modificare il documento originale.
4. Il confronto dei nomi e degli alias espliciti ignora soltanto maiuscole/minuscole e spazi esterni. Non usa prefissi, somiglianza, rimozione di "FC"/"Calcio"/anni, decodifica HTML, ordine degli array o associazioni di giocatori.
5. Un nome già composto da tre lettere rimane un codice leggibile come prima. Un'etichetta non catalogata, né elencata tra gli alias esatti, richiede un codice esplicito valido di tre lettere: senza, l'import si ferma e l'ingestione SQL rifiuta il dato senza salvarlo.

Il report `resolvedClubCodes` registra documento SHA-256, container, archivio hash, posizione JSON e versione del catalogo per ogni riferimento recuperato, una sola volta per posizione. Non stampa nomi, codici, giocatori o proprietari. Nell'import la `MappingVersion` è 41; restano i controlli sulla sorgente, l'atomicità, l'idempotenza e il blocco dopo scritture applicative. La migration aggiuntiva `ClubSeasonsAndCanonicalNames` allinea i nomi già presenti e introduce le appartenenze annuali attestate, senza cambiare PK/FK testuali, sorgente o dump. `OptionalHistoricalVoteClubs` consente di preservare i [voti legacy senza club](LEGACY-VOTE-SNAPSHOTS.md) senza inventarne uno. Non occorre una nuova opzione env. Le label originali rimangono nel dump: riconoscere un alias non riscrive il documento. Vedere [modello e sicurezza dell'upgrade](CLUB-SEASONS.md).

Un'aggiunta alla tabella richiede una nuova corrispondenza verificata, aggiornamento della versione/documentazione e test; non si aggiungono alias soltanto per superare un errore dell'import.
