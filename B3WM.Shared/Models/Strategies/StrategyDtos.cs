using B3WM.Shared.Models;

namespace B3WM.Shared.Models.Strategies
{
    public sealed class StrategyInfo
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string DetailsMd { get; set; } = "";
    }

    public sealed class StrategyArmRequest
    {
        public string Strategy { get; set; } = "";
        public string Symbol { get; set; } = "";
        /// <summary>Foto da tela (SymbolConfig.toJson do app, com daily).</summary>
        public System.Text.Json.JsonElement? ScreenConfig { get; set; }
        public DateTime? DisplayDate { get; set; }

        // Janela MANUAL do volume profile (slider from/to do drawer). Usada
        // quando o automode por estrutura está desligado; com o automode ligado
        // o backend resolve a janela (StructureWindowResolver) e ignora estes
        // campos. Enviados no arm para o backend ser a fonte única da janela.
        public DateTime? ProfileFrom { get; set; }
        public DateTime? ProfileTo { get; set; }

        // Janela MANUAL do gráfico diário (slider from/to do drawer diário),
        // com o mesmo racional de ProfileFrom/To para o automode 1440.
        public DateTime? DailyFrom { get; set; }
        public DateTime? DailyTo { get; set; }
    }

    /// <summary>
    /// Trigger manual disparado pelo botão direito em uma bubble do gráfico:
    /// reenvia a consulta da strategy como se o gatilho dela tivesse sido
    /// preenchido, sem passar pelo ShouldTrigger/filtro da sessão.
    /// </summary>
    public sealed class StrategyManualTriggerRequest
    {
        public string SessionId { get; set; } = "";
        public BubbleStorageItem? Bubble { get; set; }
    }

    public sealed class StrategySessionState
    {
        public string SessionId { get; set; } = "";
        public string Strategy { get; set; } = "";
        public string Symbol { get; set; } = "";
        /// <summary>TF da tela no PLAY (identidade da sessão, independente do lock de config).</summary>
        public int TriggerTimeFrame { get; set; } = 2;
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
        /// <summary>Data/hora completa da avaliação (canônico p/ timeline X; mesmo offset de Bar.Date).</summary>
        public DateTime At { get; set; }
        /// <summary>Hora da avaliação (candle: fechamento; bubble/exec: hora do evento). Display/compat.</summary>
        public string Time { get; set; } = "";
        /// <summary>candle_close | bubble | exec | virada do dia</summary>
        public string Event { get; set; } = "";
        public string Side { get; set; } = "";
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
        public string SnapshotHash { get; set; } = "";
        public string PositionBefore { get; set; } = "flat";
        /// <summary>O que resultou: manter | sinal abre X | sinal fecha | exec abre X @P | fecha @P (+N pts) | zera fim do dia</summary>
        public string Action { get; set; } = "";
        /// <summary>Arquivo da auditoria Jev em Data/JEV ("" quando não há).</summary>
        public string AuditFile { get; set; } = "";
        /// <summary>
        /// Identificador único do trigger manual. Vazio em triggers normais;
        /// entra na chave de merge para permitir reenviar a mesma bubble várias
        /// vezes sem o log descartar avaliações idênticas.
        /// </summary>
        public string TriggerId { get; set; } = "";
    }

    public sealed class PaperTrade
    {
        public string Side { get; set; } = "";
        /// <summary>Data/hora completa da entrada (canônico). EntryTime mantido p/ display/compat.</summary>
        public DateTime EntryAt { get; set; }
        public string EntryTime { get; set; } = "";
        public double Entry { get; set; }
        /// <summary>Data/hora completa da saída (canônico). ExitTime mantido p/ display/compat.</summary>
        public DateTime ExitAt { get; set; }
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
