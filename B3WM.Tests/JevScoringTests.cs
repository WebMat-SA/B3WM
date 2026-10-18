using B3WM.Services.AI;

namespace B3WM.Tests;

/// <summary>
/// Agregação informativa da Jev (Composite scoring) e proximidade técnica.
/// Puro, sem I/O.
/// </summary>
public class JevScoringTests
{
    private static Dictionary<string, double> All(double p) => new()
    {
        ["candles_alta"] = p, ["acima_vwap"] = p, ["estrutura_alta"] = p,
        ["acima_poc"] = p, ["delta_positivo"] = p, ["bolhas_compra"] = p,
        ["defende_suporte"] = p, ["diario_alta"] = p,
        ["sem_chop"] = p, ["amplitude_suficiente"] = p, ["sem_compressao_topovale"] = p,
        ["referencia_clara"] = p, ["sem_conflito_intra_diario"] = p,
    };

    [Fact]
    public void Evaluate_BullishIndicators_BiasComprar()
    {
        var r = JevScoring.Evaluate(All(0.8), JevWeights.Defaults());
        Assert.Equal("comprar", r.Direcao);
        Assert.True(r.DirecaoConfidence > 0 && r.DirecaoConfidence < 1);
        Assert.True(r.ConfiancaOperavel > 0.5);
        Assert.Equal(13, r.Criteria.Count);
    }

    [Fact]
    public void Evaluate_BearishIndicators_BiasVender()
    {
        var r = JevScoring.Evaluate(All(0.2), JevWeights.Defaults());
        Assert.Equal("vender", r.Direcao);
        Assert.True(r.DirecaoConfidence > 0);
    }

    [Fact]
    public void Evaluate_NeutralIndicators_BiasManter()
    {
        var r = JevScoring.Evaluate(All(0.5), JevWeights.Defaults());
        Assert.Equal("manter", r.Direcao);
        Assert.Equal(0, r.DirecaoConfidence);
    }

    [Fact]
    public void Evaluate_MissingIndicators_AreSkipped()
    {
        var r = JevScoring.Evaluate(
            new Dictionary<string, double> { ["candles_alta"] = 0.9 }, JevWeights.Defaults());
        Assert.Equal("comprar", r.Direcao);
        Assert.Equal(0, r.ConfiancaOperavel); // nenhum indicador de operabilidade
        Assert.Single(r.Criteria);
    }

    [Fact]
    public void Proximity_PrefersNearestWeightedReference()
    {
        var refs = new (string Name, double Value, double Weight)[]
        {
            ("intra", 120, 1.0),      // 4 ticks
            ("pivo_diario", 100, 2.0) // 0 ticks (peso maior)
        };
        var (prox, ticks, refName) = JevScoring.Proximity(100, refs, 5, 100);
        Assert.Equal("pivo_diario", refName);
        Assert.Equal(0, ticks);
        Assert.Equal(1, prox);
    }

    [Fact]
    public void Proximity_NormalizesByMaxTicksAndWeight()
    {
        // 1 tick = 5 pontos (WINFUT): preço 100, referência 95 → 1 tick.
        var refs = new (string Name, double Value, double Weight)[] { ("vwap", 95, 1.5) };
        var (prox, ticks, refName) = JevScoring.Proximity(100, refs, 5, 100);
        Assert.Equal("vwap", refName);
        Assert.Equal(1, ticks);
        Assert.True(prox > 0.99 && prox < 1);
    }

    [Fact]
    public void Proximity_EmptyRefs_Zero()
    {
        var (prox, _, refName) = JevScoring.Proximity(100,
            Array.Empty<(string, double, double)>(), 5, 100);
        Assert.Equal(0, prox);
        Assert.Equal("", refName);
    }

    [Fact]
    public void Weights_DefaultsCoverAllIndicators()
    {
        var w = JevWeights.Defaults();
        Assert.Equal(JevPrompt.DirectionIndicators.Length, w.Direction.Count);
        Assert.Equal(JevPrompt.OperabilityIndicators.Length, w.Operability.Count);
    }
}
