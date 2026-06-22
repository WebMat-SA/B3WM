using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace B3WM.Shared.Entity
{
    public class FishingItem
    {
        public Customers Customer { get; set; }
        public double MediumValue { get; set; }
        public double HighPercent { get; set; }
        public double LowPercent { get; set; }
        public double HighDiff { get; set; }
        public double LowDiff { get; set; }
        public Bars Bar { get; set; }
    }
}
