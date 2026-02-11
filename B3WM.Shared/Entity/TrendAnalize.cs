using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static B3WM.Shared.Entity.Trends;

namespace B3WM.Shared.Entity
{
    public class TrendAnalize
    {
        public TrendAnalize(Trends trend, Bars startBar, Bars endBar)
        {
            StartBar = startBar;
            EndBar = endBar;
            Trend = trend;

            if (Trend.Type == TypeTrend.Up)
            {
                StartValue = StartBar.Low;
                EndValue = EndBar.High;
            }
            else if (Trend.Type == TypeTrend.Down)
            {
                StartValue = StartBar.High;
                EndValue = EndBar.Low;
            }
        }

        public Trends Trend { get; set; }

        public Bars StartBar { get; set; }
        public Bars EndBar { get; set; }

        public double StartValue { get; set; }
        public double EndValue { get; set; }
    }
}
