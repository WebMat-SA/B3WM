class VwapHistoryPoint {
  final DateTime minute;
  final double vwap;
  final double sumPv;
  final int sumV;
  final String symbol;
  final DateTime day;

  VwapHistoryPoint({
    required this.minute,
    required this.vwap,
    required this.sumPv,
    required this.sumV,
    required this.symbol,
    required this.day,
  });

  factory VwapHistoryPoint.fromJson(Map<String, dynamic> json) {
    int parseSumV(dynamic v) {
      if (v == null) return 0;
      if (v is int) return v;
      if (v is double) return v.toInt();
      if (v is num) return v.toInt();
      return 0;
    }

    return VwapHistoryPoint(
      // Backend serializa como "Minute" (PascalCase System.Text.Json default).
      // Aceita ambos os casings para compat.
      minute: DateTime.parse(
          (json['minute'] ?? json['Minute']) as String),
      vwap: ((json['vwap'] ?? json['Vwap']) as num).toDouble(),
      sumPv: ((json['sumPv'] ?? json['SumPv'] ?? 0) as num).toDouble(),
      sumV: parseSumV(json['sumV'] ?? json['SumV']),
      symbol: (json['symbol'] ?? json['Symbol']) as String? ?? '',
      day: DateTime.parse(
          (json['day'] ?? json['Day']) as String),
    );
  }

  Map<String, dynamic> toJson() => {
        'minute': minute.toIso8601String(),
        'vwap': vwap,
        'sumPv': sumPv,
        'sumV': sumV,
        'symbol': symbol,
        'day': day.toIso8601String(),
      };
}
