using System.Text.Json;

namespace B3WM.Shared.Models.AI
{
    /// <summary>
    /// Auditoria de UMA chamada à Jev (issue #23): o que chegou, o que foi
    /// filtrado, a pergunta montada, a chamada feita e a resposta.
    /// Um arquivo por chamada em <c>Data/JEV/{SYM}_Jev_{data}_{hora}_{id}.json</c>.
    /// Puro (serializável via System.Text.Json); sem chave de API nunca.
    /// </summary>
    public sealed class JevAuditRecord
    {
        public string Id { get; set; } = "";
        /// <summary>Quando a auditoria foi gravada (ordenação do arquivo).</summary>
        public DateTime At { get; set; }
        /// <summary>Hora do gatilho (ev.At): bubble/candle que originou a chamada.</summary>
        public DateTime TriggerAt { get; set; }
        public string TriggerType { get; set; } = "";
        public string Symbol { get; set; } = "";
        public int TimeFrame { get; set; }
        public DateTime DisplayDate { get; set; }
        /// <summary>strategy | debug (endpoint avulso).</summary>
        public string Origin { get; set; } = "";
        public string Model { get; set; } = "";
        public string SnapshotHash { get; set; } = "";
        public double Threshold { get; set; }

        public JevAuditInput Input { get; set; } = new();
        public JevAuditFiltered Filtered { get; set; } = new();
        /// <summary>Pergunta montada: state completo (sem corte de 6000).</summary>
        public string QuestionState { get; set; } = "";
        /// <summary>Questions serializadas (BuildQuestions).</summary>
        public string QuestionQuestionsJson { get; set; } = "{}";
        public JevAuditCall Call { get; set; } = new();
        public JevAuditAnswer Answer { get; set; } = new();
    }

    /// <summary>
    /// O que CHEGOU (antes/depois do filtro de tela). Contagens para visão
    /// rápida + os JSONs REAIS de cada entrada (dados crus que o pipeline
    /// trata/filtra/ordena antes de montar o state). Tudo inline no arquivo.
    /// </summary>
    public sealed class JevAuditInput
    {
        // Contagens (resumo).
        public int CandlesReceived { get; set; }
        public int BubblesPoolTotal { get; set; }
        public int BubblesAfterScreenFilter { get; set; }
        public int ThresholdBubble { get; set; }
        public int LookbackCandles { get; set; }
        public int LookbackBubbles { get; set; }
        public List<string> LayersOn { get; set; } = new();

        // Dados reais (JSON completo de cada entrada).
        /// <summary>Spec cru da tela (SymbolConfig) que comanda os filtros.</summary>
        public JsonElement? ScreenSpec { get; set; }
        /// <summary>Cabeçalho/enquadramento da tela.</summary>
        public JsonElement? Header { get; set; }
        /// <summary>Candles que chegaram (OHLCV, últimos N).</summary>
        public JsonElement? Candles { get; set; }
        /// <summary>Pool completo de bolhas do dia, ANTES do filtro de tela.</summary>
        public JsonElement? BubblesPool { get; set; }
        /// <summary>Bolhas após o filtro de tela (antes do corte de lookback).</summary>
        public JsonElement? BubblesFiltered { get; set; }
        /// <summary>Bolhas efetivamente enviadas no state (após lookback).</summary>
        public JsonElement? BubblesSent { get; set; }
        /// <summary>Volume profile completo (todos os níveis).</summary>
        public JsonElement? Volume { get; set; }
        public JsonElement? ExtremesIntra { get; set; }
        public JsonElement? ExtremesDaily { get; set; }
        public JsonElement? Structures { get; set; }
        public JsonElement? PivotIntra { get; set; }
        public JsonElement? PivotDaily { get; set; }
        /// <summary>VWAP do dia de referência.</summary>
        public double? Vwap { get; set; }
        /// <summary>Posição aberta (null = flat).</summary>
        public JsonElement? Position { get; set; }
    }

    /// <summary>O que foi EFETIVAMENTE ENVIADO no state.</summary>
    public sealed class JevAuditFiltered
    {
        public int CandlesSent { get; set; }
        public int BubblesSent { get; set; }
        public int VolumeLevels { get; set; }
        public int ExtremesIntraCount { get; set; }
        public int ExtremesDailyCount { get; set; }
        public int StructuresCount { get; set; }
        public int PivotIntraLevels { get; set; }
        public int PivotDailyLevels { get; set; }
        public bool HasVwap { get; set; }
        public int StateChars { get; set; }
        public Dictionary<string, int> StateBlocks { get; set; } = new();
    }

    /// <summary>A CHAMADA feita (transporte).</summary>
    public sealed class JevAuditCall
    {
        public int HttpStatus { get; set; }
        public long LatencyMs { get; set; }
        public int Attempts { get; set; }
    }

    /// <summary>A RESPOSTA da Jev (crua + interpretada).</summary>
    public sealed class JevAuditAnswer
    {
        public string BodyRaw { get; set; } = "";
        public string Direcao { get; set; } = "manter";
        public double DirecaoConfidence { get; set; }
        public Dictionary<string, double> DirecaoProbabilities { get; set; } = new();
        public double ForcaTendencia { get; set; }
        public double ForcaConfidence { get; set; }
        public double ConfiancaOperavel { get; set; }
        public double EncerrarPosicao { get; set; }
        public bool ShouldTrade { get; set; }
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
    }

    public static class JevAuditFiles
    {
        public const string Dir = "JEV";

        public static string BuildFileName(string symbol, DateTime at, string id)
        {
            var sym = string.IsNullOrWhiteSpace(symbol) ? "UNKNOWN" : symbol.ToUpperInvariant();
            var safeId = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N")[..8] : id;
            return $"{sym}_Jev_{at:yyyy-MM-dd_HH-mm-ss-fff}_{safeId}.json";
        }

        public static string BuildPath(string fileName) => $"{Dir}/{fileName}";

        public static string NewId() => Guid.NewGuid().ToString("N")[..8];
    }
}
