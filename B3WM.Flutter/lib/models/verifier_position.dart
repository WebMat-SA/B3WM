class VerifierPosition {
  String side;
  double entryPrice;
  double stopPrice;
  double targetPrice;
  int quantity;
  DateTime entryDate;
  String? entryReason;

  VerifierPosition({
    required this.side,
    required this.entryPrice,
    required this.stopPrice,
    required this.targetPrice,
    required this.quantity,
    required this.entryDate,
    this.entryReason,
  });

  factory VerifierPosition.fromJson(Map<String, dynamic> json) {
    return VerifierPosition(
      side: json['side'] as String,
      entryPrice: (json['entryPrice'] as num?)?.toDouble() ?? 0,
      stopPrice: (json['stopPrice'] as num?)?.toDouble() ?? 0,
      targetPrice: (json['targetPrice'] as num?)?.toDouble() ?? 0,
      quantity: (json['quantity'] as num?)?.toInt() ?? 1,
      entryDate: DateTime.parse(json['entryDate'] as String).toLocal(),
      entryReason: json['entryReason'] as String?,
    );
  }
}
