using System.Text.Json.Serialization;
using B3WM.Services.Core;

namespace B3WM.Services.AI
{
    /// <summary>
    /// Pesos dos indicadores (criteria) usados na agregação informativa da Jev
    /// (Composite scoring): cada indicador `noul` vira uma contribuição
    /// ponderada. Os defaults vivem aqui; o fitter offline (fase B) pode
    /// sobrescrever via <c>Data/jev_weights.json</c>. Sem thresholds de
    /// operar/não-operar — o resultado é só informativo.
    /// </summary>
    public sealed class JevWeights
    {
        /// <summary>Nome do arquivo opcional (relativo à raiz de dados).</summary>
        public const string FileName = "jev_weights.json";

        /// <summary>Dimensões de direção (pares alta/baixa) e seus pesos.</summary>
        [JsonPropertyName("direction")]
        public Dictionary<string, double> Direction { get; set; } = new();

        /// <summary>Indicadores de operabilidade e seus pesos.</summary>
        [JsonPropertyName("operability")]
        public Dictionary<string, double> Operability { get; set; } = new();

        /// <summary>Banda neutra: |net| abaixo disto vira lateral.</summary>
        [JsonPropertyName("neutral_band")]
        public double NeutralBand { get; set; } = 0.25;

        /// <summary>Cobertura mínima das dimensões para sair da lateral.</summary>
        [JsonPropertyName("min_direction_coverage")]
        public double MinDirectionCoverage { get; set; } = 0.75;

        /// <summary>Consenso mínimo entre as dimensões para sair da lateral.</summary>
        [JsonPropertyName("min_consensus")]
        public double MinConsensus { get; set; } = 0.50;

        /// <summary>Horizonte (candles) para o rótulo do fitter.</summary>
        [JsonPropertyName("label_horizon")]
        public int LabelHorizon { get; set; } = 10;

        /// <summary>Deslocamento mínimo (ticks) para o rótulo do fitter.</summary>
        [JsonPropertyName("label_ticks")]
        public double LabelTicks { get; set; } = 10;

        /// <summary>Distância (ticks) considerada "longe" na normalização da proximidade.</summary>
        [JsonPropertyName("proximity_max_ticks")]
        public double ProximityMaxTicks { get; set; } = 100;

        /// <summary>Pesos default (usados quando não há arquivo válido).</summary>
        public static JevWeights Defaults() => new()
        {
            Direction = new Dictionary<string, double>
            {
                ["candles_alta"] = 1.0,
                ["acima_vwap"] = 1.0,
                ["estrutura_alta"] = 1.0,
                ["acima_poc"] = 0.75,
                ["delta_positivo"] = 0.75,
                ["bolhas_compra"] = 0.75,
                ["defende_suporte"] = 1.0,
                ["reversao_suporte"] = 2.0,
                ["exaustao_queda"] = 1.0,
                ["diario_alta"] = 1.25,
            },
            Operability = new Dictionary<string, double>
            {
                ["sem_chop"] = 1.0,
                ["amplitude_suficiente"] = 1.0,
                ["sem_compressao_topovale"] = 0.75,
                ["referencia_clara"] = 0.75,
                ["sem_conflito_intra_diario"] = 1.0,
            },
            NeutralBand = 0.25,
            MinDirectionCoverage = 0.75,
            MinConsensus = 0.50,
        };

        /// <summary>
        /// Lê <c>Data/jev_weights.json</c>; ausente/inválido/vazio cai nos
        /// defaults (campo a campo). Nunca lança.
        /// </summary>
        public static async Task<JevWeights> LoadAsync(DataKeeperBase keeper)
        {
            var defaults = Defaults();
            try
            {
                var loaded = await keeper.ReadDataAsync<JevWeights>(FileName);
                if (loaded == null)
                    return defaults;
                loaded.Direction = MergeDefaults(loaded.Direction, defaults.Direction);
                loaded.Operability = MergeDefaults(loaded.Operability, defaults.Operability);
                if (loaded.NeutralBand <= 0 || loaded.NeutralBand >= 1)
                    loaded.NeutralBand = defaults.NeutralBand;
                if (loaded.MinDirectionCoverage <= 0 || loaded.MinDirectionCoverage > 1)
                    loaded.MinDirectionCoverage = defaults.MinDirectionCoverage;
                if (loaded.MinConsensus <= 0 || loaded.MinConsensus > 1)
                    loaded.MinConsensus = defaults.MinConsensus;
                if (loaded.LabelHorizon <= 0)
                    loaded.LabelHorizon = defaults.LabelHorizon;
                if (loaded.LabelTicks <= 0)
                    loaded.LabelTicks = defaults.LabelTicks;
                if (loaded.ProximityMaxTicks <= 0)
                    loaded.ProximityMaxTicks = defaults.ProximityMaxTicks;
                return loaded;
            }
            catch
            {
                return defaults;
            }
        }

        /// <summary>
        /// Mantém chaves já calibradas e adiciona defaults para dimensões
        /// novas (compatibilidade com Data/jev_weights.json antigo).
        /// </summary>
        public static Dictionary<string, double> MergeDefaults(
            Dictionary<string, double> loaded, Dictionary<string, double> defaults)
        {
            foreach (var (key, value) in defaults)
                loaded.TryAdd(key, value);
            return loaded;
        }
    }
}
