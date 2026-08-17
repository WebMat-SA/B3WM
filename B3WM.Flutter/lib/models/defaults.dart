class Defaults {
  static const String url = 'https://localhost:5002/api/datahub';
  static const List<int> timeFrames = [1, 2, 5, 15, 30, 60, 1440];

  static String timeFrameLabel(int tf) => tf == 1440 ? '1D' : '$tf';

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
        return 4.5;
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

  static const double extremeNoiseSensitivity = 3.0;
  static const double extremeMinimumProminence = 0.15;

  static const double extremeNoiseSensitivityMin = 1.0;
  static const double extremeNoiseSensitivityMax = 10.0;
  static const double extremeNoiseSensitivityStep = 0.5;
  static const double extremeMinimumProminenceMin = 0.05;
  static const double extremeMinimumProminenceMax = 0.5;
  static const double extremeMinimumProminenceStep = 0.05;
}
