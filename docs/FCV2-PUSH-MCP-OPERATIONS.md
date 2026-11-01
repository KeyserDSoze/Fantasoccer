# Push e MCP su fcv2

## Segreti operativi

Le nuove chiavi VAPID e il certificato MCP sono stati creati direttamente nel Key Vault
`kv-fcv2-f71915`, senza stampare né salvare localmente i valori. I riferimenti, non i
valori, sono in `infra/environments/fcv2.tfvars` (`extra_app_settings`). Questi tre
segreti sono gestiti fuori da Terraform: non eliminarli durante la manutenzione.

- `push-public-key-20261101`, `push-private-key-20261101`: coppia P-256 nuova.
- `mcp-certificate-20261101`: PFX RSA con chiave privata, scadenza 31 ottobre 2028.
  Il PFX senza password resta un segreto del vault, non un file nel repository.
  Conservare il certificato: protegge anche le nuove chiavi Data Protection nel
  blob già usato da fcv2. Una rotazione futura deve mantenere la decifratura delle
  vecchie chiavi; non basta sostituirlo.

Il Subject VAPID è `https://fanta.zone`. Le chiavi VAPID non scadono, ma si ruotano
se compromesse. I browser devono iscriversi di nuovo dopo una rotazione.
Sono state rimosse le 6 vecchie associazioni dispositivo e le 6 sottoscrizioni
legacy dal database fcv2, conservando le preferenze dei 12 utenti.

L'issuer MCP è `https://apifcv2.azurewebsites.net`, la risorsa è `/mcp`, il frontend
del consenso è `https://fanta.zone/`. I client devono riconnettersi al nuovo server:
le autorizzazioni del vecchio issuer non valgono sul nuovo.

## Sicurezza operativa

- L'abilitazione è persistente nel file Terraform; non fare solo una modifica
  manuale sull'App Service, che un apply successivo annullerebbe.
- L'API valida chiavi VAPID e certificato all'avvio. In caso di problemi disabilitare
  `Push__Enabled` o `Mcp__Enabled`, senza cancellare il key ring.
- Il vecchio `apifc` resta attivo per decisione dell'operatore: le sue impostazioni
  non sono state cambiate. La rimozione delle iscrizioni su fcv2 non revoca quelle
  conservate nel legacy o nel servizio push del browser.

## Run dei job e spazio SQL

`SqlJobRuns` contiene un riepilogo per run (stato, tempi, contatori e codice),
non i dati scaricati né un log completo delle richieste. `SqlJobStates` conserva
la prossima scadenza e la lease per job e stagione. Al 1 novembre 2026: 577 run,
224 kB per la tabella con indici; database totale 177 MB.

Non esiste attualmente retention automatica per `SqlJobRuns`. Con circa 3.500–4.000
run al giorno la crescita indicativa è di qualche centinaio di MB all'anno,
non di GB al giorno; dimensione e indici vanno monitorati. La retention delle
notifiche e la pulizia della cache non cancellano le run. Prima di aggiungere una
pulizia scegliere una durata e non eliminare run attive o ancora sotto lease.

## Probabili formazioni e statistiche

Lo scheduler è sequenziale. Il refresh delle statistiche usava `DbSet.Local` dentro
il ciclo di creazione degli storici, provocando ripetuti `DetectChanges` su tutto
il grafo. Su dati reali una transazione restava aperta per oltre 20 minuti e
bloccava le scritture degli altri job: `chances` non era ancora stato eseguito.
La correzione usa una mappa delle statistiche già caricate, aggiorna le righe
storiche in posto invece di cancellarle e reinserirle, e controlla la
cancellazione. Sul database reale `statistics` ha completato in circa 55 secondi,
seguito da `chances`, `formations` e `results`. Le probabilità della giornata 10
contengono 682 giocatori. La suite include un refresh di 352 giocatori per 38
giornate, ripetuto due volte senza duplicare le righe.

MCP dietro App Service richiede `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` per
riconoscere l'HTTPS terminato dal proxy Azure. Senza questa impostazione discovery
e OAuth rispondono `invalid_request` (ID2083). Il controllo
`python3 scripts/check-mcp.py https://apifcv2.azurewebsites.net` passa con la
configurazione attuale; il login reale e il consenso restano da provare dal client.

La pagina dei voti ufficiali include anche gli allenatori (`role=all`). Il parser
ora li esclude prima della costruzione delle identità: importandoli come giocatori
le identità vuote/ambigue facevano rifiutare tutta la giornata. I controlli sui
calciatori, sulla completezza dei club e sui voti finiti restano invariati.

Verifica finale, 1 novembre alle 09:18 UTC: `official` riuscito alle 09:10 e alle
09:15 (circa 12 e 8 secondi), `statistics` riuscito alle 09:17, `chances` riuscito
alle 09:00. Nessuna transazione aperta rimasta bloccata. Pipeline e deploy API
verdi su `f8324bc`; Pages pubblicato con i collegamenti MCP visibili. Nuove
iscrizioni push e login/consenso da client MCP richiedono il collaudo dell'utente.
