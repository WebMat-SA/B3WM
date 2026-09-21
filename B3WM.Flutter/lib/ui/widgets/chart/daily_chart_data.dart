import 'dart:ui' show Color;

import '../../../models/extreme_storage_item.dart';
import '../../../services/state_service.dart';
import 'chart_data.dart';

/// Chart 1D dedicado do widget de análise diária (issue #12).
///
/// Fontes 100% diárias, isoladas do fluxo intraday:
/// - candles: `StateService.dailyBars` (TF 1440 via `getBarRange`);
/// - estruturas: step-fill do `structures1440History` (Range 1D);
/// - topos/vales: `dailyExtremes` (`getExtremeDaily`);
/// - volume profile: `dailyProfileLevels` (`GetDailyProfile`).
///
/// Reaproveita `MapFlowChart` sem mudanças: preenche os campos intraday
/// (`structures`/`extremes`/`volumeProfile`) com dados diários e deixa
/// vazios bubbles, VWAP e trading.
ChartData buildDailyChartData(StateService state) {
  final bars = List.of(state.dailyBars)
    ..sort((a, b) => a.date.compareTo(b.date));

  final candles = bars
      .map((b) => CandlePoint(
            date: b.date,
            open: b.open,
            high: b.high,
            low: b.low,
            close: b.close,
          ))
      .toList();
  final dates = bars.map((b) => b.date).toList();

  final monthSepIndices = <int>[];
  for (int i = 1; i < dates.length; i++) {
    if (dates[i].month != dates[i - 1].month ||
        dates[i].year != dates[i - 1].year) {
      monthSepIndices.add(i);
    }
  }

  double min4(double a, double b, double c, double d) =>
      a < b ? (a < c ? (a < d ? a : d) : (c < d ? c : d)) : (b < c ? (b < d ? b : d) : (c < d ? c : d));
  double max4(double a, double b, double c, double d) =>
      a > b ? (a > c ? (a > d ? a : d) : (c > d ? c : d)) : (b > c ? (b > d ? b : d) : (c > d ? c : d));
  double minPrice = bars.isEmpty
      ? 0.0
      : bars.fold<double>(
          double.infinity, (p, b) => min4(p, b.low, b.close, b.open));
  double maxPrice = bars.isEmpty
      ? 0.0
      : bars.fold<double>(double.negativeInfinity,
          (p, b) => max4(p, b.high, b.close, b.open));

  DateTime dayOnly(DateTime d) => DateTime(d.year, d.month, d.day);

  StructureLineData? structures;
  if (state.dailyStructureVisible) {
    final hist = state.structures1440History
        .where((s) => s.symbol == state.symbol && s.timeFrame == 1440)
        .toList();
    if (hist.isNotEmpty && bars.isNotEmpty) {
      final sorted = List.of(hist)
        ..sort((a, b) => a.date.compareTo(b.date));
      // Normaliza para dia: o hist pode trazer hora (ex. 18:00) enquanto as
      // barras D0 são meia-noite — sem isso o `isAfter` pula o D0.
      final histDays = sorted.map((s) => dayOnly(s.date)).toList();
      final barDays = bars.map((b) => dayOnly(b.date)).toList();
      final up = List<double?>.filled(bars.length, null);
      final down = List<double?>.filled(bars.length, null);
      final upAux = List<double?>.filled(bars.length, null);
      final downAux = List<double?>.filled(bars.length, null);
      var h = 0;
      for (var i = 0; i < bars.length; i++) {
        while (h + 1 < sorted.length &&
            !histDays[h + 1].isAfter(barDays[i])) {
          h++;
        }
        // Prefixo anterior ao hist mais antigo: sem referência.
        // Todo o resto — inclusive o D0 em formação — recebe o último valor
        // conhecido (histórico estático estendido como referência).
        if (histDays[h].isAfter(barDays[i])) continue;
        up[i] = sorted[h].upBorder;
        down[i] = sorted[h].downBorder;
        upAux[i] = sorted[h].upAuxBorder;
        downAux[i] = sorted[h].downAuxBorder;
      }
      structures = StructureLineData(
        upBorder: up,
        downBorder: down,
        upAuxBorder: upAux,
        downAuxBorder: downAux,
        visible: true,
        auxVisible: state.dailyStructureAuxVisible,
        opacity: state.dailyStructureOpacity,
      );
      // Inclui as bordas na escala: com threshold alto (ex. WINFUT 3000) a
      // estrutura pode estar longe dos candles e sumiria do chart.
      for (var i = 0; i < bars.length; i++) {
        for (final v in [up[i], down[i], upAux[i], downAux[i]]) {
          if (v == null || !v.isFinite) continue;
          if (v < minPrice) minPrice = v;
          if (v > maxPrice) maxPrice = v;
        }
      }
    }
  }

  ExtremeLineData? extremes;
  if (state.dailyExtremeVisible) {
    final dex = state.dailyExtremes;
    if (dex != null && dex.extremes.isNotEmpty) {
      extremes = ExtremeLineData(
        topPrices: dex.extremes
            .where((e) => e.type == ExtremeType.top)
            .map((e) => e.position)
            .toList(),
        valleyPrices: dex.extremes
            .where((e) => e.type == ExtremeType.valley)
            .map((e) => e.position)
            .toList(),
        visible: true,
        opacity: state.dailyExtremeOpacity,
      );
    }
  }

  final volumeProfile = <VolumeBarData>[];
  if (state.dailyProfileVisible && state.dailyProfileLevels.isNotEmpty) {
    final vols = state.dailyProfileLevels;
    final maxVol = vols.fold<int>(0, (p, v) => v.total > p ? v.total : p);
    for (int i = 0; i < vols.length; i++) {
      final v = vols[i];
      if (v.total <= 0) continue;
      final step = i < vols.length - 1
          ? (vols[i + 1].price - v.price).abs()
          : i > 0
              ? (v.price - vols[i - 1].price).abs()
              : state.dailyProfileSizeV;
      volumeProfile.add(VolumeBarData(
        price: v.price,
        total: v.total.toDouble(),
        step: step,
        isPoc: v.total >= maxVol,
        delta: v.delta.toDouble(),
      ));
    }
  }

  return ChartData(
    candles: candles,
    redBubbles: const [],
    blueBubbles: const [],
    volumeProfile: volumeProfile,
    structures: structures,
    extremes: extremes,
    vwapPoints: const [],
    minPrice: minPrice,
    maxPrice: maxPrice,
    lastPrice: bars.isNotEmpty ? bars.last.close : 0,
    dates: dates,
    daySeparatorIndices: monthSepIndices,
    rangeStart: 0,
    rangeEnd: bars.length,
    symbol: state.symbol,
    timeFrame: 1440,
    bubbleOpacity: 0.7,
    profileSizeH: state.dailyProfileSizeH,
    profileSizeV: state.dailyProfileSizeV,
    profileOpacity: state.dailyProfileOpacity,
    colorBuyer: _parseHexColor(state.colorBuyer),
    colorSeller: _parseHexColor(state.colorSeller),
    yZoom: state.yZoom,
    vwapOpacity: 0,
    vwapColor: const Color(0xFFFF8800),
  );
}

Color _parseHexColor(String hex) {
  hex = hex.replaceAll('#', '');
  if (hex.length == 6) hex = 'FF$hex';
  return Color(int.parse(hex, radix: 16));
}
