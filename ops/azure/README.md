# Azure Storage: container della cache distribuita

Il container `fantazone-cache` (configurabile con `Cache:ContainerName`) contiene soltanto proiezioni temporanee e ricostruibili (probabili formazioni, voti Live provvisori, Live dei gruppi). Vedere [ADR 0012](../../ADR/0012-cache-distribuita-e-competizioni-multi-squadra.md).

- Accesso **privato**: l'API rifiuta di avviarsi con un container ad accesso pubblico.
- La scadenza logica è applicata a ogni lettura; il job SQL `cache-cleanup` rimuove ogni giorno i Blob scaduti da più di un giorno e i lock abbandonati (al massimo 5000 cancellazioni per esecuzione; l'intero namespace è sempre esaminato).
- `cache-lifecycle-policy.json` è una protezione aggiuntiva da applicare all'account (Gestione ciclo di vita): elimina documenti non modificati da 45 giorni (oltre la retention massima di 35 giorni, quella delle probabili formazioni) e, se il versioning o gli snapshot sono attivi, le versioni e gli snapshot **attivi** precedenti dopo un giorno.
- **Soft delete.** Se è attivo a livello di account (7 giorni in `infra/storage.tf`, impostazione unica per tutti i container) ogni sovrascrittura di un Blob lascia una copia nascosta, conservata e fatturata per tutto il periodo, che le regole di lifecycle **non** toccano. Non è una scadenza: un Blob che nessuno cancella o sovrascrive (per esempio le immagini dei giocatori) resta per sempre. Per la cache il problema sono le sovrascritture: per questo l'applicazione non riscrive un documento il cui contenuto non è cambiato (riconferma al massimo ogni 5 minuti i voti Live; ADR 0012). Se le copie nascoste restano troppe dopo la misura, usare un account storage dedicato alla cache senza soft delete; ridurre i giorni di soft delete toglierebbe protezione anche alle immagini.
- Applicazione (solo dopo verifica dell'account di destinazione, non eseguita da questo repository):

```bash
az storage account management-policy create --account-name <account> --resource-group <rg> --policy @ops/azure/cache-lifecycle-policy.json
```

Nell'ambiente `fcv2` la stessa regola è creata da Terraform (`infra/storage.tf`), insieme al container privato.

Ambienti che condividono l'account usano namespace diversi (`Cache:Namespace`, default: nome dell'ambiente): i percorsi sono `fantazone-cache/{namespace}/...`, quindi la regola copre tutti gli ambienti.
