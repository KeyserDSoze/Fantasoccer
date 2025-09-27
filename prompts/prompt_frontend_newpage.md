# Prompt: creare una nuova pagina frontend Fantasoccer

## Scopo
Guidare l'LLM nella generazione completa di una nuova pagina per l'app React Native/Expo di Fantasoccer. Il risultato atteso include il file dello screen, aggiornamenti alla navigazione e ogni wiring necessario affinché la pagina sia raggiungibile.

## Contesto tecnico
- **Framework**: React Native (Expo 54, React 19).
- **UI toolkit**: [Tamagui](https://tamagui.dev/) è disponibile e consigliato per layout, stack, card, tipografia, pulsanti ecc.
- **Routing**: `src/fantasoccer/navigation/index.tsx` definisce lo stack statico tramite `createStaticNavigation`. Qualsiasi nuovo screen va registrato qui.
- **Screens**: vivono in `src/fantasoccer/screens/`. Usa `home.tsx` come riferimento per struttura, stile e uso di `ScreenContent`.

## Informazioni da raccogliere dal richiedente
1. Nome della pagina (es. `Stats`, `Settings`).
2. Scopo/feature principali da mostrare.
3. Dati o servizi da consumare (se noti).
4. Eventuali componenti riutilizzabili già esistenti.
5. Navigazione desiderata (da quale pagina deve essere raggiungibile, eventuali parametri).

## Flusso operativo suggerito all'LLM
1. **Preparare lo skeleton**
   - Creare un nuovo file in `screens/<nome>.tsx` esportando uno screen React (default export).
   - Importare e utilizzare `ScreenContent` per avere header/bar e scroll coerenti.
   - Impostare layout base con Tamagui (`YStack`, `XStack`, `Card`, `Text`, ecc.).
   - Aggiungere placeholder chiari per contenuti che richiedono dati futuri.

2. **Aggiornare la navigazione**
   - Aprire `navigation/index.tsx`.
   - Importare il nuovo screen.
   - Aggiungere l'entry nella definizione dello stack (sezione `screens`).
   - Se servono opzioni custom (header, pulsanti, parametri), configurarle nella stessa sezione.

3. **Gestire eventuali hook/dati**
   - Se la nuova pagina necessita di dati, riutilizzare servizi/hook preesistenti (es. `useSerieACalendar`, `useGroup`, ecc.).
   - In mancanza, creare hook dedicati seguendo i prompt esistenti (es. `prompt_frontend_repository.md`).

4. **Garantire coerenza UI/UX**
   - Armonizzare colori e spaziature con Tamagui theme (`$color*`, `$space*`).
   - Assicurarsi che il layout sia responsive (stack + `flexWrap`, larghezze massime come `maxWidth={600}` quando opportuno).
   - Preferire componenti già presenti in `components/` quando esistono.

5. **Testing manuale**
   - Suggerire di eseguire `npm run start` o `npm run web` per validare visualmente.
   - Se sono stati aggiunti hook/servizi, menzionare eventuali comandi di lint/test (`npm run lint`).

## Output atteso dall'LLM
- Elenco file creati/modificati con breve descrizione.
- Eventuale codice generato (screen + navigation update + altro necessario).
- Indicazioni su comandi da eseguire per verificare la pagina.
- Note su assunzioni effettuate, TODO o dati mock.

## Suggerimenti aggiuntivi
- Riutilizzare componenti tipici della home (titoli centrati, separatori, card informative) per mantenere stile coerente.
- Quando la pagina ha sezioni multiple, considerare l'uso di `Separator` Tamagui o componenti personalizzati per titoli di sezione.
- Gestire stati di caricamento/errore se vengono mostrati dati asincroni.
- Documentare nei commenti eventuali parti da completare quando le API saranno disponibili.
