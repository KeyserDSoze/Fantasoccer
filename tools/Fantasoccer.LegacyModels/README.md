# Modelli del dump legacy — solo per la migrazione

Questa DLL contiene i documenti del backend Blob/Table ritirato e le sue chiavi JSON compatte. È referenziata da `Fantasoccer.DataMigration` e, transitivamente, dai test di importazione. **L'API, Application.Core, Business e Domain non la referenziano e non la pubblicano.** Non contiene servizi, repository, controller o job eseguibili.

I tipi mantengono namespace, alias `JsonPropertyName`, campi e valori enum del formato sorgente. I modelli utilizzati anche dal motore di gioco (per esempio `Team`, `Vote`, `LeagueSetting`) restano in Domain, senza dipendenze da RepositoryFramework.

`LegacyEntity<T,TKey>` legge gli envelope `{"k": ..., "v": ...}` e quelli storici `{"Key": ..., "Value": ...}`. I nomi brevi hanno precedenza quando entrambi sono presenti, come nel precedente convertitore. Scrive il formato breve per i fixture di test; non scrive sullo storage sorgente. La compatibilità è coperta da fixture JSON letterali e dalla suite completa di importazione PostgreSQL/Azurite.

Nessun pacchetto Rystem è necessario per leggere questi formati. Non reintrodurre il framework per deserializzare i dump e non cambiare arbitrariamente i formati per semplificare il runtime.
