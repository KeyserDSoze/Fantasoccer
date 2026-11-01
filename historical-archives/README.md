# Historical archives

## Archivio pubblico — 1 novembre 2026

**[Scarica 2026-11-01.zip](https://github.com/KeyserDSoze/Fantasoccer/releases/download/legacy-history-2026-11-01/2026-11-01.zip)**

[GitHub Release](https://github.com/KeyserDSoze/Fantasoccer/releases/tag/legacy-history-2026-11-01)
· [Checksum SHA-256](https://github.com/KeyserDSoze/Fantasoccer/releases/download/legacy-history-2026-11-01/2026-11-01.zip.sha256)
· [Indice pubblico](2026-11-01-public.json)

Lo ZIP contiene due istantanee Blob:

| Cartella | Contenitori | Blob | Origine |
|---|---:|---:|---|
| `migration-import-snapshot/` | 31 | 12.166 | Copia usata per la migrazione, legacy ancora attivo |
| `legacy-final-snapshot/` | 31 | 12.167 | Export finale dopo lo stop di `apifc` |

Esclusi da **entrambe** le istantanee:

- tutto Azure Table Storage;
- i contenitori `notification`, `pushnotification`, `pushnotificationlocks`, `pushnotificationsent`;
- i contenitori `mcp-oauth` e `mcp-keys`;
- dump PostgreSQL (contengono copie dei dati esclusi), `.env` e file locali non elencati nei manifest.

Tutti gli altri blob sono mantenuti byte per byte, con checksum originali nei
manifest. L'archivio contiene ancora dati reali di fantacalcio, inclusi nomi ed
email; non è un backup completo dell'autenticazione e non va reimportato su fcv2.

Lo ZIP pesa 616.790.415 byte (circa 588 MiB), oltre il limite GitHub di 100 MiB per
un normale file Git. È quindi un allegato pubblico della Release, non un file nel
pack Git. I link, i conteggi e lo SHA-256 restano versionati in questa cartella.

Verifiche completate: CRC dello ZIP, lunghezze e SHA-256 dei blob, scansione dei
marcatori noti di credenziali nei manifest e nei blob non immagine, comando
`verify` dello strumento di migrazione su entrambe le istantanee. Il digest
dell'allegato restituito da GitHub coincide con quello locale; download pubblico
verificato a HTTP 200. La scansione automatica non è una garanzia generale di
assenza di qualsiasi dato sensibile.

Per verificare il download, mettere ZIP e checksum nella stessa cartella:

```sh
sha256sum -c 2026-11-01.zip.sha256
```

Lo script riproducibile è [`scripts/archive-public-history.py`](../scripts/archive-public-history.py).
Richiede le due copie private originali e una directory di destinazione nuova:

```sh
python3 scripts/archive-public-history.py \
  --import-snapshot migration-data/golive-prova1 \
  --final-snapshot migration-data/legacy-final-2026-11-01 \
  --output migration-data/public-history-new
python3 -m unittest discover -s scripts/tests -p test_archive_public_history.py
```

## Backup integrale privato

Il precedente `historical-archives/2026-11-01.zip` **locale** è il backup integrale
privato, non lo ZIP pubblico omonimo della Release. Non è stato modificato o
pubblicato: contiene Table Storage, chiavi RSA private e credenziali push.
Il suo indice originale è [`2026-11-01.json`](2026-11-01.json), con un SHA-256 diverso.
Entrambi i file ZIP restano esclusi da Git; il file pubblico preparato localmente
si trova in `migration-data/public-history-2026-11-01/2026-11-01.zip`.

Export finale originale verificato: 37 contenitori, 12.384 blob, 3 tabelle e
1.469.687 entità Table. Rispetto alla copia d'importazione: 3 blob nuovi, 5
modificati e 1 rimosso. I manifest pubblici elencano esplicitamente le esclusioni.

Il legacy `apifc` resta fermo. Per un eventuale rollback va prima riavviato con
`az webapp start -g fc -n apifc`, poi ripubblicato il frontend legacy; non si devono
sovrascrivere i dati PostgreSQL con queste istantanee.
