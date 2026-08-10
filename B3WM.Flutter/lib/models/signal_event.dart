class SignalEvent {
  final String symbol;
  final int timeFrame;
  final DateTime date;
  final String type;
  final String side;
  final double entryPrice;
  final double exitPrice;
  final double stopPrice;
  final double targetPrice;
  final int quantity;
  final String? reason;
  final double points;
  final double profitLoss;
  final double commission;
  final double cumulativePL;
  final bool positionOpen;

  const SignalEvent({
    required this.symbol,
    required this.timeFrame,
    required this.date,
    required this.type,
    required this.side,
    required this.entryPrice,
    required this.exitPrice,
    required this.stopPrice,
    required this.targetPrice,
    required this.quantity,
    this.reason,
    required this.points,
    required this.profitLoss,
    required this.commission,
    required this.cumulativePL,
    required this.positionOpen,
  });

  factory SignalEvent.fromJson(Map<String, dynamic> json) {
    return SignalEvent(
      symbol: json['symbol'] as String,
      timeFrame: json['timeFrame'] as int,
      date: DateTime.parse(json['date'] as String).toLocal(),
      type: json['type'] as String,
      side: json['side'] as String,
      entryPrice: (json['entryPrice'] as num?)?.toDouble() ?? 0,
      exitPrice: (json['exitPrice'] as num?)?.toDouble() ?? 0,
      stopPrice: (json['stopPrice'] as num?)?.toDouble() ?? 0,
      targetPrice: (json['targetPrice'] as num?)?.toDouble() ?? 0,
      quantity: (json['quantity'] as num?)?.toInt() ?? 1,
      reason: json['reason'] as String?,
      points: (json['points'] as num?)?.toDouble() ?? 0,
      profitLoss: (json['profitLoss'] as num?)?.toDouble() ?? 0,
      commission: (json['commission'] as num?)?.toDouble() ?? 0,
      cumulativePL: (json['cumulativePL'] as num?)?.toDouble() ?? 0,
      positionOpen: json['positionOpen'] as bool? ?? false,
    );
  }

  bool get isEntry => type.startsWith('Entry');
  bool get isExit => !isEntry;

  @override
  String toString() {
    final when =
        '${date.hour.toString().padLeft(2, '0')}:${date.minute.toString().padLeft(2, '0')}';
    return '[$when] $type $side ${isEntry ? entryPrice : exitPrice}';
  }
}
