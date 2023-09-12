using System.Collections.Generic;

namespace FantacalcioV4
{
    public record TeamByGroup(string Name, string MainLeague, List<TeamByYear> Teams);
}