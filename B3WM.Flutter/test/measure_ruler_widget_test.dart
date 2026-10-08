import 'package:flutter/gestures.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:b3wm_flutter/ui/widgets/chart/chart_data.dart';
import 'package:b3wm_flutter/ui/widgets/chart/map_flow_chart.dart';

ChartData _sampleData() {
  final start = DateTime(2024, 5, 10, 9, 0);
  final candles = List.generate(
    30,
    (i) => CandlePoint(
      date: start.add(Duration(minutes: 2 * i)),
      open: 150000.0 + i * 10,
      high: 150100.0 + i * 10,
      low: 149900.0 + i * 10,
      close: 150050.0 + i * 10,
    ),
  );
  const minP = 149800.0;
  const maxP = 150500.0;
  return ChartData(
    candles: candles,
    redBubbles: const [],
    blueBubbles: const [],
    volumeProfile: const [],
    vwapPoints: const [],
    minPrice: minP,
    maxPrice: maxP,
    lastPrice: candles.last.close,
    dates: candles.map((c) => c.date).toList(),
    symbol: 'WINFUT',
    timeFrame: 2,
  );
}

Future<void> _pumpChart(WidgetTester tester) async {
  await tester.pumpWidget(
    MaterialApp(
      home: Scaffold(
        body: SizedBox(
          width: 800,
          height: 600,
          child: MapFlowChart(
            data: _sampleData(),
            showStrategyOverlay: false,
          ),
        ),
      ),
    ),
  );
  await tester.pump();
  await tester.pump(const Duration(milliseconds: 100));
  await tester.pump(const Duration(milliseconds: 300));
}

void main() {
  testWidgets('drag puro com esquerdo NÃO mede (preserva pan)',
      (tester) async {
    await _pumpChart(tester);

    final chart = find.byType(MapFlowChart);
    expect(chart, findsOneWidget);
    final center = tester.getCenter(chart);

    final gesture = await tester.startGesture(
      center,
      kind: PointerDeviceKind.mouse,
      buttons: kPrimaryButton,
    );
    await tester.pump();
    await gesture.moveTo(center + const Offset(100, -50));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 100));

    expect(find.textContaining('pts'), findsNothing);

    await gesture.up();
    await tester.pump();
    expect(find.textContaining('pts'), findsNothing);
  });

  testWidgets('Ctrl+drag com esquerdo mede (pts + candles + intervalo)',
      (tester) async {
    await _pumpChart(tester);

    final chart = find.byType(MapFlowChart);
    expect(chart, findsOneWidget);
    final center = tester.getCenter(chart);

    await tester.sendKeyDownEvent(LogicalKeyboardKey.controlLeft);
    final gesture = await tester.startGesture(
      center,
      kind: PointerDeviceKind.mouse,
      buttons: kPrimaryButton,
    );
    await tester.pump();
    await gesture.moveTo(center + const Offset(100, -50));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 100));

    // Durante o arrasto a régua aparece.
    expect(find.textContaining('pts'), findsOneWidget);
    expect(find.textContaining('Candles'), findsOneWidget);
    expect(find.textContaining('Intervalo'), findsOneWidget);

    await gesture.up();
    await tester.pump();
    await tester.sendKeyUpEvent(LogicalKeyboardKey.controlLeft);
    await tester.pump();

    // Ao soltar, a régua some.
    expect(find.textContaining('pts'), findsNothing);
  });

  testWidgets('toggle 📏 ativo: drag com esquerdo mede sem Ctrl',
      (tester) async {
    await _pumpChart(tester);

    final chart = find.byType(MapFlowChart);
    final center = tester.getCenter(chart);

    // Ativa modo régua pelo toggle.
    await tester.tap(find.text('📏'));
    await tester.pump();

    final gesture = await tester.startGesture(
      center,
      kind: PointerDeviceKind.mouse,
      buttons: kPrimaryButton,
    );
    await tester.pump();
    await gesture.moveTo(center + const Offset(100, -50));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 100));

    expect(find.textContaining('pts'), findsOneWidget);

    // Ao soltar, a régua some (só visível durante o drag).
    await gesture.up();
    await tester.pump();
    expect(find.textContaining('pts'), findsNothing);

    // Desativa pelo toggle.
    await tester.tap(find.text('📏'));
    await tester.pump();

    // Fora do modo, drag puro volta a não medir.
    final gesture2 = await tester.startGesture(
      center,
      kind: PointerDeviceKind.mouse,
      buttons: kPrimaryButton,
    );
    await tester.pump();
    await gesture2.moveTo(center + const Offset(100, -50));
    await tester.pump();
    expect(find.textContaining('pts'), findsNothing);
    await gesture2.up();
    await tester.pump();
  });

  testWidgets('botão direito arrasta e NÃO mostra régua (pan)',
      (tester) async {
    await _pumpChart(tester);

    final chart = find.byType(MapFlowChart);
    final center = tester.getCenter(chart);

    final gesture = await tester.startGesture(
      center,
      kind: PointerDeviceKind.mouse,
      buttons: kSecondaryButton,
    );
    await tester.pump();
    await gesture.moveTo(center + const Offset(100, -50));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 100));

    // Pan com botão direito não deve abrir a régua.
    expect(find.textContaining('pts'), findsNothing);

    await gesture.up();
    await tester.pump();
    expect(find.textContaining('pts'), findsNothing);
  });

  testWidgets('soltar o mouse esconde a régua (sem persistência)',
      (tester) async {
    await _pumpChart(tester);

    final chart = find.byType(MapFlowChart);
    final center = tester.getCenter(chart);

    // Durante o Ctrl+drag a régua aparece...
    await tester.sendKeyDownEvent(LogicalKeyboardKey.controlLeft);
    final gesture = await tester.startGesture(
      center,
      kind: PointerDeviceKind.mouse,
      buttons: kPrimaryButton,
    );
    await gesture.moveTo(center + const Offset(100, -50));
    await tester.pump();
    expect(find.textContaining('pts'), findsOneWidget);

    // ...e some ao soltar.
    await gesture.up();
    await tester.pump();
    await tester.sendKeyUpEvent(LogicalKeyboardKey.controlLeft);
    await tester.pump();
    expect(find.textContaining('pts'), findsNothing);

    // Sanidade: clique simples sem arrasto não cria régua.
    final tap = await tester.startGesture(
      center,
      kind: PointerDeviceKind.mouse,
      buttons: kPrimaryButton,
    );
    await tester.pump();
    await tap.up();
    await tester.pump();
    expect(find.textContaining('pts'), findsNothing);
  });
}
