# AppIdentity Repository - Documentazione

## Panoramica

Il repository `AppIdentity` gestisce le informazioni sull'identità utente nell'applicazione Fantasoccer, inclusi:
- **Email** (chiave primaria)
- **Username** (opzionale)
- **Stato applicativo** (gruppo, lega, anno correnti)
- **Gruppi** a cui l'utente appartiene con relativi ruoli

## Architettura

### 1. Types (`types/identity.ts`)

#### Raw Interfaces
```typescript
interface AppIdentityRaw {
  u?: string;  // Username
  e: string;   // Email
  s: AppIdentityStateRaw;  // State
  g: GroupForIdentityRaw[];  // Groups
}
```

#### Clean Interfaces
```typescript
interface AppIdentity {
  id?: string;
  username?: string;
  email: string;
  state: AppIdentityState;
  groups: GroupForIdentity[];
  raw: AppIdentityRaw;
}
```

#### Enums
```typescript
enum IdentityRole {
  None = 0,
  Reader = 1,
  Participant = 2,
  Admin = 4,
  SuperAdmin = 8
}
```

### 2. Service (`services/appIdentityService.ts`)

Il servizio fornisce metodi per:

#### Operazioni CRUD Base
- `getAppIdentity(email: string)` - Recupera identità utente
- `updateAppIdentity(email: string, identity: AppIdentity)` - Aggiorna identità
- `insertAppIdentity(identity: AppIdentity)` - Crea nuova identità
- `deleteAppIdentity(email: string)` - Elimina identità

#### Gestione Stato
- `updateUserState(email: string, stateUpdates: Partial<AppIdentityState>)` - Aggiorna stato
- `updateUserGroup(email: string, groupId: string)` - Cambia gruppo corrente
- `updateUserLeague(email: string, leagueId: string)` - Cambia lega corrente
- `updateUserYear(email: string, year: number)` - Cambia anno corrente
- `updateUserWorkingEmail(email: string, workingEmail: string)` - Imposta email di lavoro

#### Gestione Gruppi
- `addUserGroup(email: string, newGroup)` - Aggiunge gruppo
- `removeUserGroup(email: string, groupId: string)` - Rimuove gruppo
- `updateUserRoleInGroup(email: string, groupId: string, newRole: IdentityRole)` - Aggiorna ruolo

#### Query Utility
- `getUserGroups(email: string)` - Ottiene lista gruppi
- `getUserState(email: string)` - Ottiene stato corrente
- `userHasRoleInGroup(email: string, groupId: string, role: IdentityRole)` - Verifica ruolo
- `userIsAdminInGroup(email: string, groupId: string)` - Verifica se è admin
- `userIsParticipantInGroup(email: string, groupId: string)` - Verifica se è partecipante

### 3. Hooks (`hooks/useAppIdentity.ts`)

#### `useAppIdentity(email: string | null, options?)`
Hook principale per gestire l'identità utente.

**Esempio:**
```typescript
function UserProfile() {
  const { identity, loading, error, refetch } = useAppIdentity(
    user?.email ?? null,
    { refreshInterval: 60000 } // Auto-refresh ogni minuto
  );

  if (loading) return <Spinner />;
  if (error) return <Text>Error: {error}</Text>;
  
  return (
    <YStack>
      <Text>Email: {identity?.email}</Text>
      <Text>Username: {identity?.username}</Text>
      <Text>Groups: {identity?.groups.length}</Text>
    </YStack>
  );
}
```

#### `useUserState(email: string | null)`
Hook per gestire lo stato applicativo dell'utente.

**Esempio:**
```typescript
function UserStateManager() {
  const { 
    state, 
    loading, 
    error, 
    updateGroup,
    updateLeague,
    updateYear 
  } = useUserState(user?.email ?? null);

  const handleGroupChange = async (newGroupId: string) => {
    const success = await updateGroup(newGroupId);
    if (success) {
      console.log('Group updated successfully');
    }
  };

  return (
    <YStack>
      <Text>Current Group: {state?.groupId}</Text>
      <Text>Current League: {state?.leagueId}</Text>
      <Text>Current Year: {state?.year}</Text>
      <Button onPress={() => handleGroupChange('new-group-id')}>
        Change Group
      </Button>
    </YStack>
  );
}
```

#### `useUserGroups(email: string | null)`
Hook per gestire i gruppi dell'utente.

**Esempio:**
```typescript
function UserGroupsList() {
  const { 
    groups, 
    loading, 
    error, 
    addGroup,
    removeGroup,
    hasRoleInGroup,
    isAdminInGroup 
  } = useUserGroups(user?.email ?? null);

  const handleAddGroup = async () => {
    const success = await addGroup({
      id: 'new-group-id',
      name: 'New Group',
      role: IdentityRole.Participant
    });
    if (success) {
      console.log('Group added');
    }
  };

  return (
    <YStack>
      {groups.map(group => (
        <Card key={group.id}>
          <Text>{group.name}</Text>
          <Text>Role: {group.role}</Text>
          {isAdminInGroup(group.id) && (
            <Text>You are admin</Text>
          )}
          <Button onPress={() => removeGroup(group.id)}>
            Leave Group
          </Button>
        </Card>
      ))}
      <Button onPress={handleAddGroup}>Add Group</Button>
    </YStack>
  );
}
```

#### `useAppIdentityOperations()`
Hook per operazioni CRUD sull'identità.

**Esempio:**
```typescript
function IdentityManager() {
  const { createIdentity, updateIdentity, deleteIdentity } = useAppIdentityOperations();

  const handleCreateIdentity = async () => {
    const newIdentity: AppIdentity = {
      email: 'user@example.com',
      username: 'newuser',
      state: {
        groupId: undefined,
        leagueId: undefined,
        year: undefined,
        meOrLeaderEmail: undefined,
        raw: { g: undefined, l: undefined, y: undefined, m: undefined }
      },
      groups: [],
      raw: {
        e: 'user@example.com',
        u: 'newuser',
        s: { g: undefined, l: undefined, y: undefined, m: undefined },
        g: []
      }
    };

    const success = await createIdentity(newIdentity);
    if (success) {
      console.log('Identity created');
    }
  };

  return (
    <Button onPress={handleCreateIdentity}>
      Create New Identity
    </Button>
  );
}
```

## Integrazione con AppProvider

Il repository può essere integrato nell'`AppProvider` esistente per gestire l'identità utente globalmente:

```typescript
// In AppProvider.tsx
import { useAppIdentity, useUserState, useUserGroups } from 'hooks/useAppIdentity';

export function AppProvider({ children }) {
  const { user } = useAuth(); // Hook esistente per autenticazione
  
  // Usa i nuovi hook per gestire l'identità
  const { identity, loading: identityLoading } = useAppIdentity(user?.email ?? null);
  const { state, updateGroup, updateLeague, updateYear } = useUserState(user?.email ?? null);
  const { groups, isAdminInGroup } = useUserGroups(user?.email ?? null);

  // Esponi dati e metodi nel contesto
  const contextValue = {
    // ... altri valori esistenti
    identity,
    currentState: state,
    userGroups: groups,
    setCurrentGroup: updateGroup,
    setCurrentLeague: updateLeague,
    setCurrentYear: updateYear,
    checkIsAdmin: isAdminInGroup,
  };

  return (
    <AppContext.Provider value={contextValue}>
      {children}
    </AppContext.Provider>
  );
}
```

## Gestione Ruoli

I ruoli sono gestiti con flag binari (bitwise):

```typescript
// Verifica singolo ruolo
const isAdmin = hasRole(userRole, IdentityRole.Admin);

// Verifica multipli ruoli
const canModify = hasRole(userRole, IdentityRole.Admin | IdentityRole.Participant);

// Ottieni nomi ruoli
const roleNames = getUserRoleNames(userRole); // ['Admin', 'Participant']

// Uso negli hook
const { isAdminInGroup, isParticipantInGroup } = useUserGroups(email);
const canManageGroup = isAdminInGroup(groupId);
```

## Note Implementative

### Chiave Repository
La chiave del repository è l'**email** dell'utente (string).

### Repository Config
Il repository è già configurato in `configurations/repositoryConfig.ts`:
```typescript
.addRepository<AppIdentityRaw, string>(x => {
  x.name = "AppIdentity";
  x.path = "AppIdentity";
  x.addHeadersEnricher(() => getAuthHeaders());
  x.addErrorHandler(handleAuthError);
})
```

### Mapping Raw ↔ Clean
Tutte le operazioni utilizzano funzioni di mapping per convertire tra raw e clean:
- `mapAppIdentity(raw)` - Raw → Clean
- `mapAppIdentityToRaw(identity)` - Clean → Raw

### Error Handling
Tutti i metodi service/hook gestiscono gli errori con try/catch e restituiscono valori fallback:
- Service: `null` o `[]` o `false`
- Hook: `error` state viene popolato

### Performance
Gli hook supportano auto-refresh opzionale tramite `refreshInterval`:
```typescript
const { identity } = useAppIdentity(email, { refreshInterval: 30000 }); // Refresh ogni 30s
```

## Testing

Esempio di test manuale in console:

```typescript
import { appIdentityService } from 'services/appIdentityService';

// Get identity
const identity = await appIdentityService.getAppIdentity('user@example.com');
console.log('Identity:', identity);

// Update state
await appIdentityService.updateUserGroup('user@example.com', 'new-group-id');

// Add group
await appIdentityService.addUserGroup('user@example.com', {
  id: 'group-123',
  name: 'My Group',
  role: IdentityRole.Admin
});

// Check role
const isAdmin = await appIdentityService.userIsAdminInGroup(
  'user@example.com',
  'group-123'
);
console.log('Is admin:', isAdmin);
```
