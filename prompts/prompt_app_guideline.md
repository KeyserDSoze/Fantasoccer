# 📘 Fantasoccer App - Development Guidelines

> Linee guida per lo sviluppo di nuove funzionalità e manutenzione della documentazione.
> **IMPORTANTE:** Leggi questo documento PRIMA di creare nuovi service, pagine, o componenti.

---

## 🎯 Prima di Iniziare

Quando ti viene chiesto di creare nuova funzionalità (service, pagina, componente), segui sempre questo workflow:

### 1️⃣ Consulta la Documentazione Esistente

**Prima di scrivere qualsiasi codice**, controlla questi file per capire cosa esiste già:

#### 📋 [prompt_app_domain.md](./prompt_app_domain.md)
**Cosa contiene:**
- Tutti i tipi TypeScript dell'applicazione (150+ types)
- Interfacce Raw/Clean per mapping backend/frontend
- Enums, Helper, Mappers, Constants
- 15 file di tipi documentati: auction, calendar, chance, game, group, hallOfFame, identity, liveGroup, log, market, pushNotification, rank, serieA, stat, team

**Quando consultarlo:**
- ✅ Prima di creare nuovi tipi o interfacce
- ✅ Quando devi mappare dati dal backend
- ✅ Quando lavori con entità di dominio (gruppi, leghe, giocatori, partite, etc.)
- ✅ Prima di aggiungere nuovi enum o costanti

**Esempio di utilizzo:**
```typescript
// ❌ NON fare questo (creare tipi duplicati)
interface MyPlayer { name: string, role: string }

// ✅ Controlla prompt_app_domain.md e usa i tipi esistenti
import { RealPlayer, Role } from 'types/serieA'
```

---

#### 🛠️ [prompt_app_utils.md](./prompt_app_utils.md)
**Cosa contiene:**
- 37+ funzioni di utilità documentate
- 7 file di utilities: idTokenDecoder, keyUtils, pkceHelper, playerImage, teamLogo, teamValidation, timezoneHelpers
- Helper per autenticazione, immagini, validazione, normalizzazione dati, timezone

**Quando consultarlo:**
- ✅ Prima di creare funzioni di utilità
- ✅ Quando lavori con token OAuth/JWT
- ✅ Quando gestisci URL di immagini (player/team)
- ✅ Quando validi formazioni o regole fantasy
- ✅ Quando lavori con date/timezone italiani
- ✅ Prima di normalizzare nomi giocatori

**Esempio di utilizzo:**
```typescript
// ❌ NON fare questo (ricreare logica esistente)
const playerKey = name.toLowerCase().replace(/\s/g, '')

// ✅ Controlla prompt_app_utils.md e usa le utility esistenti
import getPlayerKey from 'utils/keyUtils'
const playerKey = getPlayerKey(name) // gestisce accenti, diacritici, etc.
```

---

#### 🔧 [prompt_app_service.md](./prompt_app_service.md)
**Cosa contiene:**
- 23 service classes documentati con 200+ metodi
- Pattern Repository Framework
- Integrazione SignalR per real-time
- Zustand stores per state management
- Service di autenticazione, Serie A data, fantasy league, amministrazione

**Quando consultarlo:**
- ✅ Prima di creare nuovi service
- ✅ Quando devi chiamare API backend
- ✅ Quando lavori con dati real-time (SignalR)
- ✅ Quando implementi caching o state management
- ✅ Prima di duplicare chiamate API esistenti

**Esempio di utilizzo:**
```typescript
// ❌ NON fare questo (duplicare chiamate API)
const response = await fetch(`/api/RealPlayer/GetPlayers?year=${year}`)

// ✅ Controlla prompt_app_service.md e usa i service esistenti
import { realPlayerService } from 'services/realPlayerService'
const players = await realPlayerService.getPlayers(year)
```

---

### 2️⃣ Identifica Riutilizzo vs Nuova Implementazione

Dopo aver consultato la documentazione:

**✅ RIUTILIZZA** se esiste già:
- Usa i tipi, utility, e service esistenti
- Non duplicare logica
- Estendi interfacce esistenti invece di crearne di nuove
- Componi funzionalità combinando utility esistenti

**✅ CREA NUOVO** solo se:
- La funzionalità è genuinamente nuova
- Non esiste nulla di simile nella documentazione
- I tipi/utility esistenti non soddisfano il caso d'uso
- Serve un nuovo endpoint API non coperto dai service

---

### 3️⃣ Segui i Pattern Consolidati

Quando crei nuova funzionalità, segui i pattern documentati:

#### Pattern per Tipi di Dominio
```typescript
// 1. Interfaccia Raw (dal backend)
export interface MyEntityRaw {
  myProperty: string
  myDate: string  // Backend invia date come stringhe
}

// 2. Interfaccia Clean (frontend)
export interface MyEntity {
  myProperty: string
  myDate: Date    // Frontend usa oggetti Date
}

// 3. Enhanced Interface (con campi computed)
export interface EnhancedMyEntity extends MyEntity {
  computedField: string
}

// 4. Mapper Raw → Clean
export function mapRawMyEntity(raw: MyEntityRaw): MyEntity {
  return {
    myProperty: raw.myProperty,
    myDate: new Date(raw.myDate)
  }
}

// 5. Helper per logica di business
export class MyEntityHelper {
  static computeField(entity: MyEntity): string {
    // logica...
  }
}

// 6. Enhance function
export function enhanceMyEntity(entity: MyEntity): EnhancedMyEntity {
  return {
    ...entity,
    computedField: MyEntityHelper.computeField(entity)
  }
}
```

#### Pattern per Service con Repository Framework
```typescript
import { RepositoryServiceBase } from 'repository/RepositoryServiceBase'
import { MyEntity, MyEntityRaw, mapRawMyEntity } from 'types/myEntity'

export class MyEntityService extends RepositoryServiceBase<MyEntityRaw, MyEntity, string> {
  constructor() {
    super(
      'MyEntity',              // Nome repository
      (raw) => mapRawMyEntity(raw)  // Mapper function
    )
  }

  // Metodo base: già disponibile da RepositoryServiceBase
  // - get(key: string): Promise<MyEntity | null>
  // - getAll(): Promise<MyEntity[]>
  // - insert(entity: MyEntity): Promise<boolean>
  // - update(key: string, entity: MyEntity): Promise<boolean>
  // - delete(key: string): Promise<boolean>

  // Aggiungi metodi custom specifici del dominio
  async getByCustomCriteria(filter: string): Promise<MyEntity[]> {
    const all = await this.getAll()
    return all.filter(item => item.myProperty.includes(filter))
  }
}

export const myEntityService = new MyEntityService()
```

#### Pattern per Service HTTP (senza Repository)
```typescript
import { apiClient } from 'api/apiClient'

class MyHttpService {
  async fetchData(params: { id: string }): Promise<MyData> {
    const response = await apiClient.get<MyDataRaw>('/MyEndpoint/Get', { params })
    return mapRawMyData(response.data)
  }

  async saveData(payload: MyData): Promise<boolean> {
    const response = await apiClient.post('/MyEndpoint/Save', mapMyDataToRaw(payload))
    return response.data.success
  }
}

export const myHttpService = new MyHttpService()
```

#### Pattern per Utility Functions
```typescript
/**
 * Brief description of what the utility does.
 * 
 * @param input - Description of parameter
 * @returns Description of return value
 * @example
 * ```typescript
 * const result = myUtility("example")
 * // Returns: "EXAMPLE"
 * ```
 */
export function myUtility(input: string): string {
  // Implementation
  return input.toUpperCase()
}

// Export default se è la funzione principale del file
export default myUtility
```

#### Pattern per Timezone (Italian)
```typescript
import { convertUTCToItalianTimezone, formatGameDateTime } from 'utils/timezoneHelpers'

// Backend date (UTC+0 con tempo italiano)
const backendDate = "2024-03-15T18:00:00Z"

// Converti a timezone italiano corretto
const italianDate = convertUTCToItalianTimezone(backendDate)
// "2024-03-15T18:00:00+01:00" (inverno) o "2024-03-15T18:00:00+02:00" (estate)

// Formatta per display
const display = formatGameDateTime(italianDate)
// "Ven 15 Mar, 18:00"
```

#### Pattern per Immagini Azure Blob
```typescript
import { getPlayerImageUrlFromName } from 'utils/playerImage'
import { getTeamLogoUrlWithFallback } from 'utils/teamLogo'

// Player image con fallback automatico
const { src, fallback } = getPlayerImageUrlFromName(playerName)

// Team logo con fallback
const logo = getTeamLogoUrlWithFallback(groupId, year, email)
```

---

## 📝 Documentazione - Quando e Come Aggiornarla

### Quando Aggiornare la Documentazione

Aggiorna i file di documentazione **IMMEDIATAMENTE** dopo:

#### ✏️ Aggiornare prompt_app_domain.md quando:
- ✅ Crei nuovi file di tipi in `types/`
- ✅ Aggiungi nuove interfacce o tipi
- ✅ Aggiungi nuovi enum
- ✅ Crei nuovi helper o mapper
- ✅ Modifichi interfacce esistenti (breaking changes)
- ✅ Aggiungi interfacce Enhanced o Raw

**Esempio di aggiornamento:**
```markdown
## 📁 File: types/myNewEntity.ts

**Purpose:** Brief description of what this type represents.

### Enums
- **MyEnum**: Description (Value1, Value2, Value3)

### Interfaces
- **MyEntity** (MyEntityRaw): Description of entity (field1, field2, field3)
- **EnhancedMyEntity**: MyEntity + computed fields (computedField1, computedField2)

### Helpers
- **MyEntityHelper**: Description of helper methods

### Mappers
- `mapRawMyEntity()`: MyEntityRaw → MyEntity
- `enhanceMyEntity()`: MyEntity → EnhancedMyEntity
```

#### ✏️ Aggiornare prompt_app_utils.md quando:
- ✅ Crei nuovi file utility in `utils/`
- ✅ Aggiungi nuove funzioni di utilità
- ✅ Modifichi signature di funzioni esistenti
- ✅ Aggiungi nuove costanti o configurazioni
- ✅ Aggiungi helper classes

**Esempio di aggiornamento:**
```markdown
## 📁 File: utils/myNewUtil.ts

**Purpose:** Description of what this utility does.

### Functions

#### `myUtilityFunction(param: string): ReturnType`
Description of what the function does.
- **Parameters:** Param description
- **Returns:** Return value description
- **Example:** "Input" → "output"

### Usage Example
\`\`\`typescript
import { myUtilityFunction } from 'utils/myNewUtil'
const result = myUtilityFunction("test")
\`\`\`
```

#### ✏️ Aggiornare prompt_app_service.md quando:
- ✅ Crei nuovi service in `services/`
- ✅ Aggiungi nuovi metodi a service esistenti
- ✅ Modifichi signature di metodi esistenti
- ✅ Aggiungi nuovi endpoint API
- ✅ Modifichi pattern di caching o state management
- ✅ Aggiungi event handlers SignalR

**Esempio di aggiornamento:**
```markdown
## 📁 File: services/myNewService.ts

**Purpose:** Description of what this service handles.

### Class: MyNewService

#### Main Operations

##### `myMethod(params: Params): Promise<ReturnType>`
Description of what the method does.
- **Parameters:** Param descriptions
- **Returns:** Return type description
- **Repository/Endpoint:** Repository name or API endpoint
- **Use Case:** When to use this method

### Singleton Export
\`\`\`typescript
export const myNewService = new MyNewService()
\`\`\`
```

---

### Come Mantenere la Documentazione Sincronizzata

**Workflow consigliato:**

1. **Durante lo sviluppo:**
   - Prendi nota delle modifiche che fai
   - Tieni aperto il file di documentazione rilevante

2. **Prima del commit:**
   - Aggiorna i file di documentazione pertinenti
   - Verifica che gli esempi siano ancora accurati
   - Aggiorna la sezione "Last Updated" in fondo al file

3. **Breaking Changes:**
   - Documenta chiaramente cosa è cambiato
   - Spiega il motivo del cambiamento
   - Fornisci esempi di migrazione se necessario

4. **Nuove feature:**
   - Aggiungi esempi di utilizzo completi
   - Documenta tutti i parametri e return types
   - Spiega use cases comuni

---

## 🏗️ Checklist per Nuova Funzionalità

Prima di considerare completo il lavoro, verifica:

### Per Nuovi Tipi
- [ ] Ho controllato `prompt_app_domain.md` per tipi esistenti simili
- [ ] Ho creato interfacce Raw e Clean
- [ ] Ho creato mapper bidirezionali (se necessario)
- [ ] Ho creato Enhanced interface con campi computed (se serve)
- [ ] Ho creato Helper class per logica di business
- [ ] Ho aggiornato `prompt_app_domain.md` con la nuova documentazione

### Per Nuove Utility
- [ ] Ho controllato `prompt_app_utils.md` per utility esistenti simili
- [ ] La funzione è pura e riutilizzabile
- [ ] Ho aggiunto JSDoc comments
- [ ] Ho aggiunto esempi di utilizzo
- [ ] Ho gestito edge cases (null, undefined, empty strings)
- [ ] Ho aggiornato `prompt_app_utils.md` con la nuova documentazione

### Per Nuovi Service
- [ ] Ho controllato `prompt_app_service.md` per service esistenti simili
- [ ] Ho esteso `RepositoryServiceBase` se uso Repository Framework
- [ ] Ho implementato mapper Raw → Clean
- [ ] Ho gestito errori appropriatamente
- [ ] Ho implementato caching se necessario
- [ ] Ho esportato singleton instance
- [ ] Ho aggiornato `prompt_app_service.md` con la nuova documentazione

### Per Nuove Pagine/Componenti
- [ ] Ho riutilizzato componenti esistenti dove possibile
- [ ] Ho usato service esistenti per data fetching
- [ ] Ho usato tipi di dominio corretti
- [ ] Ho gestito loading e error states
- [ ] Ho seguito pattern di navigation esistenti
- [ ] Ho testato su web e mobile (se cross-platform)

---

## 🔄 Pattern di Integrazione

### Repository Framework
```typescript
// 1. Definisci il tipo nel repository (backend)
// 2. Crea Raw interface in types/
// 3. Crea Clean interface in types/
// 4. Crea mapper in types/
// 5. Crea service estendendo RepositoryServiceBase
// 6. Esporta singleton del service
// 7. Usa il service nelle pagine/componenti
```

### HTTP Endpoints (non-Repository)
```typescript
// 1. Verifica in prompt_app_service.md se l'endpoint esiste già
// 2. Crea/riusa tipi di request e response
// 3. Crea metodo nel service appropriato
// 4. Gestisci errori e autenticazione
// 5. Usa il service nelle pagine/componenti
```

### SignalR Real-time
```typescript
// 1. Definisci event handlers interface
// 2. Estendi classe base o crea nuovo hub client
// 3. Implementa connection management
// 4. Implementa hub methods (invoke)
// 5. Implementa event handlers (on)
// 6. Gestisci reconnection e cleanup
// 7. Usa il service nelle pagine/componenti
```

---

## 💡 Best Practices

### Naming Conventions
- **Types/Interfaces:** PascalCase (es. `RealPlayer`, `GameWrapper`)
- **Enums:** PascalCase per enum, PascalCase per values (es. `Role.Goalkeeper`)
- **Functions/Methods:** camelCase (es. `getPlayerKey`, `mapRawPlayer`)
- **Files:** camelCase (es. `realPlayerService.ts`, `teamValidation.ts`)
- **Constants:** UPPER_SNAKE_CASE (es. `DEFAULT_PLAYER_IMAGE_KEY`)

### File Organization
```
src/
  types/           # Domain types (Raw, Clean, Enhanced, Helpers, Mappers)
  utils/           # Pure utility functions
  services/        # API integration & business logic
  repository/      # Repository Framework base classes
  components/      # Reusable UI components
  screens/         # Pages/Screens
  hooks/           # Custom React hooks
  stores/          # Zustand state stores
```

### Code Quality
- ✅ Preferisci immutabilità (const, readonly, spread operators)
- ✅ Usa TypeScript strict mode
- ✅ Gestisci tutti i casi null/undefined
- ✅ Valida input prima di processare
- ✅ Usa async/await invece di Promise chains
- ✅ Gestisci errori con try/catch
- ✅ Logga errori per debugging

### Performance
- ✅ Implementa caching dove appropriato (vedi `liveGroupService`)
- ✅ Usa lazy loading per componenti pesanti
- ✅ Memoizza calcoli costosi
- ✅ Precarica immagini critiche
- ✅ Evita re-renders inutili (React.memo, useMemo, useCallback)

---

## 🚨 Errori Comuni da Evitare

### ❌ NON fare:
1. **Duplicare logica esistente** - Controlla sempre la documentazione prima!
2. **Creare tipi duplicati** - Riusa interfacce esistenti
3. **Ignorare mapper Raw/Clean** - Backend e frontend hanno rappresentazioni diverse
4. **Dimenticare timezone** - Usa sempre `timezoneHelpers` per date italiane
5. **Hardcodare URL immagini** - Usa `playerImage` e `teamLogo` utilities
6. **Creare chiamate API dirette** - Usa i service esistenti
7. **Ignorare gestione errori** - Gestisci sempre casi di fallimento
8. **Dimenticare di aggiornare documentazione** - Aggiorna SEMPRE i prompt file!

### ✅ Fai invece:
1. **Leggi i prompt file PRIMA di iniziare**
2. **Riusa tipi, utility, e service esistenti**
3. **Segui i pattern consolidati**
4. **Documenta mentre sviluppi**
5. **Testa su web e mobile (se cross-platform)**
6. **Gestisci edge cases**
7. **Scrivi codice auto-documentante**
8. **Mantieni la documentazione aggiornata**

---

## 📊 Quick Reference

| Cosa stai facendo | Controlla prima | Pattern da seguire | Aggiorna dopo |
|-------------------|-----------------|-------------------|---------------|
| Creare nuovi tipi | `prompt_app_domain.md` | Raw/Clean/Enhanced + Mapper + Helper | `prompt_app_domain.md` |
| Creare utility | `prompt_app_utils.md` | Pure function + JSDoc + Examples | `prompt_app_utils.md` |
| Creare service | `prompt_app_service.md` | RepositoryServiceBase o HTTP client | `prompt_app_service.md` |
| Lavorare con date | `prompt_app_utils.md` → timezoneHelpers | convertUTCToItalianTimezone | - |
| Lavorare con immagini | `prompt_app_utils.md` → playerImage/teamLogo | getPlayerImageUrlFromName | - |
| Validare formazioni | `prompt_app_utils.md` → teamValidation | validateFormation | - |
| Chiamare API | `prompt_app_service.md` | Usa service esistente | `prompt_app_service.md` (se nuovo) |
| Real-time data | `prompt_app_service.md` → auctionService | SignalR connection + events | `prompt_app_service.md` (se nuovo) |

---

## 🎓 Esempio Completo: Aggiungere Nuova Feature

**Scenario:** Voglio aggiungere funzionalità per gestire "Scommesse" tra utenti.

### Step 1: Research
```bash
# 1. Leggi prompt_app_domain.md
# Cerco: tipi simili (Market per trade, Game per partite)
# Decido: Serve nuovo tipo Bet

# 2. Leggi prompt_app_service.md
# Cerco: service simili (marketService per gestire offerte)
# Decido: Serve nuovo betService

# 3. Leggi prompt_app_utils.md
# Cerco: utility per validazione
# Decido: Posso riusare utility esistenti
```

### Step 2: Implementation

**types/bet.ts**
```typescript
// Raw interface (backend)
export interface BetRaw {
  id: string
  creatorEmail: string
  targetEmail: string
  amount: number
  description: string
  status: number  // 0=Pending, 1=Accepted, 2=Declined
  createdAt: string
}

// Clean interface (frontend)
export interface Bet {
  id: string
  creatorEmail: string
  targetEmail: string
  amount: number
  description: string
  status: BetStatus
  createdAt: Date
}

// Enum
export enum BetStatus {
  Pending = 0,
  Accepted = 1,
  Declined = 2
}

// Enhanced
export interface EnhancedBet extends Bet {
  isPending: boolean
  isResolved: boolean
  statusLabel: string
}

// Mapper
export function mapRawBet(raw: BetRaw): Bet {
  return {
    id: raw.id,
    creatorEmail: raw.creatorEmail,
    targetEmail: raw.targetEmail,
    amount: raw.amount,
    description: raw.description,
    status: raw.status as BetStatus,
    createdAt: new Date(raw.createdAt)
  }
}

// Helper
export class BetHelper {
  static getStatusLabel(status: BetStatus): string {
    switch (status) {
      case BetStatus.Pending: return 'In attesa'
      case BetStatus.Accepted: return 'Accettata'
      case BetStatus.Declined: return 'Rifiutata'
    }
  }

  static isPending(bet: Bet): boolean {
    return bet.status === BetStatus.Pending
  }

  static isResolved(bet: Bet): boolean {
    return bet.status !== BetStatus.Pending
  }
}

// Enhance
export function enhanceBet(bet: Bet): EnhancedBet {
  return {
    ...bet,
    isPending: BetHelper.isPending(bet),
    isResolved: BetHelper.isResolved(bet),
    statusLabel: BetHelper.getStatusLabel(bet.status)
  }
}
```

**services/betService.ts**
```typescript
import { RepositoryServiceBase } from 'repository/RepositoryServiceBase'
import { Bet, BetRaw, mapRawBet, enhanceBet, EnhancedBet, BetStatus } from 'types/bet'

export class BetService extends RepositoryServiceBase<BetRaw, Bet, string> {
  constructor() {
    super('Bet', (raw) => mapRawBet(raw))
  }

  async getBetsForUser(email: string): Promise<Bet[]> {
    const all = await this.getAll()
    return all.filter(bet => 
      bet.creatorEmail === email || bet.targetEmail === email
    )
  }

  async getEnhancedBetsForUser(email: string): Promise<EnhancedBet[]> {
    const bets = await this.getBetsForUser(email)
    return bets.map(enhanceBet)
  }

  async getPendingBets(email: string): Promise<Bet[]> {
    const bets = await this.getBetsForUser(email)
    return bets.filter(bet => bet.status === BetStatus.Pending)
  }

  async acceptBet(betId: string): Promise<boolean> {
    const bet = await this.get(betId)
    if (!bet || bet.status !== BetStatus.Pending) return false
    
    return this.update(betId, { ...bet, status: BetStatus.Accepted })
  }

  async declineBet(betId: string): Promise<boolean> {
    const bet = await this.get(betId)
    if (!bet || bet.status !== BetStatus.Pending) return false
    
    return this.update(betId, { ...bet, status: BetStatus.Declined })
  }
}

export const betService = new BetService()
```

### Step 3: Documentation Update

**Aggiorna prompt_app_domain.md:**
```markdown
## 📁 File: types/bet.ts

**Purpose:** User betting system with accept/decline workflow.

### Enums
- **BetStatus**: Bet status (Pending, Accepted, Declined)

### Interfaces
- **Bet** (BetRaw): Bet offer (id, creatorEmail, targetEmail, amount, description, status, createdAt)
- **EnhancedBet**: Bet + computed flags (isPending, isResolved, statusLabel)

### Helpers
- **BetHelper**: Get status label, check status flags

### Mappers
- `mapRawBet()`: BetRaw → Bet
- `enhanceBet()`: Bet → EnhancedBet
```

**Aggiorna prompt_app_service.md:**
```markdown
## 📁 File: services/betService.ts

**Purpose:** Manage user bets with accept/decline workflow using Repository Framework.

### Class: BetService

#### Main Operations

##### `getBetsForUser(email: string): Promise<Bet[]>`
Get all bets for a user (as creator or target).

##### `getEnhancedBetsForUser(email: string): Promise<EnhancedBet[]>`
Get enhanced bets with computed properties.

##### `getPendingBets(email: string): Promise<Bet[]>`
Get pending bets requiring action.

##### `acceptBet(betId: string): Promise<boolean>`
Accept a pending bet.

##### `declineBet(betId: string): Promise<boolean>`
Decline a pending bet.

### Singleton Export
\`\`\`typescript
export const betService = new BetService()
\`\`\`
```

### Step 4: Usage in Component
```typescript
import { betService } from 'services/betService'
import { EnhancedBet } from 'types/bet'

export function BetsScreen() {
  const [bets, setBets] = useState<EnhancedBet[]>([])
  const { user } = useAuth()

  useEffect(() => {
    loadBets()
  }, [user])

  async function loadBets() {
    if (!user) return
    const data = await betService.getEnhancedBetsForUser(user.email)
    setBets(data)
  }

  async function handleAccept(betId: string) {
    const success = await betService.acceptBet(betId)
    if (success) await loadBets()
  }

  async function handleDecline(betId: string) {
    const success = await betService.declineBet(betId)
    if (success) await loadBets()
  }

  return (
    <View>
      {bets.map(bet => (
        <BetCard
          key={bet.id}
          bet={bet}
          onAccept={() => handleAccept(bet.id)}
          onDecline={() => handleDecline(bet.id)}
        />
      ))}
    </View>
  )
}
```

---

## 📚 Risorse

- **prompt_app_domain.md** - Tutti i tipi di dominio
- **prompt_app_utils.md** - Tutte le utility functions
- **prompt_app_service.md** - Tutti i service layer
- **prompt_app_guideline.md** - Questo file (linee guida sviluppo)

---

**Last Updated:** October 6, 2025  
**Versione:** 1.0  
**Maintainer:** Team Fantasoccer

---

## 💬 Note Finali

Questa documentazione è viva e deve essere mantenuta aggiornata. Se trovi pattern migliori o hai suggerimenti, aggiorna questo file e condividi con il team.

**Ricorda:** La documentazione non è un peso, è un investimento che ripaga ogni volta che eviti di duplicare codice o riscrivere funzionalità già esistenti. 🚀
