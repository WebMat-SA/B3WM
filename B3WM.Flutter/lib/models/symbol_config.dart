import 'defaults.dart';

enum DateRangeMode { intraday, multiDay }

class SymbolConfig {
  int timeFrame;

  DateRangeMode dateRangeMode;
  int lookbackDays;

  bool bubbleVisible;
  double bubbleSize;
  double bubbleOpacity;
  double bubbleSizeMin;
  double bubbleSizeMax;
  int thresholdBubble;

  bool profileVisible;
  double profileSizeH;
  double profileSizeV;
  double profileOpacity;
  bool profileAutoByPriceStructure;

  bool structureVisible;
  bool structureAuxVisible;
  double structureOpacity;
  double structureRangeUpd;

  /// Distanciamento de quebra de estrutura do 1440, governado pela seção
  /// diária (issue #10). Independente do structureRangeUpd intraday.
  double structureRangeUpdDaily;

  /// Exibe as bordas da estrutura 1440 sobre o gráfico (origem da janela).
  bool dailyStructureVisible;

  bool extremeVisible;
  double extremeOpacity;
  double extremeNoiseSensitivity;
  double extremeMinimumProminence;

  bool dailyExtremeVisible;
  double dailyExtremeOpacity;
  double dailyExtremeNoiseSensitivity;
  double dailyExtremeMinimumProminence;

  bool vwapVisible;
  double vwapOpacity;
  String vwapColor;

  String colorBuyer;
  String colorSeller;

  List<int> selectedAgents;
  Map<int, int> agentThresholds;

  /// Agentes que já apareceram ao menos uma vez (persistido). Usado para
  /// auto-selecionar apenas na primeira aparição e manter a lista estável.
  List<int> knownAgents;

  bool bubbleAmountFilter;
  bool bubbleAgentsFilter;
  bool bubbleSoundEnabled;
  double bubbleSoundVolume;

  bool tradingHistoryVisible;
  bool positionVisible;
  bool openOrdersVisible;

  bool tradingPanelVisible;
  bool tradingAccountExpanded;
  bool tradingOrdersExpanded;
  bool tradingPositionsExpanded;
  bool tradingHistoryExpanded;

  SymbolConfig({
    required this.timeFrame,
    required this.dateRangeMode,
    required this.lookbackDays,
    required this.bubbleVisible,
    required this.bubbleSize,
    required this.bubbleOpacity,
    required this.bubbleSizeMin,
    required this.bubbleSizeMax,
    required this.thresholdBubble,
    required this.profileVisible,
    required this.profileSizeH,
    required this.profileSizeV,
    required this.profileOpacity,
    required this.profileAutoByPriceStructure,
    required this.structureVisible,
    required this.structureAuxVisible,
    required this.structureOpacity,
    required this.structureRangeUpd,
    required this.structureRangeUpdDaily,
    required this.dailyStructureVisible,
    required this.extremeVisible,
    required this.extremeOpacity,
    required this.extremeNoiseSensitivity,
    required this.extremeMinimumProminence,
    required this.dailyExtremeVisible,
    required this.dailyExtremeOpacity,
    required this.dailyExtremeNoiseSensitivity,
    required this.dailyExtremeMinimumProminence,
    required this.vwapVisible,
    required this.vwapOpacity,
    required this.vwapColor,
    required this.colorBuyer,
    required this.colorSeller,
    required this.selectedAgents,
    required this.agentThresholds,
    required this.knownAgents,
    required this.bubbleAmountFilter,
    required this.bubbleAgentsFilter,
    required this.bubbleSoundEnabled,
    required this.bubbleSoundVolume,
    required this.tradingHistoryVisible,
    required this.positionVisible,
    required this.openOrdersVisible,
    required this.tradingPanelVisible,
    required this.tradingAccountExpanded,
    required this.tradingOrdersExpanded,
    required this.tradingPositionsExpanded,
    required this.tradingHistoryExpanded,
  });

  SymbolConfig.withDefaults(String symbol)
      : timeFrame = 2,
        dateRangeMode = DateRangeMode.intraday,
        lookbackDays = 5,
        bubbleVisible = true,
        bubbleSize = 1.0,
        bubbleOpacity = 0.7,
        bubbleSizeMin = 20,
        bubbleSizeMax = 100,
        thresholdBubble = Defaults.thresholdBubbleSize(symbol),
        profileVisible = true,
        profileSizeH = 1.0,
        profileSizeV = 1.0,
        profileOpacity = 0.5,
        profileAutoByPriceStructure = false,
        structureVisible = true,
        structureAuxVisible = true,
        structureOpacity = 0.8,
        structureRangeUpd = Defaults.minDistanceUpdateBorder(symbol),
        structureRangeUpdDaily =
            Defaults.minDistanceUpdateBorderDaily(symbol),
        dailyStructureVisible = true,
        extremeVisible = true,
        extremeOpacity = 0.7,
        extremeNoiseSensitivity = Defaults.extremeNoiseSensitivity,
        extremeMinimumProminence = Defaults.extremeMinimumProminence,
        dailyExtremeVisible = true,
        dailyExtremeOpacity = 0.5,
        dailyExtremeNoiseSensitivity = Defaults.extremeNoiseSensitivity,
        dailyExtremeMinimumProminence = Defaults.extremeMinimumProminence,
        vwapVisible = true,
        vwapOpacity = 0.5,
        vwapColor = '#FF8800',
        colorBuyer = '#4488ff',
        colorSeller = '#ff4444',
        selectedAgents = [],
        agentThresholds = {},
        knownAgents = [],
        bubbleAmountFilter = true,
        bubbleAgentsFilter = true,
        bubbleSoundEnabled = true,
        bubbleSoundVolume = 0.5,
        tradingHistoryVisible = true,
        positionVisible = true,
        openOrdersVisible = true,
        tradingPanelVisible = true,
        tradingAccountExpanded = false,
        tradingOrdersExpanded = false,
        tradingPositionsExpanded = false,
        tradingHistoryExpanded = false;

  /// Aplica as configs legadas (globais, anteriores à separação por símbolo)
  /// sobre os defaults per-symbol, respeitando os limites válidos de cada
  /// símbolo para campos dependentes dele (thresholdBubble, structureRangeUpd).
  factory SymbolConfig.seedFromLegacy(String symbol, SymbolConfig legacy) {
    final defaults = SymbolConfig.withDefaults(symbol);
    final thresholdMin = Defaults.thresholdBubbleSize(symbol);
    final structureMax = Defaults.structureRangeUpdMax(symbol);
    final structureDailyMax = Defaults.structureRangeUpdDailyMax(symbol);
    return SymbolConfig(
      timeFrame: legacy.timeFrame,
      dateRangeMode: legacy.dateRangeMode,
      lookbackDays: legacy.lookbackDays,
      bubbleVisible: legacy.bubbleVisible,
      bubbleSize: legacy.bubbleSize,
      bubbleOpacity: legacy.bubbleOpacity,
      bubbleSizeMin: legacy.bubbleSizeMin,
      bubbleSizeMax: legacy.bubbleSizeMax,
      thresholdBubble: legacy.thresholdBubble >= thresholdMin
          ? legacy.thresholdBubble
          : defaults.thresholdBubble,
      profileVisible: legacy.profileVisible,
      profileSizeH: legacy.profileSizeH,
      profileSizeV: legacy.profileSizeV,
      profileOpacity: legacy.profileOpacity,
      profileAutoByPriceStructure: legacy.profileAutoByPriceStructure,
      structureVisible: legacy.structureVisible,
      structureAuxVisible: legacy.structureAuxVisible,
      structureOpacity: legacy.structureOpacity,
      structureRangeUpd: legacy.structureRangeUpd >= 0 &&
              legacy.structureRangeUpd <= structureMax
          ? legacy.structureRangeUpd
          : defaults.structureRangeUpd,
      structureRangeUpdDaily: legacy.structureRangeUpdDaily >= 0 &&
              legacy.structureRangeUpdDaily <= structureDailyMax
          ? legacy.structureRangeUpdDaily
          : defaults.structureRangeUpdDaily,
      dailyStructureVisible: legacy.dailyStructureVisible,
      extremeVisible: defaults.extremeVisible,
      extremeOpacity: defaults.extremeOpacity,
      extremeNoiseSensitivity: defaults.extremeNoiseSensitivity,
      extremeMinimumProminence: defaults.extremeMinimumProminence,
      dailyExtremeVisible: defaults.dailyExtremeVisible,
      dailyExtremeOpacity: defaults.dailyExtremeOpacity,
      dailyExtremeNoiseSensitivity: defaults.dailyExtremeNoiseSensitivity,
      dailyExtremeMinimumProminence: defaults.dailyExtremeMinimumProminence,
      vwapVisible: defaults.vwapVisible,
      vwapOpacity: defaults.vwapOpacity,
      vwapColor: defaults.vwapColor,
      colorBuyer: legacy.colorBuyer,
      colorSeller: legacy.colorSeller,
      selectedAgents: List.from(legacy.selectedAgents),
      agentThresholds: Map.from(legacy.agentThresholds),
      knownAgents: List.from(legacy.knownAgents),
      bubbleAmountFilter: legacy.bubbleAmountFilter,
      bubbleAgentsFilter: legacy.bubbleAgentsFilter,
      bubbleSoundEnabled: legacy.bubbleSoundEnabled,
      bubbleSoundVolume: legacy.bubbleSoundVolume,
      tradingHistoryVisible: legacy.tradingHistoryVisible,
      positionVisible: legacy.positionVisible,
      openOrdersVisible: legacy.openOrdersVisible,
      tradingPanelVisible: legacy.tradingPanelVisible,
      tradingAccountExpanded: legacy.tradingAccountExpanded,
      tradingOrdersExpanded: legacy.tradingOrdersExpanded,
      tradingPositionsExpanded: legacy.tradingPositionsExpanded,
      tradingHistoryExpanded: legacy.tradingHistoryExpanded,
    );
  }

  factory SymbolConfig.fromJson(Map<String, dynamic> json, {String symbol = ''}) =>
      SymbolConfig(
        timeFrame: json['timeFrame'] as int? ?? 2,
        dateRangeMode: DateRangeMode.values.byName(json['dateRangeMode'] as String? ?? 'intraday'),
        lookbackDays: json['lookbackDays'] as int? ?? 5,
        bubbleVisible: json['bubbleVisible'] as bool? ?? true,
        bubbleSize: (json['bubbleSize'] as num?)?.toDouble() ?? 1.0,
        bubbleOpacity: (json['bubbleOpacity'] as num?)?.toDouble() ?? 0.7,
        bubbleSizeMin: (json['bubbleSizeMin'] as num?)?.toDouble() ?? 20,
        bubbleSizeMax: (json['bubbleSizeMax'] as num?)?.toDouble() ?? 100,
        thresholdBubble: json['thresholdBubble'] as int? ??
            Defaults.thresholdBubbleSize(symbol),
        profileVisible: json['profileVisible'] as bool? ?? true,
        profileSizeH: (json['profileSizeH'] as num?)?.toDouble() ?? 1.0,
        profileSizeV: (json['profileSizeV'] as num?)?.toDouble() ?? 1.0,
        profileOpacity: (json['profileOpacity'] as num?)?.toDouble() ?? 0.5,
        profileAutoByPriceStructure:
            json['profileAutoByPriceStructure'] as bool? ?? false,
        structureVisible: json['structureVisible'] as bool? ?? true,
        structureAuxVisible: json['structureAuxVisible'] as bool? ?? true,
        structureOpacity:
            (json['structureOpacity'] as num?)?.toDouble() ?? 0.8,
        structureRangeUpd: (json['structureRangeUpd'] as num?)?.toDouble() ??
            Defaults.minDistanceUpdateBorder(symbol),
        structureRangeUpdDaily:
            (json['structureRangeUpdDaily'] as num?)?.toDouble() ??
                Defaults.minDistanceUpdateBorderDaily(symbol),
        dailyStructureVisible:
            json['dailyStructureVisible'] as bool? ?? true,
        extremeVisible: json['extremeVisible'] as bool? ?? true,
        extremeOpacity: (json['extremeOpacity'] as num?)?.toDouble() ?? 0.7,
        extremeNoiseSensitivity:
            (json['extremeNoiseSensitivity'] as num?)?.toDouble() ??
                Defaults.extremeNoiseSensitivity,
        extremeMinimumProminence:
            (json['extremeMinimumProminence'] as num?)?.toDouble() ??
                Defaults.extremeMinimumProminence,
        dailyExtremeVisible: json['dailyExtremeVisible'] as bool? ?? true,
        dailyExtremeOpacity:
            (json['dailyExtremeOpacity'] as num?)?.toDouble() ?? 0.5,
        dailyExtremeNoiseSensitivity:
            (json['dailyExtremeNoiseSensitivity'] as num?)?.toDouble() ??
                Defaults.extremeNoiseSensitivity,
        dailyExtremeMinimumProminence:
            (json['dailyExtremeMinimumProminence'] as num?)?.toDouble() ??
                Defaults.extremeMinimumProminence,
        vwapVisible: json['vwapVisible'] as bool? ?? true,
        vwapOpacity: (json['vwapOpacity'] as num?)?.toDouble() ?? 0.5,
        vwapColor: json['vwapColor'] as String? ?? '#FF8800',
        colorBuyer: json['colorBuyer'] as String? ?? '#4488ff',
        colorSeller: json['colorSeller'] as String? ?? '#ff4444',
        selectedAgents:
            (json['selectedAgents'] as List<dynamic>?)?.cast<int>() ?? [],
        agentThresholds: (json['agentThresholds'] as Map<String, dynamic>?)
                ?.map((k, v) => MapEntry(int.parse(k), v as int)) ??
            {},
        knownAgents:
            (json['knownAgents'] as List<dynamic>?)?.cast<int>() ?? [],
        bubbleAmountFilter: json['bubbleAmountFilter'] as bool? ?? true,
        bubbleAgentsFilter: json['bubbleAgentsFilter'] as bool? ?? true,
        bubbleSoundEnabled: json['bubbleSoundEnabled'] as bool? ?? true,
        bubbleSoundVolume:
            (json['bubbleSoundVolume'] as num?)?.toDouble() ?? 0.5,
        tradingHistoryVisible: json['tradingHistoryVisible'] as bool? ?? true,
        positionVisible: json['positionVisible'] as bool? ?? true,
        openOrdersVisible: json['openOrdersVisible'] as bool? ?? true,
        tradingPanelVisible: json['tradingPanelVisible'] as bool? ?? true,
        tradingAccountExpanded:
            json['tradingAccountExpanded'] as bool? ?? false,
        tradingOrdersExpanded: json['tradingOrdersExpanded'] as bool? ?? false,
        tradingPositionsExpanded:
            json['tradingPositionsExpanded'] as bool? ?? false,
        tradingHistoryExpanded:
            json['tradingHistoryExpanded'] as bool? ?? false,
      );

  Map<String, dynamic> toJson() => {
        'timeFrame': timeFrame,
        'dateRangeMode': dateRangeMode.name,
        'lookbackDays': lookbackDays,
        'bubbleVisible': bubbleVisible,
        'bubbleSize': bubbleSize,
        'bubbleOpacity': bubbleOpacity,
        'bubbleSizeMin': bubbleSizeMin,
        'bubbleSizeMax': bubbleSizeMax,
        'thresholdBubble': thresholdBubble,
        'profileVisible': profileVisible,
        'profileSizeH': profileSizeH,
        'profileSizeV': profileSizeV,
        'profileOpacity': profileOpacity,
        'profileAutoByPriceStructure': profileAutoByPriceStructure,
        'structureVisible': structureVisible,
        'structureAuxVisible': structureAuxVisible,
        'structureOpacity': structureOpacity,
        'structureRangeUpd': structureRangeUpd,
        'structureRangeUpdDaily': structureRangeUpdDaily,
        'dailyStructureVisible': dailyStructureVisible,
        'extremeVisible': extremeVisible,
        'extremeOpacity': extremeOpacity,
        'extremeNoiseSensitivity': extremeNoiseSensitivity,
        'extremeMinimumProminence': extremeMinimumProminence,
        'dailyExtremeVisible': dailyExtremeVisible,
        'dailyExtremeOpacity': dailyExtremeOpacity,
        'dailyExtremeNoiseSensitivity': dailyExtremeNoiseSensitivity,
        'dailyExtremeMinimumProminence': dailyExtremeMinimumProminence,
        'vwapVisible': vwapVisible,
        'vwapOpacity': vwapOpacity,
        'vwapColor': vwapColor,
        'colorBuyer': colorBuyer,
        'colorSeller': colorSeller,
        'selectedAgents': selectedAgents,
        'agentThresholds':
            agentThresholds.map((k, v) => MapEntry(k.toString(), v)),
        'knownAgents': knownAgents,
        'bubbleAmountFilter': bubbleAmountFilter,
        'bubbleAgentsFilter': bubbleAgentsFilter,
        'bubbleSoundEnabled': bubbleSoundEnabled,
        'bubbleSoundVolume': bubbleSoundVolume,
        'tradingHistoryVisible': tradingHistoryVisible,
        'positionVisible': positionVisible,
        'openOrdersVisible': openOrdersVisible,
        'tradingPanelVisible': tradingPanelVisible,
        'tradingAccountExpanded': tradingAccountExpanded,
        'tradingOrdersExpanded': tradingOrdersExpanded,
        'tradingPositionsExpanded': tradingPositionsExpanded,
        'tradingHistoryExpanded': tradingHistoryExpanded,
      };
}
