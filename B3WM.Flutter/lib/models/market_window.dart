/// Janela de datas de uma camada/gráfico (intraday ou diário).
/// [auto] = true quando derivada do automode por estrutura no backend;
/// false = manual (slider from/to do drawer). Espelho de
/// `B3WM.Shared/Models/MarketWindow.cs`.
class StructureWindow {
  final DateTime from;
  final DateTime to;
  final bool auto;

  StructureWindow({
    required this.from,
    required this.to,
    required this.auto,
  });

  factory StructureWindow.fromJson(Map<String, dynamic> json) => StructureWindow(
        from: DateTime.parse(json['from'] as String),
        to: DateTime.parse(json['to'] as String),
        auto: json['auto'] as bool? ?? false,
      );

  Map<String, dynamic> toJson() => {
        'from': from.toIso8601String(),
        'to': to.toIso8601String(),
        'auto': auto,
      };
}

/// Foto da janela vigente de um símbolo/timeframe: janela intraday (do
/// timeframe informado) + janela diária (1440). Recebida via SignalR
/// (`ReceiveOnMarketWindow`) e REST (`GetMarketWindow`). Fonte única da
/// janela do automode — o app apenas consome.
class MarketWindow {
  final String symbol;
  final int timeFrame;
  final StructureWindow intraday;
  final StructureWindow daily;

  MarketWindow({
    required this.symbol,
    required this.timeFrame,
    required this.intraday,
    required this.daily,
  });

  factory MarketWindow.fromJson(Map<String, dynamic> json) => MarketWindow(
        symbol: json['symbol'] as String,
        timeFrame: json['timeFrame'] as int,
        intraday: StructureWindow.fromJson(json['intraday'] as Map<String, dynamic>),
        daily: StructureWindow.fromJson(json['daily'] as Map<String, dynamic>),
      );

  Map<String, dynamic> toJson() => {
        'symbol': symbol,
        'timeFrame': timeFrame,
        'intraday': intraday.toJson(),
        'daily': daily.toJson(),
      };
}
