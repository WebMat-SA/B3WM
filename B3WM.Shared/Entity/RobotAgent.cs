using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using B3WM.Shared.Extensions;

namespace B3WM.Shared.Entity
{
    public class RobotAgent
    {
        public Ticks2.Agents Agent { get; set; }
        public Ticks2.ActionType Type { get; set; }
        [JsonIgnore]
        public List<TimeSpan> Periods { get; set; }
        public DateTime LastAction { get; set; }
        [JsonConverter(typeof(B3WM.Shared.Extensions.TimeSpanConverter))]
        public TimeSpan AverageAggression { get; set; }
        public int IncidentCounter { get; set; }
        //public int AverageAmount { get; set; }

        public DateTime NextPrediction 
        { 
            get
            {
                if (LastAction != null && AverageAggression != null)
                    return LastAction.Add(AverageAggression);
                else
                    return DateTime.MinValue;
            } 
        }

    }
}
