using System.Collections.Generic;

namespace Fantasoccer
{
    public record RealDay(int Year, int SerieADay, List<RealGame> Games);
}