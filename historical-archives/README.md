# Historical archives

Archivio storico del passaggio a PostgreSQL, 1 novembre 2026.

- L'API legacy `apifc` è stata fermata prima dell'ultimo export.
- La copia usata per la migrazione resta intatta in `migration-data/golive-prova1/`.
- Una copia separata `migration-data/legacy-final-2026-11-01/` raccoglie lo stato
  finale di Blob e Table Storage. Il resume rilegge anche i blob modificati,
  non solo i nomi nuovi. Lo stato finale non viene reimportato su fcv2.
- Il file locale previsto è `historical-archives/2026-11-01.zip`: contiene entrambe
  le istantanee e i dati del ripristino PostgreSQL, senza `.env` o credenziali
  di accesso allo storage aggiunte dall'operatore.
- I dati esportati stessi possono contenere credenziali dei dispositivi push,
  email e log di autenticazione. Lo ZIP completo è quindi escluso da Git.
  Una copia pubblica in chiaro deve essere sanitizzata separatamente, senza
  modificare il backup originale; in alternativa usare cifratura e storage privato.
- Il riepilogo pubblico può contenere conteggi, date e SHA-256 dello ZIP, mai
  contenuti dei blob, endpoint push o nomi di file che contengano email.

Export e verifica completati: 37 contenitori, 12.384 blob, 3 tabelle e 1.469.687
entità Table. Dalla copia d'importazione: 3 blob nuovi, 5 modificati e 1 rimosso.
Lo ZIP locale è stato verificato integralmente e pesa circa 663 MiB: supera anche
il limite di 100 MiB per un normale file GitHub. Il checksum e le date sono in
[`2026-11-01.json`](2026-11-01.json); nessun contenuto privato è incluso nell'indice.

`SecurityOptions` nel Table Storage contiene componenti RSA privati: non rendere
pubblico lo ZIP originale. Per un eventuale rollback va prima riavviata la vecchia
API con `az webapp start -g fc -n apifc`, poi ripubblicato il frontend legacy;
non si devono sovrascrivere i dati PostgreSQL con questa istantanea.
