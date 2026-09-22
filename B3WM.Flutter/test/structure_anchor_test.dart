import 'package:flutter_test/flutter_test.dart';

import 'package:b3wm_flutter/models/bar_storage_item.dart';
import 'package:b3wm_flutter/models/structure_change_item.dart';
import 'package:b3wm_flutter/services/state_service.dart';

BarStorageItem bar(DateTime date,
        {double high = 0, double low = 0, double close = 0}) =>
    BarStorageItem(
      date: date,
      symbol: 'WINFUT',
      timeFrame: 2,
      open: close,
      high: high,
      low: low,
      close: close,
      volume: 10,
    );

BarStorageItem day(DateTime date, {double high = 0, double low = 0}) =>
    BarStorageItem(
      date: date,
      symbol: 'WINFUT',
      timeFrame: 1440,
      open: low,
      high: high,
      low: low,
      close: low,
      volume: 10,
    );

void main() {
  group('resolveExtremeBarIndex (intraday, último toque)', () {
    test('topo: volta da confirmação ao candle do extremo', () {
      final bars = [
        bar(DateTime(2026, 1, 1, 9, 0), high: 100, low: 90, close: 95),
        bar(DateTime(2026, 1, 1, 9, 2), high: 110, low: 95, close: 100),
        bar(DateTime(2026, 1, 1, 9, 4), high: 105, low: 92, close: 93),
      ];
      final anchor = StructureChangeItem(
        date: bars[2].date, // confirmação por distanciamento
        isUp: true,
        oldValue: 100,
        newValue: 110,
      );
      expect(
        StateService.resolveExtremeBarIndex(
          bars: bars,
          anchor: anchor,
          confirmationIndex: 2,
          tolerance: 2.5,
        ),
        1,
      );
    });

    test('reteste do mesmo preço: pega o último toque', () {
      final bars = [
        bar(DateTime(2026, 1, 1, 9, 0), high: 110, low: 90, close: 100),
        bar(DateTime(2026, 1, 1, 9, 2), high: 108, low: 95, close: 100),
        bar(DateTime(2026, 1, 1, 9, 4), high: 110, low: 96, close: 101),
        bar(DateTime(2026, 1, 1, 9, 6), high: 104, low: 92, close: 93),
      ];
      final anchor = StructureChangeItem(
        date: bars[3].date,
        isUp: true,
        oldValue: 100,
        newValue: 110,
      );
      expect(
        StateService.resolveExtremeBarIndex(
          bars: bars,
          anchor: anchor,
          confirmationIndex: 3,
          tolerance: 2.5,
        ),
        2, // último toque, não o primeiro
      );
    });

    test('fundo: usa a mínima do candle', () {
      final bars = [
        bar(DateTime(2026, 1, 1, 9, 0), high: 100, low: 90, close: 95),
        bar(DateTime(2026, 1, 1, 9, 2), high: 98, low: 80, close: 90),
        bar(DateTime(2026, 1, 1, 9, 4), high: 100, low: 85, close: 97),
      ];
      final anchor = StructureChangeItem(
        date: bars[2].date,
        isUp: false,
        oldValue: 90,
        newValue: 80,
      );
      expect(
        StateService.resolveExtremeBarIndex(
          bars: bars,
          anchor: anchor,
          confirmationIndex: 2,
          tolerance: 2.5,
        ),
        1,
      );
    });

    test('preço não encontrado: fallback na confirmação', () {
      final bars = [
        bar(DateTime(2026, 1, 1, 9, 0), high: 100, low: 90, close: 95),
        bar(DateTime(2026, 1, 1, 9, 2), high: 101, low: 91, close: 96),
      ];
      final anchor = StructureChangeItem(
        date: bars[1].date,
        isUp: true,
        oldValue: 100,
        newValue: 999, // nunca tocado
      );
      expect(
        StateService.resolveExtremeBarIndex(
          bars: bars,
          anchor: anchor,
          confirmationIndex: 1,
          tolerance: 2.5,
        ),
        1,
      );
    });

    test('lowerBound: não atravessa a perna anterior', () {
      final bars = [
        bar(DateTime(2026, 1, 1, 9, 0), high: 110, low: 90, close: 100),
        bar(DateTime(2026, 1, 1, 9, 2), high: 100, low: 90, close: 95),
        bar(DateTime(2026, 1, 1, 9, 4), high: 100, low: 90, close: 95),
        bar(DateTime(2026, 1, 1, 9, 6), high: 100, low: 90, close: 95),
        bar(DateTime(2026, 1, 1, 9, 8), high: 100, low: 90, close: 95),
        bar(DateTime(2026, 1, 1, 9, 10), high: 110, low: 95, close: 100),
        bar(DateTime(2026, 1, 1, 9, 12), high: 105, low: 92, close: 93),
      ];
      final anchor = StructureChangeItem(
        date: bars[6].date,
        isUp: true,
        oldValue: 100,
        newValue: 110,
      );
      expect(
        StateService.resolveExtremeBarIndex(
          bars: bars,
          anchor: anchor,
          confirmationIndex: 6,
          lowerBound: 3,
          tolerance: 2.5,
        ),
        5, // ignora o 110 antigo do índice 0
      );
    });
  });

  group('confirmationBarIndex', () {
    test('última barra com date <= changeDate', () {
      final bars = [
        bar(DateTime(2026, 1, 1, 9, 0)),
        bar(DateTime(2026, 1, 1, 9, 2)),
        bar(DateTime(2026, 1, 1, 9, 4)),
      ];
      expect(
        StateService.confirmationBarIndex(bars, DateTime(2026, 1, 1, 9, 3)),
        1,
      );
    });
  });

  group('resolveDailyAnchorDay (diário)', () {
    test('volta do dia de confirmação ao dia do extremo', () {
      final bars = [
        day(DateTime(2026, 1, 1), high: 100, low: 90),
        day(DateTime(2026, 1, 2), high: 110, low: 95),
        day(DateTime(2026, 1, 3), high: 105, low: 92),
        day(DateTime(2026, 1, 4), high: 104, low: 93),
      ];
      final anchor = StructureChangeItem(
        date: DateTime(2026, 1, 4, 18, 0), // confirmação dias depois
        isUp: true,
        oldValue: 100,
        newValue: 110,
      );
      expect(
        StateService.resolveDailyAnchorDay(
          dailyBars: bars,
          symbol: 'WINFUT',
          anchor: anchor,
        ),
        DateTime(2026, 1, 2),
      );
    });

    test('sem barras: fallback no dia da confirmação', () {
      final anchor = StructureChangeItem(
        date: DateTime(2026, 1, 4, 18, 0),
        isUp: true,
        oldValue: 100,
        newValue: 110,
      );
      expect(
        StateService.resolveDailyAnchorDay(
          dailyBars: const [],
          symbol: 'WINFUT',
          anchor: anchor,
        ),
        DateTime(2026, 1, 4),
      );
    });
  });
}
