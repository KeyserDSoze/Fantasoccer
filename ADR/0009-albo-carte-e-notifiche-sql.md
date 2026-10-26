# Albo, carte e notifiche SQL

## Storico e carte

- I totali di carriera dell'albo appartengono a un proprietario, non a una squadra annuale inventata. Le vittorie conservano stage, ordine e anni, inclusi duplicati presenti in sorgente. I record giocatore senza anno non creano una PlayerSeason arbitraria.
- Un record partita può sopravvivere al calendario originale. La FK al calendario è nullable e distinta dallo snapshot; la UI non offre una navigazione verso una partita assente.
- Le condizioni delle carte sono righe/soglie relazionali, non JSON compatto. I flag di presenza distinguono un limite vuoto da assente; zero, false e ruoli duplicati non vengono normalizzati via.
- La cancellazione archivia una carta e non cancella versioni immagine condivise/storiche. Le nuove immagini Blob hanno nomi immutabili e un aggiornamento del riferimento soggetto a versione.
- Le librerie diverse da `default` si importano ma non diventano automaticamente pubbliche: il report richiede conferma esplicita dello scope.

## Preferenze e dispositivi

- Le preferenze sono dell'account; la subscription ha un binding globale per endpoint. La registrazione non riabilita flag salvati a false.
- Per spostare un binding attivo ad altro account occorre una prova casuale del dispositivo, ruotata ad ogni registrazione e conservata solo come hash nel DB. GET settings/MCP, log e worker non contengono la prova. Il client la conserva in storage browser o SecureStore nativo, non nella configurazione condivisa.
- I dispositivi importati non hanno una prova recuperabile: un altro account deve disattivare/riattivare la subscription sul browser. Conoscere l'endpoint da solo non autorizza il trasferimento.
- Logout/rimozione agiscono solo sui dispositivi dell'account. Una risposta 404/410 del provider può invalidare il binding soltanto se proprietario e versione sono ancora quelli usati per l'invio.
- Nessuna chiave VAPID privata è nel sorgente. PublicKey/PrivateKey/Subject vengono da env privato; se l'invio è abilitato si verificano formato e corrispondenza della coppia. La chiave pubblica viene dalle API, senza fallback incorporato nel client/worker. Cambiarla richiede riattivazione esplicita.

## Outbox e consegna

- L'import conserva flag promemoria, baseline voti, sequenza, ricevute cambi formazione, indice giornate, pending, tentativi, tag, scadenze e ricevute endpoint. Non invia nulla.
- Le pending legacy non documentano uno scope affidabile: restano in quarantena, mai attribuite a una lega analizzando testo o display name. Una conferma globale amministrativa verifica gruppo/lega/anno e destinatario proprietario corrente, registra audit e assegna un link locale; non azzera tentativi, scadenze o ricevute.
- I producer committano checkpoint e outbox insieme, con chiavi stabili. Gli eventi mercato hanno un timestamp di accodamento distinto da quello di consegna; l'import non fabbrica eventi.
- Il dispatcher acquisisce lease brevi in transazioni SQL, poi fa richieste provider fuori dalla transazione. Numero di consegne/endpoint, timeout, retry, backoff e scadenze sono limitati. Le ricevute per endpoint impediscono replay degli endpoint già completati.
- Prima di ogni invio si ricontrollano opt-out, account abilitato, membership, proprietà e partecipazione nella lega/stagione. Dopo ogni risposta si usa il lease/versione per le ricevute e l'invalidazione.
- Non si promette exactly-once tra PostgreSQL e un provider esterno: un crash dopo l'accettazione ma prima della ricevuta può produrre un retry. Il tag browser stabile consente la sostituzione; non si rigenerano tag per ogni tentativo.
- Destinazioni provider validate tramite allowlist HTTPS senza redirect; payload leggibile camelCase, TTL limitato alla scadenza. Errori VAPID 401/403 non cancellano le subscription.
- Il worker visualizza notifiche personali solo per un binding locale abilitato e lo stesso destinatario; i click restano nella stessa origine. Nessuna credenziale o prova dispositivo viene salvata nella cache worker.

L'invio resta disabilitato nei file di test. La rimozione dal sorgente di una vecchia chiave privata non la ruota sul provider: la rotazione operativa e la riattivazione dei dispositivi richiedono configurazione del gestore.
