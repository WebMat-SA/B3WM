using B3WM.Shared.Models;

namespace B3WM.Services.Indicators
{
    public class BollingerBounce : IIndicator
    {
        public string Name => "Bollinger Bounce";
        public bool Enabled { get; set; } = false;
        public int NeedsBarCount => (int)_params["Period"];

        private readonly Dictionary<string, double> _params = new()
        {
            ["Period"] = 20,
            ["Multiplier"] = 2.0
        };

        public IReadOnlyDictionary<string, double> Parameters => _params;

        public void SetParameter(string name, double value)
        {
            if (_params.ContainsKey(name))
                _params[name] = value;
        }

        public List<IndicatorResult> Evaluate(BarStorageItem bar, IReadOnlyList<BarStorageItem> history)
        {
            var period = (int)_params["Period"];
            var multiplier = _params["Multiplier"];
            var closes = history.Select(h => (double)h.Close).ToArray();
            var sma = closes.Average();
            var variance = closes.Select(c => Math.Pow(c - sma, 2)).Average();
            var stdDev = Math.Sqrt(variance);
            var upper = sma + multiplier * stdDev;
            var lower = sma - multiplier * stdDev;

            var close = (double)bar.Close;
            var results = new List<IndicatorResult>();

            if (close >= upper)
            {
                results.Add(new()
                {
                    Key = "Sell",
                    Value = close,
                    PlotType = IndicatorPlotType.Marker
                });
            }
            else if (close <= lower)
            {
                results.Add(new()
                {
                    Key = "Buy",
                    Value = close,
                    PlotType = IndicatorPlotType.Marker
                });
            }

            return results;
        }
    }
}
