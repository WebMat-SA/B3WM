using System.Text.Json.Serialization;

namespace B3WM.Services.AI
{
    /// <summary>
    /// Contrato do `state` enviado à Jev (TypeSafe System One) — JSON OBJETO,
    /// não string. Dois blocos (intraday primeiro, diário depois), cada um
    /// recortado ao seu escopo [from,to] resolvido pelo backend. Sem configs de
    /// tela e sem bloco de posição: o que importa é o mercado.
    /// Nomes das chaves travados em inglês (snake_case) de propósito — é o
    /// contrato consumido pelo modelo, não o domínio do app.
    /// </summary>
    public sealed class JevStateDto
    {
        [JsonPropertyName("symbol")] public string Symbol { get; set; } = "";
        [JsonPropertyName("strategy_timeframe")] public int StrategyTimeframe { get; set; }
        [JsonPropertyName("intraday")] public JevIntradayDto Intraday { get; set; } = new();
        [JsonPropertyName("daily")] public JevDailyDto Daily { get; set; } = new();
    }

    /// <summary>Escopo [from,to] do bloco (datas canônicas iguais às barras).</summary>
    public sealed class JevRangeDto
    {
        [JsonPropertyName("from")] public DateTime From { get; set; }
        [JsonPropertyName("to")] public DateTime To { get; set; }
    }

    public sealed class JevIntradayDto
    {
        [JsonPropertyName("scope")] public JevRangeDto Scope { get; set; } = new();
        [JsonPropertyName("candles")] public List<JevCandleDto> Candles { get; set; } = new();
        [JsonPropertyName("volume_profile")] public JevVolumeProfileDto? VolumeProfile { get; set; }
        [JsonPropertyName("bubbles")] public List<JevBubbleDto> Bubbles { get; set; } = new();
        [JsonPropertyName("pivots")] public List<JevPivotDto> Pivots { get; set; } = new();
        [JsonPropertyName("tops_valleys")] public List<JevExtremeDto> TopsValleys { get; set; } = new();
        [JsonPropertyName("structures")] public List<JevStructureDto> Structures { get; set; } = new();
        /// <summary>Pivô mais próximo do último preço (não altera ProximidadeRef).</summary>
        [JsonPropertyName("nearest_pivot")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public JevNearestPivotDto? NearestPivot { get; set; }
        /// <summary>Níveis próximos/tocados com resumo de bolhas ao redor.</summary>
        [JsonPropertyName("level_context")] public List<JevLevelContextDto> LevelContext { get; set; } = new();
    }

    /// <summary>Bloco diário: sem bolhas e sem VWAP (o gráfico 1D não os exibe).</summary>
    public sealed class JevDailyDto
    {
        [JsonPropertyName("scope")] public JevRangeDto Scope { get; set; } = new();
        [JsonPropertyName("candles")] public List<JevCandleDto> Candles { get; set; } = new();
        [JsonPropertyName("volume_profile")] public JevVolumeProfileDto? VolumeProfile { get; set; }
        [JsonPropertyName("pivots")] public List<JevPivotDto> Pivots { get; set; } = new();
        [JsonPropertyName("tops_valleys")] public List<JevExtremeDto> TopsValleys { get; set; } = new();
        [JsonPropertyName("structures")] public List<JevStructureDto> Structures { get; set; } = new();
    }

    public sealed class JevCandleDto
    {
        [JsonPropertyName("date")] public DateTime Date { get; set; }
        [JsonPropertyName("open")] public double Open { get; set; }
        [JsonPropertyName("high")] public double High { get; set; }
        [JsonPropertyName("low")] public double Low { get; set; }
        [JsonPropertyName("close")] public double Close { get; set; }
        [JsonPropertyName("volume")] public long Volume { get; set; }
        [JsonPropertyName("vwap")] public double? Vwap { get; set; }
    }

    public sealed class JevVolumeProfileDto
    {
        [JsonPropertyName("poc")] public double Poc { get; set; }
        [JsonPropertyName("delta_total")] public long DeltaTotal { get; set; }
        [JsonPropertyName("levels")] public List<JevVolumeLevelDto> Levels { get; set; } = new();
    }

    public sealed class JevVolumeLevelDto
    {
        [JsonPropertyName("price")] public double Price { get; set; }
        [JsonPropertyName("total")] public long Total { get; set; }
        [JsonPropertyName("buy")] public long Buy { get; set; }
        [JsonPropertyName("sell")] public long Sell { get; set; }
        [JsonPropertyName("delta")] public long Delta { get; set; }
    }

    public sealed class JevBubbleDto
    {
        [JsonPropertyName("date")] public DateTime Date { get; set; }
        /// <summary>Nome da corretora/agente (não o código numérico).</summary>
        [JsonPropertyName("agent")] public string Agent { get; set; } = "";
        /// <summary>buy | sell</summary>
        [JsonPropertyName("side")] public string Side { get; set; } = "";
        [JsonPropertyName("amount")] public decimal Amount { get; set; }
        [JsonPropertyName("price")] public double Price { get; set; }
        /// <summary>Bolha pertence ao candle que disparou a avaliação.</summary>
        [JsonPropertyName("is_trigger")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool IsTrigger { get; set; }
    }

    public sealed class JevNearestPivotDto
    {
        [JsonPropertyName("source")] public string Source { get; set; } = "";
        [JsonPropertyName("level")] public string Level { get; set; } = "";
        [JsonPropertyName("value")] public double Value { get; set; }
        [JsonPropertyName("distance_ticks")] public double DistanceTicks { get; set; }
        /// <summary>support | resistance | neutral</summary>
        [JsonPropertyName("side")] public string Side { get; set; } = "";
        [JsonPropertyName("touched_at")] public DateTime? TouchedAt { get; set; }
        [JsonPropertyName("reclaimed")] public bool Reclaimed { get; set; }
    }

    public sealed class JevBubbleSummaryDto
    {
        [JsonPropertyName("count")] public int Count { get; set; }
        [JsonPropertyName("buy_count")] public int BuyCount { get; set; }
        [JsonPropertyName("sell_count")] public int SellCount { get; set; }
        [JsonPropertyName("buy_amount")] public decimal BuyAmount { get; set; }
        [JsonPropertyName("sell_amount")] public decimal SellAmount { get; set; }
        [JsonPropertyName("net_amount")] public decimal NetAmount { get; set; }
    }

    public sealed class JevLevelContextDto
    {
        /// <summary>pivot | vale | topo</summary>
        [JsonPropertyName("kind")] public string Kind { get; set; } = "";
        [JsonPropertyName("source")] public string Source { get; set; } = "";
        [JsonPropertyName("level")] public string Level { get; set; } = "";
        [JsonPropertyName("value")] public double Value { get; set; }
        /// <summary>support | resistance | neutral</summary>
        [JsonPropertyName("side")] public string Side { get; set; } = "";
        [JsonPropertyName("distance_ticks")] public double DistanceTicks { get; set; }
        [JsonPropertyName("touched_at")] public DateTime? TouchedAt { get; set; }
        [JsonPropertyName("reclaimed")] public bool Reclaimed { get; set; }
        [JsonPropertyName("bubbles")] public JevBubbleSummaryDto Bubbles { get; set; } = new();
    }

    public sealed class JevPivotDto
    {
        [JsonPropertyName("source")] public string Source { get; set; } = "";
        [JsonPropertyName("level")] public string Level { get; set; } = "";
        [JsonPropertyName("value")] public double Value { get; set; }
        /// <summary>Data do 1º candle do escopo que tocou o nível (low ≤ value ≤ high); null se nunca tocou.</summary>
        [JsonPropertyName("touched_at")] public DateTime? TouchedAt { get; set; }
    }

    public sealed class JevExtremeDto
    {
        /// <summary>topo | vale</summary>
        [JsonPropertyName("type")] public string Type { get; set; } = "";
        [JsonPropertyName("price")] public double Price { get; set; }
    }

    public sealed class JevStructureDto
    {
        [JsonPropertyName("date")] public DateTime Date { get; set; }
        /// <summary>Só no bloco intraday; omitido no diário (sempre 1440).</summary>
        [JsonPropertyName("timeframe")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? Timeframe { get; set; }
        [JsonPropertyName("up")] public double Up { get; set; }
        [JsonPropertyName("down")] public double Down { get; set; }
        [JsonPropertyName("up_aux")] public double UpAux { get; set; }
        [JsonPropertyName("down_aux")] public double DownAux { get; set; }
    }
}
