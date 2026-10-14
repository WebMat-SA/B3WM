import 'package:flutter_test/flutter_test.dart';

import 'package:b3wm_flutter/services/strategy_service.dart';

void main() {
  test('DecisionItem parse At com data completa', () {
    final item = StrategyDecisionItem.fromJson({
      'kind': 'avaliacao',
      'at': '2026-10-08T09:02:00-03:00',
      'time': '09:02',
      'event': 'bubble',
      'side': 'vender',
      'confidence': 0.41,
      'encerrar': 0.09,
      'shouldTrade': false,
      'reason': 'jev jev-1.13.0 in=1978 out=103',
      'stateChars': 2301,
      'snapshotHash': 'B28C5D3EB17A',
      'positionBefore': 'flat',
      'action': 'manter',
    });
    expect(item.at, isNotNull);
    expect(item.at!.year, 2026);
    expect(item.at!.month, 10);
    expect(item.at!.day, 8);
    expect(item.at!.hour, 9);
    expect(item.at!.minute, 2);
    expect(item.time, '09:02');
  });

  test('b3Wall normaliza com offset e naive para a mesma parede', () {
    final withOffset = StrategyDecisionItem.fromJson({
      'kind': 'avaliacao',
      'at': '2026-10-13T10:00:00-03:00',
      'time': '10:00',
    });
    final naive = StrategyDecisionItem.fromJson({
      'kind': 'avaliacao',
      'at': '2026-10-13T15:43:21.711',
      'time': '15:43',
    });
    final w1 = b3Wall(withOffset.at!);
    expect(w1.hour, 10);
    expect(w1.minute, 0);
    final w2 = b3Wall(naive.at!);
    expect(w2.hour, 15);
    expect(w2.minute, 43);
    // Absolutos diferentes, paredes distintas preservadas.
    expect(withOffset.at!.toUtc(), isNot(equals(naive.at!.toUtc())));
  });

  test('DecisionItem legado sem At retorna null (fallback displayDate+time)', () {
    final item = StrategyDecisionItem.fromJson({
      'kind': 'avaliacao',
      'time': '09:06',
      'event': 'candle_close',
      'side': 'manter',
    });
    expect(item.at, isNull);
    expect(item.time, '09:06');
  });

  test('StrategyLogDay parse decisions + paperTrades com At', () {
    final log = StrategyLogDay.fromJson({
      'date': '2026-10-08T00:00:00-03:00',
      'symbol': 'WINFUT',
      'strategy': 'JevAnalysis',
      'snapshotHash': 'B28C5D3EB17A',
      'decisions': [
        {
          'kind': 'avaliacao',
          'at': '2026-10-08T09:02:00-03:00',
          'time': '09:02',
          'event': 'bubble',
          'side': 'vender',
          'action': 'manter',
        },
      ],
      'paperTrades': [
        {
          'side': 'comprar',
          'entryAt': '2026-10-08T10:48:00-03:00',
          'entryTime': '10:48',
          'entry': 206330,
          'exitAt': '2026-10-08T10:54:00-03:00',
          'exitTime': '10:54',
          'exit': 205870,
          'pts': -460,
          'exitReason': 'sinal encerrar',
          'entryConf': 0.7,
        },
      ],
      'realizedPts': -460,
    });
    expect(log.decisions, hasLength(1));
    expect(log.decisions.first.at?.hour, 9);
    expect(log.paperTrades, hasLength(1));
    expect(log.paperTrades.first.entryAt?.hour, 10);
    expect(log.paperTrades.first.exitAt?.minute, 54);
    // Chaves PascalCase do backend também parseiam.
    final pascal = StrategyLogDay.fromJson({
      'Date': '2026-10-08T00:00:00-03:00',
      'Symbol': 'WINFUT',
      'Strategy': 'JevAnalysis',
      'SnapshotHash': 'X',
      'Decisions': [
        {'Kind': 'avaliacao', 'At': '2026-10-08T09:02:00-03:00', 'Time': '09:02'},
      ],
      'PaperTrades': [],
      'RealizedPts': 0,
    });
    expect(pascal.decisions.first.at?.day, 8);
  });
}
