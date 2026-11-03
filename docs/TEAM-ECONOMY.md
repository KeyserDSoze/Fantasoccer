# Economia delle squadre

Una sola definizione, in `RosterEconomy` (server) e `TeamHelper` (frontend), usata da
aste, mercato, classifica e schermata squadre.

**Costo netto** = Σ prezzi di tutte le righe della rosa (anche di chi è uscito)
− Σ rimborsi − crediti gol già ricevuti all'asta − saldo del mercato − premio iniziale.
**Crediti disponibili** = crediti iniziali della lega − costo netto.

| Stato | Rimborso |
|---|---|
| 0 Attivo | nessuno |
| 1 Infortunato/Estero (vecchio) | metà prezzo, per difetto |
| 2 Scartato / sostituito in asta | nessuno |
| 3 Venduto ad altro giocatore | l'intero prezzo di vendita (`Revenue`) |
| 4 Rimosso | la riga sparisce, come mai acquistato |
| 5 Uscito dalla Serie A | quota fissata dalla lega al momento (`Revenue`) |

Prima della 4.1.14 uno stato 3 lasciava un costo di 2 × prezzo − importo (comprato a 41,
venduto a 58: restavano 24). Ora restano 41 − 58 = −17, cioè la squadra incassa 17 in più.

## Regola "usciti dalla Serie A"

Impostazioni lega, scheda Economia: percentuale del prezzo che torna (default 50) e
arrotondamento per difetto (default) o per eccesso. Vale quando un giocatore fuori dalla
Serie A (nessun club attuale e un club precedente) viene sostituito nell'asta di riparazione,
e con lo stato 5 dalla finestra "Cambia stato". L'importo viene salvato in `Revenue`: cambiare
la regola dopo non modifica i rimborsi già registrati. Le leghe esistenti partono dal 50%.

## Classifica

`Money` = crediti disponibili − crediti gol già ricevuti (`RankCreditGranted`, altrimenti
`MoneyFromRank`); `PlusMoney` = crediti gol maturati (gol × regola); `ValueAssets` = somma, cioè
quello che la squadra porta alla prossima asta. Colonne "+$" e "Crediti". Non valorizzato per le
leghe derivate (nessuna economia propria) né per le giornate passate (nessuno storico dei saldi).
`Money` non viene salvato negli snapshot: si calcola alla lettura dalla rosa della stagione.
