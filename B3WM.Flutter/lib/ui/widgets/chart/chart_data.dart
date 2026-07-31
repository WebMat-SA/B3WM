import 'package:flutter/foundation.dart';
import 'dart:ui' show Color;
import '../../../services/state_service.dart';
import '../../../models/ticks2.dart';
import '../../../models/trade_models.dart';

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
  final double originalAmount;
  final bool isBuy;
  final String agentName;
  final int candleIndex;
  final DateTime date;

  BubblePoint({
    required this.price,
    required this.amount,
    required this.originalAmount,
    required this.isBuy,
    required this.agentName,
    required this.candleIndex,
    required this.date,
  });
}

class VolumeBarData {
  final double price;
  final double total;
  final double step;
  final bool isPoc;
  final double delta;

  VolumeBarData({
    required this.price,
    required this.total,
    required this.step,
    required this.isPoc,
    this.delta = 0,
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

class HistoryDealPoint {
  final int candleIndex;
  final double price;
  final bool isBuy;
  final HistoryDeal deal;

  HistoryDealPoint({
    required this.candleIndex,
    required this.price,
    required this.isBuy,
    required this.deal,
  });
}

class ChartData {
  final List<CandlePoint> candles;
  final List<BubblePoint> redBubbles;
  final List<BubblePoint> blueBubbles;
  final List<VolumeBarData> volumeProfile;
  final StructureLineData? structures;
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
  final double yZoom;
  final double bubbleSizeMin;
  final double bubbleSizeMax;
  final List<PositionInfo> positions;
  final List<OrderInfo> orders;
  final List<HistoryDealPoint> historyPoints;

  ChartData({
    required this.candles,
    required this.redBubbles,
    required this.blueBubbles,
    required this.volumeProfile,
    this.structures,
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
    this.yZoom = 1.0,
    this.bubbleSizeMin = 20,
    this.bubbleSizeMax = 100,
    this.positions = const [],
    this.orders = const [],
    this.historyPoints = const [],
  });

  double get priceRange => maxPrice - minPrice;
}

String _formatCandleKey(DateTime dt) {
  return '${dt.day.toString().padLeft(2, '0')}/${dt.month.toString().padLeft(2, '0')} '
      '${dt.hour.toString().padLeft(2, '0')}:${dt.minute.toString().padLeft(2, '0')}';
}

Map<String, int> _buildDateLookup(List<DateTime> dates) {
  final lookup = <String, int>{};
  for (int i = 0; i < dates.length; i++) {
    final key = _formatCandleKey(dates[i]);
    if (!lookup.containsKey(key)) lookup[key] = i;
  }
  return lookup;
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

  double min4(double a, double b, double c, double d) =>
      a < b ? (a < c ? (a < d ? a : d) : (c < d ? c : d)) : (b < c ? (b < d ? b : d) : (c < d ? c : d));
  double max4(double a, double b, double c, double d) =>
      a > b ? (a > c ? (a > d ? a : d) : (c > d ? c : d)) : (b > c ? (b > d ? b : d) : (c > d ? c : d));
  final minPrice = visible.fold<double>(double.infinity,
      (p, b) => min4(p, b.low, b.close, b.open));
  final maxPrice = visible.fold<double>(double.negativeInfinity,
      (p, b) => max4(p, b.high, b.close, b.open));

  final candleDateLookup = _buildDateLookup(dates);

  int findCandleIndex(DateTime bubbleDate) {
    final totalMinutes = bubbleDate.hour * 60 + bubbleDate.minute;
    final candleStartMin = (totalMinutes ~/ state.timeFrame) * state.timeFrame;
    final candleStartKey = _formatCandleKey(DateTime(
      bubbleDate.year, bubbleDate.month, bubbleDate.day,
      candleStartMin ~/ 60, candleStartMin % 60,
    ));
    final exact = candleDateLookup[candleStartKey];
    if (exact != null) return exact;
    for (int i = visible.length - 1; i >= 0; i--) {
      if (visible[i].date.compareTo(bubbleDate) <= 0) return i;
    }
    return 0;
  }

  final redBubbles = <BubblePoint>[];
  final blueBubbles = <BubblePoint>[];
  if (state.bubbleVisible) {
    var bubbleSource = state.bubbles
        .where((b) => b.amount >= state.getThreshold(b.agent))
        .toList();
    if (state.selectedAgents.isNotEmpty) {
      bubbleSource = bubbleSource
          .where((b) => state.selectedAgents.contains(b.agent))
          .toList();
    }
    for (final b in bubbleSource) {
      final pt = BubblePoint(
        price: b.price,
        amount: b.amount * state.bubbleSize,
        originalAmount: b.amount,
        isBuy: b.actionType == ActionType.buy,
        agentName: Agents.fromValue(b.agent)?.description ?? '',
        candleIndex: findCandleIndex(b.date),
        date: b.date,
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
          delta: v.delta.toDouble(),
        ));
      }
    }
  }

  StructureLineData? structures;
  if (state.structureVisible) {
    final sList = state.structuresTimeFrameFilter;
    if (sList.isNotEmpty) {
      final up = List<double?>.filled(visible.length, null);
      final down = List<double?>.filled(visible.length, null);
      final upAux = List<double?>.filled(visible.length, null);
      final downAux = List<double?>.filled(visible.length, null);

      for (final s in sList) {
        final key = _formatCandleKey(s.date);
        final idx = candleDateLookup[key];
        if (idx != null && idx < visible.length) {
          up[idx] = s.upBorder;
          down[idx] = s.downBorder;
          upAux[idx] = s.upAuxBorder;
          downAux[idx] = s.downAuxBorder;
        }
      }

      structures = StructureLineData(
        upBorder: up,
        downBorder: down,
        upAuxBorder: upAux,
        downAuxBorder: downAux,
        visible: true,
        auxVisible: state.structureAuxVisible,
        opacity: state.structureOpacity,
      );
    }
  }

  int calcRemainingSeconds() {
    if (state.currentBar == null || state.timeFrame <= 0) return 0;
    final totalMinutes = state.currentBar!.date.hour * 60 + state.currentBar!.date.minute;
    final candleStartMin = (totalMinutes ~/ state.timeFrame) * state.timeFrame;
    final candleEndMin = candleStartMin + state.timeFrame;
    final now = DateTime.now();
    final candleEnd = DateTime(
      state.currentBar!.date.year,
      state.currentBar!.date.month,
      state.currentBar!.date.day,
      candleEndMin ~/ 60,
      candleEndMin % 60,
    );
    return candleEnd.difference(now).inSeconds.clamp(0, 9999);
  }

  final positions = state.positions
      .where((p) => _normalizeTradeSymbol(p.symbol) == state.symbol)
      .toList();
  final orders = state.orders
      .where((o) => _normalizeTradeSymbol(o.symbol) == state.symbol)
      .toList();

  final historyPoints = <HistoryDealPoint>[];
  if (state.timeFrame > 0) {
    for (final deal in state.history) {
      if (_normalizeTradeSymbol(deal.symbol) != state.symbol) continue;
      if (deal.price <= 0) continue;
      final dealDate = DateTime.tryParse(deal.time);
      if (dealDate == null) continue;
      final totalMinutes = dealDate.hour * 60 + dealDate.minute;
      final candleStartMin = (totalMinutes ~/ state.timeFrame) * state.timeFrame;
      final candleStartKey = _formatCandleKey(DateTime(
        dealDate.year, dealDate.month, dealDate.day,
        candleStartMin ~/ 60, candleStartMin % 60,
      ));
      final idx = candleDateLookup[candleStartKey];
      if (idx == null || idx >= visible.length) continue;
      historyPoints.add(HistoryDealPoint(
        candleIndex: idx,
        price: deal.price,
        isBuy: deal.type == 'buy',
        deal: deal,
      ));
    }
  }

  return ChartData(
    candles: candles,
    redBubbles: redBubbles,
    blueBubbles: blueBubbles,
    volumeProfile: volumeProfile,
    structures: structures,
    minPrice: minPrice,
    maxPrice: maxPrice,
    lastPrice: visible.isNotEmpty ? visible.last.close : 0,
    dates: dates,
    daySeparatorIndices: daySepIndices,
    rangeStart: rangeStart,
    rangeEnd: rangeEnd,
    symbol: state.symbol,
    timeFrame: state.timeFrame,
    remainingSeconds: calcRemainingSeconds(),
    bubbleOpacity: state.bubbleOpacity,
    profileSizeH: state.profileSizeH,
    profileSizeV: state.profileSizeV,
    profileOpacity: state.profileOpacity,
    colorBuyer: _parseHexColor(state.colorBuyer),
    colorSeller: _parseHexColor(state.colorSeller),
    yZoom: state.yZoom,
    bubbleSizeMin: state.bubbleSizeMin,
    bubbleSizeMax: state.bubbleSizeMax,
    positions: positions,
    orders: orders,
    historyPoints: historyPoints,
  );
}

String _normalizeTradeSymbol(String mt5Symbol) {
  final s = mt5Symbol.toUpperCase();
  if (s.startsWith('WIN')) return 'WINFUT';
  if (s.startsWith('WDO')) return 'WDOFUT';
  return s;
}

Color _parseHexColor(String hex) {
  hex = hex.replaceAll('#', '');
  if (hex.length == 6) hex = 'FF$hex';
  return Color(int.parse(hex, radix: 16));
}
