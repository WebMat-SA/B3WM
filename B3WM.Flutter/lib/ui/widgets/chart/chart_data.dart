import 'dart:math' as math;
import 'dart:ui' show Color;
import '../../../app_log.dart';
import '../../../services/state_service.dart';
import '../../../services/strategy_service.dart' show b3Wall;
import '../../../models/ticks2.dart';
import '../../../models/trade_models.dart';
import '../../../models/extreme_storage_item.dart';

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

class VwapPoint {
  final double price;
  final DateTime date;
  final int startCandleIndex;
  final int endCandleIndex;

  VwapPoint({
    required this.price,
    required this.date,
    required this.startCandleIndex,
    required this.endCandleIndex,
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

/// Chamada de estratégia posicionada no X (faixa timeline ⓘ).
class StrategyPoint {
  final int candleIndex;
  final DateTime at;
  final String kind; // avaliacao | execucao
  final String side;
  final double confidence;
  final double encerrar;
  final String action;
  final String reason;
  final String snapshotHash;

  StrategyPoint({
    required this.candleIndex,
    required this.at,
    required this.kind,
    required this.side,
    required this.confidence,
    required this.encerrar,
    required this.action,
    required this.reason,
    required this.snapshotHash,
  });
}

class ExtremeLineData {
  final List<double> topPrices;
  final List<double> valleyPrices;
  final bool visible;
  final double opacity;

  ExtremeLineData({
    required this.topPrices,
    required this.valleyPrices,
    required this.visible,
    required this.opacity,
  });
}

/// Níveis do Pivot Tradicional (issue #14): linhas horizontais estáticas do
/// dia atual/último pregão (P, R1..R5, S1..S5). Desenho sólido com cores
/// próprias para não confundir com o tracejado de topos/vales.
class PivotLineData {
  final double pivot;
  final List<double> resistances;
  final List<double> supports;
  final bool visible;
  final double opacity;

  PivotLineData({
    required this.pivot,
    required this.resistances,
    required this.supports,
    required this.visible,
    required this.opacity,
  });

  /// Ordena por chave (P, R1..R5, S1..S5) a partir dos níveis do backend.
  factory PivotLineData.fromLevels(
    List<({String key, double value})> levels, {
    required bool visible,
    required double opacity,
  }) {
    double pivot = double.nan;
    final resistances = <double>[];
    final supports = <double>[];
    for (final l in levels) {
      if (l.key == 'P') {
        pivot = l.value;
      } else if (l.key.startsWith('R')) {
        resistances.add(l.value);
      } else if (l.key.startsWith('S')) {
        supports.add(l.value);
      }
    }
    resistances.sort();
    supports.sort();
    return PivotLineData(
      pivot: pivot,
      resistances: resistances,
      supports: supports,
      visible: visible,
      opacity: opacity,
    );
  }
}

class ChartData {
  final List<CandlePoint> candles;
  final List<BubblePoint> redBubbles;
  final List<BubblePoint> blueBubbles;
  final List<VolumeBarData> volumeProfile;
  final StructureLineData? structures;
  final ExtremeLineData? extremes;
  final PivotLineData? pivots;
  final List<VwapPoint> vwapPoints;
  final double minPrice;
  final double maxPrice;
  final double lastPrice;
  final List<DateTime> dates;
  final List<int> daySeparatorIndices;
  final int rangeStart;
  final int rangeEnd;
  final bool profileVisible;
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
  /// Menor e maior volume (original) entre as bolhas visíveis. Usados para
  /// normalizar o raio em [bubbleSizeMin, bubbleSizeMax].
  final double bubbleAmountMin;
  final double bubbleAmountMax;
  final double vwapOpacity;
  final Color vwapColor;
  final List<PositionInfo> positions;
  final List<OrderInfo> orders;
  final List<HistoryDealPoint> historyPoints;
  final List<StrategyPoint> strategyPoints;
  final bool tradingHistoryVisible;
  final bool positionVisible;
  final bool openOrdersVisible;

  ChartData({
    required this.candles,
    required this.redBubbles,
    required this.blueBubbles,
    required this.volumeProfile,
    this.structures,
    this.extremes,
    this.pivots,
    required this.vwapPoints,
    required this.minPrice,
    required this.maxPrice,
    required this.lastPrice,
    required this.dates,
    this.daySeparatorIndices = const [],
    this.rangeStart = 0,
    this.rangeEnd = 0,
    this.profileVisible = true,
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
    this.bubbleAmountMin = 0,
    this.bubbleAmountMax = 0,
    this.vwapOpacity = 0.5,
    this.vwapColor = const Color(0xFFFF8800),
    this.positions = const [],
    this.orders = const [],
    this.historyPoints = const [],
    this.strategyPoints = const [],
    this.tradingHistoryVisible = true,
    this.positionVisible = true,
    this.openOrdersVisible = true,
  });

  double get priceRange => maxPrice - minPrice;

  /// Raio (px, em coords do canvas virtual) da bolha de [amount].
  ///
  /// Normaliza `sqrt(amount)` de `[sqrt(bubbleAmountMin), sqrt(bubbleAmountMax)]`
  /// para `[bubbleSizeMin, bubbleSizeMax]`, de modo que a menor bolha visível
  /// tende ao Min e a maior ao Max — sem saturar as pontas. Nunca lança: os
  /// limites são ordenados internamente e o fator é clampado em [0, 1].
  double bubbleRadius(double amount) {
    final lo = math.min(bubbleSizeMin, bubbleSizeMax);
    final hi = math.max(bubbleSizeMin, bubbleSizeMax);
    if (bubbleAmountMax <= bubbleAmountMin || amount <= 0) return hi;
    final smin = math.sqrt(bubbleAmountMin);
    final smax = math.sqrt(bubbleAmountMax);
    if (smax <= smin) return hi;
    final t = ((math.sqrt(amount) - smin) / (smax - smin)).clamp(0.0, 1.0);
    return lo + t * (hi - lo);
  }
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
  final visible = bars;

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
  var bubbleAmountMin = 0.0;
  var bubbleAmountMax = 0.0;
  if (state.bubbleVisible) {
    var bubbleSource = state.bubbles;
    if (state.bubbleAmountFilter) {
      bubbleSource = bubbleSource
          .where((b) => b.amount >= state.getThreshold(b.agent))
          .toList();
    }
    if (state.bubbleAgentsFilter) {
      bubbleSource = bubbleSource
          .where((b) => state.selectedAgents.contains(b.agent))
          .toList();
    }
    for (final b in bubbleSource) {
      final pt = BubblePoint(
        price: b.price,
        amount: b.amount,
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
    final all = [...redBubbles, ...blueBubbles];
    if (all.isNotEmpty) {
      bubbleAmountMin = all.first.amount;
      bubbleAmountMax = all.first.amount;
      for (final b in all) {
        if (b.amount < bubbleAmountMin) bubbleAmountMin = b.amount;
        if (b.amount > bubbleAmountMax) bubbleAmountMax = b.amount;
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
        if (v.total <= 0) continue;
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

  ExtremeLineData? extremes;
  if (state.extremeVisible) {
    final ex = state.extremes;
    if (ex != null && ex.extremes.isNotEmpty) {
      extremes = ExtremeLineData(
        topPrices: ex.extremes
            .where((e) => e.type == ExtremeType.top)
            .map((e) => e.position)
            .toList(),
        valleyPrices: ex.extremes
            .where((e) => e.type == ExtremeType.valley)
            .map((e) => e.position)
            .toList(),
        visible: true,
        opacity: state.extremeOpacity,
      );
    }
  }

  PivotLineData? pivots;
  if (state.pivotVisible) {
    final pv = state.pivotIntraday;
    if (pv != null && pv.levels.isNotEmpty) {
      pivots = PivotLineData.fromLevels(
        [for (final l in pv.levels) (key: l.key, value: l.value)],
        visible: true,
        opacity: state.pivotOpacity,
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

  final positions = state.positionVisible
      ? state.positions
          .where((p) => _normalizeTradeSymbol(p.symbol) == state.symbol)
          .toList()
      : <PositionInfo>[];
  final orders = state.openOrdersVisible
      ? state.orders
          .where((o) => _normalizeTradeSymbol(o.symbol) == state.symbol)
          .toList()
      : <OrderInfo>[];

  final historyPoints = <HistoryDealPoint>[];
  if (state.tradingHistoryVisible && state.timeFrame > 0) {
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

  // Timeline de estratégias: At -> candleIndex (mesmo bucket de findCandleIndex).
  // candle_close avalia no FECHAMENTO (= open seguinte), então recua 1s para
  // cair no candle avaliado; demais eventos usam o At direto. Fora do range
  // carregado o ponto é descartado (sem pilha no índice 0).
  final strategyPoints = <StrategyPoint>[];
  final timeline = state.strategyTimelineItems;
  if (state.strategyTimelineVisible &&
      timeline.isNotEmpty &&
      visible.isNotEmpty &&
      state.timeFrame > 0) {
    final first = visible.first.date;
    final lastEnd = visible.last.date
        .add(Duration(minutes: state.timeFrame));
    for (final t in timeline) {
      // Tudo em parede BRT (imune ao fuso da máquina): At pode ser absoluto
      // (com offset) ou parede BRT (legado/backend sem offset) — b3Wall
      // normaliza; as barras já são parede BRT.
      final wall = b3Wall(t.at);
      if (wall.isBefore(first) || wall.isAfter(lastEnd)) continue;
      final probe = t.event == 'candle_close'
          ? wall.subtract(const Duration(seconds: 1))
          : wall;
      strategyPoints.add(StrategyPoint(
        candleIndex: findCandleIndex(probe),
        at: t.at,
        kind: t.kind,
        side: t.side,
        confidence: t.confidence,
        encerrar: t.encerrar,
        action: t.action,
        reason: t.reason,
        snapshotHash: t.snapshotHash,
      ));
    }
    strategyPoints.sort((a, b) => a.candleIndex.compareTo(b.candleIndex));
  }
  // Diagnóstico: só fala quando algum item cai fora do range carregado.
  if (state.strategyTimelineVisible &&
      timeline.isNotEmpty &&
      strategyPoints.length != timeline.length) {
    logD('[timeline] ${timeline.length} itens -> ${strategyPoints.length} pontos (fora do range)');
  }

  // VWAP fonte única do servidor (VwapService: typical HLC/3 dos 1MIN).
  // Sem cálculo local: para cada candle visível usa o último ponto do
  // servidor com minute <= candle.date (funciona para qualquer TF: 1/2/5MIN…).
  final vwapPoints = <VwapPoint>[];
  if (state.vwapVisible && state.vwapHistory.isNotEmpty && visible.isNotEmpty) {
    final server = state.vwapHistory;
    var si = 0;
    var lastIdx = -1;
    for (int i = 0; i < visible.length; i++) {
      final bar = visible[i];
      while (si < server.length && !server[si].minute.isAfter(bar.date)) {
        lastIdx = si;
        si++;
      }
      if (lastIdx < 0) continue;
      final p = server[lastIdx];
      // Evita vazar ponto de outro dia quando o range cruza dias.
      if (p.minute.day != bar.date.day ||
          p.minute.month != bar.date.month ||
          p.minute.year != bar.date.year) {
        continue;
      }
      // O filtro de datas (rangeStart/rangeEnd) é SÓ do volume profile:
      // a VWAP aparece sempre cheia (dia todo), sem corte pela janela.
      vwapPoints.add(VwapPoint(
        price: p.vwap,
        date: bar.date,
        startCandleIndex: i,
        endCandleIndex: i,
      ));
    }
  }

  return ChartData(
    candles: candles,
    redBubbles: redBubbles,
    blueBubbles: blueBubbles,
    volumeProfile: volumeProfile,
    structures: structures,
    extremes: extremes,
    pivots: pivots,
    vwapPoints: vwapPoints,
    minPrice: minPrice,
    maxPrice: maxPrice,
    lastPrice: visible.isNotEmpty ? visible.last.close : 0,
    dates: dates,
    daySeparatorIndices: daySepIndices,
    rangeStart: rangeStart,
    rangeEnd: rangeEnd,
    profileVisible: state.profileVisible,
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
    bubbleAmountMin: bubbleAmountMin,
    bubbleAmountMax: bubbleAmountMax,
    vwapOpacity: state.vwapOpacity,
    vwapColor: _parseHexColor(state.vwapColor),
    positions: positions,
    orders: orders,
    historyPoints: historyPoints,
    strategyPoints: strategyPoints,
    tradingHistoryVisible: state.tradingHistoryVisible,
    positionVisible: state.positionVisible,
    openOrdersVisible: state.openOrdersVisible,
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
