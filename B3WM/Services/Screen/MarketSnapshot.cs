using B3WM.Shared.Models;

namespace B3WM.Services.Screen
{
    /// <summary>Posição para o bloco Posição do state (flat = null).</summary>
    public sealed class PositionView
    {
        public string Side { get; set; } = ""; // comprar | vender
        public double Entry { get; set; }
        public DateTime EntryTime { get; set; }
        public double UnrealizedPts { get; set; }
        public int Candles { get; set; }
    }

    public sealed class DateWindow
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
    }

    /// <summary>Cabeçalho do state: enquadramento da tela.</summary>
    public sealed class HeaderView
    {
        public DateTime DisplayDate { get; set; } = DateTime.Today;
        public string Mode { get; set; } = "intraday";
        public double? LastPrice { get; set; }
        public List<int> VisibleTimeFrames { get; set; } = new();
        public double MinDistance { get; set; }
        public int ThresholdBubble { get; set; }
        public bool VwapVisible { get; set; } = true;
        public bool PanelOpen { get; set; }
        public bool TradingHistoryVisible { get; set; } = true;
        public bool PositionVisible { get; set; } = true;
        public bool OpenOrdersVisible { get; set; } = true;
        public int BubblesTotalDay { get; set; }
        public int BubblesVisibleDay { get; set; }
    }

    /// <summary>
    /// Estado da tela em container dinâmico: camadas por chave
    /// ("candles", "bubbles", "volume", "structures", "extremesIntra",
    /// "extremesDaily", "pivotIntra", "pivotDaily", "vwap", "position",
    /// "header"). Valores continuam tipados; o CONTAINER é dinâmico:
    /// camada nova = nova chave, sem bag tipada e sem mini-cópias.
    /// Camada ausente ou desligada = linha "desligado/sem dados" no texto.
    /// </summary>
    public sealed class MarketSnapshot
    {
        public Dictionary<string, object?> Layers { get; } = new();

        public bool Has(string key) => Layers.TryGetValue(key, out var v) && v != null;

        public T? Get<T>(string key) where T : class =>
            Layers.TryGetValue(key, out var v) ? v as T : null;

        public T Value<T>(string key, T fallback) where T : struct =>
            Layers.TryGetValue(key, out var v) && v is T t ? t : fallback;

        /// <summary>Número (double/int/long/decimal) ou null.</summary>
        public double? Num(string key)
        {
            if (!Layers.TryGetValue(key, out var v) || v == null)
                return null;
            return v switch
            {
                double d => d,
                float f => f,
                long l => l,
                int i => i,
                decimal m => (double)m,
                _ => null,
            };
        }
    }

    public static class SnapshotLayers
    {
        public const string Candles = "candles";
        public const string Bubbles = "bubbles";
        public const string Volume = "volume";
        public const string Structures = "structures";
        public const string ExtremesIntra = "extremesIntra";
        public const string ExtremesDaily = "extremesDaily";
        public const string PivotIntra = "pivotIntra";
        public const string PivotDaily = "pivotDaily";
        public const string Vwap = "vwap";
        public const string Position = "position";
        public const string Header = "header";
        public const string StructureAuxVisibleFlag = "structureAuxVisible";
        public const string DailyWindow = "dailyWindow";
    }
}
