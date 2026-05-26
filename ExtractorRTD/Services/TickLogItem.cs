using B3WM.Shared.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExtractorTryd.Services
{
    public class TickLogItem
    {
        public string Symbol { get; set; }

        public string Message { get; set; }

        public Ticks2 Tick { get; set; }
    }
}
