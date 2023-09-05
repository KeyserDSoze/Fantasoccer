using System.Text.RegularExpressions;

namespace Fantasoccer
{
    public static class KeyUtils
    {
        private static readonly Regex s_regex = new("[^A-Za-z]*");
        public static string GetPlayerKey(string name)
            => s_regex.Replace(name.ToLower(), string.Empty);
    }
}
