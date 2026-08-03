import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

import 'package:b3wm_flutter/models/bar_storage_item.dart';
import 'package:b3wm_flutter/services/api_service.dart';
import 'package:b3wm_flutter/services/audio_service.dart';
import 'package:b3wm_flutter/services/preferences_service.dart';
import 'package:b3wm_flutter/services/signalr_service.dart';
import 'package:b3wm_flutter/services/state_service.dart';
import 'package:b3wm_flutter/ui/widgets/chart/chart_data.dart';
import 'package:b3wm_flutter/ui/widgets/volume_profile_drawer.dart';

class FakeStateService extends StateService {
  FakeStateService()
      : super(
          apiService: ApiService(),
          signalRService: SignalRService(
              hubUrl: 'http://localhost:5000', apiService: ApiService()),
          preferencesService: PreferencesService(),
          audioService: AudioService(),
        );

  int start = 0;
  int end = 40;
  int? appliedStart;
  int? appliedEnd;

  @override
  List<BarStorageItem> get barsTimeFrameFilter {
    return List.generate(40, (i) => BarStorageItem(
          date: DateTime(2026, 1, 1, 9, i * 2),
          symbol: 'WINFUT',
          timeFrame: 2,
          open: 100000,
          high: 100100,
          low: 99900,
          close: 100050,
          volume: 10,
        ));
  }

  @override
  int get dateRangeStart => start;
  @override
  int get dateRangeEnd => end;

  @override
  void applyVolumeFilter(int start, int end) {
    appliedStart = start;
    appliedEnd = end;
  }
}

void main() {
  testWidgets('Volume profile filter renders a draggable RangeSlider',
      (WidgetTester tester) async {
    final state = FakeStateService();

    await tester.pumpWidget(
      ChangeNotifierProvider<StateService>.value(
        value: state,
        child: const MaterialApp(
          home: Scaffold(
            body: VolumeProfileDrawer(noDrawer: true),
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();

    final slider = find.byType(RangeSlider);
    expect(slider, findsOneWidget);

    final rangeSlider = tester.widget<RangeSlider>(slider);
    expect(rangeSlider.values.start, 0);
    expect(rangeSlider.values.end, 40);

    expect(find.byIcon(Icons.restart_alt), findsNothing);

    await tester.tap(slider, warnIfMissed: false);
    await tester.pump();
  });

  testWidgets('Volume filter does not hide candles, only records the range',
      (WidgetTester tester) async {
    final state = FakeStateService()
      ..start = 10
      ..end = 20;

    final data = buildChartData(state);
    expect(data.candles.length, 40);
    expect(data.rangeStart, 10);
    expect(data.rangeEnd, 20);
  });

  testWidgets('Slider does not crash when range exceeds bar count',
      (WidgetTester tester) async {
    final state = FakeStateService()
      ..start = 0
      ..end = 500;

    await tester.pumpWidget(
      ChangeNotifierProvider<StateService>.value(
        value: state,
        child: const MaterialApp(
          home: Scaffold(
            body: VolumeProfileDrawer(noDrawer: true),
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();

    final slider = find.byType(RangeSlider);
    expect(slider, findsOneWidget);

    final rangeSlider = tester.widget<RangeSlider>(slider);
    expect(rangeSlider.values.start, lessThanOrEqualTo(rangeSlider.max));
    expect(rangeSlider.values.end, lessThanOrEqualTo(rangeSlider.max));
    expect(rangeSlider.values.start, lessThan(rangeSlider.values.end));
  });

  testWidgets('Auto mode disables the range slider',
      (WidgetTester tester) async {
    final state = FakeStateService();

    await tester.pumpWidget(
      ChangeNotifierProvider<StateService>.value(
        value: state,
        child: const MaterialApp(
          home: Scaffold(
            body: VolumeProfileDrawer(noDrawer: true),
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();

    final slider = find.byType(RangeSlider);
    expect(tester.widget<RangeSlider>(slider).onChanged, isNotNull);

    await tester.tap(find.descendant(
      of: find.widgetWithText(Row, 'Auto Mode (por Estrutura)'),
      matching: find.byType(Switch),
    ));
    await tester.pumpAndSettle();

    expect(state.profileAutoByPriceStructure, isTrue);
    expect(
        tester.widget<RangeSlider>(find.byType(RangeSlider)).onChanged, isNull);
  });
}
