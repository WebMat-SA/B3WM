namespace B3WM.Services.AI
{
    /// <summary>Resultado da agregação informativa (sem gate de operar/não-operar).</summary>
    public sealed class JevScoringResult
    {
        /// <summary>Viés: comprar | vender | manter (pelo sinal do net ponderado).</summary>
        public string Direcao { get; set; } = "manter";

        /// <summary>Confiança do viés = |net| (0..1) — só informativa.</summary>
        public double DirecaoConfidence { get; set; }

        /// <summary>Confiança de operabilidade = média ponderada (0..1).</summary>
        public double ConfiancaOperavel { get; set; }

        /// <summary>Confiança por indicador (todas as respostas 0..1).</summary>
        public Dictionary<string, double> Criteria { get; set; } = new();
    }

    /// <summary>
    /// Composite scoring (TypeSafe): combina as respostas `noul` dos
    /// indicadores com pesos. Puro, sem I/O — testável. Sem thresholds e sem
    /// decisão de operar: só viés + confianças informativas.
    /// </summary>
    public static class JevScoring
    {
        /// <summary>
        /// Agrega os indicadores (id → p, 0..1). Direção: cada indicador é uma
        /// afirmação de alta, contribuição = peso·(2p−1); o net normalizado dá
        /// o viés e a confiança. Operabilidade: média ponderada de p.
        /// </summary>
        public static JevScoringResult Evaluate(
            IReadOnlyDictionary<string, double> indicators, JevWeights weights)
        {
            double net = 0, dirW = 0;
            foreach (var (key, weight) in weights.Direction)
            {
                if (weight <= 0 || !indicators.TryGetValue(key, out var p)) continue;
                net += weight * (2 * p - 1);
                dirW += weight;
            }
            var netNorm = dirW > 0 ? net / dirW : 0;
            var bias = dirW <= 0 || Math.Abs(netNorm) < 1e-9
                ? "manter"
                : (netNorm > 0 ? "comprar" : "vender");

            double op = 0, opW = 0;
            foreach (var (key, weight) in weights.Operability)
            {
                if (weight <= 0 || !indicators.TryGetValue(key, out var p)) continue;
                op += weight * p;
                opW += weight;
            }
            var operability = opW > 0 ? op / opW : 0;

            return new JevScoringResult
            {
                Direcao = bias,
                DirecaoConfidence = Math.Clamp(Math.Abs(netNorm), 0, 1),
                ConfiancaOperavel = Math.Clamp(operability, 0, 1),
                Criteria = new Dictionary<string, double>(indicators),
            };
        }

        /// <summary>
        /// Proximidade técnica do preço à referência mais próxima (peso > 1 =
        /// referência de maior importância, ex.: pivô diário). Devolve a
        /// proximidade normalizada (1 = colado, 0 = longe), a distância crua em
        /// ticks e o nome da referência mais próxima.
        /// </summary>
        public static (double Proximidade, double Ticks, string Reference) Proximity(
            double lastPrice,
            IEnumerable<(string Name, double Value, double Weight)> references,
            double tickSize,
            double maxTicks)
        {
            if (tickSize <= 0 || maxTicks <= 0)
                return (0, double.MaxValue, "");

            var bestWeighted = double.MaxValue;
            var bestTicks = double.MaxValue;
            var bestRef = "";
            foreach (var (name, value, weight) in references)
            {
                if (weight <= 0 || double.IsNaN(value) || double.IsInfinity(value))
                    continue;
                var ticks = Math.Abs(lastPrice - value) / tickSize;
                var weighted = ticks / weight;
                if (weighted < bestWeighted)
                {
                    bestWeighted = weighted;
                    bestTicks = ticks;
                    bestRef = name;
                }
            }
            if (bestRef.Length == 0)
                return (0, double.MaxValue, "");

            var prox = Math.Clamp(1 - bestWeighted / maxTicks, 0, 1);
            return (prox, bestTicks, bestRef);
        }
    }
}
