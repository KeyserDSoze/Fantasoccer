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
            var key = string.IsNullOrWhiteSpace(abbreviation) && name.Length == 3 ? name : abbreviation;
            if (key is not { Length: 3 } || key.Any(c => !char.IsAsciiLetter(c)))
                throw new ArgumentException("A club requires its three-letter provider abbreviation.");
            return key.ToLowerInvariant();
        }
    }
}
