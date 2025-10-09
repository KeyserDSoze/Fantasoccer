# IconFactory - Documentazione

Il componente `IconFactory` centralizza la gestione di tutte le icone utilizzate nel fantacalcio.

## Utilizzo Base

```tsx
import { IconFactory } from 'components/lineup/icons'

// Icona con dimensione default (16px)
<IconFactory type="goal" />

// Icona con dimensione personalizzata
<IconFactory type="assist" size={24} />
```

## Tipi di Icone Disponibili

| Tipo | Icona | Descrizione |
|------|-------|-------------|
| `goal` | ⚽ (bianco/nero) | Gol normale |
| `penalty` | ⚽ (nero/bianco) | Rigore segnato |
| `assist` | 🎯 | Assist fornito |
| `stoppedPenalty` | 🧤 | Rigore parato (portieri) |
| `wrongedPenalty` | ❌ | Rigore sbagliato |
| `ownGoal` | 🔴 | Autogol |
| `sufferedGoal` | ⚽ (rosso) | Gol subito |
| `yellowCard` | 🟨 | Ammonizione |
| `redCard` | 🟥 | Espulsione |
| `injury` | 🩹 | Infortunio |
| `manOfTheMatch` | ⭐ | Migliore in campo |

## Componente con Label

```tsx
import { IconWithLabel } from 'components/lineup/icons'

// Usa il label di default
<IconWithLabel type="goal" />

// Label personalizzato
<IconWithLabel type="goal" customLabel="Gol fatto" />
```

## Hook per Labels

```tsx
import { useIconLabel } from 'components/lineup/icons'

const label = useIconLabel('goal') // "Gol"
```

## Esempio Completo

```tsx
import { IconFactory, useIconLabel } from 'components/lineup/icons'
import { XStack, Text } from 'tamagui'

function VoteModifier() {
  return (
    <XStack alignItems="center" gap="$2">
      <IconFactory type="goal" size={20} />
      <Text>{useIconLabel('goal')}</Text>
      <Text fontWeight="bold">+3</Text>
    </XStack>
  )
}
```

## Colori delle Icone SVG

- **Gol normale**: Pallone bianco con dettagli neri
- **Rigore segnato**: Pallone nero con dettagli bianchi
- **Gol subito**: Pallone rosso (#ef4444) con dettagli bianchi

## Note di Implementazione

- Le emoji vengono renderizzate usando `<span>` con `fontSize` per controllo dimensione
- Gli SVG usano componenti custom (`SoccerBallSVG`) con props per colori
- Size di default: `16px`
- Tutte le icone sono ottimizzate per accessibilità e performance
