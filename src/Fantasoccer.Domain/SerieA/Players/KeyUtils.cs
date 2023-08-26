namespace Fantasoccer
{
    public static class KeyUtils
    {
        public static string GetPlayerKey(string name)
            => $"{name.ToLower().Replace("è", "e'").Replace("é", "e'").Replace("à", "a'").Replace("ò", "o'").Replace("ì", "i'").Replace("ù", "u'").Replace("'", string.Empty).Replace(" ", string.Empty).Replace("\'", string.Empty).Replace("-", string.Empty).Replace(".", string.Empty).Trim()}";
    }
}
