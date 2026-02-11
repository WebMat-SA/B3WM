using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace B3WM.Shared.Entity
{
    public class OpeningDefault
    {
        public Customers Customer { get; set; }
        public DateTime Date { get; set; }
        public TimeSpan Time { get; set; }
        public double HighVariation { get; set; }
        public double LowVariation { get; set; }
    }

    public class OperationResult
    {
        public bool IsGain { get; set; }
        public bool IsLoss => !IsGain;
        public double Result { get; set; }
        public Bars BarAction { get; set; }
    }
}
