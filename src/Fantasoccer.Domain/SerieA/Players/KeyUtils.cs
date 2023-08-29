using System.Text.RegularExpressions;

namespace Fantasoccer
{
    public static class KeyUtils
    {
        private static readonly Regex s_regex = new("[^A-Za-z]*");
        public static string GetPlayerKey(string name)
            => s_regex.Replace($"{name.ToLower().Replace("è", "e'").Replace("é", "e'").Replace("à", "a'").Replace("ò", "o'").Replace("ì", "i'").Replace("ù", "u'").Replace("'", string.Empty).Replace(" ", string.Empty).Replace("\'", string.Empty).Replace("-", string.Empty).Replace(".", string.Empty).Trim()}", string.Empty);
    }
}
