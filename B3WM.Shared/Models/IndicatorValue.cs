namespace B3WM.Shared.Models
{
    public enum IndicatorPlotType
    {
        Line,
        Marker
    }

    public class IndicatorValue
    {
        public DateTime Time { get; set; }
        public string Symbol { get; set; } = "";
        public string IndicatorName { get; set; } = "";
        public string Key { get; set; } = "";
        public double Value { get; set; }
        public IndicatorPlotType PlotType { get; set; } = IndicatorPlotType.Line;
    }
}
