import 'package:flutter_test/flutter_test.dart';

import 'package:b3wm_flutter/ui/widgets/chart/chart_data.dart';
import 'package:b3wm_flutter/ui/widgets/chart/measure_ruler.dart';

List<CandlePoint> _candles2min() {
  final start = DateTime(2024, 5, 10, 9, 0);
  return List.generate(
    10,
    (i) => CandlePoint(
      date: start.add(Duration(minutes: 2 * i)),
      open: 206000.0 + i * 10,
      high: 206100.0 + i * 10,
      low: 205900.0 + i * 10,
      close: 206050.0 + i * 10,
    ),
  );
}

List<CandlePoint> _candlesDaily() {
  return List.generate(
    5,
    (i) => CandlePoint(
      date: DateTime(2024, 5, 10 + i),
      open: 100000.0 + i * 100,
      high: 100100.0 + i * 100,
      low: 99900.0 + i * 100,
      close: 100050.0 + i * 100,
    ),
  );
}

void main() {
  group('MeasureResult (issue #20)', () {
    test('pts = p2-p1 direto (206.575→207.458 = 883)', () {
      final m = computeMeasure(
        p1: 206575,
        p2: 207458,
        d1: DateTime(2024, 5, 10, 9, 0),
        d2: DateTime(2024, 5, 10, 9, 10),
        idx1: 0,
        idx2: 5,
        timeFrame: 2,
      );
      expect(m.pts, 883);
      expect(m.diferenca, 883);
      expect(m.candles, 6);
      expect(m.intervaloMinutos, 10);
      expect(m.intervaloLabel, '10 min');
      expect(m.isUp, isTrue);
    });

    test('queda fica vermelha (isUp false) e pct negativo', () {
      final m = computeMeasure(
        p1: 207458,
        p2: 206918.55,
        d1: DateTime(2024, 5, 10, 9, 10),
        d2: DateTime(2024, 5, 10, 9, 0),
        idx1: 5,
        idx2: 0,
        timeFrame: 2,
      );
      expect(m.pts, closeTo(-539.45, 0.001));
      // truncate toward zero: -539,45 → -539
      expect(m.diferenca, -539);
      expect(m.isUp, isFalse);
      expect(m.pct, closeTo(-539.45 / 207458 * 100, 0.0001));
      expect(m.candles, 6);
    });

    test('mesmo candle = 1 candle e 0 min', () {
      final d = DateTime(2024, 5, 10, 9, 0);
      final m = computeMeasure(
        p1: 100,
        p2: 100,
        d1: d,
        d2: d,
        idx1: 3,
        idx2: 3,
        timeFrame: 2,
      );
      expect(m.candles, 1);
      expect(m.intervaloMinutos, 0);
      expect(m.intervaloLabel, '0 min');
      expect(m.pts, 0);
      expect(m.pct, 0);
      expect(m.diferenca, 0);
      expect(m.isUp, isTrue);
    });

    test('1D usa pregões + dias corridos', () {
      final candles = _candlesDaily();
      final m = computeMeasureFromCandles(
        candles: candles,
        idx1: 0,
        idx2: 2,
        price1: 100000,
        price2: 100500,
        timeFrame: 1440,
      )!;
      expect(m.isDaily, isTrue);
      expect(m.candles, 3);
      expect(m.pregoes, 2);
      expect(m.intervaloDias, 2);
      expect(m.intervaloLabel, '2 pregões (2 dias)');
    });

    test('1D singular: 1 pregão (3 dias corridos Sex→Seg)', () {
      final m = computeMeasure(
        p1: 100000,
        p2: 100500,
        d1: DateTime(2024, 5, 10), // sexta
        d2: DateTime(2024, 5, 13), // segunda
        idx1: 4,
        idx2: 5,
        timeFrame: 1440,
      );
      expect(m.intervaloLabel, '1 pregão (3 dias)');
      expect(m.candles, 2);
    });

    test('fromCandles usa datas dos candles para intervalo', () {
      final candles = _candles2min();
      final m = computeMeasureFromCandles(
        candles: candles,
        idx1: 0,
        idx2: 5,
        price1: 206000,
        price2: 206883.45,
        timeFrame: 2,
      )!;
      expect(m.intervaloMinutos, 10);
      expect(m.candles, 6);
      expect(m.pts, closeTo(883.45, 0.001));
    });

    test('fromCandles retorna null sem candles', () {
      expect(
        computeMeasureFromCandles(
          candles: const [],
          idx1: 0,
          idx2: 1,
          price1: 1,
          price2: 2,
          timeFrame: 2,
        ),
        isNull,
      );
    });

    test('p1 == 0 não divide por zero', () {
      final m = computeMeasure(
        p1: 0,
        p2: 10,
        d1: DateTime(2024, 5, 10),
        d2: DateTime(2024, 5, 10),
        idx1: 0,
        idx2: 1,
        timeFrame: 2,
      );
      expect(m.pct, 0);
    });
  });

  group('formatação pt-BR', () {
    test('pts e % com vírgula', () {
      expect(formatPts(883.45), '883,45pts');
      expect(formatPts(-539.45), '-539,45pts');
      expect(formatPct(0.4277), '0,43%');
      expect(formatPct(-0.26), '-0,26%');
    });

    test('diferença inteira com milhar', () {
      expect(formatDiferenca(883), '883');
      expect(formatDiferenca(-539), '-539');
    });

    test('preço WINFUT (0 casas) usa ponto de milhar', () {
      expect(formatRulerPrice(206575, 'WINFUT'), '206.575');
      expect(formatRulerPrice(207458, 'WINFUT'), '207.458');
    });

    test('preço WDOFUT (1 casa) usa vírgula decimal', () {
      expect(formatRulerPrice(5445.5, 'WDOFUT'), '5.445,5');
    });
  });
}
