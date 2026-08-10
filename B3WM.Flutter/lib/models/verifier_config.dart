class VerifierConfig {
  static const List<String> strategyTypes = ['SmartBreakout', 'Breakout'];

  String symbol;
  int timeFrame;
  String strategyName;
  int quantity;
  bool isDayTrade;
  String dayTradeCloseTime;
  int lookbackPeriod;
  int minDistance;
  double smartVolumePct;
  double smartStructureBufferPct;
  List<int>? smartAgents;
  int bubbleThreshold;
  Map<int, int>? agentThresholds;

  VerifierConfig({
    this.symbol = 'WINFUT',
    this.timeFrame = 2,
    this.strategyName = 'SmartBreakout',
    this.quantity = 1,
    this.isDayTrade = true,
    this.dayTradeCloseTime = '13:00',
    this.lookbackPeriod = 20,
    this.minDistance = 0,
    this.smartVolumePct = 0.3,
    this.smartStructureBufferPct = 0.1,
    this.smartAgents,
    this.bubbleThreshold = 0,
    this.agentThresholds,
  });

  factory VerifierConfig.fromJson(Map<String, dynamic> json) {
    return VerifierConfig(
      symbol: json['symbol'] as String? ?? 'WINFUT',
      timeFrame: (json['timeFrame'] as num?)?.toInt() ?? 2,
      strategyName: json['strategyName'] as String? ?? 'SmartBreakout',
      quantity: (json['quantity'] as num?)?.toInt() ?? 1,
      isDayTrade: json['isDayTrade'] as bool? ?? true,
      dayTradeCloseTime: json['dayTradeCloseTime'] as String? ?? '13:00',
      lookbackPeriod: (json['lookbackPeriod'] as num?)?.toInt() ?? 20,
      minDistance: (json['minDistance'] as num?)?.toInt() ?? 0,
      smartVolumePct: (json['smartVolumePct'] as num?)?.toDouble() ?? 0.3,
      smartStructureBufferPct:
          (json['smartStructureBufferPct'] as num?)?.toDouble() ?? 0.1,
      smartAgents: json['smartAgents'] != null
          ? (json['smartAgents'] as List).map((e) => (e as num).toInt()).toList()
          : null,
      bubbleThreshold: (json['bubbleThreshold'] as num?)?.toInt() ?? 0,
      agentThresholds: json['agentThresholds'] != null
          ? (json['agentThresholds'] as Map<String, dynamic>)
              .map((k, v) => MapEntry(int.tryParse(k) ?? 0, (v as num).toInt()))
          : null,
    );
  }

  Map<String, dynamic> toJson() => {
        'symbol': symbol,
        'timeFrame': timeFrame,
        'strategyName': strategyName,
        'quantity': quantity,
        'isDayTrade': isDayTrade,
        'dayTradeCloseTime': dayTradeCloseTime,
        'lookbackPeriod': lookbackPeriod,
        'minDistance': minDistance,
        'smartVolumePct': smartVolumePct,
        'smartStructureBufferPct': smartStructureBufferPct,
        'smartAgents': smartAgents,
        'bubbleThreshold': bubbleThreshold,
        'agentThresholds':
            agentThresholds?.map((k, v) => MapEntry(k.toString(), v)),
      };

  VerifierConfig copyWith({
    String? symbol,
    int? timeFrame,
    String? strategyName,
    int? quantity,
    bool? isDayTrade,
    String? dayTradeCloseTime,
    int? lookbackPeriod,
    int? minDistance,
    double? smartVolumePct,
    double? smartStructureBufferPct,
    List<int>? smartAgents,
    int? bubbleThreshold,
    Map<int, int>? agentThresholds,
  }) {
    return VerifierConfig(
      symbol: symbol ?? this.symbol,
      timeFrame: timeFrame ?? this.timeFrame,
      strategyName: strategyName ?? this.strategyName,
      quantity: quantity ?? this.quantity,
      isDayTrade: isDayTrade ?? this.isDayTrade,
      dayTradeCloseTime: dayTradeCloseTime ?? this.dayTradeCloseTime,
      lookbackPeriod: lookbackPeriod ?? this.lookbackPeriod,
      minDistance: minDistance ?? this.minDistance,
      smartVolumePct: smartVolumePct ?? this.smartVolumePct,
      smartStructureBufferPct:
          smartStructureBufferPct ?? this.smartStructureBufferPct,
      smartAgents: smartAgents ?? this.smartAgents,
      bubbleThreshold: bubbleThreshold ?? this.bubbleThreshold,
      agentThresholds: agentThresholds ?? this.agentThresholds,
    );
  }
}
