using B3WM.Shared.Models;

namespace B3WM.Services.Indicators
{
    public interface IIndicator
    {
        string Name { get; }
        bool Enabled { get; set; }
        int NeedsBarCount { get; }
        IReadOnlyDictionary<string, double> Parameters { get; }
        void SetParameter(string name, double value);
        List<IndicatorResult> Evaluate(BarStorageItem bar, IReadOnlyList<BarStorageItem> history);
    }

    public class IndicatorResult
    {
        public string Key { get; set; } = "";
        public double Value { get; set; }
        public IndicatorPlotType PlotType { get; set; } = IndicatorPlotType.Line;
        public int TimeFrame { get; set; }
    }
}
