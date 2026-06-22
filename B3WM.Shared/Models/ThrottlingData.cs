using B3WM.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace B3WM.Shared.Models
{
    public class ThrottlingData
    {
        public IEnumerable<BarStorageItem> Candle { get; set; }
        public VolumeLevelStorageItem? Volume { get; set; }
    }
}
