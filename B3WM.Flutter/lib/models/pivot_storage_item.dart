/// Snapshot estático do Pivot Tradicional (issue #14), espelhando o
/// `ExtremeStorageItem`: o backend calcula de forma pura (sem estado ao
/// vivo) e o app só exibe linhas horizontais do dia atual/último pregão.
class PivotLevel {
  final String key;
  final double value;

  PivotLevel({required this.key, required this.value});

  factory PivotLevel.fromJson(Map<String, dynamic> json) => PivotLevel(
        key: json['key'] as String? ?? '',
        value: (json['value'] as num?)?.toDouble() ?? 0,
      );
}

class PivotStorageItem {
  final String symbol;
  final DateTime? date;
  final String source;
  final double high;
  final double low;
  final double close;
  final int lineCount;
  final List<PivotLevel> levels;

  PivotStorageItem({
    this.symbol = '',
    this.date,
    this.source = '',
    this.high = 0,
    this.low = 0,
    this.close = 0,
    this.lineCount = 2,
    this.levels = const [],
  });

  double levelValue(String key) {
    for (final l in levels) {
      if (l.key == key) return l.value;
    }
    return double.nan;
  }

  factory PivotStorageItem.fromJson(Map<String, dynamic> json) =>
      PivotStorageItem(
        symbol: json['symbol'] as String? ?? '',
        date: json['date'] != null
            ? DateTime.tryParse(json['date'] as String)
            : null,
        source: json['source'] as String? ?? '',
        high: (json['high'] as num?)?.toDouble() ?? 0,
        low: (json['low'] as num?)?.toDouble() ?? 0,
        close: (json['close'] as num?)?.toDouble() ?? 0,
        lineCount: (json['lineCount'] as num?)?.toInt() ?? 2,
        levels: (json['levels'] as List<dynamic>? ?? [])
            .map((e) => PivotLevel.fromJson(e as Map<String, dynamic>))
            .toList(),
      );
}
