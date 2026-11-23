/// Especificação canônica de um símbolo no cliente (issue #47).
///
/// Espelha o `SymbolRegistry` do backend: adicionar/ajustar um símbolo é uma
/// entrada em [Defaults._specs], sem espalhar `switch` pelos widgets.
class SymbolSpec {
  final String name;
  final String mt5Root;

  /// Caminho do asset da bandeira (PNG oficial), vazio quando não há.
  final String flag;
  final int thresholdBubbleSize;
  final double minDistanceUpdateBorder;
  final double minDistanceUpdateBorderDaily;
  final double tickSize;
  final double pointValue;
  final double structureRangeUpdStep;
  final double structureRangeUpdMax;
  final double structureRangeUpdDailyMax;
  final double structureRangeUpdDailyStep;

  const SymbolSpec({
    required this.name,
    required this.mt5Root,
    required this.flag,
    required this.thresholdBubbleSize,
    required this.minDistanceUpdateBorder,
    required this.minDistanceUpdateBorderDaily,
    required this.tickSize,
    required this.pointValue,
    required this.structureRangeUpdStep,
    required this.structureRangeUpdMax,
    required this.structureRangeUpdDailyMax,
    required this.structureRangeUpdDailyStep,
  });
}

class Defaults {
  static const String url = 'https://localhost:5002/api/datahub';

  /// Seletor principal é só intraday (issue #12): o 1D vive no widget diário.
  static const List<int> timeFrames = [1, 2, 5, 15, 30, 60];

  static String timeFrameLabel(int tf) => tf == 1440 ? '1D' : '$tf';

  static const String symbolWinfut = 'WINFUT';
  static const String symbolIndfut = 'INDFUT';
  static const String symbolWdofut = 'WDOFUT';
  static const String symbolDolfut = 'DOLFUT';

  /// Ordem do seletor: índice mini/cheio, dólar mini/cheio.
  static const List<String> symbols = [
    symbolWinfut,
    symbolIndfut,
    symbolWdofut,
    symbolDolfut,
  ];

  static const Map<String, SymbolSpec> _specs = {
    symbolWinfut: SymbolSpec(
      name: symbolWinfut,
      mt5Root: 'WIN',
      flag: 'assets/images/bandeira-brasil.png',
      thresholdBubbleSize: 250,
      minDistanceUpdateBorder: 250,
      minDistanceUpdateBorderDaily: 1000,
      tickSize: 5.0,
      pointValue: 1.0,
      structureRangeUpdStep: 5.0,
      structureRangeUpdMax: 2000,
      structureRangeUpdDailyMax: 10000,
      structureRangeUpdDailyStep: 50.0,
    ),
    // Contrato cheio do índice: mesmo preço/pontos do WIN. Lote mínimo de 5
    // contratos e liquidez bem menor que o mini: 50 (10 lotes) é o threshold
    // de detecção calibrado pelos volumes reais.
    symbolIndfut: SymbolSpec(
      name: symbolIndfut,
      mt5Root: 'IND',
      flag: 'assets/images/bandeira-brasil.png',
      thresholdBubbleSize: 50,
      minDistanceUpdateBorder: 250,
      minDistanceUpdateBorderDaily: 1000,
      tickSize: 5.0,
      pointValue: 1.0,
      structureRangeUpdStep: 5.0,
      structureRangeUpdMax: 2000,
      structureRangeUpdDailyMax: 10000,
      structureRangeUpdDailyStep: 50.0,
    ),
    symbolWdofut: SymbolSpec(
      name: symbolWdofut,
      mt5Root: 'WDO',
      flag: 'assets/images/bandeira-eua.png',
      thresholdBubbleSize: 500,
      minDistanceUpdateBorder: 4.5,
      minDistanceUpdateBorderDaily: 100,
      tickSize: 0.5,
      pointValue: 10.0,
      structureRangeUpdStep: 0.5,
      structureRangeUpdMax: 100,
      structureRangeUpdDailyMax: 1000,
      structureRangeUpdDailyStep: 1.0,
    ),
    // Contrato cheio do dólar: mesmo preço/pontos do WDO, valor do tick 5x.
    // Lote mínimo de 5; 50 (10 lotes) acompanha a liquidez real do DOL.
    symbolDolfut: SymbolSpec(
      name: symbolDolfut,
      mt5Root: 'DOL',
      flag: 'assets/images/bandeira-eua.png',
      thresholdBubbleSize: 50,
      minDistanceUpdateBorder: 2.5,
      minDistanceUpdateBorderDaily: 100,
      tickSize: 0.5,
      pointValue: 50.0,
      structureRangeUpdStep: 0.5,
      structureRangeUpdMax: 100,
      structureRangeUpdDailyMax: 1000,
      structureRangeUpdDailyStep: 1.0,
    ),
  };

  /// Fallback histórico para símbolo desconhecido (mesmos valores que os
  /// antigos `default` dos switches).
  static const SymbolSpec _fallback = SymbolSpec(
    name: '',
    mt5Root: '',
    flag: '',
    thresholdBubbleSize: 250,
    minDistanceUpdateBorder: 250,
    minDistanceUpdateBorderDaily: 1000,
    tickSize: 1.0,
    pointValue: 1.0,
    structureRangeUpdStep: 5.0,
    structureRangeUpdMax: 2000,
    structureRangeUpdDailyMax: 10000,
    structureRangeUpdDailyStep: 50.0,
  );

  static SymbolSpec specFor(String symbol) => _specs[symbol] ?? _fallback;

  static String flagFor(String symbol) => specFor(symbol).flag;

  static int thresholdBubbleSize(String symbol) =>
      specFor(symbol).thresholdBubbleSize;

  static const int thresholdBubbleMin = 5;

  static double minDistanceUpdateBorder(String symbol) =>
      specFor(symbol).minDistanceUpdateBorder;

  /// Modos do range intraday da estrutura (issue #25). As strings são a
  /// chave estável usada na API e no arquivo de config do backend.
  static const String structureRangeModeManual = 'manual';
  static const String structureRangeModeAtrPerLeg = 'atr_leg';
  static const String structureRangeModeAtrBar = 'atr_bar';

  static const List<String> structureRangeModes = [
    structureRangeModeManual,
    structureRangeModeAtrPerLeg,
    structureRangeModeAtrBar,
  ];

  static String normalizeStructureRangeMode(String? mode) {
    switch (mode) {
      case structureRangeModeAtrPerLeg:
        return structureRangeModeAtrPerLeg;
      case structureRangeModeAtrBar:
        return structureRangeModeAtrBar;
      default:
        return structureRangeModeManual;
    }
  }

  static String structureRangeModeLabel(String mode) {
    switch (normalizeStructureRangeMode(mode)) {
      case structureRangeModeAtrPerLeg:
        return 'ATR perna';
      case structureRangeModeAtrBar:
        return 'ATR barra';
      default:
        return 'Manual';
    }
  }

  /// Distanciamento de quebra de estrutura do 1440 (seção diária, issue #10).
  static double minDistanceUpdateBorderDaily(String symbol) =>
      specFor(symbol).minDistanceUpdateBorderDaily;

  static double tickSize(String symbol) => specFor(symbol).tickSize;

  static double pointValue(String symbol) => specFor(symbol).pointValue;

  static double structureRangeUpdStep(String symbol) =>
      specFor(symbol).structureRangeUpdStep;

  static double structureRangeUpdMax(String symbol) =>
      specFor(symbol).structureRangeUpdMax;

  /// Teto do slider diário (issue #10): WINFUT até 10000, WDO até 1000.
  static double structureRangeUpdDailyMax(String symbol) =>
      specFor(symbol).structureRangeUpdDailyMax;

  static double structureRangeUpdDailyStep(String symbol) =>
      specFor(symbol).structureRangeUpdDailyStep;

  static const double extremeNoiseSensitivity = 3.0;
  static const double extremeMinimumProminence = 0.15;

  static const String extremeTopColor = '#69F0AE';
  static const String extremeValleyColor = '#FF5252';
  static const String dailyExtremeTopColor = '#43A047';
  static const String dailyExtremeValleyColor = '#E53935';

  static const double extremeNoiseSensitivityMin = 1.0;
  static const double extremeNoiseSensitivityMax = 10.0;
  static const double extremeNoiseSensitivityStep = 0.5;
  static const double extremeMinimumProminenceMin = 0.05;
  static const double extremeMinimumProminenceMax = 0.5;
  static const double extremeMinimumProminenceStep = 0.05;
}
