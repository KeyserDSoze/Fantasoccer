using System.Collections.Frozen;

namespace Fantasoccer;

// Exact public provider names/codes plus documented early-season references,
// not a name-prefix rule. Evidence: docs/CLUB-CODES.md. No runtime HTTP.
public static class FantacalcioClubCodes
{
    public const string Version = "fantacalcio-2026-10-27-v2";

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
