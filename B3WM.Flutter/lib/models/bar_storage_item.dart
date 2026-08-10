import 'volume_level.dart';

class BarStorageItem {
  final DateTime date;
  final String symbol;
  final int timeFrame;
  final double open;
  final double high;
  final double low;
  final double close;
  final int volume;
  final List<VolumeLevel>? volumeLevel;
  final double? forecastPrice;

  BarStorageItem({
    required this.date,
    required this.symbol,
    required this.timeFrame,
    required this.open,
    required this.high,
    required this.low,
    required this.close,
    required this.volume,
    this.volumeLevel,
    this.forecastPrice,
  });

  factory BarStorageItem.fromJson(Map<String, dynamic> json) {
    return BarStorageItem(
      date: DateTime.parse(json['date'] as String),
      symbol: json['symbol'] as String,
      timeFrame: json['timeFrame'] as int,
      open: (json['open'] as num).toDouble(),
      high: (json['high'] as num).toDouble(),
      low: (json['low'] as num).toDouble(),
      close: (json['close'] as num).toDouble(),
      volume: (json['volume'] as num).toInt(),
      volumeLevel: json['volumeLevel'] != null
          ? (json['volumeLevel'] as List)
              .map((e) => VolumeLevel.fromJson(e as Map<String, dynamic>))
              .toList()
          : null,
      forecastPrice: json['forecastPrice'] != null
          ? (json['forecastPrice'] as num).toDouble()
          : null,
    );
  }

  Map<String, dynamic> toJson() => {
        'date': date.toIso8601String(),
        'symbol': symbol,
        'timeFrame': timeFrame,
        'open': open,
        'high': high,
        'low': low,
        'close': close,
        'volume': volume,
        'volumeLevel': volumeLevel?.map((e) => e.toJson()).toList(),
        'forecastPrice': forecastPrice,
      };

  BarStorageItem copyWith({
    DateTime? date,
    String? symbol,
    int? timeFrame,
    double? open,
    double? high,
    double? low,
    double? close,
    int? volume,
    List<VolumeLevel>? volumeLevel,
    double? Function()? forecastPrice,
  }) {
    return BarStorageItem(
      date: date ?? this.date,
      symbol: symbol ?? this.symbol,
      timeFrame: timeFrame ?? this.timeFrame,
      open: open ?? this.open,
      high: high ?? this.high,
      low: low ?? this.low,
      close: close ?? this.close,
      volume: volume ?? this.volume,
      volumeLevel: volumeLevel ?? this.volumeLevel,
      forecastPrice: forecastPrice != null ? forecastPrice() : this.forecastPrice,
    );
  }
}
