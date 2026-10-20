import 'dart:ui' show Color;

import 'package:flutter_test/flutter_test.dart';

import 'package:b3wm_flutter/ui/widgets/chart/chart_data.dart';
import 'package:b3wm_flutter/ui/widgets/chart/jev_tooltip_data.dart';

const _reason =
    'vies=vender conf=0.17 op=0.62 prox=0.90(estrutura_diaria 13t) | '
    'dir: candles_alta=36% acima_vwap=74% estrutura_alta=79% acima_poc=38% '
    'delta_positivo=3% bolhas_compra=5% defende_suporte=42% diario_alta=37% | '
    'op: sem_chop=59% amplitude_suficiente=48% sem_compressao_topovale=56% '
    'referencia_clara=86% sem_conflito_intra_diario=66%';

StrategyPoint _point({
  Map<String, double> criteria = const {},
  String reason = _reason,
}) =>
    StrategyPoint(
      candleIndex: 10,
      at: DateTime(2026, 10, 20, 9, 26),
      kind: 'avaliacao',
      event: 'bubble',
      side: 'vender',
      confidence: 0.17,
      encerrar: 0,
      action: 'manter',
      criteria: criteria,
      reason: reason,
      snapshotHash: 'ABC',
    );

void main() {
  test('usa criteria estruturado e resumo do reason', () {
    final data = JevTooltipData.fromPoint(_point(criteria: const {
      'candles_alta': 0.36,
      'acima_vwap': 0.74,
      'estrutura_alta': 0.79,
      'acima_poc': 0.38,
      'delta_positivo': 0.03,
      'bolhas_compra': 0.05,
      'defende_suporte': 0.42,
      'diario_alta': 0.37,
      'sem_chop': 0.59,
      'amplitude_suficiente': 0.48,
      'sem_compressao_topovale': 0.56,
      'referencia_clara': 0.86,
      'sem_conflito_intra_diario': 0.66,
    }));

    expect(data.isJev, isTrue);
    expect(data.vies, 'vender');
    expect(data.direcaoConfidence, 0.17);
    expect(data.operabilidade, 0.62);
    expect(data.proximidade, 0.90);
    expect(data.proximidadeRef, 'estrutura_diaria');
    expect(data.proximidadeTicks, 13);
    expect(data.direction.map((e) => e.id).toList(), [
      'candles_alta',
      'acima_vwap',
      'estrutura_alta',
      'acima_poc',
      'delta_positivo',
      'bolhas_compra',
      'defende_suporte',
      'diario_alta',
    ]);
    expect(data.operability.map((e) => e.id).toList(), [
      'sem_chop',
      'amplitude_suficiente',
      'sem_compressao_topovale',
      'referencia_clara',
      'sem_conflito_intra_diario',
    ]);
    expect(data.direction.first.label, 'Candles alta');
    expect(data.operability.first.value, 0.59);
  });

  test('faz fallback das listas dir/op do reason quando criteria vazio', () {
    final data = JevTooltipData.fromPoint(_point());

    expect(data.isJev, isTrue);
    expect(data.direction.length, 8);
    expect(data.operability.length, 5);
    expect(data.direction.first.value, closeTo(0.36, 1e-9));
    expect(data.direction.last.value, closeTo(0.37, 1e-9));
    expect(data.operability.first.value, closeTo(0.59, 1e-9));
    expect(data.operability.last.value, closeTo(0.66, 1e-9));
  });

  test('reason não-Jev mantém o texto bruto sem seções', () {
    final data = _nonJevPoint();

    expect(data.isJev, isFalse);
    expect(data.direction, isEmpty);
    expect(data.operability, isEmpty);
    expect(data.rawReason, 'mensagem qualquer da strategy');
  });

  test('rótulos de referência e faixas de cor', () {
    expect(JevTooltipData.refLabel('estrutura_diaria'), 'Estrutura diária');
    expect(JevTooltipData.refLabel('vwap'), 'VWAP');
    expect(JevTooltipData.refLabel('desconhecido_x'), 'desconhecido x');

    expect(JevTooltipData.valueColor(0.10), const Color(0xFFEF5350));
    expect(JevTooltipData.valueColor(0.50), const Color(0xFFFFB300));
    expect(JevTooltipData.valueColor(0.90), const Color(0xFF66BB6A));
  });
}

JevTooltipData _nonJevPoint() =>
    JevTooltipData.fromPoint(_point(reason: 'mensagem qualquer da strategy'));
