import 'dart:ui' show Color;

import 'chart_data.dart';

/// Um indicador da Jev já com rótulo amigável e valor 0..1.
class JevIndicator {
  final String id;
  final String label;

  /// 0..1 no eixo de direção: 0 = baixa, 0.5 = neutro, 1 = alta.
  /// Nos indicadores de operabilidade continua sendo a confiança crua.
  final double value;
  final double? bull;
  final double? bear;

  const JevIndicator(this.id, this.label, this.value, {this.bull, this.bear});

  /// Ambos os lados respondidos (log novo, com par espelhado).
  bool get paired => bull != null && bear != null;

  /// Saldo assinado (-1..1) quando os dois lados existem.
  double? get net {
    if (paired) return (bull! - bear!).clamp(-1.0, 1.0);
    return null;
  }
}

/// Dados prontos para o cartão de hover da timeline. `criteria` é a fonte
/// preferida; o `reason` antigo (texto compacto) serve de fallback para logs
/// gravados antes do campo estruturado existir.
class JevTooltipData {
  final String vies;
  final double direcaoConfidence;
  final double operabilidade;
  final double? proximidade;
  final String proximidadeRef;
  final double? proximidadeTicks;
  /// Net assinado (-1..1): negativo = baixa/venda, positivo = alta/compra.
  final double? direcaoNet;
  /// Consenso das dimensões (0..1).
  final double? direcaoConsenso;
  /// Cobertura das dimensões (0..1).
  final double? direcaoCobertura;
  /// Motivo textual da lateral (vazio quando há compra/venda ou log antigo).
  final String direcaoMotivo;
  final List<JevIndicator> direction;
  final List<JevIndicator> operability;
  final String rawReason;

  bool get isJev => direction.isNotEmpty || operability.isNotEmpty;

  const JevTooltipData({
    required this.vies,
    required this.direcaoConfidence,
    required this.operabilidade,
    this.proximidade,
    this.proximidadeRef = '',
    this.proximidadeTicks,
    this.direcaoNet,
    this.direcaoConsenso,
    this.direcaoCobertura,
    this.direcaoMotivo = '',
    required this.direction,
    required this.operability,
    required this.rawReason,
  });

  /// Dimensões de direção: id de alta, id de baixa e rótulo curto.
  /// A UI colapsa cada par em uma linha (valor = net normalizado).
  static const List<(String, String, String)> directionPairs = [
    ('candles_alta', 'candles_baixa', 'Candles'),
    ('acima_vwap', 'abaixo_vwap', 'VWAP'),
    ('estrutura_alta', 'estrutura_baixa', 'Estrutura'),
    ('acima_poc', 'abaixo_poc', 'POC'),
    ('delta_positivo', 'delta_negativo', 'Delta'),
    ('bolhas_compra', 'bolhas_venda', 'Bolhas'),
    ('defende_suporte', 'rejeita_resistencia', 'Suporte/Resist.'),
    ('diario_alta', 'diario_baixa', 'Diário'),
  ];

  /// Indicadores de operabilidade, na ordem em que a Jev os declara.
  static const List<(String, String)> operabilityLabels = [
    ('sem_chop', 'Sem chop'),
    ('amplitude_suficiente', 'Amplitude'),
    ('sem_compressao_topovale', 'Sem compressão'),
    ('referencia_clara', 'Referência clara'),
    ('sem_conflito_intra_diario', 'Sem conflito D/I'),
  ];

  static final Set<String> _knownIds = {
    for (final (bull, _, _) in directionPairs) bull,
    for (final (_, bear, _) in directionPairs) bear,
    for (final (id, _) in operabilityLabels) id,
  };

  static const Map<String, String> _refLabels = {
    'vwap': 'VWAP',
    'pivo_intra': 'Pivô intraday',
    'estrutura_intra': 'Estrutura intraday',
    'extremo_intra': 'Extremo intraday',
    'pivo_diario': 'Pivô diário',
    'estrutura_diaria': 'Estrutura diária',
    'extremo_diario': 'Extremo diário',
  };

  factory JevTooltipData.fromPoint(StrategyPoint p) {
    final parsed = _ReasonSummary.tryParse(p.reason);
    final criteria = p.criteria;

    final direction = _collectDirections(criteria);
    final operability = _collect(criteria, operabilityLabels);

    // Sem criteria (log antigo ou strategy não-Jev): aproveita as listas
    // `dir:`/`op:` do próprio reason.
    final reasonDirection = direction.isEmpty
        ? _collectDirections(parsed?.direction ?? const {})
        : direction;
    final reasonOperability = operability.isEmpty
        ? _collect(parsed?.operabilityValues ?? const {}, operabilityLabels)
        : operability;

    final rawVies = _blank(parsed?.vies) ? p.side : parsed!.vies;
    return JevTooltipData(
      vies: normalizeSide(rawVies),
      direcaoConfidence: parsed?.confidence ?? p.confidence,
      operabilidade: parsed?.operability ?? 0,
      proximidade: parsed?.proximity,
      proximidadeRef: parsed?.proximityRef ?? '',
      proximidadeTicks: parsed?.proximityTicks,
      direcaoNet: p.direcaoNet ?? parsed?.net,
      direcaoConsenso: p.direcaoConsenso ?? parsed?.consensus,
      direcaoCobertura: p.direcaoCobertura ?? parsed?.coverage,
      direcaoMotivo: _blank(p.direcaoMotivo)
          ? (parsed?.motive ?? '')
          : p.direcaoMotivo!.trim(),
      direction: reasonDirection,
      operability: reasonOperability,
      rawReason: p.reason,
    );
  }

  /// Normaliza rótulos antigos (`manter`) para o contrato atual (`lateral`).
  static String normalizeSide(String side) {
    final s = side.trim().toLowerCase();
    return s.isEmpty || s == 'manter' || s == 'neutro' ? 'lateral' : s;
  }

  /// Rótulo curto e legível para o viés.
  static String sideLabel(String side) {
    switch (normalizeSide(side)) {
      case 'comprar':
        return 'COMPRA';
      case 'vender':
        return 'VENDA';
      default:
        return 'LATERAL';
    }
  }

  /// Rótulo curto da referência de proximidade (ex.: `estrutura_diaria`).
  static String refLabel(String ref) =>
      _refLabels[ref] ?? ref.replaceAll('_', ' ');

  /// Cor de qualidade/operabilidade: baixa (vermelho), média (âmbar), alta (verde).
  static Color qualityColor(double v) {
    if (v < 0.40) return const Color(0xFFEF5350);
    if (v < 0.65) return const Color(0xFFFFB300);
    return const Color(0xFF66BB6A);
  }

  /// Compatibilidade: mesma escala de qualidade usada antes.
  static Color valueColor(double v) => qualityColor(v);

  /// Cor direcional: 0 = venda (vermelho), 0.5 = lateral (cinza),
  /// 1 = compra (azul). Alinhada aos ícones da timeline.
  static Color directionColor(double v) {
    if (v > 0.55) return const Color(0xFF42A5F5);
    if (v < 0.45) return const Color(0xFFEF5350);
    return const Color(0xFF9E9E9E);
  }

  /// Colapsa cada par alta/baixa em um único indicador direcional.
  /// `value` = net normalizado (0.5 = neutro). Logs antigos, que só tinham
  /// a pergunta de alta, continuam exibindo a confiança crua.
  static List<JevIndicator> _collectDirections(Map<String, double> values) {
    final out = <JevIndicator>[];
    final seen = <String>{};
    for (final (bullId, bearId, label) in directionPairs) {
      final bull = values[bullId];
      final bear = values[bearId];
      if (bull != null && bear != null) {
        final net = (bull - bear).clamp(-1.0, 1.0);
        out.add(JevIndicator(bullId, label, 0.5 + 0.5 * net,
            bull: bull, bear: bear));
        seen
          ..add(bullId)
          ..add(bearId);
      } else if (bull != null) {
        out.add(JevIndicator(bullId, label, bull));
        seen.add(bullId);
      } else if (bear != null) {
        out.add(JevIndicator(bearId, label, 1 - bear, bear: bear));
        seen.add(bearId);
      }
    }
    values.forEach((id, v) {
      if (seen.contains(id) || _knownIds.contains(id)) return;
      out.add(JevIndicator(id, id.replaceAll('_', ' '), v));
    });
    return out;
  }

  static List<JevIndicator> _collect(
      Map<String, double> values, List<(String, String)> labels) {
    final out = <JevIndicator>[];
    final seen = <String>{};
    for (final (id, label) in labels) {
      final v = values[id];
      if (v == null) continue;
      seen.add(id);
      out.add(JevIndicator(id, label, v));
    }
    // Critérios novos/desconhecidos não somem do cartão (os do outro grupo
    // já foram catalogados pela sua própria seção).
    values.forEach((id, v) {
      if (seen.contains(id) || _knownIds.contains(id)) return;
      final label = id.replaceAll('_', ' ');
      out.add(JevIndicator(id, label, v));
    });
    return out;
  }

  static bool _blank(String? s) => s == null || s.trim().isEmpty;
}

/// Resumo compacto do `reason` (`vies=... conf=... op=... prox=... | dir: ...`).
class _ReasonSummary {
  final String vies;
  final double confidence;
  final double operability;
  final double? proximity;
  final String proximityRef;
  final double? proximityTicks;
  final double? net;
  final double? consensus;
  final double? coverage;
  final String? motive;
  final Map<String, double> direction;
  final Map<String, double> operabilityValues;

  const _ReasonSummary({
    required this.vies,
    required this.confidence,
    required this.operability,
    required this.proximity,
    required this.proximityRef,
    required this.proximityTicks,
    required this.net,
    required this.consensus,
    required this.coverage,
    required this.motive,
    required this.direction,
    required this.operabilityValues,
  });

  static final _viesRe = RegExp(r'vies=(\S+)');
  static final _confRe = RegExp(r'\bconf=([0-9]+(?:\.[0-9]+)?)');
  static final _opRe = RegExp(r'\bop=([0-9]+(?:\.[0-9]+)?)');
  static final _netRe = RegExp(r'\bnet=(-?[0-9]+(?:\.[0-9]+)?)');
  static final _consRe = RegExp(r'\bcons=(-?[0-9]+(?:\.[0-9]+)?)');
  static final _covRe = RegExp(r'\bcov=(-?[0-9]+(?:\.[0-9]+)?)');
  static final _motivoRe =
      RegExp(r'\bmotivo=(.*?)(?=\s+(?:op|prox)=|$)');
  static final _proxRe = RegExp(
      r'\bprox=([0-9]+(?:\.[0-9]+)?)\(([^)]*?)\s+([0-9]+(?:\.[0-9]+)?)t\)');
  static final _dirRe = RegExp(r'dir:\s*(.*?)(?:\s*\|\s*op:|$)');
  static final _opSectionRe = RegExp(r'\bop:\s*(.*)$');
  static final _pairRe = RegExp(r'([A-Za-z_][A-Za-z0-9_]*)=([0-9]+(?:\.[0-9]+)?)%');

  static _ReasonSummary? tryParse(String reason) {
    if (reason.trim().isEmpty) return null;
    final vies = _viesRe.firstMatch(reason)?.group(1);
    final conf = double.tryParse(_confRe.firstMatch(reason)?.group(1) ?? '');
    if (vies == null && conf == null) return null;

    final proxMatch = _proxRe.firstMatch(reason);
    final dirSection = _dirRe.firstMatch(reason)?.group(1) ?? '';
    final opSection = _opSectionRe.firstMatch(reason)?.group(1) ?? '';

    return _ReasonSummary(
      vies: vies ?? '',
      confidence: conf ?? 0,
      operability: double.tryParse(_opRe.firstMatch(reason)?.group(1) ?? '') ?? 0,
      proximity: double.tryParse(proxMatch?.group(1) ?? ''),
      proximityRef: proxMatch?.group(2)?.trim() ?? '',
      proximityTicks: double.tryParse(proxMatch?.group(3) ?? ''),
      net: double.tryParse(_netRe.firstMatch(reason)?.group(1) ?? ''),
      consensus: double.tryParse(_consRe.firstMatch(reason)?.group(1) ?? ''),
      coverage: double.tryParse(_covRe.firstMatch(reason)?.group(1) ?? ''),
      motive: _motivoRe.firstMatch(reason)?.group(1)?.trim(),
      direction: _pairs(dirSection),
      operabilityValues: _pairs(opSection),
    );
  }

  static Map<String, double> _pairs(String section) {
    final out = <String, double>{};
    for (final m in _pairRe.allMatches(section)) {
      final id = m.group(1);
      final pct = double.tryParse(m.group(2) ?? '');
      if (id == null || pct == null) continue;
      out[id] = (pct / 100).clamp(0.0, 1.0);
    }
    return out;
  }
}
