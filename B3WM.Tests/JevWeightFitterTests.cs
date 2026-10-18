using B3WM.Services.AI;

namespace B3WM.Tests;

/// <summary>
/// Matemática do fitter (fase B): regressão logística, rótulos triple-barrier
/// e mapeamento coeficiente→peso. Puro, sem I/O.
/// </summary>
public class JevWeightFitterTests
{
    // ---- Regressão logística ----

    [Fact]
    public void Fit_LearnsPositiveCoefficient_ForInformativeFeature()
    {
        // y=1 quando x0 > 0.5; x1 é ruído.
        var x = new List<double[]>();
        var y = new List<int>();
        for (int i = 0; i < 40; i++)
        {
            double f = i / 39.0;            // 0..1
            x.Add(new[] { f, 0.5 });
            y.Add(f > 0.5 ? 1 : 0);
        }

        var b = LogisticRegression.Fit(x, y);
        Assert.Equal(3, b.Length);          // bias + 2 features
        Assert.True(b[1] > 0, $"coef de x0 deveria ser > 0, veio {b[1]}");
        Assert.True(Math.Abs(b[2]) < Math.Abs(b[1]), "x1 (ruído) deveria pesar menos");
    }

    [Fact]
    public void Fit_LearnsNegativeCoefficient_ForInvertedFeature()
    {
        // y=1 quando x0 < 0.5 → coeficiente negativo.
        var x = new List<double[]>();
        var y = new List<int>();
        for (int i = 0; i < 40; i++)
        {
            double f = i / 39.0;
            x.Add(new[] { f });
            y.Add(f < 0.5 ? 1 : 0);
        }

        var b = LogisticRegression.Fit(x, y);
        Assert.True(b[1] < 0, $"coef deveria ser < 0, veio {b[1]}");
    }

    [Fact]
    public void Fit_EmptyInput_ReturnsEmpty()
    {
        Assert.Empty(LogisticRegression.Fit(
            Array.Empty<double[]>(), Array.Empty<int>()));
    }

    // ---- Rótulos ----

    private static JevCandleDto C(double o, double h, double l, double c) =>
        new() { Open = o, High = h, Low = l, Close = c };

    private static readonly List<JevCandleDto> Entry100 = new() { C(100, 101, 99, 100) };

    [Fact]
    public void Direction_UpBarrierFirst_IsPlusOne()
    {
        var c = new List<JevCandleDto>(Entry100) { C(100, 111, 100, 110) };
        Assert.Equal(1, JevLabels.Direction(c, 0, 10, 10, 1)); // +10 ticks
    }

    [Fact]
    public void Direction_DownBarrierFirst_IsMinusOne()
    {
        var c = new List<JevCandleDto>(Entry100) { C(100, 100, 89, 90) };
        Assert.Equal(-1, JevLabels.Direction(c, 0, 10, 10, 1));
    }

    [Fact]
    public void Direction_NeitherBarrier_IsZero()
    {
        var c = new List<JevCandleDto>(Entry100) { C(100, 105, 95, 101) };
        Assert.Equal(0, JevLabels.Direction(c, 0, 10, 10, 1));
    }

    [Fact]
    public void Direction_AmbiguousBar_IsZero()
    {
        var c = new List<JevCandleDto>(Entry100) { C(100, 112, 88, 100) };
        Assert.Equal(0, JevLabels.Direction(c, 0, 10, 10, 1));
    }

    [Fact]
    public void Direction_IgnoresBarsBeyondHorizon()
    {
        var c = new List<JevCandleDto>(Entry100);
        for (int i = 0; i < 10; i++) c.Add(C(100, 101, 99, 100));
        c.Add(C(100, 111, 100, 110)); // 11º candle, além do horizonte
        Assert.Equal(0, JevLabels.Direction(c, 0, 10, 10, 1));
    }

    [Fact]
    public void Moved_CountsEitherDirection_AndAmbiguous()
    {
        var up = new List<JevCandleDto>(Entry100) { C(100, 111, 100, 110) };
        var down = new List<JevCandleDto>(Entry100) { C(100, 100, 89, 90) };
        var ambiguous = new List<JevCandleDto>(Entry100) { C(100, 112, 88, 100) };
        var flat = new List<JevCandleDto>(Entry100) { C(100, 105, 95, 101) };

        Assert.True(JevLabels.Moved(up, 0, 10, 10, 1));
        Assert.True(JevLabels.Moved(down, 0, 10, 10, 1));
        Assert.True(JevLabels.Moved(ambiguous, 0, 10, 10, 1));
        Assert.False(JevLabels.Moved(flat, 0, 10, 10, 1));
    }

    // ---- Coeficiente → peso ----

    [Fact]
    public void ComputeWeights_DegenerateFeatures_FallBackToBaseline()
    {
        var dirX = new List<double[]> { new[] { 0.9 }, new[] { 0.1 } };
        var dirY = new List<int> { 1, 0 };
        var opX = new List<double[]> { new[] { 0.9 }, new[] { 0.1 } };
        var opY = new List<int> { 1, 0 };

        // Força um coeficiente negativo impossível de representar: baseline meta.
        var baseline = JevWeights.Defaults();
        // Mapeamento usa TODOS os ids; features reais têm 1 dim → degenerado,
        // deve cair no baseline.
        var w = JevWeightFitter.ComputeWeights(dirX, dirY, opX, opY, baseline);

        Assert.Equal(baseline.Direction.Count, w.Direction.Count);
        Assert.Equal(baseline.Operability.Count, w.Operability.Count);
        Assert.Equal(baseline.LabelHorizon, w.LabelHorizon);
        Assert.Equal(baseline.LabelTicks, w.LabelTicks);
        Assert.Equal(baseline.ProximityMaxTicks, w.ProximityMaxTicks);
    }

    [Fact]
    public void ComputeWeights_ProducesNonNegativeWeights()
    {
        // 8 features de direção, separáveis por x0.
        var dirX = new List<double[]>();
        var dirY = new List<int>();
        for (int i = 0; i < 30; i++)
        {
            double f = i / 29.0;
            var row = new double[8];
            row[0] = f;
            for (int j = 1; j < 8; j++) row[j] = 0.5;
            dirX.Add(row);
            dirY.Add(f > 0.5 ? 1 : 0);
        }

        var w = JevWeightFitter.ComputeWeights(dirX, dirY,
            new List<double[]>(), new List<int>(), JevWeights.Defaults());

        Assert.All(w.Direction.Values, v => Assert.True(v >= 0));
        Assert.True(w.Direction["candles_alta"] > 0, "feature informativa deveria ganhar peso");
    }
}
