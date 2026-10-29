# Infrastruttura Terraform — ambiente `fcv2`

Crea da zero, in un nuovo resource group `fcv2`, l'ambiente applicativo su PostgreSQL. È la copia di quello attuale (`fc`/`apifc`) con PostgreSQL in più. Le risorse esistenti in `fc` non vengono lette né modificate.

| Area | Risorse | Note |
| --- | --- | --- |
| Gruppo | `azurerm_resource_group` `fcv2` | Regione `swedencentral`, modificabile solo prima del primo apply |
| API | App Service plan Linux `B1`, Web App .NET 10 | Always On (job SQL, worker push), WebSocket (SignalR), HTTPS e TLS 1.2, health check `/health/live`, autenticazione di base FTP/WebDeploy disattivata |
| Database | PostgreSQL Flexible Server 17 `B_Standard_B1ms`, database `fantasoccer` | 32 GB con crescita automatica, backup 14 giorni, password generata, connessione `SSL Mode=VerifyFull`, `prevent_destroy` |
| File e cache | Storage account StorageV2 | Contenitori pubblici in sola lettura dei blob: `images`, `logos`, `cardimages`. Privati: `fantazone-cache`, `mcp-keys`. Soft delete 7 giorni, nessun versioning, lifecycle della cache (ADR 0012) |
| Segreti | Key Vault + identità gestita assegnata all'API | Tutti i segreti dell'API sono riferimenti Key Vault: nessun valore in chiaro nelle impostazioni |
| Monitoraggio | Log Analytics (30 giorni, 1 GB/giorno), Application Insights, diagnostica di API e PostgreSQL | |
| AI | Azure OpenAI, deployment `editorial` | Modello predefinito `gpt-5.4-mini` `2026-03-17` (GlobalStandard): `gpt-4o` di produzione è deprecato per i nuovi deployment |

## Esecuzione da GitHub Actions

Il workflow [`Infrastructure fcv2`](../.github/workflows/infra-fcv2.yml) usa l'app registration già presente nei secret dell'ambiente GitHub `production`. Si tratta del secret `AZURE_CREDENTIALS`, lo stesso JSON di `azure/login`.

- **Validazione** su ogni push o PR che tocca `infra/`: `terraform fmt`, `validate` e i test offline (`terraform test`, provider Azure simulato, nessuna chiamata ad Azure).
- **Plan** automatico dopo la validazione, con il riepilogo nella pagina del run. Prima del primo apply lo stato remoto non esiste ancora: il plan usa uno stato locale vuoto e mostra tutte le risorse da creare.
- **Apply** solo manuale: *Actions → Infrastructure fcv2 → Run workflow*, con `action = apply` e `confirm = fcv2`. Il primo apply crea anche lo storage dello stato.

L'ambiente `production` oggi non ha revisori obbligatori. La conferma testuale è l'unico controllo: valuta di aggiungere *Required reviewers* all'ambiente.

### Prerequisito: permessi dell'app registration

Secondo `.github/deployment.md`, oggi il service principal ha solo `Website Contributor` sull'App Service `apifc`. Non basta per creare un resource group. Un Owner della sottoscrizione deve concedere **una sola volta**:

```bash
az role assignment create --assignee <clientId di AZURE_CREDENTIALS> --role Contributor \
  --scope /subscriptions/c46eb61c-c951-471f-aa59-bf20aacf4d75
```

Basta `Contributor`, senza Owner né User Access Administrator, perché la configurazione non crea assegnazioni di ruolo:
- Key Vault usa le *access policy*;
- lo stato Terraform è letto con la chiave dello storage account;
- l'API usa la connection string dello storage.

Con `Contributor` l'apply registra anche i resource provider necessari (`scripts/register-providers.sh`). Il plan non modifica mai la sottoscrizione.

In alternativa, l'Owner crea prima i resource group `fcv2` e `fcv2-tfstate`, concede `Contributor` solo su questi due e registra i provider. In questo caso il resource group `fcv2` va importato nello stato prima del primo apply: aggiungere un blocco `import` per `azurerm_resource_group.main`.

### Secret e stato

| Valore | Origine | Nello stato Terraform |
| --- | --- | --- |
| `MICROSOFT_CLIENT_SECRET`, `ENCRYPTION_KEY`, `ENCRYPTION_IV` | Secret GitHub `production` | **No**: variabili effimere e attributi write-only. Dopo averli cambiati in GitHub, incrementare `app_secrets_version` |
| Password PostgreSQL | Generata (`random_password`) | Sì. Per ruotarla incrementare `postgres_password_version`: server e segreto vengono aggiornati insieme |
| Chiavi storage, OpenAI, connection string App Insights | Restituite dalle risorse Azure | Sì |

Lo stato sta nello storage account `fcv2tf<hash>`, nel resource group separato `fcv2-tfstate`. Il nome è deterministico per sottoscrizione. Lo storage ha accesso pubblico disabilitato, versioning e soft delete di 30 giorni. Va trattato come un segreto: niente copie locali condivise.

## Dopo il primo apply

1. **Deploy dell'API:** workflow manuale [`Deploy API fcv2`](../.github/workflows/deploy-api-fcv2.yml) con `confirm = fcv2`. Pubblica il codice del branch scelto senza toccare le impostazioni gestite da Terraform. Allo startup l'API applica le migration EF Core sul database vuoto.
2. **Login Microsoft:** aggiungere all'app registration del social login (`95de3061-…`) l'URL di redirect del nuovo host (output `api_url`). Terraform non gestisce Microsoft Entra ID.
3. **Migrazione dei dati:**
   - aggiungere temporaneamente l'IP dell'operatore in `postgres_admin_ip_ranges` (`environments/fcv2.tfvars`) e fare apply;
   - importare seguendo [`docs/MIGRATION-RUNBOOK.md`](../docs/MIGRATION-RUNBOOK.md) e ripristinare i file nel nuovo storage;
   - togliere l'IP. Finché l'import non è concluso tenere `background_jobs_enabled = false`: la prima scrittura dell'applicazione chiude la finestra di reimportazione.
4. **Opzionale:** MCP, Web Push (chiavi VAPID) e amministratori globali diversi da quelli di `appsettings.json`, tramite `extra_app_settings` o nuovi secret nel Key Vault. Oggi `Mcp__Enabled` e `Push__Enabled` sono `false`.
5. **Frontend:** quando `fcv2` diventa produzione, aggiornare `FRONTEND_API_URL` e le variabili `API_APP_NAME` e `API_RESOURCE_GROUP`.

## Scelte e limiti

- **Rete:** PostgreSQL ha accesso pubblico con la regola "servizi Azure" (`0.0.0.0`). Gli indirizzi in uscita dell'App Service non sono noti al momento del plan. Restano obbligatori password e TLS con verifica del certificato. Rafforzamento possibile in seguito: integrazione VNet e private endpoint (`postgres_allow_azure_services = false`).
- **Utente applicativo:** l'API usa l'amministratore del server, perché applica le migration allo startup. Un ruolo dedicato richiederebbe accesso SQL dal runner GitHub.
- **Distruzione:** server e database PostgreSQL hanno `prevent_destroy`. Non c'è un workflow di destroy.
- **Costi indicativi:** App Service B1 e PostgreSQL B1ms sono i costi fissi principali. Key Vault, storage e Log Analytics (con limite giornaliero) sono contenuti. OpenAI si paga a consumo.

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
3. eseguire `scripts/state-backend.sh check`, `terraform init -backend-config=backend.hcl`, poi `terraform plan -var-file=environments/fcv2.tfvars`, fornendo i tre secret come `TF_VAR_*`.
