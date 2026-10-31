# Infrastruttura Terraform — ambiente `fcv2`

Crea da zero, in un nuovo resource group `fcv2`, l'ambiente applicativo su PostgreSQL. È la copia di quello attuale (`fc`/`apifc`) con PostgreSQL in più. L'API gira come container pubblicato in un Azure Container Registry dedicato. Le risorse esistenti in `fc` non vengono lette né modificate.

| Area | Risorse | Note |
| --- | --- | --- |
| Gruppo | `azurerm_resource_group` `fcv2` | Regione `swedencentral`, modificabile solo prima del primo apply |
| Immagini API | Azure Container Registry `Basic` | Utente admin e pull anonimo disattivati. Push da GitHub con il service principal; pull dell'App Service con identità gestita e ruolo `AcrPull`, senza password |
| API | App Service plan Linux `B1`, Web App container `apifcv2` (https://apifcv2.azurewebsites.net) | Immagine di `ops/local/api.Dockerfile`, porta 8080. Always On (job SQL, worker push), WebSocket (SignalR), HTTPS e TLS 1.2, health check `/health/live`. Autenticazione di base FTP/WebDeploy disattivata |
| Database | PostgreSQL Flexible Server 17 `B_Standard_B1ms`, database `fantasoccer` | 32 GB con crescita automatica, backup 14 giorni, password generata, connessione `SSL Mode=VerifyFull`, `prevent_destroy` |
| File e cache | Storage account StorageV2 | Contenitori pubblici in sola lettura dei blob: `images`, `logos`, `cardimages`. Privati: `fantazone-cache`, `mcp-keys`. Soft delete 7 giorni, nessun versioning, lifecycle della cache (ADR 0012) |
| Segreti | Key Vault + identità gestita assegnata all'API | Tutti i segreti dell'API sono riferimenti Key Vault: nessun valore in chiaro nelle impostazioni |
| Monitoraggio | Log Analytics (30 giorni, 1 GB/giorno), Application Insights, diagnostica di API (console, applicazione e metriche) e PostgreSQL | I log HTTP di App Service non sono raccolti: conservano la query string, che nel rinnovo del login contiene il refresh token. Le richieste restano in Application Insights, che la elimina. Il pool di connessioni dell'API è limitato a 15 (B1ms: 35 connessioni utente, container vecchio e nuovo attivi insieme durante un deploy) |
| AI | Azure OpenAI, deployment `editorial` | Modello `gpt-6-luna` `2026-09-22` (GlobalStandard). Sostituisce `gpt-4o`, deprecato per i nuovi deployment |

Il file system di un container non è persistente. Per questo le chiavi Data Protection dell'API, da cui dipendono i token di accesso, sono salvate nel blob privato `mcp-keys/fcv2/data-protection-keys.xml` (impostazione `Mcp__DataProtectionBlobName`, già supportata dall'API). Restano valide dopo riavvii e nuovi deploy.

## Esecuzione da GitHub Actions

Il workflow [`Infrastructure fcv2`](../.github/workflows/infra-fcv2.yml) usa l'app registration già presente nei secret dell'ambiente GitHub `production`. Si tratta del secret `AZURE_CREDENTIALS`, lo stesso JSON di `azure/login`.

- **Validazione** su ogni push o PR che tocca `infra/`: `terraform fmt`, `validate` e i test offline (`terraform test`, provider Azure simulato, nessuna chiamata ad Azure).
- **Plan** automatico dopo la validazione, con il riepilogo nella pagina del run. Non modifica nulla su Azure. Prima del primo apply lo stato remoto non esiste ancora: il plan usa uno stato locale vuoto e mostra tutte le risorse da creare.
- **Apply** solo manuale: *Actions → Infrastructure fcv2 → Run workflow*, con `action = apply` e `confirm = fcv2`. Esegue in ordine:
  1. registrazione dei resource provider e creazione dello storage dello stato, se mancano;
  2. creazione del **Container Registry**;
  3. build dell'immagine dell'API dal commit del run e **push** nel registry (tag = SHA del commit; un tag già presente non viene ricostruito);
  4. plan e apply del **resto dell'ambiente**: l'App Service nasce già puntando all'immagine pubblicata;
  5. deploy dell'immagine sull'App Service e attesa di `/health/live`.

Il workflow manuale [`Deploy API fcv2`](../.github/workflows/deploy-api-fcv2.yml) (`confirm = fcv2`) serve per i deploy successivi del solo codice. Costruisce e pubblica l'immagine del commit scelto e la imposta sull'App Service. Non tocca impostazioni, segreti né identità, che restano di Terraform. Terraform ignora il tag dell'immagine dopo la creazione, così i deploy non risultano come differenze nei plan.

L'ambiente `production` oggi non ha revisori obbligatori. La conferma testuale è l'unico controllo: valuta di aggiungere *Required reviewers* all'ambiente.

### Permessi

`AZURE_CREDENTIALS` è l'app registration già usata per il deploy dell'applicazione attuale (autenticazione con client secret). Ha il ruolo **Owner** sulla sottoscrizione, quindi può fare tutto senza concessioni aggiuntive:
- creare i resource group e le risorse;
- registrare i resource provider;
- pubblicare immagini nel registry;
- assegnare `AcrPull` all'identità dell'API.

Prima di creare qualsiasi risorsa l'apply verifica questo requisito (`scripts/check-permissions.sh`): basta Owner, oppure Contributor più un ruolo che consenta di assegnare ruoli. Il plan riporta lo stesso controllo come avviso.

Terraform crea una sola assegnazione di ruolo: `AcrPull` sul registry. Per il resto non servono altre assegnazioni:
- Key Vault usa le *access policy*, efficaci subito;
- lo stato Terraform è letto con la chiave dello storage account;
- l'API usa la connection string dello storage.

### Secret e stato

| Valore | Origine | Nello stato Terraform |
| --- | --- | --- |
| `MICROSOFT_CLIENT_SECRET`, `ENCRYPTION_KEY`, `ENCRYPTION_IV` | Secret GitHub `production` | **No**: variabili effimere e attributi write-only. Dopo averli cambiati in GitHub, incrementare `app_secrets_version` |
| Password PostgreSQL | Generata (`random_password`) | Sì. Per ruotarla incrementare `postgres_password_version`: server e segreto vengono aggiornati insieme |
| Chiavi storage, OpenAI, connection string App Insights | Restituite dalle risorse Azure | Sì |
| Registry | Nessuna password: identità gestita (`AcrPull`) e Microsoft Entra ID per il push | — |

Lo stato sta nello storage account `fcv2tf<hash>`, nel resource group separato `fcv2-tfstate`. Il nome è deterministico per sottoscrizione. Lo storage ha accesso pubblico disabilitato, versioning e soft delete di 30 giorni. Va trattato come un segreto: niente copie locali condivise.

## Dopo il primo apply

1. **Login Microsoft:** nessuna modifica. Il redirect del login torna al frontend (`https://fanta.zone`, già in `RedirectDomains` e nell'app registration), non all'host dell'API. CORS consente già `fanta.zone`.
2. **Migrazione dei dati:**
   - (per ora il firewall è aperto a tutti: `0.0.0.0`-`255.255.255.255` in `postgres_admin_ip_ranges`, da restringere poi) aggiungere l IP dell operatore in `postgres_admin_ip_ranges` (`environments/fcv2.tfvars`) e fare apply;
   - importare seguendo [`docs/MIGRATION-RUNBOOK.md`](../docs/MIGRATION-RUNBOOK.md) e ripristinare i file nel nuovo storage;
   - togliere l'IP. Finché l'import non è concluso tenere `background_jobs_enabled = false`: la prima scrittura dell'applicazione chiude la finestra di reimportazione. L'API si avvia comunque e applica le migration EF Core sul database vuoto.
3. **Opzionale:** MCP, Web Push (chiavi VAPID) e amministratori globali diversi da quelli di `appsettings.json`, tramite `extra_app_settings` o nuovi secret nel Key Vault. Oggi `Mcp__Enabled` e `Push__Enabled` sono `false`.

## Passaggio in produzione e rollback

Il frontend resta su GitHub Pages (`fanta.zone`). Il ramo `rollback-main` è una copia esatta di `main` prima del passaggio (commit `9ee2c0a`), protetta da cancellazione e force push e autorizzata a pubblicare su GitHub Pages.

**Prima del merge** di `refactor/postgresql-multi-group` su `main`:
1. apply dell'infrastruttura (l'API su `apifcv2` nasce con l'immagine del branch);
2. migrazione dei dati e verifica di `https://apifcv2.azurewebsites.net/health/ready`;
3. `background_jobs_enabled = true` e apply, quando l'import è concluso.

**Al merge su `main`** partono automaticamente:
- `Relational Migration Tests`. Se riesce, `Deploy API fcv2` pubblica su `apifcv2` l'immagine dello stesso commit: i test bloccano il deploy, come nella pipeline precedente.
- `Deploy GitHub Pages`. Attende fino a 15 minuti che `apifcv2` risponda a `/health/ready`, poi pubblica il nuovo frontend con `EXPO_PUBLIC_API_URL = https://apifcv2.azurewebsites.net` (sovrascrivibile con la variabile di repository `FCV2_API_URL`). Se l'API non è pronta il sito pubblicato non cambia.
- Il vecchio `Deploy API` su `main` non distribuisce più nulla: `apifc` resta com'è, come rete di sicurezza.

**Rollback:**
1. *Actions → Deploy GitHub Pages → Run workflow*, scegliendo il branch **`rollback-main`**. GitHub usa la copia del workflow di quel branch: ricostruisce il frontend precedente con `FRONTEND_API_URL`. Le variabili `FRONTEND_API_URL`, `API_APP_NAME` e `API_RESOURCE_GROUP` devono restare sui valori legacy (`https://apifc.azurewebsites.net`, `apifc`, `fc`).
2. L'API legacy `apifc` non è mai stata toccata. Solo se serve ridistribuirla: *Actions → Deploy API → Run workflow* su `rollback-main`.
3. I dati scritti nella nuova versione dopo il passaggio restano in PostgreSQL ma **non** esistono nello storage legacy: non c'è doppia scrittura.
4. Finché `main` contiene la nuova versione, un push su `main` che tocca il frontend ripubblicherebbe il nuovo sito. Dopo un rollback non pushare su `main` (o fare il revert del merge) finché il problema non è risolto.

## Scelte e limiti

- **Rete:** PostgreSQL ha accesso pubblico con la regola "servizi Azure" (`0.0.0.0`). Gli indirizzi in uscita dell'App Service non sono noti al momento del plan. Restano obbligatori password e TLS con verifica del certificato. Rafforzamento possibile in seguito: integrazione VNet e private endpoint (`postgres_allow_azure_services = false`).
- **Utente applicativo:** l'API usa l'amministratore del server, perché applica le migration allo startup. Un ruolo dedicato richiederebbe accesso SQL dal runner GitHub.
- **Pulizia del registry:** il registry `Basic` non ha una retention automatica. Le immagini vecchie (una per deploy) si possono eliminare con `az acr repository delete`.
- **Modello:** `gpt-6-luna` è un modello di ragionamento. Il limite di 4096 token di output della generazione editoriale include i token di ragionamento: se i testi risultano troncati, va alzato nel codice.
- **Distruzione:** server e database PostgreSQL hanno `prevent_destroy`. Non c'è un workflow di destroy.
- **Costi indicativi:** App Service B1 e PostgreSQL B1ms sono i costi fissi principali, insieme a un registry `Basic`. Key Vault, storage e Log Analytics (con limite giornaliero) sono contenuti. OpenAI si paga a consumo.

## Prove locali

```bash
cd infra
terraform init -backend=false
terraform validate
terraform test        # provider Azure simulato: nessuna credenziale né risorsa reale
```

Per un plan locale contro Azure:
1. impostare `ARM_CLIENT_ID`, `ARM_CLIENT_SECRET`, `ARM_TENANT_ID`, `ARM_SUBSCRIPTION_ID`;
2. eseguire `az login`;
3. eseguire `scripts/state-backend.sh check`, `terraform init -backend-config=backend.hcl`, poi `terraform plan -var-file=environments/fcv2.tfvars`, fornendo i tre secret e `TF_VAR_api_image_tag` come variabili d'ambiente.
