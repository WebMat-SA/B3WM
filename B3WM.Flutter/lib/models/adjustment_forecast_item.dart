class AdjustmentForecastItem {
  final double vwap;
  final DateTime time;
  final String symbol;
  final double sumPv;
  final int sumV;
  final DateTime day;

  AdjustmentForecastItem({
    required this.vwap,
    required this.time,
    required this.symbol,
    required this.sumPv,
    required this.sumV,
    required this.day,
  });

  factory AdjustmentForecastItem.fromJson(Map<String, dynamic> json) {
    return AdjustmentForecastItem(
      vwap: (json['vwap'] as num).toDouble(),
      time: DateTime.parse(json['time'] as String),
      symbol: json['symbol'] as String? ?? '',
      sumPv: (json['sumPv'] as num?)?.toDouble() ?? 0,
      sumV: (json['sumV'] as num?)?.toInt() ?? 0,
      day: DateTime.parse(json['day'] as String),
    );
  }
}
