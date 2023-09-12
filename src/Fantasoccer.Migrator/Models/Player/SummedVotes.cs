using System.Collections.Generic;
using System.Linq;

namespace FantacalcioV4
{
    public record SummedVotes(List<Vote> Votes)
    {
        public double GetFantaAverage(LeagueSetting setting)
        {
            double returnValue = Votes.Where(x => x.HasVote).Sum(x => x.FinalValue(x.Role, setting).Value) / Votes.Where(x => x.HasVote).Count();
            if (double.IsNaN(returnValue))
                return 0;
            return returnValue;
        }
        public double GetAverage()
        {
            double returnValue = Votes.Where(x => x.HasVote).Sum(x => x.Value) / Votes.Where(x => x.HasVote).Count();
            if (double.IsNaN(returnValue))
                return 0;
            return returnValue;
        }
        public int GetTimesInPositive()
            => Votes.Where(x => x.HasPositiveBonus).Count();
        public int GetTimesInNegative()
            => Votes.Where(x => x.HasNegativeBonus).Count();
    }
}