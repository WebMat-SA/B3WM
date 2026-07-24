import 'bar_storage_item.dart';
import 'volume_level_storage_item.dart';
import 'adjustment_forecast_item.dart';

class ThrottlingData {
  final List<BarStorageItem> candle;
  final VolumeLevelStorageItem? volume;
  final AdjustmentForecastItem? forecast;

  ThrottlingData({
    required this.candle,
    this.volume,
    this.forecast,
  });

  factory ThrottlingData.fromJson(Map<String, dynamic> json) {
    return ThrottlingData(
      candle: (json['candle'] as List?)
              ?.map((e) => BarStorageItem.fromJson(e as Map<String, dynamic>))
              .toList() ??
          [],
      volume: json['volume'] != null
          ? VolumeLevelStorageItem.fromJson(json['volume'] as Map<String, dynamic>)
          : null,
      forecast: json['forecast'] != null
          ? AdjustmentForecastItem.fromJson(json['forecast'] as Map<String, dynamic>)
          : null,
    );
  }
}
