import 'package:flutter_test/flutter_test.dart';

import 'package:b3wm_flutter/models/defaults.dart';
import 'package:b3wm_flutter/models/symbol_config.dart';
import 'package:b3wm_flutter/ui/widgets/chart/chart_data.dart';
import 'package:b3wm_flutter/ui/widgets/chart/measure_ruler.dart';

void main() {
  group('Defaults symbols (issue #47)', () {
    test('lista canônica na ordem do seletor', () {
      expect(Defaults.symbols, [
        Defaults.symbolWinfut,
        Defaults.symbolIndfut,
        Defaults.symbolWdofut,
        Defaults.symbolDolfut,
      ]);
    });

    test('bandeiras por ativo-objeto', () {
      expect(Defaults.flagFor('WINFUT'), 'assets/images/bandeira-brasil.png');
      expect(Defaults.flagFor('INDFUT'), 'assets/images/bandeira-brasil.png');
      expect(Defaults.flagFor('WDOFUT'), 'assets/images/bandeira-eua.png');
      expect(Defaults.flagFor('DOLFUT'), 'assets/images/bandeira-eua.png');
    });

    test('tick do cheio espelha o mini', () {
      expect(Defaults.tickSize('WINFUT'), 5.0);
      expect(Defaults.tickSize('INDFUT'), 5.0);
      expect(Defaults.tickSize('WDOFUT'), 0.5);
      expect(Defaults.tickSize('DOLFUT'), 0.5);
    });

    test('ranges/valores por símbolo', () {
      expect(Defaults.thresholdBubbleSize('INDFUT'), 250);
      expect(Defaults.minDistanceUpdateBorder('INDFUT'), 250);
      expect(Defaults.minDistanceUpdateBorderDaily('INDFUT'), 1000);
      expect(Defaults.structureRangeUpdStep('INDFUT'), 5.0);
      expect(Defaults.structureRangeUpdMax('INDFUT'), 2000);
      expect(Defaults.structureRangeUpdDailyMax('INDFUT'), 10000);
      expect(Defaults.structureRangeUpdDailyStep('INDFUT'), 50.0);

      expect(Defaults.thresholdBubbleSize('DOLFUT'), 500);
      expect(Defaults.minDistanceUpdateBorder('DOLFUT'), 2.5);
      expect(Defaults.minDistanceUpdateBorderDaily('DOLFUT'), 100);
      expect(Defaults.structureRangeUpdStep('DOLFUT'), 0.5);
      expect(Defaults.structureRangeUpdMax('DOLFUT'), 100);
      expect(Defaults.structureRangeUpdDailyMax('DOLFUT'), 1000);
      expect(Defaults.structureRangeUpdDailyStep('DOLFUT'), 1.0);
      expect(Defaults.pointValue('DOLFUT'), 50.0);
    });

    test('defaults legados seguem intactos', () {
      expect(Defaults.minDistanceUpdateBorder('WDOFUT'), 4.5);
      expect(Defaults.thresholdBubbleSize('WINFUT'), 250);
      expect(Defaults.pointValue('WDOFUT'), 10.0);
    });

    test('casas decimais da régua seguem o tick', () {
      expect(decimalPlacesForSymbol('INDFUT'), 0);
      expect(decimalPlacesForSymbol('DOLFUT'), 1);
      expect(decimalPlacesForSymbol('WINFUT'), 0);
      expect(decimalPlacesForSymbol('WDOFUT'), 1);
    });

    test('símbolo desconhecido mantém fallback histórico', () {
      expect(Defaults.tickSize('PETR4'), 1.0);
      expect(Defaults.thresholdBubbleSize('PETR4'), 250);
      expect(Defaults.minDistanceUpdateBorderDaily('PETR4'), 1000);
      expect(Defaults.flagFor('PETR4'), '');
    });

    test('SymbolConfig cria defaults independentes por símbolo', () {
      final ind = SymbolConfig.withDefaults('INDFUT');
      final dol = SymbolConfig.withDefaults('DOLFUT');

      expect(ind.thresholdBubble, 250);
      expect(dol.thresholdBubble, 500);
      expect(ind.daily.structureRangeUpd, 1000);
      expect(dol.daily.structureRangeUpd, 100);

      final roundTrip = SymbolConfig.fromJson(ind.toJson(), symbol: 'INDFUT');
      expect(roundTrip.thresholdBubble, ind.thresholdBubble);
      expect(roundTrip.structureRangeUpd, ind.structureRangeUpd);
      expect(roundTrip.daily.structureRangeUpd, ind.daily.structureRangeUpd);
    });
  });

  group('normalizeTradeSymbol', () {
    test('mapeia root do MT5 para o símbolo canônico', () {
      expect(normalizeTradeSymbol('WINM26'), 'WINFUT');
      expect(normalizeTradeSymbol('INDM26'), 'INDFUT');
      expect(normalizeTradeSymbol('WDOM26'), 'WDOFUT');
      expect(normalizeTradeSymbol('DOLM26'), 'DOLFUT');
      expect(normalizeTradeSymbol(r'ind$'), 'INDFUT');
      expect(normalizeTradeSymbol('DOL@N'), 'DOLFUT');
    });

    test('mantém nomes não mapeados', () {
      expect(normalizeTradeSymbol('PETR4'), 'PETR4');
    });
  });
}
