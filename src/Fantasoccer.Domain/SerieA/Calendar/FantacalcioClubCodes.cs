using System.Collections.Frozen;

namespace Fantasoccer;

// Exact public provider names/codes plus documented early-season references,
// not a name-prefix rule. Evidence: docs/CLUB-CODES.md. No runtime HTTP.
public static class FantacalcioClubCodes
{
    public const string Version = "fantacalcio-2026-10-27-v3";

    private static readonly FrozenDictionary<string, string> s_codes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Atalanta"] = "ata",
        ["Benevento"] = "ben",
        ["Bologna"] = "bol",
        ["Brescia"] = "bre",
        ["Cagliari"] = "cag",
        ["Carpi"] = "car",
        ["Catania"] = "cat",
        ["Cesena"] = "ces",
        ["Chievo"] = "chi",
        ["Como"] = "com",
        ["Cremonese"] = "cre",
        ["Crotone"] = "cro",
        ["Empoli"] = "emp",
        ["Fiorentina"] = "fio",
        ["Frosinone"] = "fro",
        ["Genoa"] = "gen",
        ["Inter"] = "int",
        ["Juventus"] = "juv",
        ["Lazio"] = "laz",
        ["Lecce"] = "lec",
        ["Livorno"] = "liv",
        ["Milan"] = "mil",
        ["Monza"] = "mon",
        ["Napoli"] = "nap",
        ["Palermo"] = "pal",
        ["Parma"] = "par",
        ["Pescara"] = "pes",
        ["Pisa"] = "pis",
        ["Roma"] = "rom",
        ["Salernitana"] = "sal",
        ["Sampdoria"] = "sam",
        ["Sassuolo"] = "sas",
        ["Siena"] = "sie",
        ["SPAL"] = "spa",
        ["Spezia"] = "spe",
        ["Torino"] = "tor",
        ["Udinese"] = "udi",
        ["Venezia"] = "ven",
        ["Verona"] = "ver"
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    // Explicit, documented aliases only. Keep them separate from canonical
    // names so two accepted labels never imply two different SQL club keys.
    private static readonly FrozenDictionary<string, string> s_aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Juve"] = "Juventus"
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyDictionary<string, string> Names => s_codes;
    public static IReadOnlyDictionary<string, string> Aliases => s_aliases;

    public static bool TryGetCode(string? name, out string code)
    {
        if (name is not null)
        {
            var label = name.Trim();
            if (s_aliases.TryGetValue(label, out var canonical)) label = canonical;
            if (s_codes.TryGetValue(label, out var found))
            {
                code = found;
                return true;
            }
        }
        code = "";
        return false;
    }
}
