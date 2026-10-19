# Notifiche push: funzionamento e verifica

## Ambito

Il canale implementato è **Web Push**, per browser/PWA compatibili. Non è un canale Expo, FCM nativo o APNs nativo: le app Android/iOS richiedono un'integrazione e credenziali dedicate. Su iOS la disponibilità delle Web Push dipende anche dall'installazione della PWA e dal supporto del sistema/browser.

**La configurazione e i valori VAPID esistenti sono intenzionalmente invariati.** Non è richiesta una nuova iscrizione dei dispositivi per cambiare chiave.

## API e salvataggi

Le API dedicate sono autenticate e ricavano l'identità dai claim. L'eventuale parametro `email` può identificare soltanto l'utente già autenticato; serve anche a impedire che una richiesta rimasta in coda venga applicata all'account successivo.

| Metodo | Percorso | Operazione |
| --- | --- | --- |
| GET | `/api/push-notifications` | Impostazioni correnti; default solo se il documento non esiste |
| PATCH | `/api/push-notifications` | Aggiornamento delle sole preferenze specificate |
| POST | `/api/push-notifications/devices` | Upsert `{ subscription, previousEndpoint? }` |
| POST | `/api/push-notifications/devices/remove` | Rimozione `{ endpoint }` dal proprio account |
| POST | `/api/push-notifications/disable` | Disattivazione di preferenze e dispositivi del proprio account |

I repository generici `PushNotificationSettings` e `PushNotificationSent` **non sono esposti** via HTTP, comprese query, operazioni e batch. Non esiste un fallback frontend ai vecchi endpoint.

- Le preferenze non riscrivono le subscription. Registrazioni e rimozioni non riscrivono preferenze lette in precedenza.
- Un upsert aggiorna anche le chiavi `p256dh`/`auth` a endpoint invariato.
- Sono accettati endpoint HTTPS dei provider browser Google, Mozilla, Apple e Windows, con chiavi P-256 valide; non URL arbitrari o redirect verso altri host. La stessa validazione si applica agli invii di subscription già salvate.
- L'attivazione esplicita di un endpoint su un altro account lo dissocia dal precedente senza esporne i dati.
- La pulizia di un endpoint scaduto rilegge lo stato corrente e confronta anche le chiavi: non cancella una subscription aggiornata mentre l'invio era in corso.
- Errori HTTP, risposte non valide e salvataggi repository non riusciti sono errori reali, non successi né documenti vuoti.
- Il default di `MatchEvents` è `true`; un `false` salvato è rispettato.

Le mutazioni condividono un lock. Con storage Azure il lock usa lease Blob rinnovate nel container privato `pushnotificationlocks`, nello stesso account dei repository. La perdita del rinnovo cancella le operazioni protette. In memoria/test si usa un semaforo condiviso. Non sostituire il lock Azure con quello in memoria in un deployment multi-istanza.

## Destinatari e affidabilità

- Gli eventi propri sono deduplicati per evento, gruppo ed e-mail destinataria, raccogliendo proprietari e co-proprietari da tutti i basket pertinenti.
- Gli eventi avversari conservano il collegamento basket/lega/incontro. Ogni destinatario usa la propria preferenza, indipendentemente dal proprietario principale. Il testo identifica gruppo, lega, squadra avversaria, giocatore ed evento.
- Checkpoint dei voti e invii da effettuare sono salvati nello stesso documento `PushNotificationSent`, prima di contattare il provider.
- Gli invii riusciti sono registrati per destinatario e dispositivo. Dopo una riuscita parziale si ritentano solo i dispositivi non completati.
- La coda riguarda anche promemoria, fine giornata, cambi formazione e mercato; il mercato usa la giornata tecnica `0` ed è ritentabile anche in assenza di una giornata live.
- I retry differiti hanno backoff fino a 15 minuti. Gli eventi scadono dopo 48 ore; i promemoria cambi live scadono alla deadline. Le scadenze vengono registrate nei log.
- Una preferenza disabilitata o un dispositivo rimosso non viene riattivato da un retry.
- HTTP 404/410 eliminano la subscription non valida; gli altri errori conservano un esito utilizzabile per il retry. I contatori sono atomici e gli errori permanenti sono contati una sola volta.
- Gli identificativi/tag sono stabili nei retry. Non si promette consegna exactly-once: un crash dopo l'accettazione del provider ma prima del salvataggio della ricevuta può richiedere un reinvio con lo stesso tag. L'accettazione del provider non certifica la visualizzazione sul dispositivo.

## Browser e ciclo di vita

Il service worker è `src/Fantasoccer.Applications/fantasoccer/public/FCServiceWorker.js` e viene pubblicato alla radice come `/FCServiceWorker.js`.

- Richiede HTTPS, eccetto gli ambienti locali consentiti dal browser.
- Il consenso viene richiesto dal gesto dell'utente, prima di attendere la registrazione del worker.
- La sottoscrizione attende l'attivazione del worker; una registrazione fallita può essere ritentata.
- `pushsubscriptionchange` conserva il rinnovo nella Cache API, anche senza finestre aperte. Non conserva token di autenticazione.
- All'avvio autenticato, al ritorno online, al ritorno in primo piano e periodicamente, `PushDeviceSync` riconcilia la subscription corrente e sostituisce il vecchio endpoint. Un rinnovo non sincronizzato resta disponibile per il tentativo successivo.
- Una rimozione remota o una disattivazione locale non viene annullata dalla riconciliazione.
- Al logout si disabilita localmente la visualizzazione, si annulla la subscription browser e si tenta la dissociazione backend prima di rimuovere il token. Il logout resta possibile se la rete non è disponibile.
- Il worker esclude notifiche disabilitate o destinate al precedente account; i payload includono l'identità destinataria. Al cambio account non viene attivato implicitamente il nuovo account.
- Gli errori di mutazione sono propagati dall'hook e mostrati nella schermata. Risposte di polling precedenti al salvataggio o relative a un altro account non sostituiscono lo stato corrente.

## Pubblicazione

1. Pubblicare backend e frontend come aggiornamento coordinato: il frontend nuovo richiede le API dedicate; quello vecchio non può più usare il repository push generico.
2. Verificare che l'identità applicativa possa creare/usare il container privato `pushnotificationlocks` nell'account storage già configurato.
3. Pubblicare anche `/FCServiceWorker.js` dalla directory `public` e verificarne l'aggiornamento sui client.
4. Non cambiare le chiavi VAPID in questo aggiornamento. I documenti esistenti sono compatibili; i nuovi campi della coda hanno valori predefiniti e il checkpoint precedente viene letto per la migrazione.

## Test

```sh
dotnet test src/Fantasoccer.Tests/Fantasoccer.Tests.csproj
```

Nella directory `src/Fantasoccer.Applications/fantasoccer`:

```sh
npm test
npx tsc --noEmit
npx expo export --platform web --output-dir /tmp/opencode/fantasoccer-push-web-check
```

Le suite coprono isolamento API, patch concorrenti, upsert/rinnovo, pulizia condizionale, co-proprietari, basket distinti, invii interrotti/parziali, provider simulati 404/410/403/503, errori frontend, rinnovo a pagina chiusa, logout e cambio account. Il trasporto dei test API usa un handler HTTP simulato: non invia notifiche a utenti reali.

Resta necessaria una verifica end-to-end autorizzata su account/dispositivi di test reali per disponibilità del browser, credenziali/provider di produzione e visualizzazione effettiva. Per il canale nativo servono integrazione Expo/FCM/APNs, credenziali e build native: non viene dichiarato supportato dal solo completamento dei test Web Push.
