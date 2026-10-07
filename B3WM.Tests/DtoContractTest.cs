using System.Reflection;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace B3WM.Tests;

/// <summary>
/// Trava anti-drift dos DTOs (mesmo padrão do ScreenSpecKeysTest): toda
/// chave JSON lida pelo Flutter precisa existir no shape serializado do
/// backend. Pega renomeação de propriedade C# que o app engoliria em
/// silêncio via `?? default`. Sem I/O de rede (só reflection + regex).
/// Limitação honesta: valores de enum (dual number/string no Dart) e chaves
/// dinâmicas (ex.: probabilities por nome) não são cobertos.
/// </summary>
public class DtoContractTest
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 8 && dir != null; i++)
        {
            if (File.Exists(Path.Combine(dir.FullName, "B3WM.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("B3WM.sln não encontrado a partir de " + AppContext.BaseDirectory);
    }

    private static string Camel(string name) =>
        string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name[1..];

    private static HashSet<string> CSharpJsonNames(params Type[] types)
    {
        var names = new HashSet<string>();
        foreach (var t in types)
        {
            foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var attr = p.GetCustomAttribute<JsonPropertyNameAttribute>();
                names.Add(attr?.Name ?? Camel(p.Name));
            }
        }
        return names;
    }

    private static HashSet<string> DartKeys(params string[] relPaths)
    {
        var root = RepoRoot();
        var keys = new HashSet<string>();
        foreach (var rel in relPaths)
        {
            var text = File.ReadAllText(Path.Combine(root, rel));
            foreach (Match m in Regex.Matches(text, @"json\[['""](\w+)['""]\]"))
                keys.Add(m.Groups[1].Value);
            // `'key':` de toJson (ignora `case 'x':` de switches/enums).
            foreach (Match m in Regex.Matches(text, @"(?<!case\s)'(\w+)'\s*:"))
                keys.Add(m.Groups[1].Value);
        }
        return keys;
    }

    private static void AssertDartCoveredByCSharp(
        string[] dartFiles, Type[] csTypes, string[]? ignoreDart = null)
    {
        var dart = DartKeys(dartFiles);
        if (ignoreDart != null)
            dart.ExceptWith(ignoreDart);
        var cs = CSharpJsonNames(csTypes);
        var missing = dart.Where(k => !cs.Contains(k)).ToList();
        Assert.True(missing.Count == 0,
            $"Chaves lidas pelo Flutter sem correspondente no backend: {string.Join(", ", missing)} " +
            $"(arquivos: {string.Join(", ", dartFiles)})");
    }

    [Fact]
    public void Bar_DartCovered()
    {
        AssertDartCoveredByCSharp(
            new[] { "B3WM.Flutter/lib/models/bar_storage_item.dart" },
            new[] { typeof(B3WM.Shared.Models.BarStorageItem), typeof(B3WM.Shared.Entity.VolumeLevel) });
    }

    [Fact]
    public void Bubble_DartCovered()
    {
        AssertDartCoveredByCSharp(
            new[] { "B3WM.Flutter/lib/models/bubble_storage_item.dart" },
            new[] { typeof(B3WM.Shared.Models.BubbleStorageItem) });
    }

    [Fact]
    public void Volume_DartCovered()
    {
        AssertDartCoveredByCSharp(
            new[]
            {
                "B3WM.Flutter/lib/models/volume_level.dart",
                "B3WM.Flutter/lib/models/volume_level_storage_item.dart",
            },
            new[] { typeof(B3WM.Shared.Entity.VolumeLevel), typeof(B3WM.Shared.Models.VolumeLevelStorageItem) });
    }

    [Fact]
    public void Structure_DartCovered()
    {
        AssertDartCoveredByCSharp(
            new[] { "B3WM.Flutter/lib/models/structure_storage_item.dart" },
            new[] { typeof(B3WM.Shared.Models.StructureStorageItem) });
    }

    [Fact]
    public void Extreme_DartCovered()
    {
        AssertDartCoveredByCSharp(
            new[] { "B3WM.Flutter/lib/models/extreme_storage_item.dart" },
            new[]
            {
                typeof(B3WM.Shared.Models.ExtremeStorageItem),
                typeof(B3WM.Shared.Models.ExtremeDetection.ExtremePoint),
                typeof(B3WM.Shared.Models.ExtremeDetection.ExtremeStatistics),
                typeof(B3WM.Shared.Models.ExtremeDetection.ExtremeDetectorOptions),
            });
    }

    [Fact]
    public void Pivot_DartCovered()
    {
        AssertDartCoveredByCSharp(
            new[] { "B3WM.Flutter/lib/models/pivot_storage_item.dart" },
            new[] { typeof(B3WM.Shared.Models.PivotStorageItem), typeof(B3WM.Shared.Models.PivotLevel) });
    }

    private static HashSet<string> PythonFields(string relPath, string className)
    {
        var text = File.ReadAllText(Path.Combine(RepoRoot(), relPath));
        var m = Regex.Match(text, $@"class {className}\(\w+\):(.*?)(?:\nclass |\Z)",
            RegexOptions.Singleline);
        Assert.True(m.Success, $"Classe {className} não achada em {relPath}");
        return Regex.Matches(m.Groups[1].Value, @"^\s{4}(\w+)\s*:",
                RegexOptions.Multiline)
            .Select(x => x.Groups[1].Value)
            .ToHashSet();
    }

    private static HashSet<string> DartSnakeKeys(params string[] relPaths)
    {
        var root = RepoRoot();
        var keys = new HashSet<string>();
        foreach (var rel in relPaths)
        {
            var text = File.ReadAllText(Path.Combine(root, rel));
            foreach (Match m in Regex.Matches(text, @"json\[['""]([a-z][a-z0-9_]*)['""]\]"))
                keys.Add(m.Groups[1].Value);
            foreach (Match m in Regex.Matches(text, @"'([a-z][a-z0-9_]*)'\s*:"))
                keys.Add(m.Groups[1].Value);
        }
        return keys;
    }

    [Fact]
    public void Trade_DartKeysExistInPython()
    {
        // Bridge MT5 é snake_case ponta a ponta (Dart -> forward cru -> pydantic).
        var py = new HashSet<string>();
        foreach (var cls in new[] { "MarketOrderRequest", "OrderResult", "CloseOrderRequest",
                     "ModifyOrderRequest", "AccountInfo", "PositionInfo", "OrderInfo", "HistoryDeal", "SymbolInfo" })
            py.UnionWith(PythonFields("B3WM.Python/models/order.py", cls));
        var dart = DartSnakeKeys(
            "B3WM.Flutter/lib/models/trade_models.dart",
            "B3WM.Flutter/lib/services/trading_service.dart");
        var missing = dart.Where(k => !py.Contains(k)).ToList();
        Assert.True(missing.Count == 0,
            $"Chaves snake_case no Flutter sem campo no Python: {string.Join(", ", missing)}");
    }

    [Fact]
    public void Trade_RequiredPythonFieldsSentByDart()
    {
        // Campos obrigatórios do pedido de mercado (sem default no pydantic:
        // symbol, volume, type) precisam sair no toJson do Dart — senão a
        // ordem quebra (422). Nota: cancelOrder manda position_ticket por
        // compatibilidade com o endpoint Python (nome do parâmetro Dart é
        // confuso, mas o fio está certo).
        var dart = DartSnakeKeys("B3WM.Flutter/lib/models/trade_models.dart");
        foreach (var k in new[] { "symbol", "volume", "type" })
            Assert.Contains(k, dart);
    }
}
