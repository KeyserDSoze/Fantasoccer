# Push Notification Platform Requirements

Panoramica delle azioni da completare per avere notifiche push funzionanti in tutte le piattaforme supportate da Fantasoccer (Web, Android, iOS).

## 📍 Prerequisiti comuni

- **Endpoint backend**: il servizio REST `PushNotificationSettings` deve accettare le subscription Web Push (endpoint, p256dh, auth) e gestire la rimozione.
- **Chiave VAPID pubblica**: attualmente è definita nel client (`webPushClient.ts`) e nel service worker (`public/scripts/FCServiceWorker.js`). Portarla in una variabile di configurazione condivisa (es. `EXPO_PUBLIC_WEB_PUSH_KEY`) e propagarla al service worker.
- **Autenticazione**: il servizio `pushNotificationService` utilizza l'e-mail dell'utente ricavata tramite `tokenStorageService`/`authService`. Assicurarsi che il token contenga il claim `email`.

## 🌐 Web (desktop + mobile browser)

1. **Service Worker**
   - Il file è disponibile in `public/scripts/FCServiceWorker.js` e viene registrato dinamicamente da `webPushClient.ts`.
   - Per l'ambiente di produzione, confermare che la cartella `public/` venga copia-incollata nello `outputDir` quando si esegue `expo export` o `npm run web`.

2. **HTTPS**
   - Le Web Push richiedono HTTPS (eccetto su `localhost`). Verificare che l'ambiente di staging/produzione esponga il sito via HTTPS.

3. **VAPID key**
   - Generare una nuova coppia VAPID (se non già disponibile) e impostare la chiave pubblica a runtime:
     1. Esporre la chiave in un env `EXPO_PUBLIC_WEB_PUSH_KEY`.
     2. Modificare `webPushClient.ts` per leggerla dall'env.
     3. Assicurarsi che il service worker riceva la chiave via `postMessage` (già previsto dal listener `SET_VAPID_KEY`).

4. **Persistenza subscription**
   - Dopo `subscribe()` il client invia la subscription a `pushNotificationService.addDevice`, che salva nel repository.
   - Implementare nel backend l'aggiornamento dei device all'evento `pushsubscriptionchange` (il service worker ha il TODO per notificare i client). Possibile opzione: `registration.active?.postMessage` e successiva `fetch` verso l'API.

5. **Permessi browser**
   - `webPushClient.requestPermission()` chiede il consenso. Alcuni browser (es. Safari) hanno API diverse o non supportano VAPID: prevedere fallback/alert nella UI.

## 🤖 Android (app Expo)

1. **Provider**: utilizzare Firebase Cloud Messaging (FCM) tramite `expo-notifications`.
2. **Configurazione**:
   - Creare un progetto Firebase, scaricare `google-services.json` e configurare `app.json` (`android.googleServicesFile`).
   - Registrare il server key di FCM sul backend per inviare notifiche.
3. **Client**:
   - Aggiungere un modulo dedicato (es. `services/expoPushClient.ts`) che usa `Notifications.getExpoPushTokenAsync`.
   - Estendere `pushNotificationService` con metodi per registrare/disiscrivere i token Expo/FCM separati da Web Push (in JSON distinguere `type: 'expo' | 'web'`).
4. **Test**:
   - Abilitare notifiche in background (`notification`, `uses-permission android:name="android.permission.POST_NOTIFICATIONS"` per Android 13+).

## 🍎 iOS (app Expo)

1. **Apple Push Notification service (APNs)**
   - Creare un `Push Notification Key` sull'Apple Developer portal e associarlo al bundle identifier (`com.keyserdsoze.fantasoccer`).
   - Aggiungere il file `.p8` nelle credenziali di EAS (`eas credentials`), aggiornare `app.json` (`ios.bundleIdentifier`).

2. **Client**
   - Con `expo-notifications`, chiedere il permesso (`Notifications.requestPermissionsAsync`).
   - Recuperare il token Expo/Native (`getExpoPushTokenAsync`) e registrarlo nel backend come per Android (magari con `platform: 'ios'`).

3. **Capacità in background**
   - Verificare il provisioning profile con la capability `Push Notifications`.

## 🔄 Sincronizzazione cross-platform

- Estendere `PushNotificationSettings` nel backend per distinguere più canali (web push vs mobile push). L'attuale schema salva solo subscription Web Push.
- Aggiornare l'API per accettare payload differenti e ritornare metadati (es. tipo dispositivo, piattaforma, data registrazione).
- Nel frontend, aggiornare `usePushNotifications` e la UI per mostrare dispositivi web/mobile separati.

## ✅ Checklist operativa

- [ ] Chiave VAPID unica in configurazione condivisa.
- [ ] Copia della cartella `public/scripts` nella build web.
- [ ] Integrazione `expo-notifications` con FCM/APNs.
- [ ] Backend capace di accettare e inviare notifiche verso Web Push + FCM + APNs.
- [ ] QA manuale su browser Chromium, Firefox, Safari (desktop/mobile) e device Android/iOS reali.

Questa guida va aggiornata mano a mano che l’implementazione prosegue (es. quando distingueremo i device mobile nel repository).