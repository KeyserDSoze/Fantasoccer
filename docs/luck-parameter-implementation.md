# Parametro Fortuna - Implementazione

## 📊 Panoramica

Il **Parametro Fortuna** è una metrica che quantifica quanto una squadra è stata fortunata o sfortunata durante la stagione, basandosi su eventi specifici nelle partite.

## 🎯 Eventi di Fortuna Implementati

### Eventi Positivi (Fortuna)

1. **Vittoria contro il peggior punteggio** (`+5 punti`)
   - Vinci contro la squadra che ha fatto il punteggio più basso del turno
   - Esempio: Vinci contro chi ha fatto 50 punti (il peggiore)

2. **Vittoria contro il secondo peggior punteggio** (`+6 punti`)
   - Vinci contro la seconda squadra peggiore del turno
   - Esempio: Vinci contro chi ha fatto 55 punti (secondo peggiore)

3. **Vittoria di misura** (`+0.5 per ogni punto di scarto, max +3`)
   - Vinci con 1 goal di scarto e meno di 6 punti di differenza
   - Esempio: 77 vs 78 → 1 punto scarto → `+0.5`
   - Esempio: 72 vs 77.5 → 5.5 punti scarto → `+2.75`

4. **Pareggio fortunato** (`scala 0-3 basata sullo scarto`)
   - Pareggi nonostante un ampio scarto di punti
   - Formula: `(scarto / 5.5) * 3`
   - Esempio: 72 vs 77.5 (5.5 scarto, massimo) → `+3`
   - Esempio: 74 vs 76 (2 punti scarto) → `+1.09`

5. **Vittoria pur essendo il peggiore** (`+7 punti`)
   - Vinci la partita pur avendo il peggior punteggio del turno
   - Esempio: Fai 55 punti (il peggiore) ma vinci perché l'avversario ha fatto 50

### Eventi Negativi (Sfortuna)

6. **Sconfitta di misura** (`-0.5 per ogni punto di scarto, max -3`)
   - Perdi con 1 goal di scarto e meno di 6 punti di differenza
   - Esempio: 77 vs 78 → 1 punto scarto → `-0.5`
   - Esempio: 72.5 vs 78 → 5.5 punti scarto → `-2.75`

7. **Sconfitta pur essendo il migliore** (`-8 punti`)
   - Perdi pur avendo il miglior punteggio del turno
   - Esempio: Fai 85 punti (il migliore) ma perdi contro uno che fa 86

8. **Sconfitta pur essendo nel top 3** (`-4 punti`)
   - Perdi pur essendo tra i migliori 3 punteggi del turno
   - Esempio: Sei secondo o terzo come punteggio ma perdi

## 📐 Formula del Parametro Fortuna

```
Fortuna Media = Somma(Punti Fortuna di tutti gli eventi) / Partite Giocate
```

### Esempio Pratico

Squadra: **AS Tronzi**
- Partite giocate: 10
- Eventi:
  - Giornata 1: Vittoria di misura (+2.5)
  - Giornata 3: Sconfitta pur essendo nel top 3 (-4)
  - Giornata 5: Pareggio fortunato (+1.5)
  - Giornata 7: Vittoria contro il peggiore (+5)
  - Giornata 9: Sconfitta di misura (-1.5)

**Totale**: +2.5 - 4 + 1.5 + 5 - 1.5 = **+3.5**  
**Media**: 3.5 / 10 = **+0.35 per partita**

## 🎨 Visualizzazione

### Nella Classifica
- Nuova colonna: **🍀** (Fortuna)
- Colore verde: Fortuna positiva
- Colore rosso: Sfortuna (valori negativi)
- Colore grigio: Neutro (0)
- Formato: `+2.5` o `-1.3`

### Dettaglio al Click
Cliccando sul valore della fortuna si apre un modal con:
1. **Riepilogo**:
   - Fortuna totale
   - Fortuna media per partita
   - Numero di partite giocate
   - Eventi positivi vs negativi

2. **Lista Eventi**:
   - Ordinata per giornata (dalla più recente)
   - Card verde/rossa per ogni evento
   - Dettagli: avversario, punteggio, motivo della fortuna

## 📂 File Implementati

### 1. `types/rank.ts`
Aggiunte le definizioni dei tipi:
```typescript
export type LuckEventType = 
  | 'win-vs-worst'
  | 'win-vs-second-worst'
  | 'win-narrow'
  | 'draw-wide-gap'
  | 'loss-narrow'
  | 'loss-as-best'
  | 'loss-as-top3'
  | 'win-as-worst'

export interface LuckEvent {
  type: LuckEventType
  points: number
  gameId: string
  gameDay: number
  opponent: string
  myScore: number
  opponentScore: number
  result: 'win' | 'draw' | 'loss'
  detail: string
}

export interface TeamLuck {
  owner: string
  name: string
  totalLuck: number
  gamesPlayed: number
  avgLuck: number
  events: LuckEvent[]
}
```

### 2. `utils/luckCalculator.ts`
Classe principale per il calcolo:
```typescript
export class LuckCalculator {
  static calculateTeamLuck(calendar: Calendar, roundId: string): Map<string, TeamLuck>
  static formatLuck(luck: number): string
  static getLuckColor(luck: number): string
}
```

### 3. `components/rank/LuckDetailModal.tsx`
Modal per mostrare i dettagli della fortuna:
- Riepilogo con statistiche
- Lista eventi ordinati
- Card colorate per evento
- Pulsante di chiusura

### 4. `components/rank/LeagueRankingWidget.tsx`
Integrazione nella classifica:
- Nuova prop `calendar?: Calendar | null`
- Nuova prop `roundId?: string | null`
- Calcolo automatico della fortuna se calendario disponibile
- Colonna 🍀 cliccabile
- Modal integrato

## 🚀 Come Usare

### Nella schermata Rank

```typescript
<LeagueRankingWidget
  // ... altre props esistenti
  calendar={calendar}  // Passa il calendario
  roundId={roundId}    // Passa l'ID del girone
  // ... 
/>
```

Il widget:
1. Calcola automaticamente la fortuna se `calendar` e `roundId` sono forniti
2. Aggiunge la colonna 🍀 alla classifica
3. Rende cliccabile il valore per mostrare i dettagli

### Risultato

- Se il calendario non è disponibile: nessuna colonna fortuna (backward compatible)
- Se il calendario è disponibile: colonna fortuna visibile e cliccabile
- Click sul valore → Modal con tutti i dettagli

## 🎲 Note Tecniche

### Partite Annullate
Le partite annullate (`isCancelled === true`) sono escluse dai calcoli.

### Performance
Il calcolo viene effettuato una sola volta quando il calendario cambia (tramite useMemo non esplicito ma grazie al fatto che il calendario è passato come prop).

### Scalabilità
Tutti gli eventi sono memorizzati, permettendo future analisi e statistiche aggiuntive.

## 💡 Proposte di Miglioramento Future

1. **Calendario Facile/Difficile**: Media punteggi avversari vs media campionato
2. **Streak di Fortuna**: Sequenze di eventi fortunati/sfortunati consecutivi
3. **Fortuna difensiva**: Eventi legati ai goal subiti
4. **Confronto con media lega**: Quanto sei più/meno fortunato della media

## ✅ Status Implementazione

- ✅ Calcolo eventi base (8 tipi)
- ✅ Integrazione nella classifica
- ✅ Modal dettagliato
- ✅ Colori e formattazione
- ✅ Gestione partite annullate
- ⏳ Test con dati reali (da validare)
- ⏳ Eventuali aggiustamenti pesi eventi
