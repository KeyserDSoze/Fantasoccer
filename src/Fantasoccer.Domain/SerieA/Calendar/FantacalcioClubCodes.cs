using System.Collections.Frozen;

namespace Fantasoccer;

// Exact public provider names/codes plus documented early-season references,
// not a name-prefix rule. Evidence: docs/CLUB-CODES.md. No runtime HTTP.
public static class FantacalcioClubCodes
{
    public const string Version = "fantacalcio-2026-10-28-v8";

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
    // Official names were read verbatim on the clubs' official websites on
    // 28 October 2026 (see docs/CLUB-CODES.md); no prefix or fuzzy matching.
    private static readonly FrozenDictionary<string, string> s_aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Juve"] = "Juventus",
        ["Hellas Verona"] = "Verona",
        ["Hellas Verona FC"] = "Verona",
        ["Como 1907"] = "Como",
        ["Venezia FC"] = "Venezia",
        ["Atalanta Bergamasca Calcio"] = "Atalanta",
        ["Benevento Calcio"] = "Benevento",
        ["Bologna Football Club 1909"] = "Bologna",
        ["Bologna FC"] = "Bologna",
        ["Cagliari Calcio"] = "Cagliari",
        ["Catania FC"] = "Catania",
        ["Cesena FC"] = "Cesena",
        ["US Cremonese"] = "Cremonese",
        ["U.S. Cremonese"] = "Cremonese",
        ["FC Crotone"] = "Crotone",
        ["F.C. Crotone"] = "Crotone",
        ["ACF Fiorentina"] = "Fiorentina",
        ["Frosinone Calcio"] = "Frosinone",
        ["Genoa CFC"] = "Genoa",
        ["Genoa Cricket and Football Club"] = "Genoa",
        ["FC Internazionale Milano"] = "Inter",
        ["Juventus FC"] = "Juventus",
        ["Juventus Football Club"] = "Juventus",
        ["S.S. Lazio"] = "Lazio",
        ["US Lecce"] = "Lecce",
        ["U.S. Lecce"] = "Lecce",
        ["Unione Sportiva Lecce"] = "Lecce",
        ["AC Milan"] = "Milan",
        ["AC Monza"] = "Monza",
        ["A.C. Monza"] = "Monza",
        ["Associazione Calcio Monza"] = "Monza",
        ["SSC Napoli"] = "Napoli",
        ["Palermo FC"] = "Palermo",
        ["Palermo F.C."] = "Palermo",
        ["Parma Calcio 1913"] = "Parma",
        ["Pescara Calcio 1936"] = "Pescara",
        ["AS Roma"] = "Roma",
        ["U.S. Salernitana 1919"] = "Salernitana",
        ["U.C. Sampdoria"] = "Sampdoria",
        ["U.S. Sassuolo"] = "Sassuolo",
        ["U.S. Sassuolo Calcio"] = "Sassuolo",
        ["Spezia Calcio"] = "Spezia",
        ["Torino FC"] = "Torino",
        ["Torino FC 1906"] = "Torino",
        ["Udinese Calcio"] = "Udinese"
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    // Provider short codes that look like valid three-letter codes but differ
    // from the verified one for the same club. Accepted only together with
    // that club's verified name or alias. Labels that are not three-letter
    // codes at all (for example "MONZ") need no entry: see KeyUtils.GetClubKey.
    private static readonly FrozenDictionary<string, string> s_codeVariants = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["hel"] = "ver"
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
