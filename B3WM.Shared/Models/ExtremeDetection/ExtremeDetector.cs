namespace B3WM.Shared.Models.ExtremeDetection
{
    /// <summary>
    /// Detector de extremos estruturais (topos e vales significativos) para
    /// séries oscilatórias, não estacionárias e ruidosas.
    ///
    /// Abordagem adaptativa (scale-space + persistência):
    /// 1. Estimativa de ruído local (desvio robusto dos resíduos);
    /// 2. Pirâmide Gaussiana multi-escala (σ crescente);
    /// 3. Rastreamento da persistência de cada extremo entre escalas
    ///    (estruturas que sobrevivem a várias escalas são significativas);
    /// 4. Representante na série original (máximo/mínimo real na vizinhança);
    /// 5. Validação da estrutura em forma de sino (subida → topo → descida);
    /// 6. Prominência relativa à linha de base local e ao ruído;
    /// 7. Fusão de candidatos próximos da mesma estrutura + alternância;
    /// 8. Regras de borda (extremos sem evidência → Indeterminate).
    /// </summary>
    public static class ExtremeDetector
    {
        /// <summary>
        /// Detecta topos e vales estruturais significativos da série.
        /// </summary>
        /// <param name="x">Eixo X (posições, ex.: Price). Precisa ter o mesmo tamanho de <paramref name="y"/>.</param>
        /// <param name="y">Eixo Y (valores, ex.: Total).</param>
        /// <param name="options">Parâmetros (valores nulos são derivados automaticamente).</param>
        public static ExtremeDetectionResult Detect(
            IReadOnlyList<double> x,
            IReadOnlyList<double> y,
            ExtremeDetectorOptions? options = null)
        {
            var result = new ExtremeDetectionResult();
            if (x == null || y == null || x.Count == 0 || x.Count != y.Count)
                return result;

            var opts = options?.Clone() ?? new ExtremeDetectorOptions();
            int n = y.Count;

            if (n < 5)
            {
                result.Statistics = BuildStatistics(x, y, opts, 1.5, 1.5);
                return result;
            }

            double spacing = EstimateSpacing(x);
            double baseScale = opts.SmoothingLevel ?? 1.5;
            double maxScale = opts.MaxScale ?? Math.Max(4.0, n / 8.0);
            maxScale = Math.Max(maxScale, baseScale * 1.5);
            double minDistance = opts.MinimumDistance ?? Math.Max(2.0, 2.0 * baseScale);
            double edgeMargin = opts.EdgeMargin ?? Math.Max(2.0, baseScale * 1.0);

            // 1. Ruído local
            double noiseStd = EstimateNoiseStd(y);

            // 2. Pirâmide multi-escala
            var scales = BuildScales(baseScale, maxScale, n);
            var smoothedPerScale = new List<double[]>(scales.Length);
            var extremaPerScale = new List<List<Extremum>>(scales.Length);
            for (int k = 0; k < scales.Length; k++)
            {
                var smoothed = GaussianSmooth(y, scales[k]);
                smoothedPerScale.Add(smoothed);
                extremaPerScale.Add(FindExtrema(smoothed));
            }

            // 3. Rastreamento de persistência (nascidos na escala mais fina)
            var tracks = new List<Track>();
            foreach (var ext in extremaPerScale[0])
            {
                tracks.Add(new Track
                {
                    Type = ext.IsPeak ? ExtremeType.Top : ExtremeType.Valley,
                    Index = ext.Index,
                    BirthIndex = ext.Index,
                    DeathScaleIndex = 0
                });
            }

            for (int k = 0; k < scales.Length - 1; k++)
            {
                var next = extremaPerScale[k + 1];
                var used = new bool[next.Count];
                double radius = 3.0 * scales[k];
                foreach (var track in tracks)
                {
                    if (track.DeathScaleIndex < k) continue; // já morreu
                    int best = -1;
                    double bestDist = double.MaxValue;
                    for (int j = 0; j < next.Count; j++)
                    {
                        if (used[j]) continue;
                        if (next[j].IsPeak != (track.Type == ExtremeType.Top)) continue;
                        double d = Math.Abs(next[j].Index - track.Index);
                        if (d <= radius && d < bestDist)
                        {
                            bestDist = d;
                            best = j;
                        }
                    }
                    if (best >= 0)
                    {
                        track.Index = next[best].Index;
                        track.DeathScaleIndex = k + 1;
                        used[best] = true;
                    }
                }
            }

            // 4–6. Representante, sino, prominência e filtros para cada track
            var candidates = new List<ExtremePoint>();
            var structures = new List<BellStructure>();

            foreach (var track in tracks)
            {
                int deathK = track.DeathScaleIndex;
                double scale = scales[deathK];
                int p = track.Index;

                // Representante na série original: máximo/mínimo local real perto
                // da posição de nascimento (escala fina). Janela pequena para não
                // "absorver" estruturas vizinhas num extremo dominante.
                int birth = track.BirthIndex;
                int radiusRep = Math.Max(2, (int)Math.Ceiling(baseScale));
                int lo = Math.Max(0, birth - radiusRep);
                int hi = Math.Min(n - 1, birth + radiusRep);
                int rep = track.Type == ExtremeType.Top ? ArgMax(y, lo, hi) : ArgMin(y, lo, hi);

                // Persistência mínima: estruturas que somem no primeiro
                // suavizamento são ruído de escala fina, não estruturais.
                if (deathK < 1)
                    continue;

                // Pés da estrutura (a partir da curva suavizada na escala da estrutura)
                var smoothed = smoothedPerScale[deathK];
                var feet = FindFeet(smoothed, p, track.Type);
                int leftFoot = feet.Left;
                int rightFoot = feet.Right;

                // Coerência do sino (subida/descida)
                double riseCoherence = CoherenceNonDecreasing(smoothed, leftFoot, rep);
                double fallCoherence = CoherenceNonIncreasing(smoothed, rep, rightFoot);
                double coherence = 0.5 * (riseCoherence + fallCoherence);

                // Prominência absoluta (valores originais nos pés)
                double leftVal = MinInWindow(y, leftFoot, 1);
                double rightVal = MinInWindow(y, rightFoot, 1);
                double peakVal = y[rep];
                double prom = track.Type == ExtremeType.Top
                    ? Math.Max(0, peakVal - Math.Max(leftVal, rightVal))
                    : Math.Max(0, Math.Min(leftVal, rightVal) - peakVal);

                // Prominência relativa à linha de base local dos pés
                double floor = track.Type == ExtremeType.Top
                    ? Math.Max(leftVal, rightVal)
                    : Math.Min(leftVal, rightVal);
                double relative = Math.Abs(peakVal - floor) / (Math.Max(1.0, Math.Abs(floor)));

                // Filtros
                double widthIdx = rightFoot - leftFoot;
                bool promOk = prom >= opts.NoiseSensitivity * noiseStd;
                bool relativeOk = relative >= opts.MinimumProminence;
                if (!promOk || !relativeOk)
                    continue;

                // Largura mínima
                if (widthIdx < minDistance)
                    continue;

                // Borda: extremo na posição inicial/final da série não tem evidência
                // confirmável de um dos lados (nada antes do primeiro / depois do último
                // ponto). Estruturas interiores — mesmo que o pé alcance a borda — têm
                // subida e descida observadas dentro dos dados e são confirmadas.
                bool isEdge = rep <= 0 || rep >= n - 1;
                var type = isEdge ? ExtremeType.Indeterminate : track.Type;

                var ep = new ExtremePoint
                {
                    Position = XAt(x, rep, spacing),
                    Value = y[rep],
                    Type = type,
                    Prominence = prom,
                    Strength = relative,
                    Scale = scale * spacing,
                    Width = widthIdx * spacing,
                    StartPosition = XAt(x, leftFoot, spacing),
                    EndPosition = XAt(x, rightFoot, spacing),
                    IsEdge = isEdge
                };

                // Confiança
                double persistenceNorm = (scale - baseScale) / (maxScale - baseScale);
                double relPromNorm = Math.Min(1.0, relative / (2.0 * opts.MinimumProminence));
                double edgePenalty = isEdge ? 0.4 : 1.0;
                double confidence = (0.35 * Math.Clamp(persistenceNorm, 0, 1) +
                                     0.35 * relPromNorm +
                                     0.30 * Math.Clamp(coherence, 0, 1)) * edgePenalty;
                ep.Confidence = Math.Clamp(confidence, 0, 1);

                candidates.Add(ep);

                if (type == ExtremeType.Top)
                {
                    structures.Add(new BellStructure
                    {
                        StartPosition = XAt(x, leftFoot, spacing),
                        EndPosition = XAt(x, rightFoot, spacing),
                        ValleyPosition = XAt(x, leftFoot, spacing),
                        TopPosition = XAt(x, rep, spacing),
                        NextValleyPosition = XAt(x, rightFoot, spacing),
                        Amplitude = Math.Max(0, peakVal - Math.Min(leftVal, rightVal)),
                        Width = widthIdx * spacing,
                        Prominence = prom,
                        Confidence = ep.Confidence,
                        Scale = ep.Scale
                    });
                }
            }

            // 7. Fusão de estruturas da mesma estrutura + alternância
            candidates = MergeSameType(candidates);

            // Reordena e separa
            var ordered = candidates.OrderBy(e => e.Position).ToList();
            result.Extremes = ordered;
            result.Structures = structures;
            result.Statistics = BuildStatistics(x, y, opts, baseScale, maxScale);
            result.Statistics.NoiseStd = noiseStd;
            result.Statistics.TopCount = result.Tops.Count;
            result.Statistics.ValleyCount = result.Valleys.Count;
            result.Statistics.IndeterminateCount = ordered.Count(e => e.Type == ExtremeType.Indeterminate);

            return result;
        }

        #region Internals

        private sealed class Extremum
        {
            public int Index;
            public bool IsPeak;
        }

        private sealed class Track
        {
            public ExtremeType Type;
            public int Index;
            public int BirthIndex;
            public int DeathScaleIndex;
        }

        private static ExtremeStatistics BuildStatistics(
            IReadOnlyList<double> x, IReadOnlyList<double> y,
            ExtremeDetectorOptions opts, double baseScale, double maxScale)
        {
            return new ExtremeStatistics
            {
                PointCount = y.Count,
                Spacing = EstimateSpacing(x),
                MinX = x.Min(),
                MaxX = x.Max(),
                BaseScale = baseScale,
                MaxScale = maxScale
            };
        }

        private static double EstimateSpacing(IReadOnlyList<double> x)
        {
            if (x.Count < 2) return 1.0;
            double step = Math.Abs(x[1] - x[0]);
            if (step > 0) return step;
            for (int i = 1; i < x.Count; i++)
            {
                double d = Math.Abs(x[i] - x[i - 1]);
                if (d > 0) return d;
            }
            return 1.0;
        }

        private static double EstimateNoiseStd(IReadOnlyList<double> y)
        {
            int n = y.Count;
            var residuals = new double[n];
            for (int i = 0; i < n; i++)
            {
                int lo = Math.Max(0, i - 2);
                int hi = Math.Min(n - 1, i + 2);
                double sum = 0;
                int cnt = 0;
                for (int j = lo; j <= hi; j++) { sum += y[j]; cnt++; }
                double mean = sum / cnt;
                residuals[i] = y[i] - mean;
            }

            var sorted = residuals.OrderBy(v => v).ToArray();
            double median = sorted[n / 2];
            var absDev = residuals.Select(r => Math.Abs(r - median)).OrderBy(v => v).ToArray();
            double mad = absDev[n / 2];
            double std = mad * 1.4826;

            double globalScale = y.Max() - y.Min();
            if (std <= 0) std = Math.Max(globalScale * 0.001, 1e-9);
            return std;
        }

        private static double[] BuildScales(double baseScale, double maxScale, int n)
        {
            int count = Math.Max(6, (int)Math.Ceiling(6.0 * Math.Log(maxScale / baseScale) / Math.Log(2)));
            count = Math.Min(count, n);
            var scales = new double[count];
            for (int k = 0; k < count; k++)
            {
                double t = count <= 1 ? 0 : (double)k / (count - 1);
                scales[k] = baseScale * Math.Pow(maxScale / baseScale, t);
            }
            return scales;
        }

        internal static double[] GaussianSmooth(IReadOnlyList<double> y, double sigma)
        {
            int n = y.Count;
            int r = Math.Max(1, (int)Math.Ceiling(3.0 * sigma));
            r = Math.Min(r, n - 1);
            if (r <= 0) return y.ToArray();

            var kernel = new double[2 * r + 1];
            double sum = 0;
            for (int i = -r; i <= r; i++)
            {
                double v = Math.Exp(-(i * i) / (2.0 * sigma * sigma));
                kernel[i + r] = v;
                sum += v;
            }
            for (int i = 0; i < kernel.Length; i++) kernel[i] /= sum;

            var result = new double[n];
            for (int i = 0; i < n; i++)
            {
                double acc = 0;
                for (int j = -r; j <= r; j++)
                {
                    acc += y[ReflectIndex(i + j, n)] * kernel[j + r];
                }
                result[i] = acc;
            }
            return result;
        }

        internal static int ReflectIndex(int i, int n)
        {
            if (i < 0) return -i;
            if (i >= n) return 2 * n - 2 - i;
            return i;
        }

        private static List<Extremum> FindExtrema(double[] s)
        {
            var res = new List<Extremum>();
            int n = s.Length;
            if (n < 3) return res;

            int i = 1;
            while (i < n - 1)
            {
                int j = i;
                while (j + 1 < n && s[j + 1] == s[j]) j++;

                double prev = s[i - 1];
                double next = j + 1 < n ? s[j + 1] : s[j];

                if (s[i] >= prev && s[i] >= next && (s[i] > prev || s[i] > next))
                {
                    res.Add(new Extremum { Index = (i + j) / 2, IsPeak = true });
                }
                else if (s[i] <= prev && s[i] <= next && (s[i] < prev || s[i] < next))
                {
                    res.Add(new Extremum { Index = (i + j) / 2, IsPeak = false });
                }
                i = j + 1;
            }
            return res;
        }

        private static (int Left, int Right) FindFeet(double[] smoothed, int p, ExtremeType type)
        {
            int n = smoothed.Length;
            bool wantMin = type == ExtremeType.Top; // pés do topo são vales (mínimos)
            int left = 0;
            for (int i = p - 1; i >= 1; i--)
            {
                if (wantMin
                    ? smoothed[i] <= smoothed[i - 1] && smoothed[i] <= smoothed[i + 1]
                    : smoothed[i] >= smoothed[i - 1] && smoothed[i] >= smoothed[i + 1])
                {
                    left = i;
                    break;
                }
            }
            int right = n - 1;
            for (int i = p + 1; i < n - 1; i++)
            {
                if (wantMin
                    ? smoothed[i] <= smoothed[i - 1] && smoothed[i] <= smoothed[i + 1]
                    : smoothed[i] >= smoothed[i - 1] && smoothed[i] >= smoothed[i + 1])
                {
                    right = i;
                    break;
                }
            }
            return (left, right);
        }

        private static double CoherenceNonDecreasing(double[] s, int from, int to)
        {
            if (to <= from) return 1.0;
            int ok = 0;
            int total = 0;
            for (int i = from; i < to; i++)
            {
                total++;
                if (s[i + 1] >= s[i]) ok++;
            }
            return total == 0 ? 1.0 : (double)ok / total;
        }

        private static double CoherenceNonIncreasing(double[] s, int from, int to)
        {
            if (to <= from) return 1.0;
            int ok = 0;
            int total = 0;
            for (int i = from; i < to; i++)
            {
                total++;
                if (s[i + 1] <= s[i]) ok++;
            }
            return total == 0 ? 1.0 : (double)ok / total;
        }

        private static int ArgMax(IReadOnlyList<double> y, int lo, int hi)
        {
            int best = lo;
            for (int i = lo; i <= hi; i++)
                if (y[i] > y[best]) best = i;
            return best;
        }

        private static int ArgMin(IReadOnlyList<double> y, int lo, int hi)
        {
            int best = lo;
            for (int i = lo; i <= hi; i++)
                if (y[i] < y[best]) best = i;
            return best;
        }

        private static double MinInWindow(IReadOnlyList<double> y, int idx, int half)
        {
            int lo = Math.Max(0, idx - half);
            int hi = Math.Min(y.Count - 1, idx + half);
            double m = double.MaxValue;
            for (int i = lo; i <= hi; i++)
                if (y[i] < m) m = y[i];
            return m;
        }

        private static double XAt(IReadOnlyList<double> x, int index, double spacing)
        {
            if (index >= 0 && index < x.Count) return x[index];
            return x[0] + index * spacing;
        }

        private static List<ExtremePoint> MergeSameType(List<ExtremePoint> candidates)
        {
            var ordered = candidates.Where(c => c.Type != ExtremeType.Indeterminate)
                .OrderBy(e => e.Position)
                .ToList();
            var indeterminate = candidates.Where(c => c.Type == ExtremeType.Indeterminate).ToList();

            // 1. Mescla estruturas da MESMA estrutura: pés (intervalos do sino)
            //    sobrepostos + mesmo tipo → mesmo pico em platô, mantém o mais forte.
            var merged = new List<ExtremePoint>();
            foreach (var c in ordered)
            {
                var last = merged.LastOrDefault();
                if (last != null && SameStructure(last, c))
                {
                    if (c.Prominence > last.Prominence)
                        merged[^1] = c;
                }
                else
                {
                    merged.Add(c);
                }
            }

            // 2. Alternância: remove o mais fraco de dois extremos do mesmo tipo
            //    que ficaram adjacentes (sem um vale/topo confirmado entre eles).
            for (int i = merged.Count - 2; i >= 0; i--)
            {
                if (merged[i].Type != merged[i + 1].Type) continue;
                if (merged[i].Prominence < merged[i + 1].Prominence)
                    merged.RemoveAt(i);
                else
                    merged.RemoveAt(i + 1);
            }

            merged.AddRange(indeterminate);
            return merged;
        }

        /// <summary>Dois extremos do mesmo tipo pertencem à mesma estrutura quando
        /// seus intervalos de sino (pés) se sobrepõem.</summary>
        private static bool SameStructure(ExtremePoint a, ExtremePoint b)
        {
            if (a.Type != b.Type) return false;
            return Math.Max(a.StartPosition, b.StartPosition) < Math.Min(a.EndPosition, b.EndPosition);
        }

        private static List<ExtremePoint> EnforceAlternation(List<ExtremePoint> candidates)
        {
            // A fusão e a alternância são feitas em MergeSameType.
            return candidates;
        }

        #endregion
    }
}