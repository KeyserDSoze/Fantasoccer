using System.Collections.Frozen;

namespace Fantasoccer;

// Exact public provider names/codes plus documented early-season references,
// not a name-prefix rule. Evidence: docs/CLUB-CODES.md. No runtime HTTP.
public static class FantacalcioClubCodes
{
    public const string Version = "fantacalcio-2026-10-28-v7";

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

    private static readonly FrozenDictionary<string, string> s_names = s_codes
        .ToFrozenDictionary(x => x.Value, x => x.Key, StringComparer.Ordinal);

    // Explicit, documented aliases only. Keep them separate from canonical
    // names so two accepted labels never imply two different SQL club keys.
    private static readonly FrozenDictionary<string, string> s_aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Juve"] = "Juventus",
        ["Hellas Verona"] = "Verona",
        ["Como 1907"] = "Como",
        ["Venezia FC"] = "Venezia"
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    // Provider short-code variants observed for the same verified club. They
    // are accepted only together with that club's verified name or alias, as
    // exact lowercase labels (no trimming), and may differ from three letters.
    private static readonly FrozenDictionary<string, string> s_codeVariants = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["hel"] = "ver",
        ["monz"] = "mon"
    }.ToFrozenDictionary(StringComparer.Ordinal);

    public static IReadOnlyDictionary<string, string> Names => s_codes;
    public static IReadOnlyDictionary<string, string> Aliases => s_aliases;
    public static IReadOnlyDictionary<string, string> CodeVariants => s_codeVariants;

    public static bool IsCodeVariant(string providerCode, string explicitCode)
        => s_codeVariants.TryGetValue(explicitCode.ToLowerInvariant(), out var canonical) && canonical == providerCode;

    public static bool TryGetName(string? code, out string name)
    {
        if (code is not null && s_names.TryGetValue(code.Trim().ToLowerInvariant(), out var found))
        {
            name = found;
            return true;
        }
        name = "";
        return false;
    }

    public static string CanonicalName(string code, string sourceName)
        => TryGetName(code, out var name) ? name : sourceName.Trim();

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
