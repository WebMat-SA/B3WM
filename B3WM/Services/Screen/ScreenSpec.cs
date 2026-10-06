using System.Text.Json;

namespace B3WM.Services.Screen
{
    /// <summary>
    /// Leitura do blob cru da tela (SymbolConfig.toJson do Flutter, com
    /// bloco `daily`). Sem espelho tipado: campo novo no app chega sozinho
    /// no JSON; o teste de contrato (`ScreenSpecKeysTest`) garante que toda
    /// chave do Dart está classificada em `Handled` (lida em algum lugar)
    /// ou `Cosmetic` (só estética, ignorada de propósito).
    /// </summary>
    public static class ScreenSpec
    {
        public static int Int(JsonElement el, string name, int fallback) =>
            el.ValueKind == JsonValueKind.Object &&
            el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number
                ? p.GetInt32() : fallback;

        public static double Num(JsonElement el, string name, double fallback) =>
            el.ValueKind == JsonValueKind.Object &&
            el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number
                ? p.GetDouble() : fallback;

        public static bool Bool(JsonElement el, string name, bool fallback) =>
            el.ValueKind == JsonValueKind.Object &&
            el.TryGetProperty(name, out var p) &&
            (p.ValueKind == JsonValueKind.True || p.ValueKind == JsonValueKind.False)
                ? p.GetBoolean() : fallback;

        public static string Str(JsonElement el, string name, string fallback) =>
            el.ValueKind == JsonValueKind.Object &&
            el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String
                ? p.GetString() ?? fallback : fallback;

        public static Dictionary<int, int> IntMap(JsonElement el, string name)
        {
            var out_ = new Dictionary<int, int>();
            if (el.ValueKind == JsonValueKind.Object &&
                el.TryGetProperty(name, out var m) && m.ValueKind == JsonValueKind.Object)
                foreach (var kv in m.EnumerateObject())
                    if (int.TryParse(kv.Name, out var k) && kv.Value.ValueKind == JsonValueKind.Number)
                        out_[k] = kv.Value.GetInt32();
            return out_;
        }

        public static HashSet<int> IntSet(JsonElement el, string name)
        {
            var out_ = new HashSet<int>();
            if (el.ValueKind == JsonValueKind.Object &&
                el.TryGetProperty(name, out var a) && a.ValueKind == JsonValueKind.Array)
                foreach (var v in a.EnumerateArray())
                    if (v.ValueKind == JsonValueKind.Number)
                        out_.Add(v.GetInt32());
            return out_;
        }

        public static JsonElement Daily(JsonElement el) =>
            el.ValueKind == JsonValueKind.Object &&
            el.TryGetProperty("daily", out var d) && d.ValueKind == JsonValueKind.Object
                ? d : default;

        public static bool IsObject(JsonElement? el) =>
            el is { ValueKind: JsonValueKind.Object };
    }

    /// <summary>
    /// Classificação das chaves do `SymbolConfig.toJson()` (+ bloco `daily`).
    /// Filtro/camada nova no Flutter: adiciona a chave em Handled (e o `Spec*`
    /// onde ela é lida) ou Cosmetic — o teste de contrato cobra.
    /// </summary>
    public static class ScreenSpecKeys
    {
        public static readonly HashSet<string> Handled = new()
        {
            "timeFrame", "dateRangeMode",
            "bubbleVisible", "thresholdBubble", "agentThresholds", "selectedAgents",
            "bubbleAmountFilter", "bubbleAgentsFilter",
            "profileVisible",
            "structureVisible", "structureAuxVisible", "structureRangeUpd",
            "extremeVisible", "extremeNoiseSensitivity", "extremeMinimumProminence",
            "pivotVisible", "pivotLineCount",
            "vwapVisible",
            "tradingHistoryVisible", "positionVisible", "openOrdersVisible", "tradingPanelVisible",
            "daily",
            // Bloco daily (mesmas chaves, semântica própria):
            "profileAutoByPriceStructure", // janela já chega resolvida (DailyFrom/To); flag só informa
            "panelVisible",
        };

        public static readonly HashSet<string> Cosmetic = new()
        {
            "bubbleSize", "bubbleOpacity", "bubbleSizeMin", "bubbleSizeMax",
            "profileSizeH", "profileSizeV", "profileOpacity",
            "structureOpacity", "extremeOpacity", "pivotOpacity",
            "vwapOpacity", "vwapColor", "colorBuyer", "colorSeller",
            "knownAgents", "bubbleSoundEnabled", "bubbleSoundVolume",
            "tradingConfigExpanded", "tradingAccountExpanded", "tradingOrdersExpanded",
            "tradingPositionsExpanded", "tradingHistoryExpanded",
            "panelFraction",
            "lookbackDays", // multi-day é fase 2; hoje o backend usa janela explícita
        };
    }
}
