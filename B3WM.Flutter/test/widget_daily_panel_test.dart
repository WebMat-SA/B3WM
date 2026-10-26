import 'dart:async';
import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:provider/provider.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'package:b3wm_flutter/main.dart';
import 'package:b3wm_flutter/services/api_service.dart';
import 'package:b3wm_flutter/services/audio_service.dart';
import 'package:b3wm_flutter/services/preferences_service.dart';
import 'package:b3wm_flutter/services/signalr_service.dart';
import 'package:b3wm_flutter/services/state_service.dart';
import 'package:b3wm_flutter/services/trading_service.dart';
import 'package:b3wm_flutter/ui/widgets/chart/daily_chart_data.dart';

class _NoopSignalRService extends SignalRService {
  _NoopSignalRService({required super.hubUrl, required super.apiService});

  @override
  Future<void> startConnection(String symbol, int? timeFrame) async {}

  @override
  Future<void> stopConnection() async {}
}

http.Response _dailyExtremeResponse() => http.Response(
      jsonEncode({
        'symbol': 'WINFUT',
        'date': '2026-08-12T00:00:00.000',
        'config': {'noiseSensitivity': 3.0, 'minimumProminence': 0.15},
        'statistics': {'topCount': 1, 'valleyCount': 0, 'pointCount': 1},
        'extremes': [
          {
            'position': 101000.0,
            'value': 9000.0,
            'type': 'Top',
            'prominence': 0.5,
            'strength': 1.0,
            'confidence': 1.0,
            'width': 2.0,
            'isEdge': false,
          },
        ],
      }),
      200,
    );

ApiService _panelApi({
  Future<http.Response> Function(http.Request)? onExtremeDaily,
}) =>
    ApiService(
      baseUrl: 'http://test.local',
      client: MockClient((request) async {
        final path = request.url.path;
        if (path.contains('GetStructureHistory')) {
          return http.Response(
              jsonEncode([
                {
                  'date': '2026-08-01T00:00:00.000',
                  'symbol': 'WINFUT',
                  'timeFrame': 1440,
                  'upBorder': 100000.0,
                  'downBorder': 99000.0,
                  'upAuxBorder': 100050.0,
                  'downAuxBorder': 98950.0,
                },
                {
                  'date': '2026-08-10T00:00:00.000',
                  'symbol': 'WINFUT',
                  'timeFrame': 1440,
                  'upBorder': 101000.0,
                  'downBorder': 99000.0,
                  'upAuxBorder': 101050.0,
                  'downAuxBorder': 98950.0,
                },
              ]),
              200);
        }
        if (path.contains('GetBarRange')) {
          return http.Response(
              jsonEncode([
                {
                  'date': '2026-08-10T00:00:00.000',
                  'symbol': 'WINFUT',
                  'timeFrame': 1440,
                  'open': 100000.0,
                  'high': 100100.0,
                  'low': 99900.0,
                  'close': 100010.0,
                  'volume': 1000,
                },
                {
                  'date': '2026-08-11T00:00:00.000',
                  'symbol': 'WINFUT',
                  'timeFrame': 1440,
                  'open': 100500.0,
                  'high': 100600.0,
                  'low': 100400.0,
                  'close': 100510.0,
                  'volume': 1000,
                },
              ]),
              200);
        }
        if (path.contains('GetDailyProfile')) {
          return http.Response(
              jsonEncode([
                {
                  'price': 100000.0,
                  'total': 5000,
                  'buyVolume': 3000,
                  'sellVolume': 2000
                },
              ]),
              200);
        }
        if (path.contains('GetExtremeDaily')) {
          if (onExtremeDaily != null) return onExtremeDaily(request);
          return _dailyExtremeResponse();
        }
        if (path.contains('GetVolume')) return http.Response('null', 200);
        if (path.contains('SetExtremeConfig')) {
          return http.Response('null', 200);
        }
        if (path.contains('GetStructure')) {
          // Evita os retries de 2s do _fetchStructures no loadData.
          return http.Response(
              jsonEncode([
                {
                  'date': '2026-09-17T09:00:00.000',
                  'symbol': 'WINFUT',
                  'timeFrame': 2,
                  'upBorder': 100000.0,
                  'downBorder': 99000.0,
                  'upAuxBorder': 100050.0,
                  'downAuxBorder': 98950.0,
                },
              ]),
              200);
        }
        return http.Response('[]', 200);
      }),
    );

void main() {
  Future<StateService> pumpApp(
      WidgetTester tester, PreferencesService prefs, ApiService api) async {
    final service = StateService(
      apiService: api,
      signalRService: _NoopSignalRService(
          hubUrl: 'http://test.local/hub', apiService: api),
      preferencesService: prefs,
      audioService: AudioService(),
    );
    await service.setSymbol('WINFUT');
    service.setTradingPanelVisible(false);

    await tester.pumpWidget(
      MultiProvider(
        providers: [
          Provider<ApiService>.value(value: api),
          Provider<TradingApiService>.value(
              value: TradingApiService(baseUrl: 'http://test.local')),
          Provider<SignalRService>.value(
              value: _NoopSignalRService(
                  hubUrl: 'http://test.local/hub', apiService: api)),
          Provider<PreferencesService>.value(value: prefs),
          Provider<AudioService>.value(value: AudioService()),
          ChangeNotifierProvider<StateService>.value(value: service),
        ],
        child: const B3WMApp(),
      ),
    );
    return service;
  }

  Future<void> settle(WidgetTester tester) async {
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 100));
    await tester.pump(const Duration(milliseconds: 500));
    await tester.pump(const Duration(seconds: 1));
  }

  testWidgets('Split diário na página completa', (tester) async {
    final view = TestWidgetsFlutterBinding.instance.platformDispatcher
        .implicitView!;
    view.physicalSize = const Size(1280, 900);
    view.devicePixelRatio = 1.0;
    addTearDown(() {
      view.resetPhysicalSize();
      view.resetDevicePixelRatio();
    });

    SharedPreferences.setMockInitialValues({});
    final prefs = PreferencesService();
    await prefs.init();

    final api = _panelApi();
    final service = await pumpApp(tester, prefs, api);
    service.setDailyPanelVisible(true);

    await settle(tester);

    // Toolbar própria do split diário, espelhando o intraday.
    expect(find.text('Análise diária (1D)'), findsOneWidget);
    expect(find.byTooltip('Estrutura 1D'), findsOneWidget);
    expect(find.byTooltip('Volume Profile 1D'), findsOneWidget);
    expect(find.byTooltip('Topos/Vales 1D'), findsOneWidget);

    // Abrir o drawer de configs na aba de estruturas.
    await tester.tap(find.byTooltip('Estrutura 1D'));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 300));
    await tester.pump(const Duration(milliseconds: 300));
    expect(find.text('Range 1D (estruturas)'), findsOneWidget);
    service.reset();
  });

  testWidgets('Seta acima do splitter expande e recolhe o painel',
      (tester) async {
    final view = TestWidgetsFlutterBinding.instance.platformDispatcher
        .implicitView!;
    view.physicalSize = const Size(1280, 900);
    view.devicePixelRatio = 1.0;
    addTearDown(() {
      view.resetPhysicalSize();
      view.resetDevicePixelRatio();
    });

    SharedPreferences.setMockInitialValues({});
    final prefs = PreferencesService();
    await prefs.init();

    final api = _panelApi();
    final service = await pumpApp(tester, prefs, api);
    await settle(tester);

    // Painel oculto: barra de expandir visível, sem toolbar do diário.
    expect(find.byTooltip('Mostrar análise diária (1D)'), findsOneWidget);
    expect(find.byTooltip('Estrutura 1D'), findsNothing);

    // Expandir pela seta.
    await tester.tap(find.byTooltip('Mostrar análise diária (1D)'));
    await settle(tester);
    expect(find.text('Análise diária (1D)'), findsOneWidget);
    expect(find.byTooltip('Ocultar análise diária'), findsOneWidget);

    // Recolher pela seta da divisória.
    await tester.tap(find.byTooltip('Ocultar análise diária'));
    await settle(tester);
    expect(find.byTooltip('Estrutura 1D'), findsNothing);
    expect(find.byTooltip('Mostrar análise diária (1D)'), findsOneWidget);
    service.reset();
  });

  testWidgets(
      'Topos/Vales 1D recarrega após mudar parâmetros durante uma carga',
      (tester) async {
    final view =
        TestWidgetsFlutterBinding.instance.platformDispatcher.implicitView!;
    view.physicalSize = const Size(1280, 900);
    view.devicePixelRatio = 1.0;
    addTearDown(() {
      view.resetPhysicalSize();
      view.resetDevicePixelRatio();
    });

    SharedPreferences.setMockInitialValues({});
    final prefs = PreferencesService();
    await prefs.init();

    final firstExtremeResponse = Completer<http.Response>();
    final extremeRequests = <http.Request>[];
    final api = _panelApi(onExtremeDaily: (request) {
      extremeRequests.add(request);
      return extremeRequests.length == 1
          ? firstExtremeResponse.future
          : Future.value(_dailyExtremeResponse());
    });
    final service = await pumpApp(tester, prefs, api);
    service.setDailyPanelVisible(true);

    await tester.pump();
    await tester.pump(const Duration(milliseconds: 100));
    expect(extremeRequests, hasLength(1));
    expect(service.isDailyExtremeLoading, isTrue);

    service.setDailyExtremeNoiseSensitivity(4.0);
    service.setDailyExtremeMinimumProminence(0.25);
    await tester.pump(const Duration(milliseconds: 800));
    await tester.pump();
    expect(extremeRequests, hasLength(1));

    firstExtremeResponse.complete(_dailyExtremeResponse());
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 100));
    await tester.pump();

    expect(extremeRequests, hasLength(2));
    expect(
        extremeRequests.last.url.queryParameters['noiseSensitivity'], '4.000');
    expect(
        extremeRequests.last.url.queryParameters['minimumProminence'], '0.250');
    expect(service.dailyExtremes?.topCount, 1);
    expect(buildDailyChartData(service).extremes?.topPrices, [101000.0]);

    await tester.tap(find.byTooltip('Topos/Vales 1D'));
    await tester.pumpAndSettle();
    expect(find.text('Noise Sensitivity'), findsOneWidget);
    expect(find.text('Atualizar diário'), findsNothing);
    expect(find.textContaining('Toque em Atualizar'), findsNothing);
    expect(
        find.textContaining('Diário: 1 topo(s) / 0 vale(s)'), findsOneWidget);
    service.reset();
  });
}
