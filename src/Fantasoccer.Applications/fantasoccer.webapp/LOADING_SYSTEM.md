# 🎯 Sistema di Loading Globale - Fantasoccer

## 📖 Panoramica

Il sistema di loading globale fornisce un overlay animato con un pallone ⚽ che può essere utilizzato ovunque nell'app durante le chiamate API o operazioni asincrone.

## 🏗️ Componenti

### 1. LoadingOverlay
Componente UI che mostra l'animazione del pallone con overlay scuro.

### 2. LoadingProvider
Provider context che gestisce lo stato globale del loading.

### 3. useGlobalLoading Hook
Hook per controllare il loading state da qualsiasi componente.

## 🚀 Setup Iniziale

### 1. Wrappa l'app con LoadingProvider
```tsx
// In app/_layout.tsx o nel root component
import { LoadingProvider } from '../providers/LoadingProvider'

export default function Layout() {
  return (
    <LoadingProvider>
      <AuthProvider>
        <YourAppContent />
      </AuthProvider>
    </LoadingProvider>
  )
}
```

## 💡 Come Usare

### 1. Uso Semplice
```tsx
import { useGlobalLoading } from '../providers/LoadingProvider'

const MyComponent = () => {
  const { showLoading, hideLoading } = useGlobalLoading()

  const handleAction = async () => {
    showLoading("Caricamento dati...")
    
    try {
      // La tua API call
      await apiCall()
      hideLoading()
    } catch (error) {
      hideLoading()
      // Gestisci errore
    }
  }
}
```

### 2. Uso con withLoading (Raccomandato)
```tsx
import { useGlobalLoading } from '../providers/LoadingProvider'

const MyComponent = () => {
  const { withLoading } = useGlobalLoading()

  const handleAction = async () => {
    try {
      const data = await withLoading(
        apiCall(), 
        "Caricamento dati Serie A..."
      )
      // Usa i dati
    } catch (error) {
      // Gestisci errore - loading già nascosto automaticamente
    }
  }
}
```

### 3. Nei Servizi API
```tsx
// In serieAService.ts
import { useGlobalLoading } from '../providers/LoadingProvider'

export class SerieAService {
  async getRealCalendar(year: number) {
    // Il loading può essere gestito dal componente che chiama
    // oppure direttamente nel servizio
    
    try {
      const response = await fetch(url)
      const data = await response.json()
      return data
    } catch (error) {
      throw error
    }
  }
}

// Nel componente
const calendar = await withLoading(
  serieAService.getRealCalendar(2024),
  "Caricamento calendario Serie A..."
)
```

## 🎨 Personalizzazione

### Messaggi Personalizzati
```tsx
// Diversi messaggi per diverse operazioni
await withLoading(loginAPI(), "Effettuando login...")
await withLoading(loadGames(), "Caricamento partite...")
await withLoading(saveTeam(), "Salvando la squadra...")
```

### Loading Condizionale
```tsx
const { isLoading, showLoading, hideLoading } = useGlobalLoading()

// Non mostrare loading se già in corso
if (!isLoading) {
  showLoading("Operazione in corso...")
}
```

## 📱 Animazioni

### Pallone Animato
- **Bounce**: Il pallone rimbalza su e giù
- **Scale**: Leggero effetto di zoom durante il bounce
- **Punti**: Tre punti animati che lampeggiano

### Overlay
- **Fade In/Out**: Transizione morbida con AnimatePresence
- **Background Blur**: Overlay scuro semitrasparente
- **Card Elevation**: Ombra e elevazione per la card centrale

## 🔧 Integrazione con AuthProvider

```tsx
// In AuthProvider.tsx
const { withLoading } = useGlobalLoading()

const exchangeToken = async (code: string) => {
  return await withLoading(
    authService.exchangeToken(code),
    "Verifica credenziali..."
  )
}

const getUserInfo = async (token: string) => {
  return await withLoading(
    authService.getUserInfo(token),
    "Caricamento profilo utente..."
  )
}
```

## 🎯 Best Practices

### 1. Messaggi Descrittivi
```tsx
// ❌ Generico
withLoading(apiCall(), "Caricamento...")

// ✅ Specifico
withLoading(apiCall(), "Caricamento classifica fantacalcio...")
```

### 2. Gestione Errori
```tsx
try {
  await withLoading(apiCall(), "Operazione in corso...")
} catch (error) {
  // Loading nascosto automaticamente
  showErrorMessage(error.message)
}
```

### 3. Non Nidificare Loading
```tsx
// ❌ Loading doppio
const data1 = await withLoading(api1(), "Loading 1...")
const data2 = await withLoading(api2(), "Loading 2...") // Sovrappone il primo

// ✅ Loading singolo per operazioni multiple
await withLoading(
  Promise.all([api1(), api2()]),
  "Caricamento dati completi..."
)
```

## 🚀 Esempi Completi

### Home Screen con Loading
```tsx
const HomeScreen = () => {
  const { withLoading } = useGlobalLoading()
  const [calendar, setCalendar] = useState(null)

  useEffect(() => {
    const loadInitialData = async () => {
      try {
        const data = await withLoading(
          serieAService.getCurrentYearCalendar(),
          "Caricamento calendario Serie A..."
        )
        setCalendar(data)
      } catch (error) {
        console.error("Errore caricamento:", error)
      }
    }

    loadInitialData()
  }, [])

  return (
    <YStack>
      {calendar ? (
        <CalendarView data={calendar} />
      ) : (
        <Text>Nessun dato disponibile</Text>
      )}
    </YStack>
  )
}
```

Il sistema è ora pronto per essere utilizzato in tutta l'app! 🎉⚽