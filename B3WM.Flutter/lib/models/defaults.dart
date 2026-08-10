class Defaults {
  static const String url = 'https://localhost:5002/api/datahub';
  static const List<int> timeFrames = [1, 2, 5, 15, 30, 60];

  static const String symbolWinfut = 'WINFUT';
  static const String symbolWdofut = 'WDOFUT';

  static int thresholdBubbleSize(String symbol) {
    switch (symbol) {
      case 'WINFUT':
        return 250;
      case 'WDOFUT':
        return 500;
      default:
        return 250;
    }
  }

  static double minDistanceUpdateBorder(String symbol) {
    switch (symbol) {
      case 'WINFUT':
        return 250;
      case 'WDOFUT':
        return 2.5;
      default:
        return 250;
    }
  }

  static double tickSize(String symbol) {
    switch (symbol) {
      case 'WINFUT':
        return 5.0;
      case 'WDOFUT':
        return 0.5;
      default:
        return 1.0;
    }
  }

  static double pointValue(String symbol) {
    switch (symbol) {
      case 'WINFUT':
        return 1.0;
      case 'WDOFUT':
        return 10.0;
      default:
        return 1.0;
    }
  }

  static double structureRangeUpdStep(String symbol) {
    switch (symbol) {
      case 'WDOFUT':
        return 0.5;
      default:
        return 5.0;
    }
  }

  static double structureRangeUpdMax(String symbol) {
    switch (symbol) {
      case 'WDOFUT':
        return 100;
      default:
        return 2000;
    }
  }
}
