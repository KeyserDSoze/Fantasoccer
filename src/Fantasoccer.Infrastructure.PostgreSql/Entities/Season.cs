namespace Fantasoccer.Infrastructure.PostgreSql.Entities;

public sealed class Season
{
    // Existing convention: 2012/13 = 1. This is not a calendar year or matchday.
    public int SeasonIndex { get; set; }
    public int StartYear { get; set; }
}
