namespace B3WM.Shared.Models
{
    /// <summary>
    /// Janela de datas de uma camada/gráfico (intraday ou diário).
    /// <see cref="Auto"/> = derivada do automode por estrutura; false = manual
    /// (slider from/to do drawer). É a fonte única consumida pelo app e pela
    /// JEV — o cálculo do automode vive no backend (StructureWindowResolver).
    /// </summary>
    public sealed class StructureWindow
    {
        public DateTime From { get; set; }

        public DateTime To { get; set; }

        public bool Auto { get; set; }
    }

    /// <summary>
    /// Foto da janela vigente de um símbolo/timeframe: a janela intraday (do
    /// timeframe informado) + a janela diária (1440). Enviada via SignalR
    /// (ReceiveOnMarketWindow) e REST (GetMarketWindow) para o app, e reusada
    /// internamente pela JEV para recortar todas as camadas do state.
    /// </summary>
    public sealed class MarketWindow
    {
        public string Symbol { get; set; } = "";

        /// <summary>Timeframe do gráfico intraday que originou esta janela.</summary>
        public int TimeFrame { get; set; }

        public StructureWindow Intraday { get; set; } = new();

        public StructureWindow Daily { get; set; } = new();
    }

    /// <summary>
    /// Pedido de resolução da janela: o app manda o símbolo/TF e as flags
    /// auto/manual (slider) da tela; o backend resolve o automode por
    /// estrutura e devolve a <see cref="MarketWindow"/>. É o gate para o
    /// backend ser a fonte única da janela (app aposenta o automode local).
    /// </summary>
    public sealed class MarketWindowRequest
    {
        public string Symbol { get; set; } = "";
        public int TimeFrame { get; set; } = 5;

        /// <summary>Automode intraday por estrutura (flag da tela).</summary>
        public bool ProfileAuto { get; set; }
        public DateTime? ProfileFrom { get; set; }
        public DateTime? ProfileTo { get; set; }

        /// <summary>
        /// Range carregado na tela (multi-dia) para o automode resolver sobre
        /// as MESMAS barras do app. Null/vazio → só o dia de hoje (intraday).
        /// </summary>
        public DateTime? RangeFrom { get; set; }
        public DateTime? RangeTo { get; set; }

        /// <summary>Automode diário por estrutura (flag da tela).</summary>
        public bool DailyAuto { get; set; }
        public DateTime? DailyFrom { get; set; }
        public DateTime? DailyTo { get; set; }
    }
}
