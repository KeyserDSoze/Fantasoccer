# Verifica dei pulsanti di ricaricamento — 2 novembre 2026

Non sono disponibili statistiche sui click: la rimozione si basa sul comportamento del codice, non sull'ipotesi che nessuno li usi. Un pulsante di ricaricamento rilegge le API, non anticipa i job che producono i dati.

## Rimossi perché ridondanti

| Schermata / widget | Aggiornamento già presente |
| --- | --- |
| Live (barra, calendario e classifica, desktop e mobile) | Polling ogni 15 secondi dell'ultima proiezione; ricalcolo server ogni 5 minuti solo se cambiano le sorgenti |
| Calendario e classifica della lega | Polling ogni 2 minuti, anche con dati vuoti |
| Home: calendario Serie A e classifica Serie A | Calendario ogni 10 secondi anche fuori dal live, classifica ogni 3 minuti |
| Home: partita e classifica della lega | Dalla 4.0.6 proiezione Live ogni 15 secondi nella finestra 2 ore prima / 14 ore dopo (chiusura anticipata al consolidamento); calendario e classifica ufficiali ogni minuto per l'ultima giornata |
| Statistiche giocatori | Statistiche ogni 2 minuti; aggiunto lo stesso aggiornamento delle rose per mantenere corrette proprietà e filtri dopo il mercato. Rimossa anche la doppia richiesta iniziale delle rose e ignorate risposte di selezioni precedenti |
| Log | Polling ogni minuto |
| Preferenze e dispositivi push (elenco sul server) | Polling ogni 30 secondi |

Nei widget calendario/classifica `onRetry` compare **solo in errore**, non in fondo alle tabelle né negli stati vuoti. I «Riprova» restano per recuperare subito dopo problemi di rete. Gli intervalli già esistenti non sono stati accelerati.

## Mantenuti perché non sono ricaricamenti ridondanti

| Controllo | Motivo |
| --- | --- |
| Serie A admin: Ricarica | Polling disabilitato intenzionalmente; ricaricare il calendario sovrascrive la bozza delle modifiche |
| Partecipanti / qualificazioni | Nessun polling; ricaricare ripristina anche la selezione delle qualificate. Non va fatto automaticamente mentre si sceglie |
| Apertura: Aggiorna | Nessun polling, rilegge una classifica preparata esplicitamente; distinto da «Prepara / ricalcola» |
| Piattaforma: Aggiorna elenco | Nessun polling per gli elenchi amministrativi paginati |
| Carte admin: Aggiorna | Ricarica all'ingresso/focus, ma non periodicamente |
| Collegamenti MCP: Aggiorna collegamenti | Nessun polling; serve anche per vedere nuovi collegamenti autorizzati da un client esterno |
| Home senza gruppi: Sono stato invitato | Caricamento su ingresso e dopo modifiche locali, non polling delle nuove adesioni |
| Push: Aggiorna stato dispositivo | Verifica permessi/subscription del browser, non soltanto l'elenco server |
| Aggiorna foto, ricalcoli amministrativi, salvataggi | Comandi che modificano dati o avviano operazioni, non una semplice GET |
| Aggiorna ora (nuova versione PWA) | Attiva il service worker nuovo e ricarica l'app; non è un aggiornamento dei dati |
| Callback dopo salvataggio formazione | Necessaria per confermare la modifica e le versioni dal server; non è un pulsante manuale |

Gli esempi didattici in `examples/` non sono schermate dell'app e non sono stati modificati. Nessun nuovo polling introdotto sugli editor per non cancellare modifiche non salvate.
