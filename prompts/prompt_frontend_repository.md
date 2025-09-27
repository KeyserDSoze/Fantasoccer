# Prompt: integrare una nuova repository nel frontend Fantasoccer

### Obiettivo
Guidare l'LLM (o chi implementa) nell'aggiungere il supporto a **una nuova repository backend** nel progetto frontend React Native/Expo. Il risultato atteso è una pipeline completa che parte dai tipi TypeScript e arriva fino all'hook da usare nei componenti, seguendo gli stessi pattern già usati per `Group` (`types/group.ts`, `services/groupService.ts`, `hooks/useGroup.ts`, `configurations/repositoryConfig.ts`).

---

## Prerequisiti
- Le classi/DTO C# esposte dal backend per la nuova repository (nomi dei campi, tipi, chiavi).
- Il path REST/Repository Framework da chiamare (es. `"Competition"`, `"Rank"`, ecc.).
- Conoscenza dell'ambiente front-end: Expo 54, React Native 0.81, Tamagui, Repository Framework client (`rystem.repository.client`).

> **Convention importante**: per ogni entità esistono
> 1. **Raw interfaces** che riproducono _esattamente_ i payload C#/JSON (nomi brevi, camelCase compatibile con backend).
> 2. **Mapping functions** che trasformano le raw interfaces in oggetti TypeScript leggibili (“clean”/“friendly”).
> 3. (Opzionale) **Helper/Enhanced types** con proprietà calcolate o metodi utility.

---

## Procedura step-by-step

### 1. Creare il file `types/<entity>.ts`
1. Copiare la struttura di `types/group.ts` come riferimento.
2. Definire:
	 - `Raw` interfaces che riprendono i campi compressi delle classi C#.
	 - Le controparti “clean” con nomi parlanti.
	 - Le `mapRaw*` functions per convertire raw → clean.
	 - Eventuali helper (es. `Helper` class con metodi statici) o versioni `Enhanced<...>`.
3. Esportare tutto ciò che dovrà essere riutilizzato in service/hook.

> **Tip**: mantenere gli stessi alias usati dal backend (ad es. `i`, `n`, `l`, …) nella sezione raw, poi mappare in un oggetto pulito (`id`, `name`, `leagues`, …).

### 2. Registrare la repository in `configurations/repositoryConfig.ts`
1. Importare i nuovi raw types se necessario.
2. Nella chain `RepositoryServices.Create(baseUrl)…` aggiungere:
	 ```ts
	 .addRepository<NewEntityRaw, KeyType>(x => {
		 x.name = "NewEntity";          // Nome logico della repository
		 x.path = "NewEntity";          // Segmento API
		 x.addHeadersEnricher(() => getAuthHeaders());
		 x.addErrorHandler(handleAuthError);
	 })
	 ```
3. Verificare che `getAuthHeaders`/`handleAuthError` restino condivisi (non duplicarli).
4. Se la repository richiede parametri complessi per la chiave, definire il relativo tipo raw (`KeyRaw`) e passarlo come generic.

### 3. Creare il service `services/<entity>Service.ts`
1. Copiare lo scheletro da `groupService.ts`.
2. Includere metodi minimi:
	 - `get<Entity>(id)` → usa `RepositoryServices.Repository<Raw, Key>("NewEntity")`, esegue `get`, fa mapping raw → clean.
	 - `get<EntityPlural>()` o metodi derivati che combinano più chiamate (uso di `Promise.all`, mapping, filtri, helper).
3. Gestire errori con `try/catch`, loggare con `console.error` coerentemente al progetto, restituire fallback sensati (`null`, `[]`).
4. Esportare un’istanza singleton (`export const newEntityService = new NewEntityService()`).

### 4. Esporre gli hook in `hooks/use<Entity>.ts`
1. Riprendere la struttura di `useGroup`:
	 - State locali (`useState` per dati, loading, error).
	 - `useCallback` per la funzione `fetch` che richiama il service e gestisce mapping.
	 - `useEffect` per il fetch iniziale e (opzionale) auto-refresh (`setInterval`).
	 - Restituire una shape `Use<Entity>Result` con dati, flag e `refetch`.
2. Se servono più hook (es. lista vs singolo elemento, versioni “enhanced”, filtri), organizzarli nello stesso file seguendo le sezioni commentate come in `useGroup.ts`.

### 5. Collegare servizi/provide (quando necessario)
- Se l’entità deve essere disponibile in contesti globali (es. scelta di un elemento corrente), aggiornare i provider esistenti (`AppProvider`, ecc.) con lo stesso pattern dei gruppi:
	- Stato locale per entità correnti.
	- Effetto che richiama il nuovo hook/service al cambio di autenticazione o di dipendenze.
	- Metodi pubblici (`setCurrent<Entity>`, `refresh<EntityPlural>`, …) che richiamano il service e aggiornano il backend tramite `authService.updateUserState` se necessario.

### 6. Checklist finale
- [ ] Tipi raw, clean e mapping creati in `types/<entity>.ts`.
- [ ] Repository registrata in `repositoryConfig.ts` con headers/token refresh.
- [ ] Service dedicato con metodi principali e fallback coerenti.
- [ ] Hook(s) che espongono dati, loading, error, refetch.
- [ ] Provider o componenti aggiornati per usare i nuovi hook, se richiesto.
- [ ] Import/export aggiunti eventualmente a barrel files (`types/index.ts`, ecc.).
- [ ] (Opzionale) Test manuale con `npm run start` verificando il fetch nei log di rete.

---

## Template rapidi

### Mapping raw → clean
```ts
interface NewEntityRaw { i: string; n: string; }
interface NewEntity { id: string; name: string; }

const mapRawNewEntity = (raw: NewEntityRaw): NewEntity => ({
	id: raw.i,
	name: raw.n,
});
```

### Service base
```ts
import { RepositoryServices } from 'rystem.repository.client';
import { mapRawNewEntity, NewEntity, NewEntityRaw } from 'types/newEntity';

class NewEntityService {
	async getNewEntity(id: string): Promise<NewEntity | null> {
		try {
			const repository = RepositoryServices.Repository<NewEntityRaw, string>('NewEntity');
			const raw = await repository.get(id);
			return raw ? mapRawNewEntity(raw) : null;
		} catch (error) {
			console.error('Error fetching NewEntity:', error);
			return null;
		}
	}
}

export const newEntityService = new NewEntityService();
```

### Hook base
```ts
export function useNewEntity(id: string | null): UseNewEntityResult {
	const [entity, setEntity] = useState<NewEntity | null>(null);
	const [loading, setLoading] = useState(false);
	const [error, setError] = useState<string | null>(null);

	const fetchEntity = useCallback(async () => {
		if (!id) { setEntity(null); return; }
		setLoading(true);
		try {
			const result = await newEntityService.getNewEntity(id);
			setEntity(result);
		} catch (err) {
			setError(err instanceof Error ? err.message : 'Failed to fetch entity');
		} finally {
			setLoading(false);
		}
	}, [id]);

	useEffect(() => { fetchEntity(); }, [fetchEntity]);

	return { entity, loading, error, refetch: fetchEntity };
}
```

---

## Note aggiuntive
- Evitare di richiamare direttamente `authService.getUserInfo` nei servizi: il token refresh è già gestito in `repositoryConfig.ts`.
- Se la repository espone endpoint molto grandi, considerare paginazione o filtri lato service prima di esporre i dati all’hook.
- Mantenere i log informativi coerenti con il resto del codice (`console.debug` per tracing, `console.error` per errori reali).
- Ricordarsi di aggiornare eventuali `index.ts` o barrel files perché le nuove esportazioni siano disponibili globalmente.
