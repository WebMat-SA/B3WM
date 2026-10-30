namespace B3WM.Services.AI
{
    /// <summary>
    /// Dimensão de direção com duas perguntas espelhadas (alta e baixa).
    /// O contraste entre elas evita que o viés de concordância do modelo
    /// transforme "baixa confiança na alta" em sinal de venda.
    /// </summary>
    public sealed record JevDirectionDimension(string Id, string BullId, string BearId);

    /// <summary>Resultado da agregação informativa (sem gate de operar/não-operar).</summary>
    public sealed class JevScoringResult
    {
        /// <summary>Viés: comprar | vender | lateral.</summary>
        public string Direcao { get; set; } = "lateral";

        /// <summary>Confiança do viés (0..1) — já penalizada por conflito/cobertura.</summary>
        public double DirecaoConfidence { get; set; }

        /// <summary>Net assinado (-1..1): positivo = alta, negativo = baixa.</summary>
        public double DirecaoNet { get; set; }

        /// <summary>Consenso (0..1): 1 = dimensões concordam; 0 = conflito total.</summary>
        public double DirecaoConsenso { get; set; }

        /// <summary>Cobertura das dimensões (0..1): fração dos pesos com os dois lados respondidos.</summary>
        public double DirecaoCobertura { get; set; }

        /// <summary>Motivo da lateral (vazio quando há compra/venda).</summary>
        public string DirecaoMotivo { get; set; } = "";

        /// <summary>Confiança de operabilidade = média ponderada (0..1).</summary>
        public double ConfiancaOperavel { get; set; }

        /// <summary>Confiança por indicador (todas as respostas 0..1).</summary>
        public Dictionary<string, double> Criteria { get; set; } = new();

        /// <summary>Contribuição assinada por dimensão (peso × (alta − baixa)).</summary>
        public Dictionary<string, double> Contributions { get; set; } = new();
    }

    /// <summary>
    /// Composite scoring (TypeSafe): combina as respostas `noul` dos
    /// indicadores com pesos. Puro, sem I/O — testável. Sem decisão de operar:
    /// produz viés cético (comprar/vender/lateral) + confianças informativas.
    /// </summary>
    public static class JevScoring
    {
        /// <summary>
        /// Agrega os indicadores (id → p, 0..1). Direção: cada dimensão tem
        /// uma pergunta de alta e uma de baixa; a contribuição é
        /// peso·(p_alta − p_baixa). O net normalizado só vira compra/venda se
        /// superar a banda neutra, houver cobertura suficiente e consenso
        /// mínimo; caso contrário, lateral. Operabilidade: média ponderada.
        /// </summary>
        public static JevScoringResult Evaluate(
            IReadOnlyDictionary<string, double> indicators,
            JevWeights weights,
            IReadOnlyList<JevDirectionDimension>? dimensions = null)
        {
            dimensions ??= JevPrompt.DirectionDimensions;

            double netSum = 0, absSum = 0, coveredW = 0, totalW = 0;
            var contributions = new Dictionary<string, double>();
            foreach (var dim in dimensions)
            {
                if (!weights.Direction.TryGetValue(dim.Id, out var weight) || weight <= 0)
                    continue;
                totalW += weight;

                var hasBull = indicators.TryGetValue(dim.BullId, out var bull);
                var hasBear = indicators.TryGetValue(dim.BearId, out var bear);
                if (!hasBull || !hasBear)
                    continue;

                coveredW += weight;
                var d = Math.Clamp(bull, 0, 1) - Math.Clamp(bear, 0, 1);
                contributions[dim.Id] = weight * d;
                netSum += weight * d;
                absSum += weight * Math.Abs(d);
            }

            var net = coveredW > 0 ? netSum / coveredW : 0;
            var coverage = totalW > 0 ? coveredW / totalW : 0;
            var consensus = absSum > 0 ? Math.Abs(netSum) / absSum : 0;
            var band = Math.Clamp(weights.NeutralBand, 0, 0.99);

            var lateral = coveredW <= 0
                || coverage < weights.MinDirectionCoverage
                || consensus < weights.MinConsensus
                || Math.Abs(net) < band;

            var bias = lateral
                ? "lateral"
                : (net > 0 ? "comprar" : "vender");

            var motive = "";
            if (lateral)
            {
                if (coveredW <= 0)
                {
                    motive = "sem dados";
                }
                else
                {
                    var parts = new List<string>();
                    if (coverage < weights.MinDirectionCoverage)
                        parts.Add("cobertura baixa");
                    if (absSum > 0 && consensus < weights.MinConsensus)
                        parts.Add("conflito entre dimensões");
                    if (Math.Abs(net) < band)
                        parts.Add("evidência fraca");
                    motive = string.Join(" · ", parts);
                }
            }

            var confidence = lateral
                ? 0
                : Math.Clamp(
                    ((Math.Abs(net) - band) / (1 - band))
                    * consensus * coverage,
                    0, 1);

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
                DirecaoConfidence = confidence,
                DirecaoNet = Math.Clamp(net, -1, 1),
                DirecaoConsenso = Math.Clamp(consensus, 0, 1),
                DirecaoCobertura = Math.Clamp(coverage, 0, 1),
                DirecaoMotivo = motive,
                ConfiancaOperavel = Math.Clamp(operability, 0, 1),
                Criteria = new Dictionary<string, double>(indicators),
                Contributions = contributions,
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
