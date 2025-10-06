# 🎨 Fantasoccer App - UI Components Documentation

> Comprehensive reference for reusable UI components built with Tamagui.
> These components provide consistent UX patterns across the application and should be reused whenever possible.

---

## 📖 Overview

L'applicazione utilizza componenti UI predefiniti per garantire:

- 🎯 **Consistenza visiva** - Stesso look & feel in tutta l'app
- ♻️ **Riutilizzo** - DRY principle applicato all'UI
- 📱 **Responsive** - Adattamento automatico web/mobile
- ⚡ **Performance** - Componenti ottimizzati e memoizzati
- 🌓 **Dark mode** - Supporto automatico tema chiaro/scuro

**IMPORTANTE:** Prima di creare nuovi componenti UI, **controlla sempre questo file** per vedere se esiste già un componente riutilizzabile!

---

## 🎯 Componenti Principali

### 📁 Component: LeagueSelectorsWidget

**File:** `components/LeagueSelectorsWidget.tsx`

**Purpose:** Widget unificato per selezione lega, stagione, e girone con gestione state locale.

#### Caratteristiche Chiave

✅ **State Locale (NON modifica context globale)**
- Il componente gestisce la selezione internamente
- **Non** modifica lo stato globale dell'utente (selectedYear, selectedLeague, etc.)
- Permette di tornare alle impostazioni precedenti con F5/refresh
- Ideale per pagine che necessitano filtri temporanei

✅ **Flessibilità**
- Mostra/nasconde sezioni (lega, anno, girone) con flags booleani
- Callback opzionali per ogni selezione
- Supporto per hint e label personalizzati
- Footer customizzabile

✅ **UX**
- Selezione visuale con bottoni colorati
- Evidenziazione della selezione corrente
- Indicatori per lega principale e girone default
- Wrapping automatico per schermi piccoli

#### Props

```typescript
type LeagueSelectorsWidgetProps = {
  // Lega
  leagues: LeagueOption[]
  selectedLeagueId: string | null
  onSelectLeague?: (leagueId: string) => void
  
  // Anno
  years?: number[]
  selectedYear?: number | null
  onSelectYear?: (year: number) => void
  formatYearLabel?: (year: number) => string
  
  // Girone/Round
  rounds?: RoundOption[]
  selectedRoundId?: string | null
  onSelectRound?: (roundId: string) => void
  
  // Configurazione
  title?: string
  leagueHint?: string
  yearHint?: string
  roundHint?: string
  emptyLeagueLabel?: string
  
  // Visibilità sezioni
  showLeagueSection?: boolean
  showYearSection?: boolean
  showRoundSection?: boolean
  
  // Footer personalizzato
  footer?: ReactNode
}

type LeagueOption = {
  id: string
  name: string
  isMain?: boolean  // Mostra "• Principale"
}

type RoundOption = {
  id: string
  label: string
  isDefault?: boolean  // Mostra "• Default"
}
```

#### Esempi di Utilizzo

**Esempio 1: Selezione completa (lega + anno + girone)**

```typescript
import { LeagueSelectorsWidget } from 'components/LeagueSelectorsWidget'

function MyPage() {
  const [selectedLeagueId, setSelectedLeagueId] = useState<string | null>(null)
  const [selectedYear, setSelectedYear] = useState<number | null>(null)
  const [selectedRoundId, setSelectedRoundId] = useState<string | null>(null)

  const leagues = [
    { id: 'league1', name: 'Lega A', isMain: true },
    { id: 'league2', name: 'Lega B' }
  ]

  const years = [2024, 2023, 2022]

  const rounds = [
    { id: 'round1', label: 'Girone A', isDefault: true },
    { id: 'round2', label: 'Girone B' }
  ]

  return (
    <LeagueSelectorsWidget
      leagues={leagues}
      selectedLeagueId={selectedLeagueId}
      onSelectLeague={setSelectedLeagueId}
      years={years}
      selectedYear={selectedYear}
      onSelectYear={setSelectedYear}
      rounds={rounds}
      selectedRoundId={selectedRoundId}
      onSelectRound={setSelectedRoundId}
    />
  )
}
```

**Esempio 2: Solo selezione anno**

```typescript
<LeagueSelectorsWidget
  leagues={leagues}
  selectedLeagueId={currentLeagueId}
  years={[2024, 2023, 2022]}
  selectedYear={year}
  onSelectYear={setYear}
  showLeagueSection={false}  // Nasconde selezione lega
  showRoundSection={false}   // Nasconde selezione girone
  title="Seleziona stagione"
  yearHint="Scegli l'anno di riferimento"
/>
```

**Esempio 3: Con footer personalizzato**

```typescript
<LeagueSelectorsWidget
  leagues={leagues}
  selectedLeagueId={leagueId}
  onSelectLeague={setLeagueId}
  footer={
    <Button onPress={handleApply}>
      Applica filtri
    </Button>
  }
/>
```

#### Best Practices

✅ **Usa sempre questo componente per selezioni multiple**
```typescript
// ✅ Good: Componente unificato
<LeagueSelectorsWidget {...props} />

// ❌ Bad: Reinventare la ruota
<YStack>
  <Text>Seleziona lega</Text>
  {leagues.map(league => <Button>...</Button>)}
</YStack>
```

✅ **State locale per filtri temporanei**
```typescript
// ✅ Good: Non modifica context globale
const [localYear, setLocalYear] = useState(2024)
<LeagueSelectorsWidget selectedYear={localYear} onSelectYear={setLocalYear} />

// ❌ Bad: Modifica context per filtro temporaneo
const { setSelectedYear } = useApp()
<LeagueSelectorsWidget onSelectYear={setSelectedYear} />
```

✅ **Nascondi sezioni non necessarie**
```typescript
// ✅ Good: Solo le sezioni rilevanti
<LeagueSelectorsWidget
  showLeagueSection={false}
  showRoundSection={false}
  // Solo year section visibile
/>
```

---

### 📁 Component: LeagueCalendarWidget

**File:** `components/calendar/LeagueCalendarWidget.tsx`

**Purpose:** Visualizzazione calendario giornate con navigazione a partite.

#### Caratteristiche Chiave

✅ **Visualizzazione completa calendario**
- Lista giornate con partite
- Risultati (gol e punti)
- Stato partite (giocate, annullate, pending)
- Navigazione diretta alla schermata Game

✅ **Gestione stati**
- Loading state con spinner
- Error state con retry
- Empty state
- Selection incomplete state

✅ **Context fallback**
- Accetta groupId, leagueId, year come props
- Fallback automatico al context globale se non forniti
- Ideale per widget embedded

#### Props

```typescript
type LeagueCalendarWidgetProps = {
  leagueName?: string | null
  seasonLabel?: string | null
  roundLabel?: string | null
  pendingGamesCount?: number
  days: CalendarDay[]
  loading?: boolean
  error?: string | null
  isSelectionIncomplete?: boolean
  onRefresh?: () => void
  
  // Navigation fallback
  groupId?: string | null
  leagueId?: string | null
  year?: number | null
  
  // Customization
  selectionMessage?: string
  emptyMessage?: string
  loadingMessage?: string
  refreshLabel?: string
  errorActionLabel?: string
}
```

#### Esempio di Utilizzo

```typescript
import { LeagueCalendarWidget } from 'components/calendar/LeagueCalendarWidget'
import { useCalendar } from 'hooks/useCalendar'

function CalendarPage() {
  const leagueKey = { groupId: 'g1', leagueId: 'l1', year: 2024 }
  const { days, loading, error, refetch } = useCalendar(leagueKey)

  return (
    <LeagueCalendarWidget
      leagueName="Lega A"
      seasonLabel="2024"
      roundLabel="Girone Unico"
      days={days}
      loading={loading}
      error={error}
      onRefresh={refetch}
    />
  )
}
```

---

### 📁 Component: LeagueRankingWidget

**File:** `components/rank/LeagueRankingWidget.tsx`

**Purpose:** Tabella classifica con statistiche dettagliate.

#### Caratteristiche Chiave

✅ **Tabella completa statistiche**
- Posizione in classifica
- Punti, vittorie, pareggi, sconfitte
- Gol fatti/subiti, differenza reti
- Punti voto, media punti
- Patrimonio squadra

✅ **Responsive layout**
- Desktop: tutte le colonne
- Mobile: colonne essenziali (compatte)
- Scroll orizzontale automatico se necessario

✅ **Evidenziazione visiva**
- Top 3 con colori distintivi (oro, argento, bronzo)
- Riga alternata per leggibilità
- Font bold per valori importanti

#### Props

```typescript
type LeagueRankingWidgetProps = {
  leagueName?: string | null
  seasonLabel?: string | null
  roundLabel?: string | null
  teamCount?: number | null
  rankedTeams: EnhancedRankedTeam[]
  loading?: boolean
  error?: string | null
  isSelectionIncomplete?: boolean
  onRefresh?: () => void
  
  // Customization
  selectionMessage?: string
  emptyMessage?: string
  loadingMessage?: string
  refreshLabel?: string
  errorActionLabel?: string
}
```

#### Esempio di Utilizzo

```typescript
import { LeagueRankingWidget } from 'components/rank/LeagueRankingWidget'
import { useRank } from 'hooks/useRank'

function RankingPage() {
  const leagueKey = { groupId: 'g1', leagueId: 'l1', year: 2024 }
  const { enhancedRank, loading, error, refetch, getEnhancedRoundRanking } = useRank(leagueKey)
  
  const teams = getEnhancedRoundRanking('round1')

  return (
    <LeagueRankingWidget
      leagueName="Lega A"
      seasonLabel="2024"
      roundLabel="Girone Unico"
      rankedTeams={teams}
      loading={loading}
      error={error}
      onRefresh={refetch}
    />
  )
}
```

---

### 📁 Component: SerieALiveWidget

**File:** `components/seriea/SerieALiveWidget.tsx`

**Purpose:** Widget completo per partite live Serie A con voti in tempo reale.

#### Caratteristiche Chiave

✅ **Real-time updates**
- Polling automatico durante partite live
- Voti live aggiornati ogni 10 secondi
- Classifica Serie A con refresh intelligente
- Evidenziazione partite in corso

✅ **Visualizzazione completa**
- Partite del giorno con orari
- Risultati live (gol)
- Voti giocatori in tempo reale
- Statistiche dettagliate (gol, assist, ammonizioni, espulsioni)
- Classifica Serie A

✅ **Ottimizzazione polling**
- Attivo solo durante partite live
- Disattivazione automatica quando non necessario
- Intervalli configurabili

#### Props

```typescript
type SerieALiveWidgetProps = {
  refreshInterval?: number          // Default: 30000ms (30s)
  enablePolling?: boolean           // Default: true
  pollOnlyWhenLive?: boolean        // Default: true (polling solo durante live)
}
```

#### Esempio di Utilizzo

```typescript
import { SerieALiveWidget } from 'components/seriea/SerieALiveWidget'

function SerieAPage() {
  return (
    <SerieALiveWidget
      refreshInterval={30000}
      enablePolling={true}
      pollOnlyWhenLive={true}  // Polling intelligente
    />
  )
}
```

---

### 📁 Component: SoccerField

**File:** `components/lineup/SoccerField.tsx`

**Purpose:** Campo da calcio interattivo per gestione formazione con drag & drop.

#### Caratteristiche Chiave

✅ **Responsive orientation**
- Desktop: campo orizzontale (tradizionale)
- Mobile: campo verticale (sfrutta meglio lo schermo)
- SVG scalabile con viewBox dinamico

✅ **Gestione formazione completa**
- Drag & drop giocatori
- Swap tra ruoli compatibili
- Validazione formazione (3-5 difensori, 3-5 centrocampisti, 1-3 attaccanti)
- Portiere fisso (non rimuovibile)
- Tribune per giocatori non schierati
- Panchina con backup per ruolo

✅ **Player cards responsive**
- Scale automatica basata su dimensione campo
- Incremento 60% su mobile per visibilità
- Posizionamento preciso per ruolo
- Info complete (voto, bonus, chance)

✅ **Save functionality**
- Salvataggio formazione tramite gameService
- Toast feedback successo/errore
- Validazione pre-salvataggio

#### Props

```typescript
type SoccerFieldProps = {
  gameWrapper: GameWrapper
  isOwner: boolean
  canEdit: boolean
  onSave?: () => void
}
```

#### Esempio di Utilizzo

```typescript
import { SoccerField } from 'components/lineup/SoccerField'
import { useGame } from 'hooks/useGame'

function GamePage({ gameId, leagueId, year, groupId }) {
  const { gameWrapper, loading } = useGame({ gameId, leagueId, year, groupId })
  const { user } = useAuth()
  
  const isOwner = gameWrapper?.teams.some(t => t.owner === user?.email)
  const canEdit = gameWrapper?.canEdit ?? false

  if (loading) return <Spinner />

  return (
    <SoccerField
      gameWrapper={gameWrapper}
      isOwner={isOwner}
      canEdit={canEdit}
      onSave={() => console.log('Formation saved!')}
    />
  )
}
```

---

## 🎯 Componenti Layout

### BackButton

**File:** `components/layout/BackButton.tsx`

**Purpose:** Bottone navigazione indietro consistente.

```typescript
import { BackButton } from 'components/layout/BackButton'

<BackButton /> // Navigazione automatica indietro
```

---

### Container

**File:** `components/layout/Container.tsx`

**Purpose:** Container responsive con padding e max-width.

```typescript
import { Container } from 'components/layout/Container'

<Container>
  <YourContent />
</Container>
```

---

### ScreenContent

**File:** `components/layout/ScreenContent.tsx`

**Purpose:** Wrapper per contenuto schermata con scroll.

```typescript
import { ScreenContent } from 'components/layout/ScreenContent'

<ScreenContent>
  <YourPageContent />
</ScreenContent>
```

---

### NavigationBar

**File:** `components/layout/NavigationBar.tsx`

**Purpose:** Barra navigazione principale con menu.

---

### SwitchThemeButton

**File:** `components/layout/SwitchThemeButton.tsx`

**Purpose:** Toggle tema chiaro/scuro.

```typescript
import { SwitchThemeButton } from 'components/layout/SwitchThemeButton'

<SwitchThemeButton />
```

---

## 🎨 Componenti Icone & Grafiche

### BonusIcons

**File:** `components/icons/BonusIcons.tsx`

**Purpose:** Icone per bonus giocatori (gol, assist, ammonizioni, etc.).

---

### TeamLogo

**File:** `components/team/TeamLogo.tsx`

**Purpose:** Logo squadra con fallback automatico.

```typescript
import { TeamLogo } from 'components/team/TeamLogo'

<TeamLogo
  groupId={groupId}
  year={year}
  email={ownerEmail}
  size={48}
/>
```

---

### SoccerBallSVG, AssistSVG, BehaviourCardSVG

**File:** `components/lineup/icons/*`

**Purpose:** Icone SVG per eventi partita.

---

## 🎨 Pattern di Design

### Card Container

Usa sempre `Card` di Tamagui per contenitori:

```typescript
import { Card } from 'tamagui'

<Card
  padding="$4"
  backgroundColor="$color2"
  borderRadius="$4"
  borderWidth="$0.5"
  borderColor="$borderColor"
>
  <YourContent />
</Card>
```

### Loading States

Pattern consistente per loading:

```typescript
if (loading) {
  return (
    <YStack padding="$4" alignItems="center">
      <Spinner size="large" color="$blue10" />
      <Text color="$color11" marginTop="$2">
        Caricamento...
      </Text>
    </YStack>
  )
}
```

### Error States

Pattern consistente per errori:

```typescript
if (error) {
  return (
    <Card padding="$4" backgroundColor="$red2" borderColor="$red8">
      <XStack gap="$3" alignItems="center">
        <AlertTriangle size={24} color="$red11" />
        <YStack flex={1}>
          <Text color="$red11" fontWeight="600">Errore</Text>
          <Text color="$red11" fontSize="$3">{error}</Text>
        </YStack>
        <Button size="$3" onPress={refetch}>Riprova</Button>
      </XStack>
    </Card>
  )
}
```

### Empty States

Pattern consistente per stati vuoti:

```typescript
if (items.length === 0) {
  return (
    <Card padding="$6" alignItems="center">
      <Text color="$color10" fontSize="$5" textAlign="center">
        Nessun elemento trovato
      </Text>
    </Card>
  )
}
```

---

## 🎯 Tamagui Best Practices

### Spacing

Usa sempre i token di spacing:

```typescript
// ✅ Good: Token di spacing
<YStack gap="$4" padding="$3" margin="$2" />

// ❌ Bad: Valori hardcoded
<YStack gap={16} padding={12} margin={8} />
```

### Colors

Usa sempre i token di colore:

```typescript
// ✅ Good: Token di colore (supporta dark mode)
<Text color="$color11" />
<Card backgroundColor="$color2" borderColor="$borderColor" />

// ❌ Bad: Colori hardcoded
<Text color="#000000" />
<Card backgroundColor="#ffffff" />
```

### Font Sizes

Usa sempre i token di dimensione:

```typescript
// ✅ Good: Token di font size
<Text fontSize="$3" />
<H3 fontSize="$6" />

// ❌ Bad: Dimensioni hardcoded
<Text fontSize={14} />
```

### Responsive

Usa `useMedia` hook per responsive:

```typescript
import { useMedia } from 'tamagui'

const media = useMedia()
const isDesktop = media.gtMd ?? false
const isMobile = !isDesktop

<YStack gap={isDesktop ? '$4' : '$3'} />
```

---

## 📊 Component Categories Summary

| Category | Components | Purpose |
|----------|-----------|---------|
| **Widgets** | LeagueSelectorsWidget, LeagueCalendarWidget, LeagueRankingWidget, SerieALiveWidget | Widget completi per funzionalità specifiche |
| **Layout** | Container, ScreenContent, BackButton, NavigationBar | Struttura pagina e navigazione |
| **Lineup** | SoccerField, PlayerCard, PlayerSwapModal, PlayerViewModal | Gestione formazione e giocatori |
| **Icons** | BonusIcons, SoccerBallSVG, AssistSVG, BehaviourCardSVG | Icone ed elementi grafici |
| **Team** | TeamLogo | Elementi relativi a squadre |
| **Theme** | SwitchThemeButton, SplashScreen, FantasoccerLogo | Tema e branding |

---

## 🚨 Regole Fondamentali

### ❌ NON fare:

1. **Ricreare widget esistenti** - Controlla sempre questo file prima!
2. **Hardcodare colori/spacing** - Usa sempre i token Tamagui
3. **Modificare state globale in widget di filtro** - Usa state locale per filtri temporanei
4. **Creare layout custom senza Container/ScreenContent** - Usa i componenti layout
5. **Ignorare responsive** - Ogni componente deve funzionare su web e mobile
6. **Dimenticare dark mode** - Usa sempre token di colore che supportano dark mode

### ✅ Fai invece:

1. **Riusa widget esistenti** - LeagueSelectorsWidget per selezioni, widgets per visualizzazioni
2. **Usa token Tamagui** - `$color11`, `$4`, `$blue10`, etc.
3. **State locale per filtri** - `useState` per selezioni temporanee
4. **Segui pattern layout** - Container → ScreenContent → YourContent
5. **Testa su entrambi i dispositivi** - Web (desktop/tablet) e mobile
6. **Gestisci tutti gli stati** - Loading, error, empty, success

---

## 💡 Quick Reference

| Cosa ti serve | Componente da usare | File |
|---------------|---------------------|------|
| Selezione lega/anno/girone | `LeagueSelectorsWidget` | LeagueSelectorsWidget.tsx |
| Calendario partite | `LeagueCalendarWidget` | calendar/LeagueCalendarWidget.tsx |
| Classifica | `LeagueRankingWidget` | rank/LeagueRankingWidget.tsx |
| Partite live Serie A | `SerieALiveWidget` | seriea/SerieALiveWidget.tsx |
| Campo con formazione | `SoccerField` | lineup/SoccerField.tsx |
| Logo squadra | `TeamLogo` | team/TeamLogo.tsx |
| Bottone indietro | `BackButton` | layout/BackButton.tsx |
| Container pagina | `Container` + `ScreenContent` | layout/* |
| Toggle tema | `SwitchThemeButton` | layout/SwitchThemeButton.tsx |

---

## 📚 Risorse Correlate

- **prompt_app_hook.md** - Hook per data fetching da usare con i widget
- **prompt_app_service.md** - Service per le API calls
- **prompt_app_domain.md** - Types per props dei componenti
- **Tamagui Docs** - https://tamagui.dev

---

**Last Updated:** October 6, 2025  
**Total Components:** 20+ reusable components  
**UI Framework:** Tamagui with responsive design  
**Code Coverage:** Complete UI layer for consistent UX

---

## 💬 Note Finali

I componenti UI sono il **layer visuale** dell'applicazione:

- ✅ **Riusa sempre** - Non duplicare componenti esistenti
- ✅ **State locale per filtri** - LeagueSelectorsWidget non modifica context globale
- ✅ **Token Tamagui** - Colori, spacing, font sizes sempre da token
- ✅ **Responsive by default** - Web e mobile supportati nativamente
- ✅ **Pattern consistenti** - Loading, error, empty states sempre uguali

Quando crei una **nuova pagina**, usa i **widget esistenti** e i **pattern documentati** qui! 🎨
