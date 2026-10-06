namespace B3WM.Shared.Models.Strategies
{
    public sealed class StrategyInfo
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
    }

    public sealed class StrategyArmRequest
    {
        public string Strategy { get; set; } = "";
        public string Symbol { get; set; } = "";
        /// <summary>Foto da tela (SymbolConfig.toJson do app, com daily).</summary>
        public System.Text.Json.JsonElement? ScreenConfig { get; set; }
        public DateTime? DisplayDate { get; set; }
    }

    public sealed class StrategySessionState
    {
        public string SessionId { get; set; } = "";
        public string Strategy { get; set; } = "";
        public string Symbol { get; set; } = "";
        public bool Paused { get; set; }
        public string SnapshotHash { get; set; } = "";
        public string? Position { get; set; }
        public int Decisions { get; set; }
        public double RealizedPts { get; set; }
        public DateTime StartedAt { get; set; }
        public string? LastDecision { get; set; }
    }

    public sealed class PaperTrade
    {
        public string Side { get; set; } = "";
        public string EntryTime { get; set; } = "";
        public double Entry { get; set; }
        public string ExitTime { get; set; } = "";
        public double Exit { get; set; }
        public double Pts { get; set; }
        public string ExitReason { get; set; } = "";
        public double EntryConf { get; set; }
    }

    public sealed class StrategyLogDay
    {
        public DateTime Date { get; set; }
        public string Symbol { get; set; } = "";
        public string Strategy { get; set; } = "";
        public string SnapshotHash { get; set; } = "";
        public List<string> Decisions { get; set; } = new();
        public List<PaperTrade> PaperTrades { get; set; } = new();
        public double RealizedPts { get; set; }
    }
}
