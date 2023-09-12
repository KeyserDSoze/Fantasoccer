using System.Collections.Generic;

namespace FantacalcioV4
{
    public record AnnualLeague(string Id, LeagueType Type, LeagueSetting Settings, List<Team> Teams) : MiniLeague(Id, Type);
}