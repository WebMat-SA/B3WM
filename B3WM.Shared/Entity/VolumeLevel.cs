using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace B3WM.Shared.Entity
{
    public class VolumeLevel
    {
        public double Price { get; set; }
        public long Total { get; set; }
        public long BuyVolume { get; set; }
        public long SellVolume { get; set; }

        public long Delta => BuyVolume - SellVolume;
    }
}
