using System.Text.RegularExpressions;

namespace B3WM.Tests;

/// <summary>
/// Trava anti-drift dos filtros: toda chave do `SymbolConfig.toJson()` do
/// Flutter (incluindo o bloco `daily`) precisa estar classificada em
/// `ScreenSpecKeys.Handled` (lida em algum lugar) ou `.Cosmetic` (só
/// estética, ignorada de propósito). Filtro novo no app → este teste falha
/// até a chave ser classificada em 1 lugar.
/// </summary>
public class ScreenSpecKeysTest
{
    [Fact]
    public void AllDartKeys_AreClassified()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        var top = ExtractToJsonKeys(Path.Combine(repoRoot, "B3WM.Flutter", "lib", "models", "symbol_config.dart"));
        var daily = ExtractToJsonKeys(Path.Combine(repoRoot, "B3WM.Flutter", "lib", "models", "daily_analysis_config.dart"));
        var all = top.Concat(daily).Distinct().ToList();

        Assert.True(all.Count > 20, "Não achei as chaves do Dart — parser quebrou?");
        var unknown = all
            .Where(k => !B3WM.Services.Screen.ScreenSpecKeys.Handled.Contains(k) &&
                        !B3WM.Services.Screen.ScreenSpecKeys.Cosmetic.Contains(k))
            .ToList();
        Assert.True(unknown.Count == 0,
            "Chaves do Flutter sem classificação em ScreenSpecKeys: " + string.Join(", ", unknown));
    }

    /// <summary>Extrai as chaves de primeiro nível dos mapas toJson() ('key': ...).</summary>
    private static List<string> ExtractToJsonKeys(string path)
    {
        Assert.True(File.Exists(path), $"Arquivo Dart não encontrado: {path}");
        var text = File.ReadAllText(path);
        // Pega a região do toJson() até o fim do mapa (primeiro "};" no nível).
        var idx = text.IndexOf("toJson()", StringComparison.Ordinal);
        Assert.True(idx >= 0, $"toJson() não achado em {path}");
        var region = text[idx..Math.Min(idx + 4000, text.Length)];
        return Regex.Matches(region, @"'([a-zA-Z0-9_]+)'\s*:")
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToList();
    }
}
