using B3WM.Services.Screen;
using B3WM.Shared.Models;

namespace B3WM.Tests;

/// <summary>
/// Paridade do automode por estrutura entre backend (StructureWindowResolver)
/// e app (state_service.dart). Casos portados de
/// B3WM.Flutter/test/structure_anchor_test.dart. Funções puras — sem I/O.
/// </summary>
public class StructureWindowResolverTests
{
    private const string Sym = "WINFUT";

    private static BarStorageItem Bar(int h, int m, double high, double low, int tf = 2) => new()
    {
        Symbol = Sym,
        TimeFrame = tf,
        Date = new DateTime(2026, 9, 8, h, m, 0),
        Open = low,
        High = high,
        Low = low,
        Close = high,
        Volume = 10,
    };

    private static BarStorageItem Day(int d, double high, double low) => new()
    {
        Symbol = Sym,
        TimeFrame = 1440,
        Date = new DateTime(2026, 9, d, 18, 0, 0),
        Open = low,
        High = high,
        Low = low,
        Close = high,
        Volume = 10,
    };

    private static StructureStorageItem Struct(
        int h, int m, double up = 0, double down = 0,
        double upAux = 0, double downAux = 0, int tf = 2) => new()
    {
        Symbol = Sym,
        TimeFrame = tf,
        Date = new DateTime(2026, 9, 8, h, m, 0),
        UpBorder = up,
        DownBorder = down,
        UpAuxBorder = upAux,
        DownAuxBorder = downAux,
    };

    private static StructureChange Change(DateTime date, bool isUp, double oldV, double newV) =>
        new(date, isUp, oldV, newV);

    // ---------------- confirmationBarIndex ----------------

    [Fact]
    public void ConfirmationBarIndex_UltimaBarraAntesDaData()
    {
        var bars = new List<BarStorageItem> { Bar(9, 0, 100, 90), Bar(9, 2, 110, 95), Bar(9, 4, 105, 92) };
        var idx = StructureWindowResolver.ConfirmationBarIndex(bars, new DateTime(2026, 9, 8, 9, 3, 0));
        Assert.Equal(1, idx);
    }

    // ---------------- resolveExtremeBarIndex ----------------

    [Fact]
    public void ResolveExtremeBarIndex_TopoVoltaAoAux()
    {
        var bars = new List<BarStorageItem> { Bar(9, 0, 100, 90), Bar(9, 2, 110, 95), Bar(9, 4, 105, 92) };
        var anchor = Change(bars[2].Date, true, 100, 110);
        var idx = StructureWindowResolver.ResolveExtremeBarIndex(
            bars, new List<StructureStorageItem>(), Sym, 2, anchor, 2, 0, 2.5);
        Assert.Equal(1, idx);
    }

    [Fact]
    public void ResolveExtremeBarIndex_Reteste()
    {
        var bars = new List<BarStorageItem>
        {
            Bar(9, 0, 110, 90), Bar(9, 2, 108, 95), Bar(9, 4, 110, 96), Bar(9, 6, 104, 92),
        };
        var anchor = Change(bars[3].Date, true, 100, 110);
        var idx = StructureWindowResolver.ResolveExtremeBarIndex(
            bars, new List<StructureStorageItem>(), Sym, 2, anchor, 3, 0, 2.5);
        Assert.Equal(0, idx);
    }

    [Fact]
    public void ResolveExtremeBarIndex_Fundo()
    {
        var bars = new List<BarStorageItem>
        {
            Bar(9, 0, 100, 90), Bar(9, 2, 98, 80), Bar(9, 4, 99, 80), Bar(9, 6, 100, 85),
        };
        var anchor = Change(bars[3].Date, false, 90, 80);
        var idx = StructureWindowResolver.ResolveExtremeBarIndex(
            bars, new List<StructureStorageItem>(), Sym, 2, anchor, 3, 0, 2.5);
        Assert.Equal(1, idx);
    }

    [Fact]
    public void ResolveExtremeBarIndex_FontePrimariaAux()
    {
        var bars = new List<BarStorageItem> { Bar(9, 0, 100, 90), Bar(9, 2, 110, 95), Bar(9, 4, 105, 92) };
        var structures = new List<StructureStorageItem>
        {
            Struct(9, 0, upAux: 100),
            Struct(9, 2, upAux: 110),
        };
        var anchor = Change(bars[2].Date, true, 100, 110);
        var idx = StructureWindowResolver.ResolveExtremeBarIndex(
            bars, structures, Sym, 2, anchor, 2, 0, 2.5);
        Assert.Equal(1, idx);
    }

    [Fact]
    public void ResolveExtremeBarIndex_FallbackConfirmacao()
    {
        var bars = new List<BarStorageItem> { Bar(9, 0, 100, 90), Bar(9, 2, 101, 95), Bar(9, 4, 102, 92) };
        var anchor = Change(bars[2].Date, true, 100, 999);
        var idx = StructureWindowResolver.ResolveExtremeBarIndex(
            bars, new List<StructureStorageItem>(), Sym, 2, anchor, 2, 0, 2.5);
        Assert.Equal(2, idx);
    }

    [Fact]
    public void ResolveExtremeBarIndex_RespeitaLowerBound()
    {
        var bars = new List<BarStorageItem>
        {
            Bar(9, 0, 110, 90), Bar(9, 2, 108, 95), Bar(9, 4, 100, 96),
            Bar(9, 6, 100, 92), Bar(9, 8, 100, 91), Bar(9, 10, 110, 92), Bar(9, 12, 105, 93),
        };
        var anchor = Change(bars[6].Date, true, 100, 110);
        var idx = StructureWindowResolver.ResolveExtremeBarIndex(
            bars, new List<StructureStorageItem>(), Sym, 2, anchor, 6, 3, 2.5);
        Assert.Equal(5, idx);
    }

    // ---------------- oppositeResetDate ----------------

    [Fact]
    public void OppositeResetDate_AchaMudancaDeLadoOposto()
    {
        var changes = new List<StructureChange>
        {
            Change(new DateTime(2026, 9, 8, 9, 8, 0), true, 100, 110),
            Change(new DateTime(2026, 9, 8, 9, 6, 0), false, 90, 80),
            Change(new DateTime(2026, 9, 8, 9, 4, 0), false, 95, 90),
            Change(new DateTime(2026, 9, 8, 9, 0, 0), true, 90, 100),
        };
        var reset = StructureWindowResolver.OppositeResetDate(changes, 1);
        Assert.Equal(new DateTime(2026, 9, 8, 9, 0, 0), reset);
    }

    [Fact]
    public void OppositeResetDate_NuloSemReset()
    {
        var changes = new List<StructureChange>
        {
            Change(new DateTime(2026, 9, 8, 9, 6, 0), false, 90, 80),
            Change(new DateTime(2026, 9, 8, 9, 2, 0), false, 95, 90),
        };
        var reset = StructureWindowResolver.OppositeResetDate(changes, 0);
        Assert.Null(reset);
    }

    [Fact]
    public void OppositeResetDate_LimitaPerna()
    {
        var bars = new List<BarStorageItem>
        {
            Bar(9, 0, 100, 90), Bar(9, 2, 96, 80), Bar(9, 4, 98, 85),
            Bar(9, 6, 100, 80), Bar(9, 8, 103, 88),
        };
        var anchor = Change(bars[4].Date, false, 90, 80);
        var a = StructureWindowResolver.ResolveExtremeBarIndex(
            bars, new List<StructureStorageItem>(), Sym, 2, anchor, 4, 2, 2.5);
        var b = StructureWindowResolver.ResolveExtremeBarIndex(
            bars, new List<StructureStorageItem>(), Sym, 2, anchor, 4, 0, 2.5);
        Assert.Equal(3, a);
        Assert.Equal(1, b);
    }

    // ---------------- resolveDailyAnchorDay ----------------

    [Fact]
    public void ResolveDailyAnchorDay_PrimeiroToque()
    {
        var bars = new List<BarStorageItem> { Day(1, 100, 90), Day(2, 110, 95), Day(3, 108, 92), Day(4, 109, 93) };
        var anchor = Change(new DateTime(2026, 9, 4, 18, 0, 0), true, 100, 110);
        var day = StructureWindowResolver.ResolveDailyAnchorDay(
            bars, new List<StructureStorageItem>(), Sym, anchor);
        Assert.Equal(new DateTime(2026, 9, 2), day);
    }

    [Fact]
    public void ResolveDailyAnchorDay_FontePrimariaAux()
    {
        var bars = new List<BarStorageItem> { Day(1, 100, 90), Day(2, 110, 95), Day(3, 108, 92), Day(4, 109, 93) };
        var history = new List<StructureStorageItem>
        {
            new() { Symbol = Sym, TimeFrame = 1440, Date = new DateTime(2026, 9, 1, 18, 0, 0), UpAuxBorder = 100 },
            new() { Symbol = Sym, TimeFrame = 1440, Date = new DateTime(2026, 9, 2, 18, 0, 0), UpAuxBorder = 110 },
        };
        var anchor = Change(new DateTime(2026, 9, 4, 18, 0, 0), true, 100, 110);
        var day = StructureWindowResolver.ResolveDailyAnchorDay(bars, history, Sym, anchor);
        Assert.Equal(new DateTime(2026, 9, 2), day);
    }

    [Fact]
    public void ResolveDailyAnchorDay_VazioUsaConfirmacao()
    {
        var anchor = Change(new DateTime(2026, 9, 4, 18, 0, 0), true, 100, 110);
        var day = StructureWindowResolver.ResolveDailyAnchorDay(
            new List<BarStorageItem>(), new List<StructureStorageItem>(), Sym, anchor);
        Assert.Equal(new DateTime(2026, 9, 4), day);
    }

    // ---------------- ResolveIntraday (âncora) ----------------

    [Fact]
    public void ResolveIntraday_EscolheAncoraEIniciaNoToque()
    {
        var bars = new List<BarStorageItem> { Bar(9, 0, 100, 90), Bar(9, 2, 110, 80), Bar(9, 4, 105, 85) };
        var structures = new List<StructureStorageItem>
        {
            Struct(9, 0, up: 110, down: 90, downAux: 95),
            Struct(9, 2, up: 110, down: 80, downAux: 80),
            Struct(9, 4, up: 120, down: 80, downAux: 85),
        };
        var result = StructureWindowResolver.ResolveIntraday(bars, structures, Sym, 2, 5.0);
        Assert.NotNull(result);
        Assert.Equal(1, result!.Value.StartIndex);
        Assert.Equal(3, result.Value.EndIndex);
        Assert.Equal(new DateTime(2026, 9, 8, 9, 2, 0), result.Value.From);
        Assert.Equal(new DateTime(2026, 9, 8, 9, 4, 0), result.Value.To);
    }
}
