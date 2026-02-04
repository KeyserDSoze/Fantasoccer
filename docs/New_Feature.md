# 🎴 Sistema Poteri Speciali - Fantacalcio con Carte

## 📋 Descrizione Generale

Una nuova modalità di fantacalcio dove i giocatori durante l'asta escono con **poteri speciali casuali**, creando un'esperienza unica e strategica ad ogni asta. Ogni giocatore ha un colore associato che può formare combo con carte modulo giocabili durante la stagione.

---

## ⚡ Poteri Speciali dei Giocatori

### Esempi di Poteri:
- **Mirror Striker**: Prende lo stesso bonus del primo attaccante degli avversari
- **Eternal Boost**: Riceve sempre un +1 al voto finale
- **Flex Player**: Può essere schierato fuori ruolo senza penalità
- **Double Goal**: I suoi gol valgono doppio
- **Penalty Master**: Rigori segnati danno +5 invece di +3
- **Captain Aura**: Dà +0.5 a tutti i giocatori della stessa squadra reale schierati
- **Shield Keeper**: Come portiere, gol subiti danno -0.5 invece di -1
- **Comeback King**: Se parte dalla panchina e viene schierato, +2 bonus
- **Wild Card**: Una volta a stagione può copiare il potere di un altro giocatore
- **Tactician**: Permette di cambiare modulo a metà giornata

---

## 🎨 Sistema Colori

Ogni giocatore con potere speciale ha un **colore associato**:

### Colori e Significati:
- 🔴 **Rosso** - Offensivo (bonus a gol e assist)
- 🔵 **Blu** - Difensivo (bonus a clean sheet e parate)
- 🟢 **Verde** - Supporto (bonus a compagni di squadra)
- 🟡 **Giallo** - Utility (effetti speciali e flessibilità)
- 🟣 **Viola** - Leggendario (poteri unici e potenti)
- ⚫ **Nero** - Wildcard (effetti imprevedibili)

### Punti Colore:
- Ogni giocatore con potere dà **punti colore** alla squadra
- I punti colore si accumulano durante la stagione
- Più giocatori dello stesso colore = bonus combo

---

## 🃏 Carte Modulo

Durante la stagione, le squadre possono giocare **carte modulo** che interagiscono con i colori:

### Esempi di Carte:
- **Red Storm** (3 🔴 richiesti): Tutti gli attaccanti prendono +1 per una giornata
- **Blue Wall** (3 🔵 richiesti): Difesa prende bonus clean sheet anche con 1 gol subito
- **Green Harmony** (2 🟢 + 2 🟡): Tutti i centrocampisti si scambiano assist bonus
- **Rainbow Power** (1 di ogni colore): +3 punti bonus alla squadra
- **Dark Ritual** (3 ⚫): Scambia un giocatore con un avversario per una giornata
- **Legendary Combo** (2 🟣): Attiva tutti i poteri speciali contemporaneamente

### Meccaniche Carte:
- Le carte si ottengono tramite achievement, eventi speciali, o mini-giochi
- Ogni carta può essere giocata **una sola volta** a stagione
- Le carte si attivano **prima della giornata** (fase di pre-match)
- Alcune carte richiedono combinazioni di colori specifiche

---

## 🎲 Generazione Casuale durante l'Asta

### Meccanica:
1. Durante l'asta, quando un giocatore esce, c'è una **probabilità casuale** che abbia un potere speciale
2. Il colore e il potere vengono **generati al momento dell'uscita**
3. I giocatori possono vedere il potere **prima di fare offerte**
4. Ogni asta crea una **composizione unica** di poteri

### Rarità Poteri:
- ⚪ **Comune** (40%): Poteri semplici (+0.5, +1)
- 🟢 **Non comune** (30%): Poteri medi (doppio bonus specifico)
- 🔵 **Raro** (20%): Poteri complessi (combo con compagni)
- 🟣 **Epico** (8%): Poteri forti (effetti su tutta la squadra)
- 🟠 **Leggendario** (2%): Poteri unici (cambiano le regole del gioco)

---

## 📊 Strategia e Bilanciamento

### Considerazioni Strategiche:
- **Diversificazione**: Avere molti colori diversi permette più combo
- **Specializzazione**: Concentrarsi su un colore per massimizzare i bonus
- **Sinergie**: Alcuni poteri funzionano meglio insieme
- **Rischio/Ricompensa**: Giocatori leggendari costano di più ma sono game-changer

### Bilanciamento:
- I poteri non devono essere **pay-to-win** (fortuna bilanciata)
- Giocatori normali rimangono **competitivi**
- Le carte modulo sono **limitate** per evitare spam
- Il sistema di colori crea **scelte difficili** (non si può avere tutto)

---

## 🛠️ Implementazione Tecnica (Idee)

### Frontend:
- **Badge colore** sul player durante l'asta
- **Modal dettaglio** potere quando si clicca sul giocatore
- **Indicatore colori** nella rosa squadra
- **Gestore carte modulo** (deck builder)
- **Animazioni speciali** quando si attiva un potere

### Backend:
- **Tabella PoweredPlayer**: id, playerId, powerId, colorId, auctionId
- **Tabella Powers**: id, name, description, rarity, effect (JSON)
- **Tabella ModuleCards**: id, name, description, colorRequirements (JSON), effect
- **Tabella PlayerCards**: teamId, cardId, used, usedInDay

### Game Logic:
- **Generazione casuale** durante ShowNextPlayer (server-side)
- **Calcolo bonus** durante ComputeScore (considera poteri attivi)
- **Validazione carte** prima dell'attivazione (check colori sufficienti)
- **History log** di tutte le attivazioni poteri e carte

---

## 🎯 Roadmap Implementazione

### Fase 1 - MVP (Minimum Viable Product):
1. Sistema base generazione poteri casuali
2. 5-10 poteri semplici implementati
3. Sistema colori base (3 colori)
4. Visualizzazione poteri durante asta

### Fase 2 - Carte Modulo:
1. Database carte modulo
2. Sistema accumulo punti colore
3. UI deck builder per gestire carte
4. Attivazione carte pre-giornata

### Fase 3 - Bilanciamento & Espansione:
1. Analytics per bilanciare poteri
2. Nuovi poteri basati su feedback
3. Nuove carte modulo
4. Achievement e ricompense

### Fase 4 - Advanced Features:
1. Trading di carte tra giocatori
2. Tornei speciali con regole custom
3. Stagionali (poteri tematici)
4. Draft mode (scelta poteri invece di casualità)

---

## 💡 Note e Idee Aggiuntive

- **Tema**: Potrebbe essere tematizzato (es. supereroi, fantasy medievale, sci-fi)
- **Narrativa**: Ogni potere potrebbe avere una storia/lore
- **Cosmetics**: Skin speciali per giocatori con poteri leggendari
- **Tornei**: Modalità torneo dove tutti partono con le stesse carte
- **Mercato**: Scambio di giocatori con poteri tra squadre (più valore)
- **Evoluzione**: Giocatori possono "evolvere" il loro potere durante la stagione

---

## 🔮 Visione Futura

Questo sistema trasforma il fantacalcio da semplice gestione rosa a **gioco di strategia con elementi TCG** (Trading Card Game), dove:
- Ogni asta è **imprevedibile e unica**
- La strategia va **oltre i semplici voti**
- I manager devono **pianificare a lungo termine**
- Si creano **meta-game e combo discovery**
- La community può **condividere strategie e deck**

Il gioco diventa più **sociale, strategico e rigiocabile** ad ogni stagione! 🎮⚽🃏
