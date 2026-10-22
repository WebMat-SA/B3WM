using B3WM.Services.Screen;
using B3WM.Shared.Models;
using B3WM.Shared.Models.AI;

namespace B3WM.Services.Strategies
{
    /// <summary>Evento de mercado que pode disparar uma strategy.</summary>
    public abstract class StrategyEvent
    {
        public string Symbol { get; set; } = "";
        public DateTime At { get; set; }
    }

    public sealed class CandleClosed : StrategyEvent
    {
        public BarStorageItem Bar { get; set; } = null!;
    }

    public sealed class BubbleAppeared : StrategyEvent
    {
        public BubbleStorageItem Item { get; set; } = null!;
    }

    /// <summary>Posição paper vigente (zerada no fim do dia).</summary>
    public sealed class PaperPosition
    {
        public string Side { get; set; } = ""; // comprar | vender
        public double Entry { get; set; }
        public DateTime EntryTime { get; set; }
        public double EntryConf { get; set; }
    }

    /// <summary>Contexto da avaliação: spec cru + snapshot + estado paper.</summary>
    public sealed class StrategyContext
    {
        public string Symbol { get; set; } = "";
        /// <summary>TF da sessão (foto do PLAY). Bubble não carrega TF no evento.</summary>
        public int TimeFrame { get; set; } = 2;
        /// <summary>Blob cru da tela (foto do Arm). Parse sob demanda e descarte.</summary>
        public string ScreenSpecJson { get; set; } = "{}";
        public string SnapshotHash { get; set; } = "";
        /// <summary>Preenchido por strategies que resolvem o snapshot (ex.: IA).</summary>
        public Screen.MarketSnapshot? Snapshot { get; set; }
        public PaperPosition? Position { get; set; }
    }

    /// <summary>Decisão de uma strategy (log-only; envio real comentado no runner).</summary>
    public sealed class StrategyDecision
    {
        /// <summary>comprar | vender | lateral | manter | encerrar</summary>
        public string Side { get; set; } = "manter";
        public double Confidence { get; set; }
        public double Encerrar { get; set; }
        public bool ShouldTrade { get; set; }
        /// <summary>Confiança por criteria (JEV — informativo). Vazio no resto.</summary>
        public Dictionary<string, double> Criteria { get; set; } = new();
        public string Reason { get; set; } = "";
        /// <summary>Net direcional JEV (-1..1): negativo = baixa, positivo = alta.</summary>
        public double? DirecaoNet { get; set; }
        /// <summary>Consenso das dimensões JEV (0..1).</summary>
        public double? DirecaoConsenso { get; set; }
        /// <summary>Cobertura das dimensões JEV (0..1).</summary>
        public double? DirecaoCobertura { get; set; }
        /// <summary>Motivo da lateral JEV (vazio quando há direção).</summary>
        public string? DirecaoMotivo { get; set; }
        public int StateChars { get; set; }
        /// <summary>Arquivo da auditoria Jev em Data/JEV ("" quando não há).</summary>
        public string AuditFile { get; set; } = "";
    }

    /// <summary>Parâmetro declarativo de strategy (renderizado na aba).</summary>
    public sealed class StrategyParam
    {
        /// <summary>number | int | bool</summary>
        public string Type { get; set; } = "number";
        public string Key { get; set; } = "";
        public string Label { get; set; } = "";
        public double Min { get; set; }
        public double Max { get; set; }
        public double Default { get; set; }
        public double Step { get; set; } = 1;
    }
}
