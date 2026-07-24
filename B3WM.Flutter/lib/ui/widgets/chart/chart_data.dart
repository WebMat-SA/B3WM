import 'dart:ui' show Color;
import '../../../services/state_service.dart';
import '../../../models/ticks2.dart';
import '../../../models/indicator_value.dart';

class CandlePoint {
  final DateTime date;
  final double open;
  final double high;
  final double low;
  final double close;

  CandlePoint({
    required this.date,
    required this.open,
    required this.high,
    required this.low,
    required this.close,
  });

  bool get isUp => close >= open;
}

class BubblePoint {
  final double price;
  final double amount;
  final bool isBuy;
  final String agentName;
  final int candleIndex;

  BubblePoint({
    required this.price,
    required this.amount,
    required this.isBuy,
    required this.agentName,
    required this.candleIndex,
  });
}

class VolumeBarData {
  final double price;
  final double total;
  final double step;
  final bool isPoc;

  VolumeBarData({
    required this.price,
    required this.total,
    required this.step,
    required this.isPoc,
  });
}

class StructureLineData {
  final List<double?> upBorder;
  final List<double?> downBorder;
  final List<double?> upAuxBorder;
  final List<double?> downAuxBorder;
  final bool visible;
  final bool auxVisible;
  final double opacity;

  StructureLineData({
    required this.upBorder,
    required this.downBorder,
    required this.upAuxBorder,
    required this.downAuxBorder,
    required this.visible,
    required this.auxVisible,
    required this.opacity,
  });
}

class ForecastLineData {
  final List<double?> values;
  final double? currentVwap;
  final bool visible;

  ForecastLineData({
    required this.values,
    this.currentVwap,
    required this.visible,
  });
}

class IndicatorLineData {
  final List<double?> values;
  final String color;
  final double opacity;
  final bool visible;

  IndicatorLineData({
    required this.values,
    this.color = '#888888',
    this.opacity = 0.8,
    this.visible = true,
  });
}

class IndicatorMarkerData {
  final int index;
  final double value;
  final String color;
  final String label;
  final double size;
  final bool visible;

  IndicatorMarkerData({
    required this.index,
    required this.value,
    required this.color,
    this.label = '',
    this.size = 20,
    this.visible = true,
  });
}

class ChartData {
  final List<CandlePoint> candles;
  final List<BubblePoint> redBubbles;
  final List<BubblePoint> blueBubbles;
  final List<VolumeBarData> volumeProfile;
  final StructureLineData? structures;
  final ForecastLineData? forecast;
  final Map<String, IndicatorLineData> indicatorLines;
  final List<IndicatorMarkerData> indicatorMarkers;
  final double minPrice;
  final double maxPrice;
  final double lastPrice;
  final List<DateTime> dates;
  final List<int> daySeparatorIndices;
  final int rangeStart;
  final int rangeEnd;
  final String symbol;
  final int timeFrame;
  final DateTime? currentBarTime;
  final int remainingSeconds;
  final double bubbleOpacity;
  final double profileSizeH;
  final double profileSizeV;
  final double profileOpacity;
  final Color colorBuyer;
  final Color colorSeller;

  ChartData({
    required this.candles,
    required this.redBubbles,
    required this.blueBubbles,
    required this.volumeProfile,
    this.structures,
    this.forecast,
    this.indicatorLines = const {},
    this.indicatorMarkers = const [],
    required this.minPrice,
    required this.maxPrice,
    required this.lastPrice,
    required this.dates,
    this.daySeparatorIndices = const [],
    this.rangeStart = 0,
    this.rangeEnd = 0,
    this.symbol = '',
    this.timeFrame = 2,
    this.currentBarTime,
    this.remainingSeconds = 0,
    this.bubbleOpacity = 0.7,
    this.profileSizeH = 1.0,
    this.profileSizeV = 1.0,
    this.profileOpacity = 0.5,
    this.colorBuyer = const Color(0xFF4488ff),
    this.colorSeller = const Color(0xFFFF4444),
  });

  double get priceRange => maxPrice - minPrice;
}

ChartData buildChartData(StateService state) {
  final bars = state.barsTimeFrameFilter;
  final rangeStart = state.dateRangeStart.clamp(0, bars.length);
  final rangeEnd = state.dateRangeEnd > 0
      ? state.dateRangeEnd.clamp(0, bars.length)
      : bars.length;
  final visible = bars.sublist(rangeStart, rangeEnd);

  final candles = visible.map((b) => CandlePoint(
    date: b.date,
    open: b.open,
    high: b.high,
    low: b.low,
    close: b.close,
  )).toList();

  final dates = visible.map((b) => b.date).toList();
  final daySepIndices = <int>[];
  for (int i = 1; i < dates.length; i++) {
    if (dates[i].day != dates[i - 1].day) daySepIndices.add(i);
  }

  double _min4(double a, double b, double c, double d) =>
      a < b ? (a < c ? (a < d ? a : d) : (c < d ? c : d)) : (b < c ? (b < d ? b : d) : (c < d ? c : d));
  double _max4(double a, double b, double c, double d) =>
      a > b ? (a > c ? (a > d ? a : d) : (c > d ? c : d)) : (b > c ? (b > d ? b : d) : (c > d ? c : d));
  final minPrice = visible.fold<double>(double.infinity,
      (p, b) => _min4(p, b.low, b.close, b.open));
  final maxPrice = visible.fold<double>(double.negativeInfinity,
      (p, b) => _max4(p, b.high, b.close, b.open));

  int _findCandleIndex(DateTime bubbleDate) {
    for (int i = 0; i < visible.length; i++) {
      if (visible[i].date.compareTo(bubbleDate) >= 0) {
        return i;
      }
    }
    return visible.length - 1;
  }

  final redBubbles = <BubblePoint>[];
  final blueBubbles = <BubblePoint>[];
  if (state.bubbleVisible) {
    var bubbleSource = state.bubbles;
    if (state.bubbleAgentsFilter) {
      bubbleSource = bubbleSource
          .where((b) => state.selectedAgents.contains(b.agent))
          .toList();
    }
    if (state.bubbleAmountFilter) {
      bubbleSource = bubbleSource
          .where((b) => b.amount >= state.getThreshold(b.agent))
          .toList();
    }
    for (final b in bubbleSource) {
      final pt = BubblePoint(
        price: b.price,
        amount: b.amount * state.bubbleSize,
        isBuy: b.actionType == ActionType.buy,
        agentName: Agents.fromValue(b.agent)?.description ?? '',
        candleIndex: _findCandleIndex(b.date),
      );
      if (b.actionType == ActionType.buy) {
        blueBubbles.add(pt);
      } else {
        redBubbles.add(pt);
      }
    }
  }

  List<VolumeBarData> volumeProfile = [];
  if (state.profileVisible) {
    final vols = state.filteredVolumeLevels ?? state.volumeLevels;
    if (vols.isNotEmpty) {
      final maxVol = vols.fold<int>(0, (p, v) => v.total > p ? v.total : p);
      for (int i = 0; i < vols.length; i++) {
        final v = vols[i];
        final step = i < vols.length - 1
            ? (vols[i + 1].price - v.price).abs()
            : i > 0
                ? (v.price - vols[i - 1].price).abs()
                : state.profileSizeV;
        volumeProfile.add(VolumeBarData(
          price: v.price,
          total: v.total.toDouble(),
          step: step,
          isPoc: v.total >= maxVol,
        ));
      }
    }
  }

  StructureLineData? structures;
  if (state.structureVisible) {
    final sList = state.structuresTimeFrameFilter;
    if (sList.isNotEmpty) {
      final last = sList.last;
      structures = StructureLineData(
        upBorder: List.filled(visible.length, last.upBorder),
        downBorder: List.filled(visible.length, last.downBorder),
        upAuxBorder: List.filled(visible.length, last.upAuxBorder),
        downAuxBorder: List.filled(visible.length, last.downAuxBorder),
        visible: true,
        auxVisible: state.structureAuxVisible,
        opacity: state.structureOpacity,
      );
    }
  }

  ForecastLineData? forecast;
  if (state.forecastVisible && state.currentForecast != null) {
    forecast = ForecastLineData(
      values: state.forecastHistory.map((e) => e.$2).toList(),
      currentVwap: state.currentForecast!.vwap,
      visible: true,
    );
  }

  final indicatorLines = <String, IndicatorLineData>{};
  final indicatorMarkers = <IndicatorMarkerData>[];
  for (final entry in state.indicatorData.entries) {
    final lines = entry.value;
    final plotType = state.indicatorPlotType[entry.key] ?? IndicatorPlotType.line;
    if (plotType == IndicatorPlotType.line) {
      indicatorLines[entry.key] = IndicatorLineData(
        values: lines.map((e) => e.$2).toList(),
        color: '#888888',
        opacity: 0.8,
        visible: true,
      );
    } else {
      for (final l in lines) {
        if (l.$2 != null) {
          final idx = bars.indexWhere((b) => b.date == l.$1);
          if (idx >= 0) {
            indicatorMarkers.add(IndicatorMarkerData(
              index: idx - rangeStart,
              value: l.$2!,
              color: '#ffaa00',
              label: entry.key,
              visible: true,
            ));
          }
        }
      }
    }
  }

  return ChartData(
    candles: candles,
    redBubbles: redBubbles,
    blueBubbles: blueBubbles,
    volumeProfile: volumeProfile,
    structures: structures,
    forecast: forecast,
    indicatorLines: indicatorLines,
    indicatorMarkers: indicatorMarkers,
    minPrice: minPrice,
    maxPrice: maxPrice,
    lastPrice: visible.isNotEmpty ? visible.last.close : 0,
    dates: dates,
    daySeparatorIndices: daySepIndices,
    rangeStart: rangeStart,
    rangeEnd: rangeEnd,
    symbol: state.symbol,
    timeFrame: state.timeFrame,
    remainingSeconds: 0,
    bubbleOpacity: state.bubbleOpacity,
    profileSizeH: state.profileSizeH,
    profileSizeV: state.profileSizeV,
    profileOpacity: state.profileOpacity,
    colorBuyer: _parseHexColor(state.colorBuyer),
    colorSeller: _parseHexColor(state.colorSeller),
  );
}

Color _parseHexColor(String hex) {
  hex = hex.replaceAll('#', '');
  if (hex.length == 6) hex = 'FF$hex';
  return Color(int.parse(hex, radix: 16));
}
