using B3WM.Shared.Models;

namespace B3WM.Services.Indicators
{
    public class BollingerBands : IIndicator
    {
        public string Name => "Bollinger Bands";
        public bool Enabled { get; set; } = true;
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

            return new()
            {
                new() { Key = "Upper", Value = sma + multiplier * stdDev },
                new() { Key = "Middle", Value = sma },
                new() { Key = "Lower", Value = sma - multiplier * stdDev },
            };
        }
    }
}
