using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace B3WM.Shared.Entity
{
    public class TrendParameters
    {
        public double bonusCandle { get; set; } = 4.0;
        public double percentilAllowedEnter { get; set; } = 0.0005;
        public int NubmersToAnalize { get; set; }
        public int NumbersToBeInside { get; set; }
        public double angle { get; set; } = 25.0;
        public int ticksNumber { get; set; } = 200;
        public DateTime? SuggestionDateNull { get; set; } = null;
        public TimeSpan? timeDiffNull { get; set; } = null;

    }
}
