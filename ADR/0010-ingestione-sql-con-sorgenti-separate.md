# Ingestione SQL con sorgenti separate

## Decisione

I parser esistenti non vengono riattivati registrando repository Blob nel percorso PostgreSQL. Le dipendenze di lettura necessarie ai parser sono estratte in `IFootballIngestionReader`, con lettori SQL e legacy separati; i provider HTTP restano specifici e non salvano dati.

`FootballIngestionApplicationService` è il solo writer per catalogo, calendario, voti, probabilità e statistiche nuove. Le API HTTP/MCP usano `POST /api/football/refresh` con DTO leggibile, ruolo globale verificato e rilettura dell'autorizzazione dopo il fetch e sotto il lock di scrittura. Il runner interno userà lo stesso servizio senza impersonare un utente.

- I fetch di rete avvengono prima della transazione SQL. Errori, cancellazioni, parse vuote o voti ufficiali privi di una squadra reale non cancellano gli snapshot salvati.
- I nomi normalizzati sono alias della sorgente; più identità candidate bloccano l'ingestione, non vengono fuse scegliendo una riga casuale.
- Il calendario mantiene gli ordinali delle partite già note e i rinvii espliciti. Le date wall-clock dei parser sono convertite con Europe/Rome: gli orari DST ambigui/non validi sono rifiutati anche durante l'ingestione, non solo nell'import.
- Un catalogo improvvisamente troppo piccolo viene rifiutato prima di disattivare centinaia di giocatori. I trasferimenti reali aggiornano solo rose stagionali e disponibilità future, non lo storico iniziato.
- I voti a tavolino per squadre rinviate conservano ruolo e squadra del giocatore; il confronto di abbreviazioni vuote non può assegnarli a tutto il catalogo.
- Il calcolo statistiche è estratto in un motore puro condiviso tra adapter legacy e SQL. Un refresh dei conteggi non cancella descrizioni editoriali già salvate.
- L'automatismo formazioni crea soltanto una giornata futura mancante, con finanze correnti e posizioni della precedente per i giocatori rimasti. Non azzera contatori/storico delle formazioni già inviate. L'ottimizzazione automatica richiede l'opt-in del proprietario.
- Il ricalcolo programmato ha un comando interno distinto dal ricalcolo manuale e riusa lo stesso core; può elaborare solo risultati assenti e non sovrascrive risultati importati/salvati.
- Il refresh foto ufficiali riusa la stessa sorgente e lo stesso Blob key delle immagini esistenti, ma legge catalogo/rose e autorizzazioni da SQL. Le scritture accettano UUID; solo gli alias di presentazione/cache restano chiavi nome. Il refresh di una rosa è limitato ai suoi proprietari e alla stagione corrente, con riepilogo di errori parziali e timeout.

## Stato

Le sorgenti e le scritture SQL sono implementate e testate con provider sintetici. La schedulazione/orchestrazione completa, il refresh albo e le integrazioni news/AI restano da completare: `BackgroundJobs:Enabled` rimane rifiutato, non viene acceso il vecchio scheduler per aggirare questi limiti.
