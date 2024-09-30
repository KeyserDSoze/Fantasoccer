using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fantasoccer
{
    public sealed class RandomCalendarDay
    {
        public required int Day { get; set; }
        public required int Round { get; set; }
        public required RandomCalendarGame Game { get; set; }
    }
}
