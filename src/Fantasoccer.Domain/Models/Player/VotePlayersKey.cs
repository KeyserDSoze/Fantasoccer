using RepositoryFramework;

namespace Fantasoccer
{
    public record VotePlayersKey : IKey
    {
        public required int Year { get; set; }
        public required int Day { get; set; }
        public static IKey Parse(string keyAsString)
        {
            var splitted = keyAsString.Split('-');
            return new VotePlayersKey { Year = int.Parse(splitted[0]), Day = int.Parse(splitted[1]) };
        }

        public string AsString()
        {
            return $"{Year}-{Day}";
        }
    }
}
