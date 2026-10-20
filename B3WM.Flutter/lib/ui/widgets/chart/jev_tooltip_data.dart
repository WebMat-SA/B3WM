import 'dart:ui' show Color;

import 'chart_data.dart';

/// Um indicador da Jev já com rótulo amigável e valor 0..1.
class JevIndicator {
  final String id;
  final String label;
  final double value;

  const JevIndicator(this.id, this.label, this.value);
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
    required this.direction,
    required this.operability,
    required this.rawReason,
  });

  /// Indicadores de direção, na ordem em que a Jev os declara.
  static const List<(String, String)> directionLabels = [
    ('candles_alta', 'Candles alta'),
    ('acima_vwap', 'Acima do VWAP'),
    ('estrutura_alta', 'Estrutura alta'),
    ('acima_poc', 'Acima do POC'),
    ('delta_positivo', 'Delta positivo'),
    ('bolhas_compra', 'Bolhas compra'),
    ('defende_suporte', 'Defende suporte'),
    ('diario_alta', 'Diário alta'),
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
    for (final (id, _) in directionLabels) id,
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

    final direction = _collect(criteria, directionLabels);
    final operability = _collect(criteria, operabilityLabels);

    // Sem criteria (log antigo ou strategy não-Jev): aproveita as listas
    // `dir:`/`op:` do próprio reason.
    final reasonDirection =
        direction.isEmpty ? _collect(parsed?.direction ?? const {}, directionLabels) : direction;
    final reasonOperability = operability.isEmpty
        ? _collect(parsed?.operabilityValues ?? const {}, operabilityLabels)
        : operability;

    return JevTooltipData(
      vies: _blank(parsed?.vies) ? p.side : parsed!.vies,
      direcaoConfidence: parsed?.confidence ?? p.confidence,
      operabilidade: parsed?.operability ?? 0,
      proximidade: parsed?.proximity,
      proximidadeRef: parsed?.proximityRef ?? '',
      proximidadeTicks: parsed?.proximityTicks,
      direction: reasonDirection,
      operability: reasonOperability,
      rawReason: p.reason,
    );
  }

  /// Rótulo curto da referência de proximidade (ex.: `estrutura_diaria`).
  static String refLabel(String ref) =>
      _refLabels[ref] ?? ref.replaceAll('_', ' ');

  /// Cor por faixa de confiança: baixa (vermelho), média (âmbar), alta (verde).
  static Color valueColor(double v) {
    if (v < 0.40) return const Color(0xFFEF5350);
    if (v < 0.65) return const Color(0xFFFFB300);
    return const Color(0xFF66BB6A);
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
  final Map<String, double> direction;
  final Map<String, double> operabilityValues;

  const _ReasonSummary({
    required this.vies,
    required this.confidence,
    required this.operability,
    required this.proximity,
    required this.proximityRef,
    required this.proximityTicks,
    required this.direction,
    required this.operabilityValues,
  });

  static final _viesRe = RegExp(r'vies=(\S+)');
  static final _confRe = RegExp(r'\bconf=([0-9]+(?:\.[0-9]+)?)');
  static final _opRe = RegExp(r'\bop=([0-9]+(?:\.[0-9]+)?)');
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
