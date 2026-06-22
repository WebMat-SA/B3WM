using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace B3WM.Shared.Entity
{
    public class TWapAgent
    {
        public Ticks2.Agents Agent { get; set; }
        public Ticks2.ActionType Type { get; set; }
        public double VolumeinOneMinute { get; set; }
        public double RSquared { get; set; }
        public double VolumePercent { get; set; }
    }
}
