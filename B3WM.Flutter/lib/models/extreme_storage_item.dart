enum ExtremeType {
  top,
  valley,
  indeterminate;

  /// Aceita tanto o valor numérico do enum (SignalR/arquivos) quanto a forma
  /// em string usada pela API HTTP (JsonStringEnumConverter), ex: 0/'Top'.
  static ExtremeType fromServer(dynamic value) {
    if (value is String) {
      switch (value.toLowerCase()) {
        case 'top':
          return ExtremeType.top;
        case 'valley':
          return ExtremeType.valley;
        default:
          return ExtremeType.indeterminate;
      }
    }
    final intValue = value is num ? value.toInt() : 2;
    if (intValue == 0) return ExtremeType.top;
    if (intValue == 1) return ExtremeType.valley;
    return ExtremeType.indeterminate;
  }
}

class ExtremePoint {
  final double position;
  final double value;
  final ExtremeType type;
  final double prominence;
  final double strength;
  final double confidence;
  final double width;
  final bool isEdge;

  ExtremePoint({
    required this.position,
    required this.value,
    required this.type,
    required this.prominence,
    required this.strength,
    required this.confidence,
    required this.width,
    required this.isEdge,
  });

  factory ExtremePoint.fromJson(Map<String, dynamic> json) => ExtremePoint(
        position: (json['position'] as num?)?.toDouble() ?? 0,
        value: (json['value'] as num?)?.toDouble() ?? 0,
        type: ExtremeType.fromServer(json['type']),
        prominence: (json['prominence'] as num?)?.toDouble() ?? 0,
        strength: (json['strength'] as num?)?.toDouble() ?? 0,
        confidence: (json['confidence'] as num?)?.toDouble() ?? 0,
        width: (json['width'] as num?)?.toDouble() ?? 0,
        isEdge: json['isEdge'] as bool? ?? false,
      );
}

/// Snapshot da detecção de topos/vales estruturais enviada pelo servidor
/// (B3WM.Shared ExtremeStorageItem).
class ExtremeStorageItem {
  final String symbol;
  final DateTime? date;
  final DateTime? periodFrom;
  final DateTime? periodTo;
  final List<ExtremePoint> extremes;
  final double noiseSensitivity;
  final double minimumProminence;
  final int topCount;
  final int valleyCount;

  ExtremeStorageItem({
    this.symbol = '',
    this.date,
    this.periodFrom,
    this.periodTo,
    this.extremes = const [],
    this.noiseSensitivity = 3.0,
    this.minimumProminence = 0.15,
    this.topCount = 0,
    this.valleyCount = 0,
  });

  factory ExtremeStorageItem.fromJson(Map<String, dynamic> json) {
    final stats = json['statistics'] as Map<String, dynamic>?;
    final config = json['config'] as Map<String, dynamic>?;
    return ExtremeStorageItem(
      symbol: json['symbol'] as String? ?? '',
      date: json['date'] != null ? DateTime.tryParse(json['date'] as String) : null,
      periodFrom: json['periodFrom'] != null
          ? DateTime.tryParse(json['periodFrom'] as String)
          : null,
      periodTo: json['periodTo'] != null
          ? DateTime.tryParse(json['periodTo'] as String)
          : null,
      extremes: (json['extremes'] as List<dynamic>? ?? [])
          .map((e) => ExtremePoint.fromJson(e as Map<String, dynamic>))
          .toList(),
      noiseSensitivity:
          (config?['noiseSensitivity'] as num?)?.toDouble() ?? 3.0,
      minimumProminence:
          (config?['minimumProminence'] as num?)?.toDouble() ?? 0.15,
      topCount: stats?['topCount'] as int? ?? 0,
      valleyCount: stats?['valleyCount'] as int? ?? 0,
    );
  }
}