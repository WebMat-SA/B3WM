using System.Text;
using System.Text.Json;
using B3WM.Services;
using B3WM.Services.Core;
using B3WM.Shared.Entity;
using B3WM.Shared.Models;
using B3WM.Shared.Models.ExtremeDetection;

namespace B3WM.Tests;

/// <summary>
/// Testes do detector de topos/vales estruturais (ExtremeDetector).
/// Cobrem casos sintéticos (sino limpo/ruidoso, platô, borda) e os
/// casos reais validados no perfil de volume de 2026-08-12.
/// </summary>
public class ExtremeDetectionTests
{
    private static string RepoRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));

    private static string DataPath(string file) => Path.Combine(RepoRoot, "B3WM", "Data", file);

    #region Casos sintéticos

    [Fact]
    public void CleanBell_DetectsSingleTop()
    {
        var (x, y) = CleanBell(100, 30);
        var result = ExtremeDetector.Detect(x, y);

        var top = Assert.Single(result.Tops);
        Assert.InRange(top.Position, 25, 35);
        Assert.Empty(result.Valleys);
        Assert.NotEmpty(result.Structures);
    }

    [Fact]
    public void NoisyBell_StillDetectsTop()
    {
        var rnd = new Random(42);
        var (x, y) = CleanBell(100, 30);
        var noisy = y.Select(v => v + (rnd.NextDouble() - 0.5) * 40).ToArray();

        var result = ExtremeDetector.Detect(x, noisy);

        var top = Assert.Single(result.Tops);
        Assert.InRange(top.Position, 25, 35);
    }

    [Fact]
    public void Plateau_MergesMultiplePeaksIntoStrongest()
    {
        // três picos próximos formando um platô: o mais alto deve ser o representante
        var x = new double[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19 };
        var y = new double[] { 1, 2, 3, 4, 3, 4, 3, 4, 3, 2, 1, 2, 3, 4, 5, 4, 3, 2, 1, 0 };

        var result = ExtremeDetector.Detect(x, y);

        var top = Assert.Single(result.Tops);
        Assert.InRange(top.Position, 13.5, 14.5);
    }

    [Fact]
    public void RisingEdge_DoesNotForceTopOrValley()
    {
        // série monotonicamente crescente: sem extremos confirmados
        var x = new double[30];
        var y = new double[30];
        for (int i = 0; i < 30; i++)
        {
            x[i] = i;
            y[i] = i;
        }

        var result = ExtremeDetector.Detect(x, y);

        Assert.Empty(result.Tops);
        Assert.Empty(result.Valleys);
    }

    [Fact]
    public void SingleBell_ReportsOneStructure()
    {
        var (x, y) = CleanBell(60, 25);
        var result = ExtremeDetector.Detect(x, y);

        Assert.Single(result.Structures);
        var structure = result.Structures[0];
        Assert.True(Math.Abs(result.Tops[0].Position - structure.TopPosition!.Value) < 0.01);
        Assert.True(structure.Amplitude > 0);
    }

    #endregion

    #region Casos reais (perfil de volume 2026-08-12)

    [Fact]
    public void RealProfile_RegionA_ConfirmsStructuralTop()
    {
        var (x, y) = LoadRealProfile();

        var result = ExtremeDetector.Detect(x, y);

        // Região A (~171395): esperado TOPO estrutural em 171390/171395/171400.
        var topsInRegion = result.Tops
            .Where(t => t.Position >= 171385 && t.Position <= 171405)
            .ToList();
        Assert.NotEmpty(topsInRegion);
        var top = topsInRegion.First();
        Assert.InRange(top.Position, 171385, 171405);
        Assert.Equal(ExtremeType.Top, top.Type);

        // deve ser o máximo global da série
        double globalMaxX = x[ArgMax(y)];
        Assert.Equal(globalMaxX, top.Position, 3);
    }

    [Fact]
    public void RealProfile_RegionB_DoesNotForceValleyOutsideSample()
    {
        var (x, y) = LoadRealProfile();

        var result = ExtremeDetector.Detect(x, y);

        // Região B (~170535): o preço está fora da amostra (primeiro ponto 170545).
        // Nenhum vale deve ser confirmado nessa vizinhança (seria forçar borda).
        var forced = result.Extremes
            .Where(e => e.Type == ExtremeType.Valley && e.Position <= 170550)
            .ToList();
        Assert.Empty(forced);
    }

    [Fact]
    public void RealProfile_ProducesAlternatingSequence()
    {
        var (x, y) = LoadRealProfile();
        var result = ExtremeDetector.Detect(x, y);

        var confirmed = result.Extremes
            .Where(e => e.Type != ExtremeType.Indeterminate)
            .OrderBy(e => e.Position)
            .ToList();

        for (int i = 0; i < confirmed.Count - 1; i++)
            Assert.NotEqual(confirmed[i].Type, confirmed[i + 1].Type);
    }

    [Fact]
    public void RealProfile_PartialPeriod_StillDetectsStructures()
    {
        // recorta um trecho do meio do dia (período parcial)
        var (x, y) = LoadRealProfile();
        int lo = 60, hi = 180;
        var subX = x.Skip(lo).Take(hi - lo).ToArray();
        var subY = y.Skip(lo).Take(hi - lo).ToArray();

        var result = ExtremeDetector.Detect(subX, subY);

        Assert.True(result.Tops.Count + result.Valleys.Count > 0);
        foreach (var e in result.Extremes)
            Assert.InRange(e.Position, subX[0], subX[^1]);
    }

    #endregion

    #region Relatório HTML/SVG

    [Fact]
    public void Generate_HTML_Report()
    {
        var (x, y) = LoadRealProfile();
        var result = ExtremeDetector.Detect(x, y);

        string html = BuildReport(x, y, result);
        string path = DataPath($"ExtremeDetection_{DateTime.Now:yyyy-MM-dd}.html");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, html);

        Assert.True(File.Exists(path));
        Assert.Contains("<svg", html);
    }

    #endregion

    #region Detecção ciente da data (BuildProfileFromFiles)

    /// <summary>
    /// Dia inteiro a partir do arquivo persistido do VolumeService da data:
    /// deve montar o perfil e o detector encontrar estruturas (dados de 2026-08-14).
    /// </summary>
    [Fact]
    public async Task DateAware_FullDay_FromPersistedVolumeFile_DetectsStructures()
    {
        var keeper = new RepoDataKeeper(Path.Combine(RepoRoot, "B3WM", "Data"));
        var profile = await ExtremeService.BuildProfileFromFiles(
            keeper, "WINFUT", new DateTime(2026, 8, 14), null, null);

        Assert.NotEmpty(profile);

        var prices = profile.Select(v => v.Price).ToArray();
        var totals = profile.Select(v => (double)v.Total).ToArray();
        var result = ExtremeDetector.Detect(prices, totals);

        Assert.True(result.Tops.Count + result.Valleys.Count > 0);
        Assert.NotEmpty(result.Structures);
    }

    /// <summary>
    /// Janela parcial: subtrai o snapshot do início do snapshot do fim usando
    /// os candles 1MIN persistidos (mesmo conceito do Volume Profile parcial).
    /// </summary>
    [Fact]
    public async Task DateAware_PartialWindow_SubtractsStartSnapshot()
    {
        string tmp = Path.Combine(Path.GetTempPath(), "b3wm_extreme_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp);
        try
        {
            var keeper = new RepoDataKeeper(tmp);
            var day = new DateTime(2026, 8, 14);

            // perfil cumulativo do dia (arquivo do VolumeService)
            await keeper.WriteDataAsync(
                $"WINFUT_{nameof(VolumeService)}_{day:yyyy-MM-dd}.json",
                new VolumeLevelStorageItem
                {
                    Date = day,
                    Symbol = "WINFUT",
                    Volumes = new List<VolumeLevel>
                    {
                        new() { Price = 10, Total = 100 },
                        new() { Price = 20, Total = 200 },
                        new() { Price = 30, Total = 300 },
                    }
                });

            // candles 1MIN com snapshots parciais
            VolumeLevel Lv(long p, long t) => new() { Price = p, Total = t };
            await keeper.WriteDataAsync(
                $"WINFUT_{nameof(CandleService)}_1MIN_{day:yyyy-MM-dd}.json",
                new List<BarStorageItem>
                {
                    new()
                    {
                        Date = day.AddHours(9),
                        Symbol = "WINFUT",
                        TimeFrame = 1,
                        VolumeLevel = new List<VolumeLevel> { Lv(10, 40), Lv(20, 80) }
                    },
                    new()
                    {
                        Date = day.AddHours(9).AddMinutes(1),
                        Symbol = "WINFUT",
                        TimeFrame = 1,
                        VolumeLevel = new List<VolumeLevel> { Lv(10, 70), Lv(20, 150), Lv(30, 90) }
                    }
                });

            var profile = await ExtremeService.BuildProfileFromFiles(
                keeper, "WINFUT", day,
                from: day.AddHours(9),
                to: day.AddHours(9).AddMinutes(1));

            var byPrice = profile.ToDictionary(v => v.Price, v => v.Total);
            Assert.Equal(30, byPrice[10]);   // 70 - 40
            Assert.Equal(70, byPrice[20]);   // 150 - 80
            Assert.Equal(90, byPrice[30]);   // só no fim
        }
        finally
        {
            try { Directory.Delete(tmp, recursive: true); } catch { }
        }
    }

    private sealed class RepoDataKeeper : DataKeeperBase
    {
        private readonly string _root;
        public RepoDataKeeper(string root) => _root = root;
        protected override string RootDirectory => _root;
    }

    #endregion

    #region Helpers

    private static (double[] x, double[] y) CleanBell(int n, int center)
    {
        var x = Enumerable.Range(0, n).Select(i => (double)i).ToArray();
        var y = new double[n];
        for (int i = 0; i < n; i++)
            y[i] = 100 * Math.Exp(-(i - center) * (i - center) / 40.0) + 1;
        return (x, y);
    }

    private static (double[] x, double[] y) LoadRealProfile()
    {
        string json = File.ReadAllText(DataPath("WINFUT_VolumeService_2026-08-12.json"));
        using var doc = JsonDocument.Parse(json);
        var vols = doc.RootElement.GetProperty("Volumes");
        var x = new List<double>();
        var y = new List<double>();
        foreach (var v in vols.EnumerateArray())
        {
            x.Add(v.GetProperty("Price").GetDouble());
            y.Add(v.GetProperty("Total").GetDouble());
        }
        return (x.ToArray(), y.ToArray());
    }

    private static int ArgMax(IReadOnlyList<double> y)
    {
        int best = 0;
        for (int i = 1; i < y.Count; i++)
            if (y[i] > y[best]) best = i;
        return best;
    }

    private static string BuildReport(double[] x, double[] y, ExtremeDetectionResult result)
    {
        double minY = y.Min();
        double maxY = y.Max();
        double minX = x.Min();
        double maxX = x.Max();

        int W = 1100, H = 420;
        double Px(double v) => 60 + (v - minX) / (maxX - minX) * (W - 120);
        double Py(double v) => H - 40 - (v - minY) / (maxY - minY) * (H - 80);

        var sb = new StringBuilder();
        sb.AppendLine("<html><head><meta charset='utf-8'><title>Detecção de Topos/Vales Estruturais</title>");
        sb.AppendLine("<style>body{font-family:Arial,sans-serif;margin:24px}h2{color:#333}.leg{font-size:12px}</style></head><body>");
        sb.AppendLine($"<h2>Detecção de Topos/Vales Estruturais — WINFUT 2026-08-12</h2>");
        sb.AppendLine($"<p class='leg'>Pontos: {result.Statistics.PointCount} | Ruído σ: {result.Statistics.NoiseStd:F1} | " +
                      $"Topos: {result.Statistics.TopCount} | Vales: {result.Statistics.ValleyCount} | " +
                      $"Indeterminados: {result.Statistics.IndeterminateCount}</p>");
        sb.AppendLine($"<svg width='{W}' height='{H}' viewBox='0 0 {W} {H}'>");

        // curva do perfil
        sb.Append("<polyline points='");
        for (int i = 0; i < x.Length; i++)
            sb.Append($"{Px(x[i]):F1},{Py(y[i]):F1} ");
        sb.AppendLine("' fill='none' stroke='#999' stroke-width='1.5'/>");

        // linhas de topo (vermelho) e vale (verde)
        foreach (var e in result.Extremes)
        {
            string color = e.Type switch
            {
                ExtremeType.Top => "#d32f2f",
                ExtremeType.Valley => "#2e7d32",
                _ => "#9e9e9e"
            };
            sb.AppendLine($"<line x1='50' y1='{Py(e.Position):F1}' x2='{W - 50}' y2='{Py(e.Position):F1}' " +
                          $"stroke='{color}' stroke-width='1.5' stroke-dasharray='6,4'/>");
            sb.AppendLine($"<text x='{W - 46}' y='{Py(e.Position) - 4:F1}' font-size='11' fill='{color}'>{e.Position:F0}</text>");
        }

        sb.AppendLine("</svg>");

        sb.AppendLine("<table border='1' cellspacing='0' cellpadding='4' style='border-collapse:collapse;font-size:13px'>");
        sb.AppendLine("<tr><th>Tipo</th><th>Preço</th><th>Total</th><th>Prominência</th><th>Força</th><th>Largura</th></tr>");
        foreach (var e in result.Extremes.OrderBy(e => e.Position))
        {
            sb.AppendLine($"<tr><td>{e.Type}</td><td>{e.Position:F0}</td><td>{e.Value:F0}</td>" +
                          $"<td>{e.Prominence:F0}</td><td>{e.Strength:F3}</td><td>{e.Width:F0}</td></tr>");
        }
        sb.AppendLine("</table></body></html>");
        return sb.ToString();
    }

    #endregion
}