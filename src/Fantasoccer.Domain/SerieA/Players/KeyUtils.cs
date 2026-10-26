using System.Text.RegularExpressions;
using System.Globalization;

namespace Fantasoccer
{
    public static class KeyUtils
    {
        private static readonly Regex s_regex = new("[^A-Za-z]*");
        public static string GetPlayerKey(string name)
            => s_regex.Replace(name.ToLower(), string.Empty);

        // Readable representation only. SQL uses separate year/player-key fields.
        public static string GetSeasonPlayerKey(string name, int year)
        {
            if (year <= 0) throw new ArgumentOutOfRangeException(nameof(year));
            var key = GetPlayerKey(name);
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("A player name must produce a nonempty normalized key.");
            return year.ToString(CultureInfo.InvariantCulture) + "/" + key;
        }

        public static string GetClubKey(string? abbreviation, string name)
        {
            var known = FantacalcioClubCodes.TryGetCode(name, out var providerCode);
            var key = abbreviation;
            if (string.IsNullOrWhiteSpace(key))
                key = known ? providerCode : name.Length == 3 ? name : null;
            if (key is not { Length: 3 } || key.Any(c => !char.IsAsciiLetter(c)))
                throw new ArgumentException("A club requires a verified name or its three-letter provider abbreviation.");
            key = key.ToLowerInvariant();
            if (known && key != providerCode)
                throw new ArgumentException("The explicit club code contradicts the verified provider name.");
            return key;
        }
    }
}
