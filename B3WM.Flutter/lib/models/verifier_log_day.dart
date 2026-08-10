import 'signal_event.dart';
import 'verifier_config.dart';

class VerifierLogDay {
  String symbol;
  int timeFrame;
  DateTime date;
  VerifierConfig? config;
  List<SignalEvent> signals;
  int totalTrades;
  int winCount;
  int lossCount;
  double winRate;
  double netProfit;
  double grossProfit;
  double grossLoss;
  double maxDrawdown;
  List<double> equityCurve;

  VerifierLogDay({
    required this.symbol,
    required this.timeFrame,
    required this.date,
    this.config,
    List<SignalEvent>? signals,
    this.totalTrades = 0,
    this.winCount = 0,
    this.lossCount = 0,
    this.winRate = 0,
    this.netProfit = 0,
    this.grossProfit = 0,
    this.grossLoss = 0,
    this.maxDrawdown = 0,
    List<double>? equityCurve,
  })  : signals = signals ?? [],
        equityCurve = equityCurve ?? [];

  factory VerifierLogDay.fromJson(Map<String, dynamic> json) {
    return VerifierLogDay(
      symbol: json['symbol'] as String? ?? '',
      timeFrame: (json['timeFrame'] as num?)?.toInt() ?? 0,
      date: DateTime.parse(json['date'] as String).toLocal(),
      config: json['config'] != null
          ? VerifierConfig.fromJson(json['config'] as Map<String, dynamic>)
          : null,
      signals: (json['signals'] as List?)
              ?.map((e) => SignalEvent.fromJson(e as Map<String, dynamic>))
              .toList() ??
          [],
      totalTrades: (json['totalTrades'] as num?)?.toInt() ?? 0,
      winCount: (json['winCount'] as num?)?.toInt() ?? 0,
      lossCount: (json['lossCount'] as num?)?.toInt() ?? 0,
      winRate: (json['winRate'] as num?)?.toDouble() ?? 0,
      netProfit: (json['netProfit'] as num?)?.toDouble() ?? 0,
      grossProfit: (json['grossProfit'] as num?)?.toDouble() ?? 0,
      grossLoss: (json['grossLoss'] as num?)?.toDouble() ?? 0,
      maxDrawdown: (json['maxDrawdown'] as num?)?.toDouble() ?? 0,
      equityCurve: (json['equityCurve'] as List?)
              ?.map((e) => (e as num).toDouble())
              .toList() ??
          [],
    );
  }
}
