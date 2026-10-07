import '../models/ticks2.dart';

extension DateTimeExtensions on DateTime {
  DateTime getCandleStart(int timeFrameMinutes) {
    if (timeFrameMinutes <= 0) {
      throw ArgumentError('TimeFrame must be greater than zero');
    }
    final totalMinutes = hour * 60 + minute;
    final candleStartMinutes = (totalMinutes ~/ timeFrameMinutes) * timeFrameMinutes;
    return DateTime(year, month, day, candleStartMinutes ~/ 60, candleStartMinutes % 60);
  }

  Duration getRemainingTimeCandle(DateTime currentTime, int timeFrameMinutes) {
    if (timeFrameMinutes <= 0) {
      throw ArgumentError('TimeFrame must be greater than zero');
    }
    final totalMinutes = hour * 60 + minute;
    final candleStartMinutes = (totalMinutes ~/ timeFrameMinutes) * timeFrameMinutes;
    final candleEndMinutes = candleStartMinutes + timeFrameMinutes;
    final candleEnd = DateTime(year, month, day, candleEndMinutes ~/ 60, candleEndMinutes % 60);
    return candleEnd.difference(currentTime);
  }
}

extension AgentsExtension on int {
  String agentDescription() {
    // This is handled by the Agents class directly in ticks2.dart
    return agentsDescription(this);
  }
}

String agentsDescription(int agentValue) {
  // Fonte única: Agents em ticks2.dart (antes duplicava o mapa aqui).
  return Agents.fromValue(agentValue)?.description ?? 'Desconhecido';
}
