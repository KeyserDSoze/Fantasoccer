using System.Collections.Frozen;

namespace Fantasoccer;

// Exact public provider names and three-letter codes, not a name-prefix rule.
// Evidence: docs/CLUB-CODES.md. Import/runtime never fetch this catalogue online.
public static class FantacalcioClubCodes
{
    public const string Version = "fantacalcio-2026-10-26-v1";

    private static readonly FrozenDictionary<string, string> s_codes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Atalanta"] = "ata",
        ["Benevento"] = "ben",
        ["Bologna"] = "bol",
        ["Brescia"] = "bre",
        ["Cagliari"] = "cag",
        ["Carpi"] = "car",
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
        ["SPAL"] = "spa",
        ["Spezia"] = "spe",
        ["Torino"] = "tor",
        ["Udinese"] = "udi",
        ["Venezia"] = "ven",
        ["Verona"] = "ver"
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyDictionary<string, string> Names => s_codes;

    public static bool TryGetCode(string? name, out string code)
    {
        if (name is not null && s_codes.TryGetValue(name.Trim(), out var found))
        {
            code = found;
            return true;
        }
        code = "";
        return false;
    }
}
