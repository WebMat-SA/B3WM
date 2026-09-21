import 'defaults.dart';

/// Configurações exclusivas do widget de análise diária (issue #12),
/// persistidas como bloco `daily:{}` dentro do `Config_$symbol`.
class DailyAnalysisConfig {
  /// Range DIÁRIO da estrutura (só o timeframe 1440). Independente do
  /// `SymbolConfig.structureRangeUpd` (intraday): um nunca toca no outro,
  /// nem na persistência (`daily:{}`) nem nas rotas do backend.
  double structureRangeUpd;
  bool structureVisible;
  bool structureAuxVisible;
  double structureOpacity;

  /// Volume Profile diário (fonte: perfil agregado do backend).
  bool profileVisible;
  double profileSizeH;
  double profileSizeV;
  double profileOpacity;

  /// Topos/vales do diário.
  bool extremeVisible;
  double extremeOpacity;
  double extremeNoiseSensitivity;
  double extremeMinimumProminence;

  /// Janela do perfil/extremos diários: 0 = âncora automática (última perna
  /// do 1440), senão número de dias (30/60/90).
  int windowDays;

  /// Painel split embutido (divide a tela com o gráfico principal).
  /// Fração da altura total ocupada pelo painel (divisória arrastável).
  bool panelVisible;
  double panelFraction;

  DailyAnalysisConfig({
    required this.structureRangeUpd,
    required this.structureVisible,
    required this.structureAuxVisible,
    required this.structureOpacity,
    required this.profileVisible,
    required this.profileSizeH,
    required this.profileSizeV,
    required this.profileOpacity,
    required this.extremeVisible,
    required this.extremeOpacity,
    required this.extremeNoiseSensitivity,
    required this.extremeMinimumProminence,
    required this.windowDays,
    required this.panelVisible,
    required this.panelFraction,
  });

  DailyAnalysisConfig.withDefaults(String symbol)
      : structureRangeUpd = Defaults.minDistanceUpdateBorderDaily(symbol),
        structureVisible = true,
        structureAuxVisible = true,
        structureOpacity = 0.8,
        profileVisible = true,
        profileSizeH = 1.0,
        profileSizeV = 1.0,
        profileOpacity = 0.5,
        extremeVisible = true,
        extremeOpacity = 0.5,
        extremeNoiseSensitivity = Defaults.extremeNoiseSensitivity,
        extremeMinimumProminence = Defaults.extremeMinimumProminence,
        windowDays = 0,
        panelVisible = false,
        panelFraction = 0.5;

  /// Lê o bloco `daily` aninhado; quando ausente (configs salvas pela
  /// issue #10), faz fallback para as chaves flat antigas.
  factory DailyAnalysisConfig.fromJson(Map<String, dynamic> json,
      {String symbol = ''}) {
    final nested = json['daily'] as Map<String, dynamic>?;
    final src = nested ?? json;
    double numOr(String key, double fallback) =>
        (src[key] as num?)?.toDouble() ?? fallback;
    return DailyAnalysisConfig(
      structureRangeUpd: (src['structureRangeUpd'] as num?)?.toDouble() ??
          (json['structureRangeUpdDaily'] as num?)?.toDouble() ??
          Defaults.minDistanceUpdateBorderDaily(symbol),
      structureVisible: src['structureVisible'] as bool? ??
          json['dailyStructureVisible'] as bool? ??
          true,
      structureAuxVisible: src['structureAuxVisible'] as bool? ?? true,
      structureOpacity: numOr('structureOpacity', 0.8),
      profileVisible: src['profileVisible'] as bool? ?? true,
      profileSizeH: numOr('profileSizeH', 1.0),
      profileSizeV: numOr('profileSizeV', 1.0),
      profileOpacity: numOr('profileOpacity', 0.5),
      extremeVisible: src['extremeVisible'] as bool? ??
          json['dailyExtremeVisible'] as bool? ??
          true,
      extremeOpacity: (src['extremeOpacity'] as num?)?.toDouble() ??
          (json['dailyExtremeOpacity'] as num?)?.toDouble() ??
          0.5,
      extremeNoiseSensitivity:
          (src['extremeNoiseSensitivity'] as num?)?.toDouble() ??
              (json['dailyExtremeNoiseSensitivity'] as num?)?.toDouble() ??
              Defaults.extremeNoiseSensitivity,
      extremeMinimumProminence:
          (src['extremeMinimumProminence'] as num?)?.toDouble() ??
              (json['dailyExtremeMinimumProminence'] as num?)?.toDouble() ??
              Defaults.extremeMinimumProminence,
      windowDays: (src['windowDays'] as num?)?.toInt() ?? 0,
      panelVisible: src['panelVisible'] as bool? ?? false,
      panelFraction:
          ((src['panelFraction'] as num?)?.toDouble() ?? 0.5).clamp(0.3, 0.7),
    );
  }

  Map<String, dynamic> toJson() => {
        'structureRangeUpd': structureRangeUpd,
        'structureVisible': structureVisible,
        'structureAuxVisible': structureAuxVisible,
        'structureOpacity': structureOpacity,
        'profileVisible': profileVisible,
        'profileSizeH': profileSizeH,
        'profileSizeV': profileSizeV,
        'profileOpacity': profileOpacity,
        'extremeVisible': extremeVisible,
        'extremeOpacity': extremeOpacity,
        'extremeNoiseSensitivity': extremeNoiseSensitivity,
        'extremeMinimumProminence': extremeMinimumProminence,
        'windowDays': windowDays,
        'panelVisible': panelVisible,
        'panelFraction': panelFraction,
      };
}
