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
  double? direcaoNet,
  double? direcaoConsenso,
  double? direcaoCobertura,
  String? direcaoMotivo,
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
      direcaoNet: direcaoNet,
      direcaoConsenso: direcaoConsenso,
      direcaoCobertura: direcaoCobertura,
      direcaoMotivo: direcaoMotivo,
      snapshotHash: 'ABC',
    );

void main() {
  test('colapsa cada par alta/baixa em um indicador de direção', () {
    final data = JevTooltipData.fromPoint(_point(criteria: const {
      'candles_alta': 0.80,
      'candles_baixa': 0.20,
      'acima_vwap': 0.70,
      'abaixo_vwap': 0.30,
      'estrutura_alta': 0.60,
      'estrutura_baixa': 0.40,
      'acima_poc': 0.55,
      'abaixo_poc': 0.45,
      'delta_positivo': 0.90,
      'delta_negativo': 0.10,
      'bolhas_compra': 0.40,
      'bolhas_venda': 0.30,
      'defende_suporte': 0.60,
      'rejeita_resistencia': 0.20,
      'diario_alta': 0.50,
      'diario_baixa': 0.50,
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
    expect(data.direction.first.label, 'Candles');
    // net 0.80 - 0.20 = 0.60 → normalizado 0.80.
    expect(data.direction.first.value, closeTo(0.80, 1e-9));
    expect(data.direction.first.bull, 0.80);
    expect(data.direction.first.bear, 0.20);
    expect(data.direction.last.label, 'Diário');
    expect(data.direction.last.value, closeTo(0.50, 1e-9));
    expect(data.operability.first.value, 0.59);
  });

  test('novos pares de reversão aparecem no tooltip', () {
    final data = JevTooltipData.fromPoint(_point(criteria: const {
      'reversao_suporte': 0.80,
      'reversao_resistencia': 0.20,
      'exaustao_queda': 0.70,
      'exaustao_alta': 0.30,
    }));

    expect(data.direction.map((e) => e.id).toList(), [
      'reversao_suporte',
      'exaustao_queda',
    ]);
    expect(data.direction.first.label, 'Reversão S/R');
    expect(data.direction.last.label, 'Exaustão');
  });

  test('faz fallback das listas dir/op do reason quando criteria vazio', () {
    final data = JevTooltipData.fromPoint(_point());

    expect(data.isJev, isTrue);
    expect(data.direction.length, 8);
    expect(data.operability.length, 5);
    // Log antigo só tem a pergunta de alta: mantém a confiança crua.
    expect(data.direction.first.value, closeTo(0.36, 1e-9));
    expect(data.direction.last.value, closeTo(0.37, 1e-9));
    expect(data.operability.first.value, closeTo(0.59, 1e-9));
    expect(data.operability.last.value, closeTo(0.66, 1e-9));
  });

  test('lateral mantém o viés e o rótulo lateral', () {
    final data = JevTooltipData.fromPoint(StrategyPoint(
      candleIndex: 10,
      at: DateTime(2026, 10, 20, 9, 26),
      kind: 'avaliacao',
      event: 'bubble',
      side: 'lateral',
      confidence: 0,
      encerrar: 0,
      action: 'manter',
      criteria: const {'candles_alta': 0.8, 'candles_baixa': 0.8},
      reason: 'vies=lateral conf=0.00 op=0.00 prox=0.00( 0t) | dir: | op: ',
      snapshotHash: 'ABC',
    ));

    expect(data.isJev, isTrue);
    expect(data.vies, 'lateral');
    expect(data.direction.single.value, closeTo(0.5, 1e-9));
  });

  test('usa campos estruturados de saldo, consenso e cobertura', () {
    final data = JevTooltipData.fromPoint(_point(
      criteria: const {'candles_alta': 0.80, 'candles_baixa': 0.20},
      direcaoNet: -0.42,
      direcaoConsenso: 0.75,
      direcaoCobertura: 1.0,
      direcaoMotivo: 'evidência fraca',
    ));

    expect(data.direcaoNet, -0.42);
    expect(data.direcaoConsenso, 0.75);
    expect(data.direcaoCobertura, 1.0);
    expect(data.direcaoMotivo, 'evidência fraca');
    expect(data.direction.first.net, closeTo(0.60, 1e-9));
  });

  test('faz fallback de net/cons/cov/motivo pelo reason', () {
    final data = JevTooltipData.fromPoint(_point(
      reason: 'vies=vender conf=0.17 net=-0.42 cons=0.75 cov=1.00 '
          'motivo=evidência fraca op=0.62 '
          'prox=0.90(estrutura_diaria 13t) | dir: | op: ',
    ));

    expect(data.direcaoNet, -0.42);
    expect(data.direcaoConsenso, 0.75);
    expect(data.direcaoCobertura, 1.0);
    expect(data.direcaoMotivo, 'evidência fraca');
  });

  test('normaliza manter antigo para lateral', () {
    expect(JevTooltipData.normalizeSide('manter'), 'lateral');
    expect(JevTooltipData.normalizeSide('LATERAL'), 'lateral');
    expect(JevTooltipData.normalizeSide('comprar'), 'comprar');
    expect(JevTooltipData.sideLabel('manter'), 'LATERAL');
    expect(JevTooltipData.sideLabel('vender'), 'VENDA');
    expect(JevTooltipData.sideLabel('comprar'), 'COMPRA');
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
    expect(JevTooltipData.directionColor(0.10), const Color(0xFFEF5350));
    expect(JevTooltipData.directionColor(0.50), const Color(0xFF9E9E9E));
    expect(JevTooltipData.directionColor(0.90), const Color(0xFF42A5F5));
  });
}

JevTooltipData _nonJevPoint() =>
    JevTooltipData.fromPoint(_point(reason: 'mensagem qualquer da strategy'));
