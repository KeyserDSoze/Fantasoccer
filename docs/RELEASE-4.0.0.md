# Fantasoccer 4.0.0 — 31 ottobre 2026

## Novità

- Dati su PostgreSQL (ambiente `fcv2`, Azure Italy North) al posto dello storage Blob/Table del legacy. Il backend non usa più il runtime Rystem; restano `Rystem.DependencyInjection` e `Rystem.Concurrency`.
- Più competizioni per squadra nella stessa lega: partecipazioni esplicite, formazioni per competizione, formazione condivisa di default con impostazione per squadra e copia tra competizioni.
- Una giornata senza formazione viene valutata con l'ultima formazione della squadra, non più con la penalità.
- Login Microsoft gestito direttamente da Fantasoccer (PKCE); i refresh token applicativi durano 365 giorni.

## Cambiamenti per gli utenti

- **Accesso di nuovo obbligatorio.** Il nuovo server ha un proprio key ring: i token emessi da `apifc` non sono validi. Il frontend, al `REFRESH_FAILED`, cancella il token e mostra il login.
- Collegamenti MCP e notifiche push sospesi (`Mcp__Enabled=false`, `Push__Enabled=false`). La chiave VAPID del legacy è nella cronologia pubblica di `main`: il push va riattivato solo con chiavi nuove.
- Aste e liste randomizzate storiche non importate (`IMPORT_LEGACY_AUCTIONS=false`); mercato salvato con i nomi lunghi tra il 6/10/2024 e il 4/10/2025 non importato; restano nel dump verificato.
- 16 utenti storici hanno solo il nome (`IMPORT_LEGACY_NAMED_OWNERS=true`), senza email inventata.
- Nessuno snapshot delle partite storiche: il dettaglio si regge sulla rosa di giornata e dà `409 historical_roster_unavailable` dove manca (stagione 15: 80 lati nelle giornate 1–3).

## Dati

Importati da un export esplorativo del legacy in uso (circa 21:30 UTC del 31/10/2026): 40 utenti, 1 gruppo, 280 squadre, 9.268 rose, 203.969 voti, 123 tabelle con conteggi identici dopo il restore. Il legacy `apifc` non è stato fermato: scritture successive all'export non sono nel nuovo ambiente.

## Pubblicazione

`git push origin refactor/postgresql-multi-group:main` avvia `Relational Migration Tests`, poi `Deploy API fcv2` e `Deploy GitHub Pages` (che punta a `apifcv2` e allo storage `stfcv2f71915`). Il deploy delle Pages rifiuta ogni account blob che non sia `stfcv2*`.

## Rollback

`gh workflow run deploy-pages.yml --ref rollback-main` ripubblica il frontend verso `apifc`, che resta attivo e intatto. Non cancellare il vecchio client secret Microsoft prima della fine della finestra di rollback.

## Dopo il go-live

Stringere `RedirectDomains` e CORS (`fantacalcio.xyz`, `localhost`), riattivare il push con nuove chiavi VAPID, ruotare i segreti finiti nella cronologia pubblica, rimuovere il dump reale da `migration-data/`, rivedere lo SKU dell'App Service (legacy P0v3, `fcv2` B1) e chiudere i warning EF su `formations` e `postponed-results`.
