import 'package:intl/intl.dart';

import '../../../models/defaults.dart';
import 'chart_data.dart';

/// Régua de medição da issue #20.
///
/// Convenções (documentadas aqui para travar o comportamento):
/// - `pts = p2 - p1` (diferença direta de preço). Ex: `206.575 → 207.458`
///   (pt-BR, ponto = milhar) = `883pts`. NÃO multiplica por 1000 — o exemplo
///   `206.575→207.458 = 883pts` dos prints só fecha com diferença direta.
/// - `% = (p2-p1)/p1*100` (0 quando `p1 == 0`).
/// - `Diferença = pts.truncate()` (toward zero: `883,45 → 883`,
///   `-539,45 → -539`).
/// - `Candles = abs(idx2-idx1)+1` (inclusivo: mesmo candle = 1).
/// - `Intervalo`: intraday = `abs(d2-d1)` em minutos (`N min`);
///   1D (`timeFrame == 1440`) = `abs(idx2-idx1)` pregões + dias corridos
///   (`abs(d2-d1).inDays`).
class MeasureResult {
  final double p1;
  final double p2;
  final DateTime d1;
  final DateTime d2;
  final int idx1;
  final int idx2;
  final int timeFrame;
  final bool isDaily;

  MeasureResult({
    required this.p1,
    required this.p2,
    required this.d1,
    required this.d2,
    required this.idx1,
    required this.idx2,
    required this.timeFrame,
  }) : isDaily = timeFrame == 1440;

  double get pts => p2 - p1;

  double get pct => p1 == 0 ? 0 : (p2 - p1) / p1 * 100;

  /// Inteiro toward-zero para paridade com os prints (`883,45 → 883`).
  int get diferenca => pts.truncate();

  /// Inclusivo: arrastar no mesmo candle = 1.
  int get candles => (idx2 - idx1).abs() + 1;

  /// Pregões de distância (0 no mesmo candle). Só relevante no 1D.
  int get pregoes => (idx2 - idx1).abs();

  int get intervaloMinutos => d2.difference(d1).inMinutes.abs();

  int get intervaloDias => d2.difference(d1).inDays.abs();

  bool get isUp => p2 >= p1;

  /// `N min` no intraday; `N pregões (M dias)` no 1D.
  String get intervaloLabel {
    if (isDaily) {
      final p = pregoes;
      final dd = intervaloDias;
      final pregoesStr = p == 1 ? '1 pregão' : '$p pregões';
      final diasStr = dd == 1 ? '1 dia' : '$dd dias';
      return '$pregoesStr ($diasStr)';
    }
    final m = intervaloMinutos;
    return m == 1 ? '1 min' : '$m min';
  }
}

MeasureResult computeMeasure({
  required double p1,
  required double p2,
  required DateTime d1,
  required DateTime d2,
  required int idx1,
  required int idx2,
  required int timeFrame,
}) {
  return MeasureResult(
    p1: p1,
    p2: p2,
    d1: d1,
    d2: d2,
    idx1: idx1,
    idx2: idx2,
    timeFrame: timeFrame,
  );
}

/// Constrói a régua direto dos candles (índices já clampados).
MeasureResult? computeMeasureFromCandles({
  required List<CandlePoint> candles,
  required int idx1,
  required int idx2,
  required double price1,
  required double price2,
  required int timeFrame,
}) {
  if (candles.isEmpty) return null;
  final a = idx1.clamp(0, candles.length - 1);
  final b = idx2.clamp(0, candles.length - 1);
  return MeasureResult(
    p1: price1,
    p2: price2,
    d1: candles[a].date,
    d2: candles[b].date,
    idx1: a,
    idx2: b,
    timeFrame: timeFrame,
  );
}

final _ptsFmt = NumberFormat('#,##0.00', 'pt_BR');
final _pctFmt = NumberFormat('#,##0.00', 'pt_BR');
final _intFmt = NumberFormat('#,##0', 'pt_BR');

String formatPts(double pts) => '${_ptsFmt.format(pts)}pts';

String formatPct(double pct) => '${_pctFmt.format(pct)}%';

String formatDiferenca(int diferenca) => _intFmt.format(diferenca);

int decimalPlacesForSymbol(String symbol) {
  final tick = Defaults.tickSize(symbol);
  if (tick == tick.roundToDouble()) return 0;
  final str = tick.toStringAsFixed(10);
  final dot = str.indexOf('.');
  return str.substring(dot + 1).replaceAll(RegExp(r'0+$'), '').length;
}

String formatRulerPrice(double price, String symbol) {
  final dec = decimalPlacesForSymbol(symbol);
  final pattern = dec == 0 ? '#,##0' : '#,##0.${'0' * dec}';
  return NumberFormat(pattern, 'pt_BR').format(price);
}
