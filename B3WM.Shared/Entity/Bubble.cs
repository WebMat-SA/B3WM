using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace B3WM.Shared.Entity
{
    public class Bubble
    {
        public double Price { get; set; }
        public Ticks2.Agents? Agent { get; set; }
        public decimal Amount { get; set; }
        public DateTime Time { get; set; }
        public Ticks2.ActionType? ActionType { get; set; }
    }
}
