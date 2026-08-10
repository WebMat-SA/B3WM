enum IndicatorPlotType { line, marker }

class IndicatorValue {
  final DateTime time;
  final String symbol;
  final String indicatorName;
  final String key;
  final double value;
  final IndicatorPlotType plotType;
  final int timeFrame;

  IndicatorValue({
    required this.time,
    required this.symbol,
    required this.indicatorName,
    required this.key,
    required this.value,
    this.plotType = IndicatorPlotType.line,
    required this.timeFrame,
  });

  factory IndicatorValue.fromJson(Map<String, dynamic> json) {
    return IndicatorValue(
      time: DateTime.parse(json['time'] as String),
      symbol: json['symbol'] as String? ?? '',
      indicatorName: json['indicatorName'] as String? ?? '',
      key: json['key'] as String? ?? '',
      value: (json['value'] as num).toDouble(),
      plotType: json['plotType']?.toString() == 'Marker'
          ? IndicatorPlotType.marker
          : IndicatorPlotType.line,
      timeFrame: json['timeFrame'] as int? ?? 0,
    );
  }
}
