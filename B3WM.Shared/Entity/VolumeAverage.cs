using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace B3WM.Shared.Entity
{
    public class VolumeAverage
    {
        public string Symbol { get; set; }
        public double Last5Days { get; set; }
        public double Last10Days { get; set; }
        public double Last20Days { get; set; }
    }
}
