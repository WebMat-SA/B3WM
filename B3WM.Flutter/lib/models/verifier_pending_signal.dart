class VerifierPendingSignal {
  String side;
  String? reason;
  double stopLossPrice;
  double takeProfitPrice;

  VerifierPendingSignal({
    required this.side,
    this.reason,
    this.stopLossPrice = 0,
    this.takeProfitPrice = 0,
  });

  bool get isBuy => side == 'Buy';

  factory VerifierPendingSignal.fromJson(Map<String, dynamic> json) {
    return VerifierPendingSignal(
      side: json['side'] as String? ?? '',
      reason: json['reason'] as String?,
      stopLossPrice: (json['stopLossPrice'] as num?)?.toDouble() ?? 0,
      takeProfitPrice: (json['takeProfitPrice'] as num?)?.toDouble() ?? 0,
    );
  }
}
