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
        public double Threshold { get; set; } = 0.7;

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
    /// Resultado estruturado da Jev + gate local. Só logado (debug), nunca
    /// executa ordem (envio real comentado no runner).
    /// </summary>
    public sealed class JevAnalysisResult
    {
        public string Symbol { get; set; } = string.Empty;
        public string Model { get; set; } = "jev-latest";
        public string Direcao { get; set; } = "manter";
        public double DirecaoConfidence { get; set; }
        public Dictionary<string, double> DirecaoProbabilities { get; set; } = new();
        public double ForcaTendencia { get; set; }
        public double ForcaConfidence { get; set; }
        public double ConfiancaOperavel { get; set; }
        public bool ShouldTrade { get; set; }
        /// <summary>
        /// Resposta ao quesito encerrar_posicao (noul 0..1): a IA recomenda
        /// encerrar a posição atual. Só faz sentido com posição aberta.
        /// </summary>
        public double EncerrarPosicao { get; set; }
        public double Threshold { get; set; }
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
        public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
        public string StatePreview { get; set; } = string.Empty;
    }

    /// <summary>
    /// Regra confidence-gated routing: só sinaliza trade quando a Jev escolhe
    /// comprar/vender com confiança acima do threshold E o contexto é
    /// operável. Função pura para teste unitário.
    /// </summary>
    public static class JevDecision
    {
        public static bool ApplyGate(string direcao, double direcaoConfidence, double operavel, double threshold)
        {
            if (string.IsNullOrWhiteSpace(direcao))
                return false;
            var d = direcao.Trim().ToLowerInvariant();
            if (d != "comprar" && d != "vender")
                return false;
            if (direcaoConfidence < threshold)
                return false;
            if (operavel <= 0.5)
                return false;
            return true;
        }
    }
}
