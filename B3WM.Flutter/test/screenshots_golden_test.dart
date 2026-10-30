// Golden tests that render the B3WM Flutter app for the README screenshots.
//
// Run with:
//   flutter test test/screenshots_golden_test.dart --update-goldens
//
// The generated PNG files land in test/goldens/ and are copied to the
// screenshots/ folder of the repository.

import 'dart:convert';
import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:provider/provider.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'package:b3wm_flutter/main.dart';
import 'package:b3wm_flutter/models/bar_storage_item.dart';
import 'package:b3wm_flutter/models/bubble_storage_item.dart';
import 'package:b3wm_flutter/models/market_window.dart';
import 'package:b3wm_flutter/models/structure_storage_item.dart';
import 'package:b3wm_flutter/models/trade_models.dart';
import 'package:b3wm_flutter/models/volume_level.dart';
import 'package:b3wm_flutter/models/volume_level_storage_item.dart';
import 'package:b3wm_flutter/models/extreme_storage_item.dart';
import 'package:b3wm_flutter/models/pivot_storage_item.dart';
import 'package:b3wm_flutter/models/vwap_history_point.dart';
import 'package:b3wm_flutter/services/api_service.dart';
import 'package:b3wm_flutter/services/audio_service.dart';
import 'package:b3wm_flutter/services/preferences_service.dart';
import 'package:b3wm_flutter/services/signalr_service.dart';
import 'package:b3wm_flutter/services/state_service.dart';
import 'package:b3wm_flutter/services/strategy_service.dart';
import 'package:b3wm_flutter/services/trading_service.dart';

import 'readme_data.dart';

const _goldenRoot = Key('golden-root');

late ReadmeSessionData _data;

bool _sameDay(DateTime a, DateTime b) =>
    a.year == b.year && a.month == b.month && a.day == b.day;

bool _within(DateTime date, DateTime from, DateTime to) =>
    !date.isBefore(DateTime(from.year, from.month, from.day)) &&
    !date.isAfter(DateTime(to.year, to.month, to.day, 23, 59, 59));

double get _lastPrice => _data.bars.last.close;

AccountInfo _buildAccount() => AccountInfo(
      login: 7123456,
      balance: 10000.00,
      equity: 10236.00,
      profit: 236.00,
      margin: 620.00,
      marginFree: 9616.00,
      marginLevel: 1650.00,
      leverage: 100,
      currency: 'BRL',
      server: 'MetaQuotes-Demo',
      tradeAllowed: true,
      name: 'B3WM Demo',
    );

List<PositionInfo> _buildPositions() => [
      PositionInfo(
        ticket: 101,
        symbol: 'WINFUT',
        type: 'buy',
        volume: 2,
        priceOpen: _lastPrice - 120,
        sl: _lastPrice - 420,
        tp: _lastPrice + 380,
        priceCurrent: _lastPrice,
        profit: 240.00,
        swap: 0,
        commission: 2.00,
        magic: 1234,
        comment: 'b3wm',
        time: '2026-10-28 10:22:00',
      ),
      PositionInfo(
        ticket: 102,
        symbol: 'WDOFUT',
        type: 'sell',
        volume: 1,
        priceOpen: 5.4450,
        sl: 5.4650,
        tp: 5.4000,
        priceCurrent: 5.4310,
        profit: 14.00,
        swap: 0,
        commission: 1.00,
        magic: 1234,
        comment: 'b3wm',
        time: '2026-10-28 11:05:00',
      ),
    ];

List<OrderInfo> _buildOrders() => [
      OrderInfo(
        ticket: 55,
        symbol: 'WINFUT',
        type: 'sell_limit',
        volume: 1,
        priceOpen: _lastPrice + 260,
        sl: _lastPrice + 520,
        tp: _lastPrice - 180,
        timeSetup: '2026-10-28 09:15:00',
        timeExpiration: '',
        state: 'Valid',
        comment: 'b3wm',
        magic: 1234,
      ),
      OrderInfo(
        ticket: 56,
        symbol: 'WDOFUT',
        type: 'buy_limit',
        volume: 1,
        priceOpen: 5.4100,
        sl: 5.3900,
        tp: 5.4600,
        timeSetup: '2026-10-28 09:40:00',
        timeExpiration: '',
        state: 'Valid',
        comment: 'b3wm',
        magic: 1234,
      ),
    ];

List<HistoryDeal> _buildHistory() => [
      HistoryDeal(
          ticket: 91,
          symbol: 'WINFUT',
          type: 'buy',
          volume: 2,
          price: _lastPrice - 140,
          profit: 120.00,
          time: '2026-10-28 09:18:00',
          comment: 'b3wm',
          magic: 1234),
      HistoryDeal(
          ticket: 92,
          symbol: 'WINFUT',
          type: 'sell',
          volume: 2,
          price: _lastPrice - 40,
          profit: 60.00,
          time: '2026-10-28 10:41:00',
          comment: 'b3wm',
          magic: 1234),
      HistoryDeal(
          ticket: 93,
          symbol: 'WDOFUT',
          type: 'buy',
          volume: 1,
          price: 5.4400,
          profit: 40.00,
          time: '2026-10-28 11:12:00',
          comment: 'b3wm',
          magic: 1234),
    ];

class FakeApiService extends ApiService {
  FakeApiService(this.data) : super(baseUrl: 'http://test.local');

  final ReadmeSessionData data;

  @override
  Future<List<BarStorageItem>> getBars(String symbol, DateTime date) async =>
      data.bars.where((b) => _sameDay(b.date, date)).toList();

  @override
  Future<List<BarStorageItem>> getBarRange(
      String symbol, DateTime startDate, DateTime endDate, int timeFrame) async {
    final source = timeFrame == 1440 ? data.dailyBars : data.bars;
    return source
        .where((b) => _within(b.date, startDate, endDate))
        .toList();
  }

  @override
  Future<List<BubbleStorageItem>> getBubbles(
          String symbol, DateTime date) async =>
      data.bubbles.where((b) => _sameDay(b.date, date)).toList();

  @override
  Future<List<BubbleStorageItem>> getBubbleRange(
          String symbol, DateTime startDate, DateTime endDate) async =>
      data.bubbles
          .where((b) => _within(b.date, startDate, endDate))
          .toList();

  @override
  Future<VolumeLevelStorageItem?> getVolume(
          String symbol, DateTime date) async =>
      _sameDay(data.volume.date, date) ? data.volume : null;

  @override
  Future<List<StructureStorageItem>> getStructure(
          String symbol, DateTime date, double minDistance) async =>
      data.structures.where((s) => _sameDay(s.date, date)).toList();

  @override
  Future<List<StructureStorageItem>> getStructureRange(String symbol,
          DateTime startDate, DateTime endDate, double minDistance) async =>
      data.structures
          .where((s) => _within(s.date, startDate, endDate))
          .toList();

  @override
  Future<List<StructureStorageItem>> setStructureDistance(
          String symbol, double minDistance) async =>
      data.structures;

  @override
  Future<List<StructureStorageItem>> setStructureDistanceForTimeFrame(
          String symbol, int timeFrame, double minDistance) async =>
      timeFrame == 1440 ? data.structuresDaily : data.structures;

  @override
  Future<List<StructureStorageItem>> getStructureHistory(
      String symbol, int timeFrame, double minDistance,
      {int days = 90}) async =>
      timeFrame == 1440 ? data.structuresDaily : data.structures;

  @override
  Future<ExtremeStorageItem?> getExtreme(String symbol, DateTime date,
          {DateTime? from, DateTime? to}) async =>
      data.extreme;

  @override
  Future<ExtremeStorageItem?> setExtremePeriod(String symbol,
          {DateTime? date, DateTime? from, DateTime? to}) async =>
      data.extreme;

  @override
  Future<ExtremeStorageItem?> setExtremeConfig(String symbol,
          {DateTime? date,
          DateTime? from,
          DateTime? to,
          double? noiseSensitivity,
          double? minimumProminence}) async =>
      data.extreme;

  @override
  Future<ExtremeStorageItem?> getExtremeDaily(String symbol,
          {DateTime? from,
          DateTime? to,
          double? noiseSensitivity,
          double? minimumProminence}) async =>
      data.extremeDaily;

  @override
  Future<List<VolumeLevel>> getDailyProfile(String symbol,
          {DateTime? from, DateTime? to}) async =>
      data.dailyProfile;

  @override
  Future<PivotStorageItem?> getPivotIntraday(String symbol,
          {int? lineCount}) async =>
      data.pivotIntraday;

  @override
  Future<PivotStorageItem?> getPivotDaily(String symbol,
          {int? lineCount}) async =>
      data.pivotDaily;

  @override
  Future<List<VwapHistoryPoint>> getVwapHistory(
          String symbol, DateTime date) async =>
      data.vwap.where((p) => _sameDay(p.day, date)).toList();

  @override
  Future<List<VwapHistoryPoint>> getVwapRange(
          String symbol, DateTime startDate, DateTime endDate) async =>
      data.vwap
          .where((p) => _within(p.day, startDate, endDate))
          .toList();

  @override
  Future<MarketWindow?> getMarketWindow({
    required String symbol,
    required int timeFrame,
    bool profileAuto = true,
    DateTime? profileFrom,
    DateTime? profileTo,
    DateTime? rangeFrom,
    DateTime? rangeTo,
    bool dailyAuto = true,
    DateTime? dailyFrom,
    DateTime? dailyTo,
  }) async =>
      MarketWindow(
        symbol: symbol,
        timeFrame: timeFrame,
        intraday: StructureWindow(
          from: data.bars.first.date,
          to: data.bars.last.date,
          auto: true,
        ),
        daily: StructureWindow(
          from: data.dailyFrom,
          to: data.dailyTo,
          auto: true,
        ),
      );

  @override
  Future<DateTime> findLastDateWithData(String symbol) async => data.session;
}

class FakeSignalRService extends SignalRService {
  FakeSignalRService(ApiService api)
      : super(hubUrl: 'http://test.local/hub', apiService: api);

  @override
  bool get started => false;

  @override
  bool get isConnected => true;

  @override
  Future<void> startConnection(String symbol, int? timeFrame) async {}

  @override
  Future<void> stopConnection() async {}

  @override
  Future<void> dispose() async {}

  @override
  Future<MarketWindow?> subscribeMarketWindow({
    required String symbol,
    required int timeFrame,
    bool profileAuto = true,
    DateTime? profileFrom,
    DateTime? profileTo,
    DateTime? rangeFrom,
    DateTime? rangeTo,
    bool dailyAuto = true,
    DateTime? dailyFrom,
    DateTime? dailyTo,
  }) async =>
      null;
}

class FakeTradingApiService extends TradingApiService {
  FakeTradingApiService() : super(baseUrl: 'http://test.local');

  @override
  Future<AccountInfo?> getAccountInfo() async => _buildAccount();

  @override
  Future<List<PositionInfo>> getPositions({String? symbol}) async =>
      _buildPositions();

  @override
  Future<List<OrderInfo>> getOpenOrders() async => _buildOrders();

  @override
  Future<List<HistoryDeal>> getHistory(String symbol,
          {String fromDate = '', String toDate = ''}) async =>
      _buildHistory();
}

class FakeStrategyService extends StrategyService {
  FakeStrategyService(this.data)
      : super(
          baseUrl: 'http://test.local',
          client: MockClient((_) async => http.Response('[]', 200)),
        );

  final ReadmeSessionData data;

  StrategySession get session {
    final decisions = data.strategyLog.decisions;
    final last = decisions.isEmpty ? null : decisions.last;
    return StrategySession(
      sessionId: 'jev-winfut-2',
      strategy: 'JevAnalysis',
      symbol: 'WINFUT',
      triggerTimeFrame: 2,
      paused: false,
      snapshotHash: data.strategyLog.snapshotHash,
      position: 'flat',
      decisions: decisions.length,
      realizedPts: data.strategyLog.realizedPts,
      lastDecision: last == null
          ? null
          : '${last.side} conf=${last.confidence.toStringAsFixed(2)}',
      reportItems: decisions.length > 8
          ? decisions.sublist(decisions.length - 8)
          : decisions,
    );
  }

  @override
  Future<List<StrategyInfo>> list() async => data.strategies;

  @override
  Future<List<StrategySession>> state() async => [session];

  @override
  Future<List<StrategyLogDay>> log(String symbol, DateTime date) async =>
      _sameDay(date, data.session) ? [data.strategyLog] : [];

  @override
  void dispose() {}
}

class _Env {
  final ApiService api;
  final TradingApiService tradingApi;
  final SignalRService signalR;
  final PreferencesService prefs;
  final AudioService audio;
  final StateService state;
  final FakeStrategyService strategy;

  _Env({
    required this.api,
    required this.tradingApi,
    required this.signalR,
    required this.prefs,
    required this.audio,
    required this.state,
    required this.strategy,
  });
}

Future<_Env> _createEnv() async {
  SharedPreferences.setMockInitialValues(_data.prefs);
  final prefs = PreferencesService();
  await prefs.init();
  final api = FakeApiService(_data);
  final signalR = FakeSignalRService(api);
  final tradingApi = FakeTradingApiService();
  final audio = AudioService();
  final strategy = FakeStrategyService(_data);
  final state = StateService(
    apiService: api,
    signalRService: signalR,
    preferencesService: prefs,
    audioService: audio,
    strategyService: strategy,
  );
  await state.setSymbol('WINFUT');
  state.updatePositions(_buildPositions());
  state.updateOrders(_buildOrders());
  state.updateHistory(_buildHistory());
  return _Env(
    api: api,
    tradingApi: tradingApi,
    signalR: signalR,
    prefs: prefs,
    audio: audio,
    state: state,
    strategy: strategy,
  );
}

Widget _buildApp(_Env env) {
  return RepaintBoundary(
    key: _goldenRoot,
    child: MultiProvider(
      providers: [
        Provider<ApiService>.value(value: env.api),
        Provider<TradingApiService>.value(value: env.tradingApi),
        Provider<SignalRService>.value(value: env.signalR),
        Provider<PreferencesService>.value(value: env.prefs),
        Provider<AudioService>.value(value: env.audio),
        ChangeNotifierProvider<StateService>.value(value: env.state),
      ],
      child: const B3WMApp(),
    ),
  );
}

Future<void> _settle(WidgetTester tester) async {
  await tester.pump();
  await tester.pump(const Duration(milliseconds: 60));
  await tester.pump(const Duration(milliseconds: 250));
  await tester.pump(const Duration(milliseconds: 250));
  await tester.pump(const Duration(milliseconds: 500));
}

Future<void> _capture(WidgetTester tester, String name) async {
  await expectLater(
    find.byKey(_goldenRoot),
    matchesGoldenFile('goldens/$name.png'),
  );
}

Future<void> _cancelTimers(WidgetTester tester, _Env env) async {
  await tester.pumpWidget(const SizedBox.shrink());
  await tester.pump(const Duration(seconds: 6));
  env.state.reset();
}

Future<void> _openDrawerTab(
    WidgetTester tester, _Env env, String tooltip, String name) async {
  await tester.pumpWidget(_buildApp(env));
  await _settle(tester);
  await tester.tap(find.byTooltip(tooltip));
  await _settle(tester);
  await _capture(tester, name);
  await _cancelTimers(tester, env);
}

void _prepareIntraday(_Env env) {
  env.state.setTradingPanelVisible(false);
  env.state.setDailyPanelVisible(false);
  env.state.setStructureVisible(true);
  env.state.setBubbleVisible(true);
  env.state.setProfileVisible(true);
  env.state.setExtremeVisible(true);
  env.state.setPivotVisible(true);
  env.state.setVwapVisible(true);
}

void main() {
  setUpAll(() async {
    _data = await loadReadmeSessionData();
    await _loadFonts();
  });

  setUp(() {
    final view = TestWidgetsFlutterBinding.instance.platformDispatcher
        .implicitView!;
    view.physicalSize = const Size(1280, 900);
    view.devicePixelRatio = 1.0;
  });

  tearDown(() {
    final view = TestWidgetsFlutterBinding.instance.platformDispatcher
        .implicitView!;
    view.resetPhysicalSize();
    view.resetDevicePixelRatio();
  });

  testWidgets('golden - visao geral do grafico', (tester) async {
    final env = await _createEnv();
    _prepareIntraday(env);
    await tester.pumpWidget(_buildApp(env));
    await _settle(tester);
    await _capture(tester, 'overview');
    await _cancelTimers(tester, env);
  });

  testWidgets('golden - painel de trading', (tester) async {
    final env = await _createEnv();
    _prepareIntraday(env);
    env.state.setTradingPanelVisible(true);
    env.state.setTradingConfigExpanded(false);
    env.state.setTradingAccountExpanded(true);
    env.state.setTradingOrdersExpanded(false);
    env.state.setTradingPositionsExpanded(true);
    env.state.setTradingHistoryExpanded(true);
    await tester.pumpWidget(_buildApp(env));
    await _settle(tester);
    await _capture(tester, 'trading_panel');
    await _cancelTimers(tester, env);
  });

  testWidgets('golden - aba Bubbles', (tester) async {
    final env = await _createEnv();
    _prepareIntraday(env);
    await _openDrawerTab(tester, env, 'Notificações de bubbles', 'drawer_bubbles');
  });

  testWidgets('golden - aba Estrutura', (tester) async {
    final env = await _createEnv();
    _prepareIntraday(env);
    await _openDrawerTab(tester, env, 'Notificações de estrutura', 'drawer_estrutura');
  });

  testWidgets('golden - aba Volume Profile', (tester) async {
    final env = await _createEnv();
    _prepareIntraday(env);
    await _openDrawerTab(
        tester, env, 'Volume Profile', 'drawer_volume_profile');
  });

  testWidgets('golden - aba Topos/Vales', (tester) async {
    final env = await _createEnv();
    _prepareIntraday(env);
    await _openDrawerTab(tester, env, 'Topos/Vales', 'drawer_extreme');
  });

  testWidgets('golden - aba Pivot Tradicional', (tester) async {
    final env = await _createEnv();
    _prepareIntraday(env);
    await _openDrawerTab(
        tester, env, 'Pivot Tradicional', 'drawer_pivot');
  });

  testWidgets('golden - aba VWAP Diario', (tester) async {
    final env = await _createEnv();
    _prepareIntraday(env);
    await _openDrawerTab(tester, env, 'VWAP Diário', 'drawer_vwap');
  });

  testWidgets('golden - aba Backup', (tester) async {
    final env = await _createEnv();
    _prepareIntraday(env);
    await _openDrawerTab(
        tester, env, 'Backup (exportar/importar)', 'drawer_backup');
  });

  testWidgets('golden - aba Estrategias', (tester) async {
    final env = await _createEnv();
    _prepareIntraday(env);
    final client = MockClient((request) async {
      if (request.url.path == '/api/Strategy/List') {
        final list = _data.strategies
            .map((s) => {
                  'name': s.name,
                  'description': s.description,
                  'detailsMd': s.detailsMd,
                })
            .toList();
        return http.Response(jsonEncode(list), 200,
            headers: {'content-type': 'application/json'});
      }
      if (request.url.path == '/api/Strategy/State') {
        return http.Response('[]', 200,
            headers: {'content-type': 'application/json'});
      }
      return http.Response('{}', 200,
          headers: {'content-type': 'application/json'});
    });
    await http.runWithClient(() async {
      await _openDrawerTab(
          tester, env, 'Estratégias (rodar)', 'drawer_strategy');
    }, () => client);
  });

  testWidgets('golden - painel diario 1D', (tester) async {
    final env = await _createEnv();
    _prepareIntraday(env);
    env.state.setDailyPanelVisible(true);
    await env.state.loadDailyAll();
    await tester.pumpWidget(_buildApp(env));
    await _settle(tester);
    await tester.pump(const Duration(milliseconds: 500));
    await _capture(tester, 'daily_panel');
    await _cancelTimers(tester, env);
  });
}

Future<void> _loadFonts() async {
  final root = Platform.environment['FLUTTER_ROOT'];
  final fontsDir = (root != null && root.isNotEmpty)
      ? '$root/bin/cache/artifacts/material_fonts'
      : 'C:/tools/flutter/bin/cache/artifacts/material_fonts';
  final loader = FontLoader('Roboto');
  for (final name in [
    'roboto-regular.ttf',
    'roboto-bold.ttf',
    'roboto-medium.ttf'
  ]) {
    final bytes = await File('$fontsDir/$name').readAsBytes();
    final data =
        ByteData.view(bytes.buffer, bytes.offsetInBytes, bytes.lengthInBytes);
    loader.addFont(Future.value(data));
  }
  await loader.load();

  final iconsLoader = FontLoader('MaterialIcons');
  final iconBytes =
      await File('$fontsDir/materialicons-regular.otf').readAsBytes();
  final iconData = ByteData.view(
      iconBytes.buffer, iconBytes.offsetInBytes, iconBytes.lengthInBytes);
  iconsLoader.addFont(Future.value(iconData));
  await iconsLoader.load();
}
