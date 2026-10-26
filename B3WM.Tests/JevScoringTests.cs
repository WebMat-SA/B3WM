using B3WM.Services.AI;

namespace B3WM.Tests;

/// <summary>
/// Agregação informativa da Jev (Composite scoring) e proximidade técnica.
/// Puro, sem I/O.
/// </summary>
public class JevScoringTests
{
    /// <summary>Todas as dimensões com a mesma evidência de alta/baixa.</summary>
    private static Dictionary<string, double> All(double bull, double bear) => new()
    {
        ["candles_alta"] = bull, ["candles_baixa"] = bear,
        ["acima_vwap"] = bull, ["abaixo_vwap"] = bear,
        ["estrutura_alta"] = bull, ["estrutura_baixa"] = bear,
        ["acima_poc"] = bull, ["abaixo_poc"] = bear,
        ["delta_positivo"] = bull, ["delta_negativo"] = bear,
        ["bolhas_compra"] = bull, ["bolhas_venda"] = bear,
        ["defende_suporte"] = bull, ["rejeita_resistencia"] = bear,
        ["diario_alta"] = bull, ["diario_baixa"] = bear,
        ["sem_chop"] = 0.8, ["amplitude_suficiente"] = 0.8,
        ["sem_compressao_topovale"] = 0.8, ["referencia_clara"] = 0.8,
        ["sem_conflito_intra_diario"] = 0.8,
    };

    [Fact]
    public void Evaluate_BullishPairs_BiasComprar()
    {
        var r = JevScoring.Evaluate(All(0.8, 0.1), JevWeights.Defaults());
        Assert.Equal("comprar", r.Direcao);
        Assert.True(r.DirecaoConfidence > 0 && r.DirecaoConfidence < 1);
        Assert.Equal(0.7, r.DirecaoNet, 6);
        Assert.Equal(1, r.DirecaoConsenso, 6);
        Assert.Equal(1, r.DirecaoCobertura, 6);
        Assert.Equal("", r.DirecaoMotivo);
        Assert.True(r.ConfiancaOperavel > 0.5);
        Assert.Equal(21, r.Criteria.Count);
    }

    [Fact]
    public void Evaluate_BearishPairs_BiasVender()
    {
        var r = JevScoring.Evaluate(All(0.1, 0.8), JevWeights.Defaults());
        Assert.Equal("vender", r.Direcao);
        Assert.True(r.DirecaoConfidence > 0);
        Assert.Equal(-0.7, r.DirecaoNet, 6);
    }

    [Fact]
    public void Evaluate_NeutralPairs_BiasLateral()
    {
        var r = JevScoring.Evaluate(All(0.5, 0.5), JevWeights.Defaults());
        Assert.Equal("lateral", r.Direcao);
        Assert.Equal(0, r.DirecaoConfidence);
        Assert.Equal("evidência fraca", r.DirecaoMotivo);
    }

    [Fact]
    public void Evaluate_ConflictingPairs_BiasLateral()
    {
        var r = JevScoring.Evaluate(All(0.9, 0.9), JevWeights.Defaults());
        Assert.Equal("lateral", r.Direcao);
        Assert.Equal(0, r.DirecaoConfidence);
        Assert.Equal(0, r.DirecaoNet, 6);
        Assert.Equal("evidência fraca", r.DirecaoMotivo);
    }

    [Fact]
    public void Evaluate_WeakNetInsideBand_BiasLateral()
    {
        var r = JevScoring.Evaluate(All(0.6, 0.45), JevWeights.Defaults());
        Assert.Equal("lateral", r.Direcao);
        Assert.Equal(0.15, r.DirecaoNet, 6);
        Assert.True(Math.Abs(r.DirecaoNet) < JevWeights.Defaults().NeutralBand);
        Assert.Contains("evidência fraca", r.DirecaoMotivo);
    }

    [Fact]
    public void Evaluate_InsufficientCoverage_BiasLateral()
    {
        var crit = new Dictionary<string, double>
        {
            ["candles_alta"] = 0.95, ["candles_baixa"] = 0.05,
        };
        var r = JevScoring.Evaluate(crit, JevWeights.Defaults());
        Assert.Equal("lateral", r.Direcao);
        Assert.True(r.DirecaoCobertura < JevWeights.Defaults().MinDirectionCoverage);
        Assert.Contains("cobertura baixa", r.DirecaoMotivo);
    }

    [Fact]
    public void Evaluate_MissingSideDoesNotCountAsCoverage()
    {
        var crit = new Dictionary<string, double>
        {
            ["candles_alta"] = 0.95,
            ["acima_vwap"] = 0.95, ["abaixo_vwap"] = 0.05,
            ["estrutura_alta"] = 0.95, ["estrutura_baixa"] = 0.05,
            ["acima_poc"] = 0.95, ["abaixo_poc"] = 0.05,
            ["delta_positivo"] = 0.95, ["delta_negativo"] = 0.05,
            ["bolhas_compra"] = 0.95, ["bolhas_venda"] = 0.05,
            ["defende_suporte"] = 0.95, ["rejeita_resistencia"] = 0.05,
            ["diario_alta"] = 0.95, ["diario_baixa"] = 0.05,
        };
        var r = JevScoring.Evaluate(crit, JevWeights.Defaults());
        // A dimensão sem o lado de baixa não entra no net nem na cobertura.
        Assert.Equal("comprar", r.Direcao);
        Assert.True(r.DirecaoCobertura > 0 && r.DirecaoCobertura < 1);
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
    public void Weights_DefaultsCoverAllDimensions()
    {
        var w = JevWeights.Defaults();
        Assert.Equal(JevPrompt.DirectionDimensionIds.Length, w.Direction.Count);
        Assert.Equal(JevPrompt.OperabilityIndicators.Length, w.Operability.Count);
    }
}
