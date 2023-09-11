using RepositoryFramework;

namespace Fantasoccer
{
    public class TeamDayKey : IDefaultKey
    {
        public required string Group { get; set; }
        public required int Year { get; set; }
        public required string Email { get; set; }
        public required string BasketId { get; set; }
        public required int Day { get; set; }
    }
}
