using System.Text.RegularExpressions;
using System.Globalization;

namespace Fantasoccer
{
    public static class KeyUtils
    {
        private static readonly Regex s_regex = new("[^A-Za-z]*");
        public static string GetPlayerKey(string name)
            => s_regex.Replace(name.ToLower(), string.Empty);

        public static string GetSeasonPlayerKey(string name, int year)
        {
            if (year <= 0) throw new ArgumentOutOfRangeException(nameof(year));
            var key = GetPlayerKey(name);
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("A player name must produce a nonempty normalized key.");
            return year.ToString(CultureInfo.InvariantCulture) + ":" + key;
        }
    }
}
