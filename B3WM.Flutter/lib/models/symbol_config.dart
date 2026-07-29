import 'defaults.dart';

class SymbolConfig {
  int timeFrame;

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

  String colorBuyer;
  String colorSeller;

  List<int> selectedAgents;
  Map<int, int> agentThresholds;

  bool bubbleAmountFilter;
  bool bubbleAgentsFilter;
  bool bubbleSoundEnabled;

  SymbolConfig({
    required this.timeFrame,
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
    required this.colorBuyer,
    required this.colorSeller,
    required this.selectedAgents,
    required this.agentThresholds,
    required this.bubbleAmountFilter,
    required this.bubbleAgentsFilter,
    required this.bubbleSoundEnabled,
  });

  SymbolConfig.withDefaults(String symbol)
      : timeFrame = 2,
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
        colorBuyer = '#4488ff',
        colorSeller = '#ff4444',
        selectedAgents = [],
        agentThresholds = {},
        bubbleAmountFilter = true,
        bubbleAgentsFilter = true,
        bubbleSoundEnabled = true;

  factory SymbolConfig.fromJson(Map<String, dynamic> json) => SymbolConfig(
        timeFrame: json['timeFrame'] as int? ?? 2,
        bubbleVisible: json['bubbleVisible'] as bool? ?? true,
        bubbleSize: (json['bubbleSize'] as num?)?.toDouble() ?? 1.0,
        bubbleOpacity: (json['bubbleOpacity'] as num?)?.toDouble() ?? 0.7,
        bubbleSizeMin: (json['bubbleSizeMin'] as num?)?.toDouble() ?? 20,
        bubbleSizeMax: (json['bubbleSizeMax'] as num?)?.toDouble() ?? 100,
        thresholdBubble: json['thresholdBubble'] as int? ?? 250,
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
        structureRangeUpd:
            (json['structureRangeUpd'] as num?)?.toDouble() ?? 250,
        colorBuyer: json['colorBuyer'] as String? ?? '#4488ff',
        colorSeller: json['colorSeller'] as String? ?? '#ff4444',
        selectedAgents:
            (json['selectedAgents'] as List<dynamic>?)?.cast<int>() ?? [],
        agentThresholds: (json['agentThresholds'] as Map<String, dynamic>?)
                ?.map((k, v) => MapEntry(int.parse(k), v as int)) ??
            {},
        bubbleAmountFilter: json['bubbleAmountFilter'] as bool? ?? true,
        bubbleAgentsFilter: json['bubbleAgentsFilter'] as bool? ?? true,
        bubbleSoundEnabled: json['bubbleSoundEnabled'] as bool? ?? true,
      );

  Map<String, dynamic> toJson() => {
        'timeFrame': timeFrame,
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
        'colorBuyer': colorBuyer,
        'colorSeller': colorSeller,
        'selectedAgents': selectedAgents,
        'agentThresholds':
            agentThresholds.map((k, v) => MapEntry(k.toString(), v)),
        'bubbleAmountFilter': bubbleAmountFilter,
        'bubbleAgentsFilter': bubbleAgentsFilter,
        'bubbleSoundEnabled': bubbleSoundEnabled,
      };
}
