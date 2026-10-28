using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Fantasoccer.Application.Core.Groups;

// Integrity rules enforced by PostgreSQL (constraints and triggers) surface as
// stable API faults, whichever path (HTTP, MCP, job) reached the database.
public static class DatabaseFaults
{
    private static readonly Dictionary<string, (int Status, string Message)> Known = new(StringComparer.Ordinal)
    {
        ["league_owner_conflict"] = (409, "Un utente non può essere proprietario o comproprietario di più squadre nella stessa lega e stagione."),
        ["derived_team_ownership"] = (409, "Una squadra derivata non ha proprietari propri: valgono quelli della squadra originale."),
        ["derived_team_has_own_data"] = (409, "La squadra ha già proprietari o rose proprie e non può diventare derivata."),
        ["derived_team_roster"] = (409, "Una squadra derivata usa la rosa della squadra originale: nessuna rosa o economia propria."),
        ["derived_basket_team_without_origin"] = (409, "Le squadre di un basket derivato devono essere collegate a una squadra originale."),
        ["qualification_without_source_participation"] = (409, "La squadra originale non partecipa alla lega di provenienza indicata."),
        ["league_basket_kind_mismatch"] = (409, "Una lega autonoma usa basket autonomi; una lega derivata usa basket derivati."),
        ["league_mode_locked"] = (409, "La modalità della lega non può cambiare dopo averla collegata a basket o leghe di origine."),
        ["league_derivation_requires_derived_league"] = (409, "Solo una lega derivata può dipendere da leghe di origine."),
        ["league_derivation_cycle"] = (409, "Dipendenza circolare fra leghe: operazione rifiutata."),
        ["team_derivation_immutable"] = (409, "Il collegamento alla squadra originale non può essere modificato."),
    };

    public static bool TryTranslate(Exception exception, out ApplicationFault fault)
    {
        fault = null!;
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is not PostgresException postgres) continue;
            string? code = null;
            if (postgres.SqlState == PostgresErrorCodes.UniqueViolation && postgres.ConstraintName == "PK_LeagueParticipantOwners") code = "league_owner_conflict";
            else if (postgres.SqlState is PostgresErrorCodes.CheckViolation or PostgresErrorCodes.RaiseException && postgres.MessageText.StartsWith("fz:", StringComparison.Ordinal))
                code = postgres.MessageText[3..];
            if (code is not null && Known.TryGetValue(code, out var known))
            {
                fault = new ApplicationFault(known.Status, code, known.Message);
                return true;
            }
        }
        return false;
    }

    // Runs a write and rethrows a translated fault when a database rule refuses it.
    public static async Task<T> GuardAsync<T>(Func<Task<T>> write)
    {
        try { return await write(); }
        catch (Exception exception) when (exception is DbUpdateException or PostgresException && TryTranslate(exception, out _))
        {
            TryTranslate(exception, out var fault);
            throw fault;
        }
    }

    public static Task GuardAsync(Func<Task> write) => GuardAsync<bool>(async () => { await write(); return true; });
}
