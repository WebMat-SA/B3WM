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
        /// <summary>Todos os itens do dia (mais recentes primeiro), p/ exibir sob a sessão.</summary>
        public List<StrategyDecisionLog> ReportItems { get; set; } = new();
    }

    /// <summary>
    /// Um item do relatório: cada avaliação (gatilho) e cada execução paper
    /// geram um. Exibido sob a sessão aberta na aba Estratégia.
    /// </summary>
    public sealed class StrategyDecisionLog
    {
        /// <summary>avaliacao | execucao</summary>
        public string Kind { get; set; } = "avaliacao";
        /// <summary>Hora da avaliação (candle: fechamento; bubble/exec: hora do evento).</summary>
        public string Time { get; set; } = "";
        /// <summary>candle_close | bubble | exec | virada do dia</summary>
        public string Event { get; set; } = "";
        public string Side { get; set; } = "";
        public double Confidence { get; set; }
        public double Encerrar { get; set; }
        public bool ShouldTrade { get; set; }
        public string Reason { get; set; } = "";
        public int StateChars { get; set; }
        public string SnapshotHash { get; set; } = "";
        public string PositionBefore { get; set; } = "flat";
        /// <summary>O que resultou: manter | sinal abre X | sinal fecha | exec abre X @P | fecha @P (+N pts) | zera fim do dia</summary>
        public string Action { get; set; } = "";
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
        public List<StrategyDecisionLog> Decisions { get; set; } = new();
        public List<PaperTrade> PaperTrades { get; set; } = new();
        public double RealizedPts { get; set; }
    }
}
