# Documentazione API Fantasoccer

## Indice
1. [Panoramica Generale](#panoramica-generale)
2. [Endpoint per Categoria](#endpoint-per-categoria)
3. [Modelli Dati Principali](#modelli-dati-principali)
4. [Endpoint Business Logic](#endpoint-business-logic)
5. [Enumerazioni](#enumerazioni)

---

## Panoramica Generale

Questa documentazione descrive le API REST per l'applicazione Fantasoccer, un sistema di gestione fantacalcio che include:

- **Gestione Gruppi e Leghe**: Creazione e gestione di gruppi di giocatori e leghe
- **Gestione Squadre**: CRUD per squadre fantasy e formazioni giornaliere
- **Giocatori Reali**: Database di giocatori della Serie A con statistiche
- **Calendario e Partite**: Gestione del calendario delle partite reali e fantasy
- **Mercato**: Sistema di scambi tra utenti
- **Classifiche e Statistiche**: Ranking e hall of fame
- **Aste**: Sistema di aste per l'acquisizione giocatori
- **Notifiche Push**: Sistema di notifiche
- **News**: Gestione delle news

### Struttura Base URL
Base URL: `/api/{controller}/{action}`

---

## Endpoint per Categoria

### 1. Gestione Identità e Autenticazione

#### AppIdentity
- **GET** `/api/AppIdentity/Get?key={key}` - Ottieni identità app
- **GET** `/api/AppIdentity/Delete?key={key}` - Elimina identità app
- **GET** `/api/AppIdentity/Exist?key={key}` - Verifica esistenza identità
- **POST** `/api/AppIdentity/Insert?key={key}` - Inserisci nuova identità
- **POST** `/api/AppIdentity/Update?key={key}` - Aggiorna identità
- **POST** `/api/AppIdentity/Query` - Query filtrata
- **POST** `/api/AppIdentity/Operation?op={operation}` - Operazioni personalizzate
- **POST** `/api/AppIdentity/Batch` - Operazioni batch
- **GET** `/api/AppIdentity/Bootstrap?key={key}` - Inizializzazione

### 2. Gestione Gruppi e Leghe

#### Group
- **GET** `/api/Group/Get?key={key}` - Ottieni gruppo
- **GET** `/api/Group/Delete?key={key}` - Elimina gruppo
- **GET** `/api/Group/Exist?key={key}` - Verifica esistenza gruppo
- **POST** `/api/Group/Insert?key={key}` - Crea nuovo gruppo
- **POST** `/api/Group/Update?key={key}` - Aggiorna gruppo
- **POST** `/api/Group/Query` - Query filtrata gruppi
- **POST** `/api/Group/Batch` - Operazioni batch gruppi

#### LiveGroup
- **GET** `/api/LiveGroup/Get?key={key}` - Ottieni gruppo live
- **POST** `/api/LiveGroup/Insert?key={key}` - Crea gruppo live
- **POST** `/api/LiveGroup/Update?key={key}` - Aggiorna gruppo live
- **POST** `/api/LiveGroup/Query` - Query gruppi live

### 3. Gestione Squadre

#### Team
- **GET** `/api/Team/Get?key={key}` - Ottieni squadra
- **GET** `/api/Team/Delete?key={key}` - Elimina squadra
- **POST** `/api/Team/Insert?key={key}` - Crea squadra
- **POST** `/api/Team/Update?key={key}` - Aggiorna squadra
- **POST** `/api/Team/Query` - Query squadre

#### Team/day (Formazioni Giornaliere)
- **GET** `/api/Team/day/Get?key={key}` - Ottieni formazione giornaliera
- **POST** `/api/Team/day/Insert?key={key}` - Crea formazione giornaliera
- **POST** `/api/Team/day/Update?key={key}` - Aggiorna formazione
- **POST** `/api/Team/day/Query` - Query formazioni giornaliere

#### TeamWithAi
- **GET** `/api/TeamWithAi/Get?key={key}` - Ottieni suggerimenti AI per squadra
- **POST** `/api/TeamWithAi/Insert?key={key}` - Inserisci suggerimento AI
- **POST** `/api/TeamWithAi/Update?key={key}` - Aggiorna suggerimento AI

### 4. Calendario e Partite

#### Calendar
- **GET** `/api/Calendar/Get?key={key}` - Ottieni calendario
- **POST** `/api/Calendar/Insert?key={key}` - Crea calendario
- **POST** `/api/Calendar/Update?key={key}` - Aggiorna calendario
- **POST** `/api/Calendar/Query` - Query calendario

#### PlayedGame
- **GET** `/api/PlayedGame/Get?key={key}` - Ottieni partita giocata
- **POST** `/api/PlayedGame/Insert?key={key}` - Inserisci partita giocata
- **POST** `/api/PlayedGame/Update?key={key}` - Aggiorna partita
- **POST** `/api/PlayedGame/Query` - Query partite giocate

### 5. Giocatori e Statistiche

#### RealPlayersWrapper
- **GET** `/api/RealPlayersWrapper/Get?key={key}` - Ottieni giocatori reali
- **POST** `/api/RealPlayersWrapper/Insert?key={key}` - Inserisci giocatori reali
- **POST** `/api/RealPlayersWrapper/Update?key={key}` - Aggiorna giocatori reali
- **POST** `/api/RealPlayersWrapper/Query` - Query giocatori reali

#### StatPlayersWrapper
- **GET** `/api/StatPlayersWrapper/Get?key={key}` - Ottieni statistiche giocatori
- **POST** `/api/StatPlayersWrapper/Insert?key={key}` - Inserisci statistiche
- **POST** `/api/StatPlayersWrapper/Update?key={key}` - Aggiorna statistiche
- **POST** `/api/StatPlayersWrapper/Query` - Query statistiche giocatori

#### VotedRealPlayerWrapper/live
- **GET** `/api/VotedRealPlayerWrapper/live/Get?key={key}` - Ottieni voti live giocatori
- **POST** `/api/VotedRealPlayerWrapper/live/Insert?key={key}` - Inserisci voti live
- **POST** `/api/VotedRealPlayerWrapper/live/Update?key={key}` - Aggiorna voti live

#### ChancedRealPlayerWrapper
- **GET** `/api/ChancedRealPlayerWrapper/Get?key={key}` - Ottieni probabilità formazione
- **POST** `/api/ChancedRealPlayerWrapper/Insert?key={key}` - Inserisci probabilità
- **POST** `/api/ChancedRealPlayerWrapper/Update?key={key}` - Aggiorna probabilità

### 6. Classifiche e Hall of Fame

#### Rank
- **GET** `/api/Rank/Get?key={key}` - Ottieni classifica
- **POST** `/api/Rank/Insert?key={key}` - Crea classifica
- **POST** `/api/Rank/Update?key={key}` - Aggiorna classifica
- **POST** `/api/Rank/Query` - Query classifiche

#### HallOfFame
- **GET** `/api/HallOfFame/Get?key={key}` - Ottieni hall of fame
- **POST** `/api/HallOfFame/Insert?key={key}` - Inserisci record hall of fame
- **POST** `/api/HallOfFame/Update?key={key}` - Aggiorna hall of fame
- **POST** `/api/HallOfFame/Query` - Query hall of fame

### 7. Mercato

#### MarketWrapper
- **GET** `/api/MarketWrapper/Get?key={key}` - Ottieni mercato
- **POST** `/api/MarketWrapper/Insert?key={key}` - Inserisci proposta mercato
- **POST** `/api/MarketWrapper/Update?key={key}` - Aggiorna proposta mercato
- **POST** `/api/MarketWrapper/Query` - Query mercato

### 8. News

#### NewsWrapper/news
- **GET** `/api/NewsWrapper/news/Get?key={key}` - Ottieni news
- **POST** `/api/NewsWrapper/news/Insert?key={key}` - Inserisci news
- **POST** `/api/NewsWrapper/news/Update?key={key}` - Aggiorna news
- **POST** `/api/NewsWrapper/news/Query` - Query news

### 9. Notifiche Push

#### PushNotificationSettings
- **GET** `/api/PushNotificationSettings/Get?key={key}` - Ottieni impostazioni notifiche
- **POST** `/api/PushNotificationSettings/Insert?key={key}` - Inserisci impostazioni
- **POST** `/api/PushNotificationSettings/Update?key={key}` - Aggiorna impostazioni

#### PushNotificationSent
- **GET** `/api/PushNotificationSent/Get?key={key}` - Ottieni notifiche inviate
- **POST** `/api/PushNotificationSent/Insert?key={key}` - Segna notifica come inviata
- **POST** `/api/PushNotificationSent/Query` - Query notifiche inviate

### 10. Configurazioni

#### Settings
- **GET** `/api/Settings/Get?key={key}` - Ottieni impostazioni
- **POST** `/api/Settings/Insert?key={key}` - Inserisci impostazioni
- **POST** `/api/Settings/Update?key={key}` - Aggiorna impostazioni

#### FantasoccerLog
- **GET** `/api/FantasoccerLog/Get?key={key}` - Ottieni log
- **POST** `/api/FantasoccerLog/Insert?key={key}` - Inserisci log
- **POST** `/api/FantasoccerLog/Query` - Query log

---

## Endpoint Business Logic

### Aste
- **GET** `/Auction/GetGroup?groupId={groupId}` - Ottieni gruppo per asta
- **GET** `/Auction/GetAllPlayers?year={year}` - Ottieni tutti i giocatori per anno
- **GET** `/Auction/GetTeamName?email={email}&group={group}&basket={basket}&year={year}` - Ottieni nome squadra
- **GET** `/Auction/GetTeams?group={group}&basket={basket}&year={year}` - Ottieni squadre in asta
- **GET** `/Auction/SetPlayer?email={email}&group={group}&league={league}&basket={basket}&year={year}&playerName={playerName}&price={price}&isRandom={isRandom}` - Assegna giocatore in asta
- **GET** `/Auction/GetNextPlayer?group={group}&league={league}&year={year}&role={role}&isRandom={isRandom}` - Ottieni prossimo giocatore per ruolo

### Utility
- **GET** `/Ping` - Endpoint di health check

---

## Modelli Dati Principali

### Group
```json
{
  "id": "string",
  "name": "string", 
  "description": "string",
  "maxMembers": "integer",
  "creator": "string",
  "users": ["UserOfAGroup"],
  "leagues": ["League"],
  "creationTime": "datetime",
  "isPrivate": "boolean",
  "password": "string"
}
```

### Team
```json
{
  "n": "string", // nome
  "o": "string", // proprietario
  "a": ["string"], // amministratori
  "p": ["Player"], // giocatori
  "r": "integer", // soldi rimanenti
  "m": "integer", // modulo
  "d": "datetime" // data modifica
}
```

### Player
```json
{
  "n": "string", // nome
  "t": "RealTeam", // squadra reale
  "r": "Role", // ruolo (0=P, 1=D, 2=C, 3=A, -1=ALL)
  "a": "boolean", // attivo
  "vh": "boolean", // disponibilità casa/trasferta
  "p": "integer", // prezzo
  "s": "PlayerInTeamStatus", // status nel team
  "k": "FantaSoccerRole" // ruolo fantacalcio
}
```

### StatPlayer (Giocatore con Statistiche)
```json
{
  "n": "string", // nome
  "t": "RealTeam", // squadra reale
  "r": "Role", // ruolo
  "a": "boolean", // attivo
  "vh": "boolean", // disponibilità
  "z": "float", // media voto
  "f": "float", // media fantamedia
  "v": "integer", // partite giocate
  "q": "integer", // quotazione
  "s": "integer", // punti stagionali
  "g": "integer", // gol
  "p": "integer", // rigori
  "u": "integer", // rigori sbagliati
  "j": "integer", // assist
  "m": "integer", // ammonizioni
  "w": "integer", // espulsioni
  "o": "integer", // autogol
  "y": "integer", // gol subiti (portieri)
  "d": "integer", // parate (portieri)
  "e": "integer", // reti inviolate
  "c": "integer", // inserimento/e
  "ij": "integer", // infortunio/squalifica
  "lg": ["StatPlayerGame"] // storico partite
}
```

### RealTeam
```json
{
  "n": "string", // nome
  "a": "string" // abbreviazione
}
```

### Vote (Voto Giocatore)
```json
{
  "r": "Role", // ruolo
  "v": "float", // voto
  "i": "boolean", // ha giocato
  "g": "integer", // gol
  "p": "integer", // rigori
  "a": "integer", // assist
  "s": "integer", // parate
  "d": "integer", // gol subiti
  "w": "integer", // ammonizioni
  "o": "integer", // espulsioni
  "t": "Behaviour", // comportamento
  "c": "boolean", // capitano
  "h": "boolean", // casa
  "u": "boolean", // fuori casa
  "n": "boolean", // neutro
  "j": "boolean" // infortunato
}
```

### Market (Proposta di Mercato)
```json
{
  "id": "string",
  "buyer": "string", // compratore
  "seller": "string", // venditore
  "sellerPlayers": ["Player"], // giocatori dal venditore
  "buyerPlayers": ["Player"], // giocatori dal compratore
  "moneyFromBuyer": "integer", // soldi dal compratore
  "moneyFromSeller": "integer", // soldi dal venditore
  "approvers": ["string"], // approvatori
  "deniers": ["string"], // negatori
  "status": "MarketStatus", // stato proposta
  "creationTime": "datetime"
}
```

### Calendar
```json
{
  "l": "string", // lega
  "y": "integer", // anno
  "d": {
    "dayNumber": "Day" // giorni con partite
  }
}
```

### Day
```json
{
  "y": "integer", // anno
  "a": "integer", // numero giornata
  "g": ["CalendarGame"] // partite
}
```

### CalendarGame
```json
{
  "h": "string", // squadra casa
  "a": "string", // squadra trasferta
  "d": "datetime", // data/ora
  "r": "GameResult" // risultato
}
```

### Rank (Classifica)
```json
{
  "d": "integer", // giornata
  "r": {
    "leagueId": ["RankedTeam"] // squadre classificate per lega
  }
}
```

### RankedTeam
```json
{
  "n": "string", // nome squadra
  "o": "string", // proprietario
  "p": "float", // punti
  "v": "integer", // vittorie
  "d": "integer", // pareggi
  "e": "integer", // sconfitte
  "g": "integer", // gol fatti
  "s": "integer", // gol subiti
  "x": "float", // media punti
  "w": "float", // media voto
  "z": "integer", // posizione
  "m": "integer" // movimento (+/- posizioni)
}
```

### News
```json
{
  "league": "string", // lega
  "title": "string", // titolo
  "content": "string", // contenuto
  "date": "datetime", // data pubblicazione
  "image": "string" // url immagine
}
```

### PushNotificationSettings
```json
{
  "matchEvents": "boolean", // eventi partita
  "opponentMatchEvents": "boolean", // eventi avversari
  "deploymentReminder": "boolean", // promemoria formazione
  "endDay": "boolean", // fine giornata
  "marketEvents": "boolean", // eventi mercato
  "subscriptions": ["PushSubscription"] // sottoscrizioni push
}
```

---

## Enumerazioni

### Role (Ruoli Giocatori)
- `0` - Portiere (P)
- `1` - Difensore (D) 
- `2` - Centrocampista (C)
- `3` - Attaccante (A)
- `-1` - Tutti i ruoli

### PlayerInTeamStatus
- `0` - Titolare
- `1` - Panchina
- `2` - Fuori rosa
- `3` - Infortunato
- `4` - Squalificato

### MarketStatus
- `0` - In attesa
- `1` - Approvata
- `2` - Respinta
- `3` - Cancellata
- `4` - Scaduta
- `5` - In votazione
- `6` - Completata

### GameResult
- `0` - Da giocare
- `1` - Casa
- `2` - Pareggio
- `3` - Trasferta
- `4` - Rinviata

### CommandType (per operazioni batch)
- `0` - Insert
- `1` - Update  
- `2` - Upsert
- `3` - Delete

---

## Pattern Comuni

### Chiavi Composite
Molti endpoint utilizzano chiavi composite per identificare le risorse:

- **TeamKey**: `{g: groupId, y: year, b: basketId, e: email}`
- **TeamDayKey**: `{g: groupId, y: year, b: basketId, d: day, e: email}`
- **LeagueKey**: `{g: groupId, l: leagueId}`
- **GameKey**: `{g: groupId, l: leagueId, y: year, d: day, i: gameId}`
- **YearDayKey**: `{y: year, d: day}`

### Operazioni Standard
Ogni entità supporta le operazioni CRUD standard:

- **Get**: Recupera singola risorsa
- **Insert**: Crea nuova risorsa
- **Update**: Aggiorna risorsa esistente
- **Delete**: Elimina risorsa
- **Query**: Ricerca filtrata
- **Exist**: Verifica esistenza
- **Batch**: Operazioni multiple
- **Bootstrap**: Inizializzazione

### Filtri Query
Le operazioni di Query accettano filtri tramite `SerializableFilter`:

```json
{
  "o": [
    {
      "Path": "propertyName",
      "Operator": "Equal|GreaterThan|LessThan|Contains|...",
      "Value": "filterValue"
    }
  ]
}
```