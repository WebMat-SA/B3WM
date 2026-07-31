class StructureStorageItem {
  final DateTime date;
  final String symbol;
  final int timeFrame;
  final double upBorder;
  final double downBorder;
  final double upAuxBorder;
  final double downAuxBorder;

  StructureStorageItem({
    required this.date,
    required this.symbol,
    required this.timeFrame,
    required this.upBorder,
    required this.downBorder,
    required this.upAuxBorder,
    required this.downAuxBorder,
  });

  factory StructureStorageItem.fromJson(Map<String, dynamic> json) {
    return StructureStorageItem(
      date: DateTime.parse(json['date'] as String),
      symbol: json['symbol'] as String,
      timeFrame: json['timeFrame'] as int,
      upBorder: (json['upBorder'] as num).toDouble(),
      downBorder: (json['downBorder'] as num).toDouble(),
      upAuxBorder: (json['upAuxBorder'] as num).toDouble(),
      downAuxBorder: (json['downAuxBorder'] as num).toDouble(),
    );
  }

  Map<String, dynamic> toJson() => {
        'date': date.toIso8601String(),
        'symbol': symbol,
        'timeFrame': timeFrame,
        'upBorder': upBorder,
        'downBorder': downBorder,
        'upAuxBorder': upAuxBorder,
        'downAuxBorder': downAuxBorder,
      };
}
