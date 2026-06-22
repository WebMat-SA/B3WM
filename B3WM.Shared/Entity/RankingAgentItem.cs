using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace B3WM.Shared.Entity
{
    public class RankingAgentItem
    {
        public string Symbol { get; set; }
        public Ticks2.Agents Agent { get; set; }
        public long Volume { get; set; }
        public double AveragePrice { get; set; }
        public int Position { get; set; }
        public Ticks2.ActionType Type { get; set; }
    }
}
