import 'volume_level.dart';
import 'bar_storage_item.dart';

class VolumeLevelStorageItem {
  final int id;
  final DateTime date;
  final String symbol;
  final int timeFrame;
  final List<VolumeLevel> volumes;

  VolumeLevelStorageItem({
    required this.id,
    required this.date,
    required this.symbol,
    required this.timeFrame,
    required this.volumes,
  });

  factory VolumeLevelStorageItem.fromJson(Map<String, dynamic> json) {
    return VolumeLevelStorageItem(
      id: json['id'] as int? ?? 0,
      date: DateTime.parse(json['date'] as String),
      symbol: json['symbol'] as String? ?? '',
      timeFrame: json['timeFrame'] as int? ?? 0,
      volumes: (json['volumes'] as List?)
              ?.map((e) => VolumeLevel.fromJson(e as Map<String, dynamic>))
              .toList() ??
          [],
    );
  }

  Map<String, dynamic> toJson() => {
        'id': id,
        'date': date.toIso8601String(),
        'symbol': symbol,
        'timeFrame': timeFrame,
        'volumes': volumes.map((e) => e.toJson()).toList(),
      };

  static List<VolumeLevel> fromTo(
      List<BarStorageItem> data, DateTime from, DateTime to) {
    final barTo = data.where((b) => b.date == to).firstOrNull;
    final barFrom = data.where((b) => b.date == from).firstOrNull;
    if (barTo == null || barFrom == null) return [];

    DateTime dateOnly(DateTime dt) => DateTime(dt.year, dt.month, dt.day);
    var result = barTo.volumeLevel ?? <VolumeLevel>[];
    final fd = dateOnly(from);
    final td = dateOnly(to);
    final grouped = data
        .where((b) {
          final bd = dateOnly(b.date);
          return !bd.isBefore(fd) && bd.isBefore(td);
        })
        .groupBy((b) => dateOnly(b.date));

    for (final entry in grouped.entries) {
      final lastEntry = entry.value
          .reduce((a, b) => a.date.compareTo(b.date) > 0 ? a : b);
      if (lastEntry.volumeLevel != null) {
        result = operation(result, lastEntry.volumeLevel!, 'Sum');
      }
    }

    result = operation(result, barFrom.volumeLevel ?? [], 'Diff');
    result.sort((a, b) => a.price.compareTo(b.price));
    return result;
  }

  static List<VolumeLevel> operation(
      List<VolumeLevel> vol1, List<VolumeLevel> vol2, String op) {
    final map = <double, VolumeLevel>{};
    for (final item in vol1) {
      final match = vol2.where((v) => v.price == item.price).firstOrNull;
      if (op == 'Sum') {
        map[item.price] = VolumeLevel(
          total: item.total + (match?.total ?? 0),
          buyVolume: item.buyVolume + (match?.buyVolume ?? 0),
          sellVolume: item.sellVolume + (match?.sellVolume ?? 0),
          price: item.price,
        );
      } else {
        map[item.price] = VolumeLevel(
          total: item.total - (match?.total ?? 0),
          buyVolume: item.buyVolume - (match?.buyVolume ?? 0),
          sellVolume: item.sellVolume - (match?.sellVolume ?? 0),
          price: item.price,
        );
      }
    }
    return map.values.toList();
  }
}

extension _ListGroupBy<K, V> on Iterable<V> {
  Map<K, List<V>> groupBy(K Function(V) keyFn) {
    final map = <K, List<V>>{};
    for (final item in this) {
      final key = keyFn(item);
      map.putIfAbsent(key, () => []).add(item);
    }
    return map;
  }
}
