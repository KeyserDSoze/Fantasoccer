# Copertura della migrazione — stato effettivo

Questo inventario collega registrazioni Storage e servizi frontend alle sostituzioni. La [matrice contratti/policy](MATRICE-CONTRATTI-E-POLICY.md) documenta le famiglie di operazioni, le rimozioni intenzionali del CRUD generico e i relativi hook autorizzativi/business.

Fonti: `Fantasoccer.Infrastructure.Storage/ServiceCollectionExtensions.cs`, `Program.cs`, `Fantasoccer.Business/ServiceCollectionExtensions.cs` e `fantasoccer/services/`.
Le registrazioni Rystem generano anche endpoint, autorizzazioni e pipeline business: il solo elenco dei container non basta a sostituirle.

| Dati/factory legacy | Contenitore o tabella | Backend relazionale | Frontend |
| --- | --- | --- | --- |
| AppIdentity | `appidentity` | Login/claim/profilo/selezioni SQL, inviti senza copia Blob | `authService`, `appIdentityService`, AuthProvider/AppProvider senza Raw |
| Group | `group` | Gruppi, membri, basket, partecipazioni, proprietà/storico, tipi/regolamenti normalizzati | `groupService`, amministrazione gruppi/leghe/basket su DTO diretti |
| Settings | `settings` | Preferenze SQL; loghi e nomi squadra con servizi SQL/file dedicati | `settingsService` senza decoder/streaming di vecchi documenti |
| Team stagionale | `team` | Rose, prezzi, vendite/riacquisti e fondi importati | `teamService` su DTO SQL; Game invia solo ID/posizioni/versioni |
| Team/day | `dailyteams` | Snapshot/storico importati; formazione con whitelist, versione origine e scadenze | Game/SoccerField su DTO leggibili, cambi live e proposta formazione con ID |
| Team/opening | `openingteams`, `openingdailyteams` | Import per lega/anno competizione, FK squadra anno precedente e stagione della lega | `openingCompetitionService` su API SQL; preparazione e classifica in verifica |
| OpeningCompetitionStandings | `openingstandings` | Modello/import per squadra N-1, lega N, punteggi e premi originali | DTO leggibili; preparazione POST distinta dalla lettura |
| Calendar, PlayedGame | `calendar`, `playedgame` | Import stage/turno/giornata, risultati e giocatori salvati; generazione/ricalcolo espliciti SQL | `calendarService`, `gameService`, `leagueManagerService` su nuove API |
| Rank, Rank/day | `rank`, `dailyrank` | Import/classifiche stagionali e snapshot giornalieri distinti; aggiornamento atomico dal ricalcolo | `rankService` legge DTO SQL, nessuna scrittura generica client |
| RealCalendar, RealRank | `realcalendar`, `realrank` | Calendario/classifica condivisi importati; nuovo writer ingestione SQL con provider separati, parse validate e orari Roma/UTC espliciti | `serieAService` e `recalculationService` su API SQL, rinvio e refresh con autorizzazione globale |
| RealPlayersWrapper | `realplayerswrapper` | Import globale Player/PlayerSeason; refresh catalogo SQL protegge da parse vuote/troncate e sincronizza disponibilità future | `realPlayerService` legge API SQL |
| RealPlayersWrapper/random | `randomizedplayers` | Ordine/visibilità per lega-stagione, import e randomizzazione versionata; identità giocatori condivise | `realPlayerService` e aste su API SQL |
| RealTeamWrapper | `realteamwrapper` | Club importati e condivisi | Serie A su API SQL |
| VotedRealPlayerWrapper/live, notification, official | `live`, `notification`, `official` | `official`/`notification` in SQL; `live` validato e lasciato nel dump: i voti provvisori sono uno snapshot globale della cache distribuita (ADR 0012), pubblicabile al cutover con `seed-operational-cache` | `realVoteService` su DTO leggibili |
| ChancedRealPlayerWrapper | `chancedrealplayerwrapper` | Validato, conservato nel dump e non importato in SQL (dato operativo della cache distribuita, ADR 0012) | `chanceService`, contratto API invariato |
| StatPlayersWrapper | `statplayerswrapper` | Import somme/conteggi/descrizioni/storico; reader SQL formazione ottimale e motore statistiche puro condiviso con legacy | `statPlayerService` su API SQL |
| LiveGroup | `livegroup` | Proiezione SQL; snapshot derivati legacy archiviati nel dump verificato e dichiarati in `preservedArtifacts`, mai fallback attivo | `liveGroupService` senza cache globale cross-account |
| HallOfFame | `halloffame` | Import/lettura e refresh incrementale SQL, checkpoint annuali riconciliati, totali per proprietario e record standalone preservati | `hallOfFameService` senza Raw; refresh/conferma espliciti; navigazione solo per record collegati |
| MarketWrapper (non esposto) | `marketwrapper` | Import normalizzato, trasferimenti atomici/versionati, eventi tipizzati e producer outbox SQL; dispatcher con opt-out e scope correnti | `marketService`/tipi senza Raw; creazione via ID e versioni rose, votazioni via versione proposta |
| AuctionWrapper | `auction` | Import relazionale con decifratura esplicita delle liste legacy, snapshot/timer/vincite; comandi HTTP/MCP/SignalR condivisi e audit modifiche rosa | `auctionService` e aste live/display su DTO leggibili, ID e versioni; eliminato reader compatto temporaneo gruppi |
| CardWrapper | `cards` | Librerie, condizioni/soglie relazionali, import e CRUD globale autorizzato/versionato; librerie non-default non pubblicate senza riconciliazione | `cardService` su DTO leggibili, editor preserva false/zero/condizioni, immagini versionate Blob |
| NewsWrapper, TeamWithAi | `news`, `teamwithai` | Import/letture SQL; generazione opzionale esplicita, scope, timeout, lock e CAS; suggerimenti non modificano rose | `editorialService` su DTO leggibili; nessuna chiamata AI automatica dai GET |
| PushNotificationSettings / Sent (non esposti) | `pushnotification`, `pushnotificationsent` | Preferenze/dispositivi/checkpoint/outbox importati; pending legacy in quarantena, producer/dispatcher SQL con lease e controlli correnti testati | DTO leggibili, proof privata solo in registrazione, chiave pubblica da server, rinnovo/rotazione espliciti, worker con verifica destinatario |
| FantasoccerLog | Table `logs` | Archivio privato SQL tipizzato con provenienza esatta; log/lease/ricevute job SQL; OpenTelemetry separato | `fantasoccerLogService` su API limitate/redatte; rimossa l'ultima dipendenza repository npm |
| OAuthDocument (non esposto) | `mcp-oauth` | Import/store SQL privato, CAS/retention e revalidazione account SQL | `mcpConnectionService` resta sul contratto OAuth leggibile esistente |
| Chiavi / eventuali SecurityOptions storiche | `mcp-keys`, percorso Data Protection, eventuale Table SecurityOptions | Conservazione/configurazione separata; verificare la presenza reale dei dati legacy | Sessioni/login/MCP |
| File | `images`, `logos`, `cardimages` | Restano Blob; loghi/carte versionati; refresh foto ufficiali con sorgente condivisa e lettore SQL, nessun repository dati Blob | TeamLogo/carte da URL DTO; refresh giocatore/rosa via ID e autorizzazioni correnti |

I job sono orchestrati dal worker SQL con lease/fencing; rimangono disabilitati nei default locali, così come AI e push. Le riconciliazioni dipendenti dai dati reali restano esplicite: checkpoint crediti/albo, scope dei pending push, eventuali librerie non-default e container sconosciuti. Non si dichiarano risolte senza il dump e le conferme del gestore.

## Condizioni per dichiarare pronto il test completo

1. Nessuna riga necessaria al gioco rimane «da migrare» o implicitamente delegata al repository automatico.
2. Ogni API frontend attiva usa il nuovo contratto leggibile; non basta avere la sola schermata workspace.
3. Import e riconciliazione coprono tutti i dati necessari, con report completo e nessuna discrepanza bloccante.
4. Controller, MCP, hub e job condividono i servizi e applicano gli stessi confini autorizzativi.
5. Login/provisioning degli amici e selezioni di gruppo/lega/stagione funzionano senza dipendere dalle copie legacy delle membership.
6. Il ciclo di prova comprende gameplay, storico, mercato, aste, notifiche e immagini, non soltanto home/health/Swagger.

Finché queste condizioni non sono soddisfatte, il Compose e il workspace sono strumenti di sviluppo, **non la conferma che l'intera riscrittura sia terminata**.
