using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Text.Json.Serialization;

namespace B3WM.Shared.Entity
{
    public class TimesAndSales
    {
        public TimesAndSales(string paper)
        {
            Customer = new Customers(paper, paper, true);
            Ticks = new List<Ticks2>();
            TicksCross = new List<Ticks2>();
        }
        public TimesAndSales(Customers customer)
        {
            Customer = customer;
            Ticks = new List<Ticks2>();
            TicksCross = new List<Ticks2>();
        }
        public TimesAndSales() 
        { 
            Ticks = new List<Ticks2>(); 
            TicksCross = new List<Ticks2>(); 
        }

        [JsonIgnore]
        public IList<Ticks2> Ticks { get; set; }
        [JsonIgnore]
        public IList<Ticks2> TicksCross { get; set; }

        public IList<BookInfo> Book { get; set; }
        public IList<RobotAgent> Robots { get; set; }
        public IList<SequentialAgent> SequentialAgents { get; set; }
        public IList<OfferRenews> OfferRenews { get; set; }
        public IDictionary<Ticks2.Agents, List<SequentialNotorious>> SequentialNotorious { get; set; }
        public IDictionary<Ticks2.Agents, List<OfferRenewBreakups>> OfferRenewBreakups { get; set; }
        public int SequentialNotoriousCount {
            
            get
            {
                if (SequentialNotorious != null)
                    return (SequentialNotorious.Values.SelectMany(x => x)).Count();

                return 0;
            } 
        }
        public Trends Trends { get; set; }

        public IList<TWapAgent> TWapAgents { get; set; }
        public IList<TWapNotorious> TWapNotorious { get; set; }


        public MovimentShakers MovimentShaker { get; set; }

        public Striking Striking { get; set; }


        public Customers Customer { get; set; }

        public decimal Spread { get => (((decimal)LastAsk - (decimal)LastBid) >= 0.01m ? ((decimal)LastAsk - (decimal)LastBid) - 0.01m : ((decimal)LastAsk - (decimal)LastBid)); }
        public double Last { get; set; }
        public double LastBid { get; set; }
        public double LastAsk { get; set; }
        public double TeoryPrice { get; set; }
        public double LastTeoryPrice { get; set; }
        
        public double VWapIntraDay { get; set; }
        [JsonIgnore]
        public DateTime LastTimeVwapDayUpdate { get; set; }
        public double MaxDaily { get; set; }
        public double MinDaily { get; set; }

        public decimal Volatility1Min { get; set; }
        public int? TicksCount { get; set; }
        public long? TicksVolume { get; set; }

        public string TimeToEndsAuction { get; set; }

        public double? FinancialVolume { get; set; }

        public double? VarPercent { get; set; }
        public double? Var { get; set; }

        public double UsualVariation { get; set; }

        public bool IsInAuction { get; set; } = true;

        [JsonIgnore]
        public bool MustGetFirstValueAfterAuction { get; set; } = true;
        [JsonIgnore]
        public bool HadGotValue { get; set; } = false;

        [JsonIgnore]
        public double ValueAuctionUp { get => FirstValueAfterAuction * 1.1; }
        [JsonIgnore]
        public double ValueAuctionDown { get => FirstValueAfterAuction * 0.9; }

        [JsonIgnore]
        public double Gap { get => (IsInAuction && TeoryPrice > 0) ? TeoryPrice - Last : 0; }

        [JsonIgnore]
        public double GapPercent { get => (Gap != 0) ? (Gap / Last) * 100.0 : 0; }

        [JsonIgnore]
        public double GapPercentModule { get => Math.Abs(GapPercent); }

        [JsonIgnore]
        public double Distortion { get => (VWapIntraDay != 0) ? Last - VWapIntraDay : 0; }

        [JsonIgnore]
        public double DistortionPercent { get => (Distortion != 0) ? (Distortion / Last) * 100.0 : 0; }

        [JsonIgnore]
        public double DistortionPercentModule { get => Math.Abs(DistortionPercent); }

        [JsonIgnore]
        public Ticks _FirstValueAfterAuction { get; set; }
        [JsonIgnore]
        public Ticks _LastValueBeforeAuction { get; set; }

        public double FirstValueAfterAuction { get; set; }

        public bool IsAnomalousBuyer { get; set; }
        public bool IsAnomalousSeller { get; set; }

        public IDictionary<Ticks2.Agents, int> AgentsVolumes { get; set; }
        public IDictionary<Ticks2.Agents, double> AgentsParticipating { get; set; }

        public VolumeAverage VolumeAverage { get; set; }


        public static decimal VWapCalc(IEnumerable<Bars> bars, double Last)
        {
            if (bars != null && bars.Count() > 0)
            {
                // soma todos os volumes negociados dentro desse periodo
                decimal volumeSum = (decimal)bars.Sum(q => q.Volume / 100.0m);

                //typicalPrice = ((Candle.High + Candle.Low + Candle.Close)/3.0m)

                // pivot = média ponderada entre o valor da (negociação vezes volume negociado) / somatorio de volumes
                decimal pivot = bars.Sum(q => ((((decimal)q.High + (decimal)q.Low + (decimal)q.Close)/3.0m) * q.Volume) / 100.0m) / volumeSum;

                return pivot;
            }

            return (decimal)Last;
        }

        private decimal Volatility(int TimeInMinutes)
        {
            // pega todos os ticks de compra e venda no papel no periodo = TimeInMinutes
            var temp = Ticks.Where(q => q.Time >= DateTime.Now.AddMinutes(TimeInMinutes * -1));

            //se existirem ticks nesse periodo faça
            if (temp.Count() > 0)
            {
                // soma todos os volumes negociados dentro desse periodo
                decimal volumeSum = (decimal)temp.Sum(q => q.Volume / 100.0m);

                // pivot = média ponderada entre o valor da (negociação vezes volume negociado) / somatorio de volumes
                decimal pivot = temp.Sum(q => ((decimal)q.Value * q.Volume) / 100.0m) / volumeSum;

                //a diferença entre cada valor negociado e o pivot vezes o volume negociado
                decimal averageSum = (decimal)temp.Sum(q => (Math.Abs((decimal)q.Value - pivot)) * (q.Volume / 100.0m));

                //somatorio de todas as diferenças acima dividido pelo volume negociado (media aritimetica)
                return (averageSum / volumeSum);
            }

            return 0;
        }

        public void Calculate()
        {
            Volatility1Min = Volatility(1);
        }
    }
}
