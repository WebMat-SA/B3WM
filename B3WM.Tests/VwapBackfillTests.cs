using B3WM.Services.Core;
using B3WM.Shared.Models;

namespace B3WM.Tests;

/// <summary>
/// Backfill VWAP (dias anteriores a partir do 1MIN): matemática pura do
/// VwapService.BuildHistory — mesmo acumulado typical HLC/3 do ao vivo.
/// </summary>
public class VwapBackfillTests
{
    private static BarStorageItem Bar(string symbol, DateTime date, double h, double l, double c, long v, int tf = 1) => new()
    {
        Date = date, Symbol = symbol, TimeFrame = tf,
        Open = c, High = h, Low = l, Close = c, Volume = v,
    };

    [Fact]
    public void BuildHistory_CumulativeTypicalWeighted()
    {
        var day = new DateTime(2026, 10, 5);
        var bars = new List<BarStorageItem>
        {
            Bar("WINFUT", day.AddHours(9), h: 102, l: 98, c: 100, v: 10), // typical 100
            Bar("WINFUT", day.AddHours(9).AddMinutes(1), h: 104, l: 100, c: 103, v: 30), // typical 102.333...
        };
        var hst = VwapService.BuildHistory("WINFUT", bars, day);

        Assert.Equal(2, hst.Count);
        Assert.Equal(100.0, hst[0].Vwap, precision: 9);
        var expected = (100.0 * 10 + (104.0 + 100.0 + 103.0) / 3 * 30) / 40;
        Assert.Equal(expected, hst[1].Vwap, precision: 9);
        Assert.Equal(40, hst[1].SumV);
        Assert.Equal("WINFUT", hst[1].Symbol);
        Assert.Equal(day.Date, hst[1].Day.Date);
    }

    [Fact]
    public void BuildHistory_GapMinutes_RepeatLastVwap()
    {
        var day = new DateTime(2026, 10, 5);
        var bars = new List<BarStorageItem>
        {
            Bar("WINFUT", day.AddHours(9), h: 102, l: 98, c: 100, v: 10),
            Bar("WINFUT", day.AddHours(9).AddMinutes(3), h: 102, l: 98, c: 100, v: 10),
        };
        var hst = VwapService.BuildHistory("WINFUT", bars, day);

        Assert.Equal(4, hst.Count); // 09:00, fillers 09:01/09:02, 09:03
        Assert.Equal(hst[0].Vwap, hst[1].Vwap, precision: 9);
        Assert.Equal(hst[0].Vwap, hst[2].Vwap, precision: 9);
        Assert.Equal(hst[0].Minute.AddMinutes(1), hst[1].Minute);
        Assert.Equal(hst[0].Minute.AddMinutes(2), hst[2].Minute);
        Assert.Equal(20, hst[3].SumV); // fillers não acumulam volume
    }

    [Fact]
    public void BuildHistory_SkipsOtherSymbolTfAndDay_DuplicateFirstWins()
    {
        var day = new DateTime(2026, 10, 5);
        var t = day.AddHours(9);
        var bars = new List<BarStorageItem>
        {
            Bar("WINFUT", t, h: 102, l: 98, c: 100, v: 10),
            Bar("WINFUT", t, h: 200, l: 200, c: 200, v: 999), // duplicata: ignorada
            Bar("WDOFUT", t.AddMinutes(1), h: 102, l: 98, c: 100, v: 10), // outro símbolo
            Bar("WINFUT", t.AddMinutes(1), h: 102, l: 98, c: 100, v: 10, tf: 2), // outro TF
            Bar("WINFUT", t.AddDays(1), h: 102, l: 98, c: 100, v: 10), // outro dia
        };
        var hst = VwapService.BuildHistory("WINFUT", bars, day);

        var point = Assert.Single(hst);
        Assert.Equal(100.0, point.Vwap, precision: 9);
        Assert.Equal(10, point.SumV);
    }

    [Fact]
    public void BuildHistory_EmptyDay_ReturnsEmpty()
    {
        Assert.Empty(VwapService.BuildHistory("WINFUT", new List<BarStorageItem>(), new DateTime(2026, 10, 5)));
    }
}
