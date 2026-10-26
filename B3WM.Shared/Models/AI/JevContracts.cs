namespace B3WM.Shared.Models.AI
{
    /// <summary>
    /// Pedido de análise (endpoint avulso Ai/Analyze). Leva o blob cru da
    /// tela (SymbolConfig.toJson do app, com bloco daily) + parâmetros
    /// operacionais (janelas, lookbacks, threshold). Filtros de camadas vêm
    /// SÓ do blob — sem espelho tipado, sem campos avulsos.
    /// </summary>
    public sealed class JevAnalyzeRequest
    {
        public string Symbol { get; set; } = string.Empty;
        public int TimeFrame { get; set; } = 5;
        public int LookbackCandles { get; set; } = 60;
        public int LookbackBubbles { get; set; } = 30;

        /// <summary>Dia exibido na tela (default: hoje). Se != hoje, só arquivos (sem live).</summary>
        public DateTime? DisplayDate { get; set; }

        /// <summary>Timeframes de estrutura visíveis. Default: [TimeFrame].</summary>
        public List<int>? VisibleTimeFrames { get; set; }

        /// <summary>
        /// Tela toda (blob opaco = SymbolConfig.toJson() do Flutter, com
        /// bloco daily). {} ou ausente = defaults.
        /// </summary>
        public System.Text.Json.JsonElement? ScreenConfig { get; set; }

        /// <summary>
        /// Janela do volume profile intraday (range do slider; null = dia
        /// inteiro). Espelha `_extremeRangeFromBars` do app.
        /// </summary>
        public DateTime? ProfileFrom { get; set; }
        public DateTime? ProfileTo { get; set; }

        /// <summary>Janela do gráfico diário (slider inferior). Default: últimos 60 dias.</summary>
        public DateTime? DailyFrom { get; set; }
        public DateTime? DailyTo { get; set; }
    }

    /// <summary>
    /// Resultado estruturado da Jev — INFORMATIVO (Composite scoring). Não
    /// decide operar/não-operar: mostra a confiança de cada criteria, um viés
    /// e confianças agregadas. A decisão de operar é manual.
    /// </summary>
    public sealed class JevAnalysisResult
    {
        public string Symbol { get; set; } = string.Empty;
        public string Model { get; set; } = "jev-latest";

        /// <summary>Viés cético: comprar | vender | lateral.</summary>
        public string Direcao { get; set; } = "lateral";

        /// <summary>Confiança do viés (0..1), penalizada por conflito/cobertura. Informativa.</summary>
        public double DirecaoConfidence { get; set; }

        /// <summary>Net assinado (-1..1): positivo = alta, negativo = baixa.</summary>
        public double DirecaoNet { get; set; }

        /// <summary>Consenso entre as dimensões (0..1).</summary>
        public double DirecaoConsenso { get; set; }

        /// <summary>Cobertura das dimensões respondidas (0..1).</summary>
        public double DirecaoCobertura { get; set; }

        /// <summary>Motivo da lateral (vazio quando há compra/venda).</summary>
        public string DirecaoMotivo { get; set; } = "";

        /// <summary>Confiança de operabilidade = média ponderada (0..1).</summary>
        public double ConfiancaOperavel { get; set; }

        /// <summary>Confiança por indicador/criteria (id → 0..1).</summary>
        public Dictionary<string, double> Criteria { get; set; } = new();

        /// <summary>Proximidade técnica ao gatilho (0 = longe, 1 = colado). Informativa.</summary>
        public double Proximidade { get; set; }

        /// <summary>Distância crua até a referência mais próxima, em ticks.</summary>
        public double ProximidadeTicks { get; set; }

        /// <summary>Nome da referência mais próxima (ex.: pivo_diario, vwap).</summary>
        public string ProximidadeRef { get; set; } = string.Empty;

        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
        public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
        public string StatePreview { get; set; } = string.Empty;
        /// <summary>Arquivo da auditoria em Data/JEV ("" quando não gravado).</summary>
        public string AuditFile { get; set; } = string.Empty;
    }
}
