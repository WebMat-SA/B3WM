import 'signal_event.dart';
import 'verifier_config.dart';
import 'verifier_pending_signal.dart';
import 'verifier_position.dart';

class VerifierState {
  String symbol;
  int timeFrame;
  bool isRunning;
  VerifierConfig? config;
  VerifierPosition? openPosition;
  VerifierPendingSignal? pendingSignal;
  int totalTrades;
  int winCount;
  int lossCount;
  double winRate;
  double netProfit;
  double grossProfit;
  double grossLoss;
  double maxDrawdown;
  List<SignalEvent> signals;
  List<double> equityCurve;

  VerifierState({
    required this.symbol,
    required this.timeFrame,
    this.isRunning = false,
    this.config,
    this.openPosition,
    this.pendingSignal,
    this.totalTrades = 0,
    this.winCount = 0,
    this.lossCount = 0,
    this.winRate = 0,
    this.netProfit = 0,
    this.grossProfit = 0,
    this.grossLoss = 0,
    this.maxDrawdown = 0,
    List<SignalEvent>? signals,
    List<double>? equityCurve,
  })  : signals = signals ?? [],
        equityCurve = equityCurve ?? [];

  factory VerifierState.fromJson(Map<String, dynamic> json) {
    return VerifierState(
      symbol: json['symbol'] as String? ?? '',
      timeFrame: (json['timeFrame'] as num?)?.toInt() ?? 0,
      isRunning: json['isRunning'] as bool? ?? false,
      config: json['config'] != null
          ? VerifierConfig.fromJson(json['config'] as Map<String, dynamic>)
          : null,
      openPosition: json['openPosition'] != null
          ? VerifierPosition.fromJson(
              json['openPosition'] as Map<String, dynamic>)
          : null,
      pendingSignal: json['pendingSignal'] != null
          ? VerifierPendingSignal.fromJson(
              json['pendingSignal'] as Map<String, dynamic>)
          : null,
      totalTrades: (json['totalTrades'] as num?)?.toInt() ?? 0,
      winCount: (json['winCount'] as num?)?.toInt() ?? 0,
      lossCount: (json['lossCount'] as num?)?.toInt() ?? 0,
      winRate: (json['winRate'] as num?)?.toDouble() ?? 0,
      netProfit: (json['netProfit'] as num?)?.toDouble() ?? 0,
      grossProfit: (json['grossProfit'] as num?)?.toDouble() ?? 0,
      grossLoss: (json['grossLoss'] as num?)?.toDouble() ?? 0,
      maxDrawdown: (json['maxDrawdown'] as num?)?.toDouble() ?? 0,
      signals: (json['signals'] as List?)
              ?.map((e) => SignalEvent.fromJson(e as Map<String, dynamic>))
              .toList() ??
          [],
      equityCurve: (json['equityCurve'] as List?)
              ?.map((e) => (e as num).toDouble())
              .toList() ??
          [],
    );
  }
}
