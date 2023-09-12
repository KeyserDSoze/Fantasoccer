using System.Collections.Generic;

namespace FantacalcioV4
{
    public record League(string Name, bool IsMain, int Participant, List<AnnualLeague> Years);
}