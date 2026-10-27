import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:b3wm_flutter/ui/widgets/chart/chart_data.dart';
import 'package:b3wm_flutter/ui/widgets/chart/chart_painter.dart';
import 'package:b3wm_flutter/ui/widgets/chart/map_flow_chart.dart';

ChartData _sampleData(int candleCount) {
  final start = DateTime(2026, 10, 26, 9);
  final candles = List.generate(
    candleCount,
    (i) => CandlePoint(
      date: start.add(Duration(minutes: i * 2)),
      open: 1000 + i.toDouble(),
      high: 1010 + i.toDouble(),
      low: 990 + i.toDouble(),
      close: 1005 + i.toDouble(),
    ),
  );
  return ChartData(
    candles: candles,
    redBubbles: const [],
    blueBubbles: const [],
    volumeProfile: const [],
    vwapPoints: const [],
    minPrice: 990,
    maxPrice: 1010.0 + candleCount,
    lastPrice: candles.last.close,
    dates: candles.map((c) => c.date).toList(),
    symbol: 'WINFUT',
    timeFrame: 2,
  );
}

Widget _chartFrame({required int candleCount, required double height}) {
  return MaterialApp(
    home: Scaffold(
      body: SizedBox(
        width: 800,
        height: height,
        child: MapFlowChart(
          data: _sampleData(candleCount),
          showStrategyTimeline: false,
        ),
      ),
    ),
  );
}

ChartData _prependCandles(ChartData data, int count) {
  final firstDate = data.candles.first.date;
  final olderCandles = List.generate(
    count,
    (i) => CandlePoint(
      date: firstDate.subtract(Duration(minutes: (count - i) * 2)),
      open: -1000 + i.toDouble(),
      high: -990 + i.toDouble(),
      low: -1010 + i.toDouble(),
      close: -995 + i.toDouble(),
    ),
  );
  final candles = [...olderCandles, ...data.candles];
  return ChartData(
    candles: candles,
    redBubbles: data.redBubbles,
    blueBubbles: data.blueBubbles,
    volumeProfile: data.volumeProfile,
    vwapPoints: data.vwapPoints,
    minPrice: olderCandles.first.low,
    maxPrice: data.maxPrice,
    lastPrice: data.lastPrice,
    dates: candles.map((c) => c.date).toList(),
    symbol: data.symbol,
    timeFrame: data.timeFrame,
  );
}

class _MutableChartHost extends StatefulWidget {
  const _MutableChartHost({super.key, required this.initialData});

  final ChartData initialData;

  @override
  State<_MutableChartHost> createState() => _MutableChartHostState();
}

class _MutableChartHostState extends State<_MutableChartHost> {
  late ChartData _data = widget.initialData;

  void prependCandles(int count) {
    setState(() => _data = _prependCandles(_data, count));
  }

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      home: Scaffold(
        body: SizedBox(
          width: 800,
          height: 560,
          child: MapFlowChart(
            data: _data,
            showStrategyTimeline: false,
          ),
        ),
      ),
    );
  }
}

Future<void> _settleLayout(WidgetTester tester) async {
  await tester.pump();
  await tester.pump(const Duration(milliseconds: 100));
}

void main() {
  testWidgets('fit horizontal does not compress chart vertically',
      (tester) async {
    await tester.pumpWidget(_chartFrame(candleCount: 7000, height: 560));
    await _settleLayout(tester);

    final viewer = tester.widget<InteractiveViewer>(
      find.byType(InteractiveViewer),
    );
    final transform = viewer.transformationController!.value;
    expect(transform[0], lessThan(0.1));
    expect(transform[5], 1.0);
    expect(viewer.minScale, closeTo(transform[0], 0.0001));
    expect(viewer.maxScale, 5.0);
  });

  testWidgets('fit de poucos candles mantém escalas dos dois eixos em 1',
      (tester) async {
    await tester.pumpWidget(_chartFrame(candleCount: 30, height: 560));
    await _settleLayout(tester);

    final viewer = tester.widget<InteractiveViewer>(
      find.byType(InteractiveViewer),
    );
    final transform = viewer.transformationController!.value;
    expect(transform[0], 1.0);
    expect(transform[5], 1.0);
  });

  testWidgets('prepend de histórico preserva viewport e inclui todos candles',
      (tester) async {
    final hostKey = GlobalKey<_MutableChartHostState>();
    await tester.pumpWidget(
      _MutableChartHost(key: hostKey, initialData: _sampleData(7000)),
    );
    await _settleLayout(tester);

    final before = tester.widget<InteractiveViewer>(
      find.byType(InteractiveViewer),
    );
    final controller = before.transformationController!;
    final beforeMatrix = controller.value.clone();
    beforeMatrix[0] = 0.05;
    beforeMatrix.setTranslationRaw(
      -1000,
      beforeMatrix.getTranslation().y,
      0,
    );
    controller.value = beforeMatrix;
    await tester.pump();
    final beforePrepend = controller.value.clone();
    final oldMinScale = before.minScale;

    hostKey.currentState!.prependCandles(25);
    await _settleLayout(tester);

    final after = tester.widget<InteractiveViewer>(
      find.byType(InteractiveViewer),
    );
    final paint = tester.widget<CustomPaint>(
      find.byWidgetPredicate(
        (widget) => widget is CustomPaint && widget.painter is ChartPainter,
      ),
    );
    final afterMatrix = after.transformationController!.value;
    expect(paint.size.width, 7025 * 8.0);
    expect(afterMatrix[0], beforePrepend[0]);
    expect(
      afterMatrix.getTranslation().x,
      closeTo(beforePrepend.getTranslation().x - 25 * 8.0 * 0.05, 0.001),
    );
    expect(after.minScale, lessThan(oldMinScale));
  });

  testWidgets('Y label drag zoom remains usable after vertical resize',
      (tester) async {
    await tester.pumpWidget(_chartFrame(candleCount: 7000, height: 560));
    await _settleLayout(tester);

    final start =
        tester.getTopLeft(find.byType(MapFlowChart)) + const Offset(750, 280);
    await tester.dragFrom(start, const Offset(0, -80));
    await _settleLayout(tester);

    ChartPainter painter() => tester
        .widget<CustomPaint>(
          find.byWidgetPredicate(
            (widget) => widget is CustomPaint && widget.painter is ChartPainter,
          ),
        )
        .painter! as ChartPainter;

    expect(painter().yZoom, greaterThan(1.0));
    final beforeResize =
        tester.widget<InteractiveViewer>(find.byType(InteractiveViewer));
    expect(beforeResize.transformationController!.value[5], 1.0);

    await tester.pumpWidget(_chartFrame(candleCount: 7000, height: 320));
    await _settleLayout(tester);

    final afterResize =
        tester.widget<InteractiveViewer>(find.byType(InteractiveViewer));
    expect(afterResize.transformationController!.value[5], 1.0);
    expect(painter().yZoom, greaterThan(1.0));
  });

  testWidgets('vertical pan clamps both ends at maximum Y zoom after reset',
      (tester) async {
    await tester.pumpWidget(_chartFrame(candleCount: 30, height: 560));
    await _settleLayout(tester);
    await tester.sendKeyEvent(LogicalKeyboardKey.space);
    await tester.pump();

    final chart = tester.getTopLeft(find.byType(MapFlowChart));
    await tester.dragFrom(
        chart + const Offset(750, 280), const Offset(0, -500));
    await _settleLayout(tester);

    final paintFinder = find.byWidgetPredicate(
      (widget) => widget is CustomPaint && widget.painter is ChartPainter,
    );
    final paint = tester.widget<CustomPaint>(paintFinder);
    final painter = paint.painter! as ChartPainter;
    expect(painter.yZoom, 10.0);

    final viewerFinder = find.byType(InteractiveViewer);
    final viewer = tester.widget<InteractiveViewer>(viewerFinder);
    final controller = viewer.transformationController!;
    final areaHeight = paint.size.height;
    final range = painter.data.priceRange;
    final paddedMin = painter.data.minPrice - range * 0.25;
    final paddedMax = painter.data.maxPrice + range * 0.25;
    final center = (paddedMin + paddedMax) / 2;
    final halfRange = (paddedMax - paddedMin) / 2 / painter.yZoom;
    final adjustedMin = center - halfRange;
    final adjustedRange = 2 * halfRange;
    final yTop = areaHeight -
        ((painter.data.maxPrice - adjustedMin) / adjustedRange * areaHeight);
    final yBottom = areaHeight -
        ((painter.data.minPrice - adjustedMin) / adjustedRange * areaHeight);
    final minimumTy = 8.0 - yBottom;
    final maximumTy = areaHeight - 8.0 - yTop;

    await tester.dragFrom(
      chart + const Offset(300, 280),
      const Offset(0, 10000),
    );
    await _settleLayout(tester);
    expect(
      controller.value.getTranslation().y,
      closeTo(maximumTy, 0.1),
    );

    await tester.dragFrom(
      chart + const Offset(300, 280),
      const Offset(0, -20000),
    );
    await _settleLayout(tester);
    expect(
      controller.value.getTranslation().y,
      closeTo(minimumTy, 0.1),
    );
  });
}
