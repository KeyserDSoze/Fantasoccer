# Azure Storage: container della cache distribuita

Il container `fantazone-cache` (configurabile con `Cache:ContainerName`) contiene soltanto proiezioni temporanee e ricostruibili (probabili formazioni, voti Live provvisori, Live dei gruppi). Vedere [ADR 0012](../../ADR/0012-cache-distribuita-e-competizioni-multi-squadra.md).

- Accesso **privato**: l'API rifiuta di avviarsi con un container ad accesso pubblico.
- La scadenza logica è applicata a ogni lettura; il job SQL `cache-cleanup` rimuove ogni giorno i Blob scaduti da più di un giorno e i lock abbandonati.
- `cache-lifecycle-policy.json` è una protezione aggiuntiva da applicare all'account (Gestione ciclo di vita): elimina documenti non modificati da 30 giorni (oltre la retention massima di 21 giorni) e, se il versioning o gli snapshot sono attivi, le versioni precedenti dopo un giorno. I voti Live sono riscritti ogni minuto durante le partite: senza questa regola il versioning accumulerebbe migliaia di versioni per giornata.
- Se il soft delete dei Blob è attivo a livello di account, i Blob cancellati restano recuperabili (e fatturati) per il periodo configurato: per la cache è consigliato un account o un periodo breve.
- Applicazione (solo dopo verifica dell'account di destinazione, non eseguita da questo repository):

```bash
az storage account management-policy create --account-name <account> --resource-group <rg> --policy @ops/azure/cache-lifecycle-policy.json
```

Ambienti che condividono l'account usano namespace diversi (`Cache:Namespace`, default: nome dell'ambiente): i percorsi sono `fantazone-cache/{namespace}/...`, quindi la regola copre tutti gli ambienti.
