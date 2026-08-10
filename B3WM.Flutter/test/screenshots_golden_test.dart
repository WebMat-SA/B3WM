// Golden tests that render the B3WM Flutter app with realistic sample data.
//
// These are used to generate the screenshots shown in the project README.
// Run with:
//   flutter test test/screenshots_golden_test.dart --update-goldens
//
// The generated PNG files land in test/goldens/ and are copied to the
// screenshots/ folder of the repository.

import 'dart:convert';
import 'dart:io';
import 'dart:math';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'package:b3wm_flutter/main.dart';
import 'package:b3wm_flutter/models/bar_storage_item.dart';
import 'package:b3wm_flutter/models/bubble_storage_item.dart';
import 'package:b3wm_flutter/models/signal_event.dart';
import 'package:b3wm_flutter/models/structure_storage_item.dart';
import 'package:b3wm_flutter/models/ticks2.dart';
import 'package:b3wm_flutter/models/trade_models.dart';
import 'package:b3wm_flutter/models/verifier_config.dart';
import 'package:b3wm_flutter/models/verifier_position.dart';
import 'package:b3wm_flutter/models/verifier_state.dart';
import 'package:b3wm_flutter/models/volume_level.dart';
import 'package:b3wm_flutter/models/volume_level_storage_item.dart';
import 'package:b3wm_flutter/services/api_service.dart';
import 'package:b3wm_flutter/services/audio_service.dart';
import 'package:b3wm_flutter/services/preferences_service.dart';
import 'package:b3wm_flutter/services/signalr_service.dart';
import 'package:b3wm_flutter/services/state_service.dart';
import 'package:b3wm_flutter/services/trading_service.dart';

const _goldenRoot = Key('golden-root');

// ---------------------------------------------------------------------------
// Deterministic random generator so the sample data is stable between runs.
// ---------------------------------------------------------------------------
class _Rng {
  int _state;
  _Rng(int seed) : _state = seed;
  double nextDouble() {
    _state = (_state * 1103515245 + 12345) & 0x7fffffff;
    return _state / 0x7fffffff;
  }

  int nextInt(int max) => (nextDouble() * max).floor();
}

// ---------------------------------------------------------------------------
// Sample data
// ---------------------------------------------------------------------------

final List<BarStorageItem> _kSampleBars = _buildBars();
final List<BubbleStorageItem> _kSampleBubbles = _buildBubbles();
final List<StructureStorageItem> _kSampleStructures = _buildStructures();

List<BarStorageItem> _buildBars() {
  final rng = _Rng(20240510);
  final bars = <BarStorageItem>[];
  final start = DateTime(2024, 5, 10, 9, 0);
  double price = 151000.0;
  for (int i = 0; i < 110; i++) {
    final date = start.add(Duration(minutes: 2 * i));
    final open = price;
    final drift = (rng.nextDouble() - 0.5) * 10 - (price - 151000) * 0.02;
    final close = open + drift;
    final high = max(open, close) + rng.nextDouble() * 12;
    final low = min(open, close) - rng.nextDouble() * 12;
    bars.add(BarStorageItem(
      date: date,
      symbol: 'WINFUT',
      timeFrame: 2,
      open: open,
      high: high,
      low: low,
      close: close,
      volume: 200 + rng.nextInt(2800),
    ));
    price = close;
  }
  return bars;
}

List<BubbleStorageItem> _buildBubbles() {
  final rng = _Rng(7);
  final bars = _kSampleBars;
  const agents = [122, 238, 114, 16, 13, 40, 127, 3, 39, 6003];
  final bubbles = <BubbleStorageItem>[];
  for (int i = 0; i < 14; i++) {
    final bar = bars[20 + rng.nextInt(bars.length - 30)];
    bubbles.add(BubbleStorageItem(
      price: bar.close + (rng.nextDouble() - 0.5) * 20,
      agent: agents[rng.nextInt(agents.length)],
      amount: 1400 + rng.nextDouble() * 2800,
      date: bar.date,
      actionType: rng.nextDouble() > 0.5 ? ActionType.buy : ActionType.sale,
      symbol: 'WINFUT',
    ));
  }
  return bubbles;
}

VolumeLevelStorageItem _buildVolume() {
  final bars = _kSampleBars;
  double minP = bars.first.low;
  double maxP = bars.first.high;
  for (final b in bars) {
    if (b.low < minP) minP = b.low;
    if (b.high > maxP) maxP = b.high;
  }
  final rng = _Rng(11);
  final levels = <VolumeLevel>[];
  for (double p = minP; p <= maxP + 0.001; p += 5) {
    final buy = 200 + rng.nextInt(2500);
    final sell = 200 + rng.nextInt(2500);
    levels.add(VolumeLevel(
      price: p,
      total: buy + sell,
      buyVolume: buy,
      sellVolume: sell,
    ));
  }
  return VolumeLevelStorageItem(
    id: 1,
    date: bars.last.date,
    symbol: 'WINFUT',
    timeFrame: 2,
    volumes: levels,
  );
}

List<StructureStorageItem> _buildStructures() {
  final bars = _kSampleBars;
  const deltas = [
    [151010.0, 150960.0],
    [151040.0, 150985.0],
    [151025.0, 150965.0],
    [151070.0, 151010.0],
    [151100.0, 151040.0],
    [151085.0, 151020.0],
    [151130.0, 151060.0],
    [151160.0, 151095.0],
  ];
  final structures = <StructureStorageItem>[];
  for (int i = 0; i < deltas.length; i++) {
    final bar = bars[i * 14];
    structures.add(StructureStorageItem(
      date: bar.date,
      symbol: 'WINFUT',
      timeFrame: 2,
      upBorder: deltas[i][0],
      downBorder: deltas[i][1],
      upAuxBorder: deltas[i][0] + 30,
      downAuxBorder: deltas[i][1] - 30,
    ));
  }
  return structures;
}

VerifierState _buildVerifierState() {
  final config = VerifierConfig(
    symbol: 'WINFUT',
    timeFrame: 2,
    strategyName: 'SmartBreakout',
    isDayTrade: true,
    dayTradeCloseTime: '13:00',
    quantity: 1,
  );
  final d = DateTime(2024, 5, 10);
  SignalEvent s({
    required String type,
    required String side,
    required double entry,
    double exit = 0,
    double pl = 0,
    required int i,
  }) =>
      SignalEvent(
        symbol: 'WINFUT',
        timeFrame: 2,
        date: d.add(Duration(minutes: i)),
        type: type,
        side: side,
        entryPrice: entry,
        exitPrice: exit,
        stopPrice: 0,
        targetPrice: 0,
        quantity: 1,
        points: 0,
        profitLoss: pl,
        commission: 0,
        cumulativePL: 0,
        positionOpen: type.startsWith('Entry'),
      );
  return VerifierState(
    symbol: 'WINFUT',
    timeFrame: 2,
    isRunning: true,
    config: config,
    openPosition: VerifierPosition(
      side: 'Buy',
      entryPrice: 151480.50,
      stopPrice: 151230.50,
      targetPrice: 151820.50,
      quantity: 2,
      entryDate: d,
      entryReason: 'Bubble acima da borda superior',
    ),
    totalTrades: 12,
    winCount: 7,
    lossCount: 5,
    winRate: 0.58,
    netProfit: 352.50,
    grossProfit: 1280.00,
    grossLoss: 927.50,
    maxDrawdown: 215.00,
    signals: [
      s(type: 'Entry Buy', side: 'Buy', entry: 151480.5, i: 10),
      s(type: 'Exit Buy', side: 'Buy', entry: 151480.5, exit: 151512.5, pl: 32.0, i: 22),
      s(type: 'Entry Sell', side: 'Sell', entry: 151540.0, i: 40),
      s(type: 'Exit Sell', side: 'Sell', entry: 151540.0, exit: 151558.5, pl: -18.5, i: 55),
      s(type: 'Entry Buy', side: 'Buy', entry: 151455.0, i: 70),
      s(type: 'Exit Buy', side: 'Buy', entry: 151455.0, exit: 151479.0, pl: 24.0, i: 88),
    ],
    equityCurve: [1000, 1010, 995, 1030, 1050, 1040, 1100, 1120, 1150, 1180, 1200, 1235],
  );
}

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
        priceOpen: 151450.00,
        sl: 151220.00,
        tp: 151820.00,
        priceCurrent: 151486.00,
        profit: 72.00,
        swap: 0,
        commission: 2.00,
        magic: 1234,
        comment: 'b3wm',
        time: '2024-05-10 10:22:00',
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
        time: '2024-05-10 11:05:00',
      ),
    ];

List<OrderInfo> _buildOrders() => [
      OrderInfo(
        ticket: 55,
        symbol: 'WINFUT',
        type: 'sell_limit',
        volume: 1,
        priceOpen: 151620.00,
        sl: 151740.00,
        tp: 151420.00,
        timeSetup: '2024-05-10 09:15:00',
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
        timeSetup: '2024-05-10 09:40:00',
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
          price: 151450.00,
          profit: 120.00,
          time: '2024-05-10 09:18:00',
          comment: 'b3wm',
          magic: 1234),
      HistoryDeal(
          ticket: 92,
          symbol: 'WINFUT',
          type: 'sell',
          volume: 2,
          price: 151510.00,
          profit: 60.00,
          time: '2024-05-10 10:41:00',
          comment: 'b3wm',
          magic: 1234),
      HistoryDeal(
          ticket: 93,
          symbol: 'WDOFUT',
          type: 'buy',
          volume: 1,
          price: 5.4400,
          profit: 40.00,
          time: '2024-05-10 11:12:00',
          comment: 'b3wm',
          magic: 1234),
    ];

// ---------------------------------------------------------------------------
// Fake services (no network)
// ---------------------------------------------------------------------------

class FakeApiService extends ApiService {
  FakeApiService() : super(baseUrl: 'http://test.local');

  @override
  Future<List<BarStorageItem>> getBars(String symbol, DateTime date) async =>
      _kSampleBars;

  @override
  Future<List<BubbleStorageItem>> getBubbles(
          String symbol, DateTime date) async =>
      _kSampleBubbles;

  @override
  Future<VolumeLevelStorageItem?> getVolume(
          String symbol, DateTime date) async =>
      _buildVolume();

  @override
  Future<List<StructureStorageItem>> getStructure(
          String symbol, DateTime date, double minDistance) async =>
      _kSampleStructures;

  @override
  Future<DateTime> findLastDateWithData(String symbol) async =>
      DateTime(2024, 5, 10);

  @override
  Future<VerifierState?> getVerifierState(String symbol, int timeFrame) async =>
      _buildVerifierState();
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

// ---------------------------------------------------------------------------
// Environment + helpers
// ---------------------------------------------------------------------------

class _Env {
  final ApiService api;
  final TradingApiService tradingApi;
  final SignalRService signalR;
  final PreferencesService prefs;
  final AudioService audio;
  final StateService state;

  _Env({
    required this.api,
    required this.tradingApi,
    required this.signalR,
    required this.prefs,
    required this.audio,
    required this.state,
  });
}

// Config real do app rodando (shared_preferences.json do usuário), aplicada
// aos goldens para refletir a visualização real: barras de volume finas,
// cores suaves de compra/venda, opacidades e bolhas pequenas.
final Map<String, Object> _kRealConfig = {
  'b3wm_Config_WINFUT': jsonEncode({
    'timeFrame': 2,
    'bubbleVisible': true,
    'bubbleSize': 0.15,
    'bubbleOpacity': 0.25,
    'bubbleSizeMin': 5.0,
    'bubbleSizeMax': 200.0,
    'thresholdBubble': 475,
    'profileVisible': true,
    'profileSizeH': 2.0,
    'profileSizeV': 0.25,
    'profileOpacity': 0.86,
    'profileAutoByPriceStructure': true,
    'structureVisible': true,
    'structureAuxVisible': false,
    'structureOpacity': 0.2,
    'structureRangeUpd': 200.0,
    'colorBuyer': '#9696b4',
    'colorSeller': '#b49696',
    'selectedAgents': [122, 238, 114, 16, 13, 40, 127, 3, 39],
    'agentThresholds': {'114': 700, '3': 1225, '39': 1000, '6003': 800},
    'knownAgents': [],
    'bubbleAmountFilter': true,
    'bubbleAgentsFilter': true,
    'bubbleSoundEnabled': false,
    'bubbleSoundVolume': 0.08,
    'tradingHistoryVisible': false,
    'positionVisible': false,
    'openOrdersVisible': true,
    'tradingPanelVisible': false,
    'tradingAccountExpanded': false,
    'tradingOrdersExpanded': false,
    'tradingPositionsExpanded': false,
    'tradingHistoryExpanded': false,
  }),
};

Future<_Env> _createEnv() async {
  SharedPreferences.setMockInitialValues(_kRealConfig);
  final prefs = PreferencesService();
  await prefs.init();
  final api = FakeApiService();
  final signalR = FakeSignalRService(api);
  final tradingApi = FakeTradingApiService();
  final audio = AudioService();
  final state = StateService(
    apiService: api,
    signalRService: signalR,
    preferencesService: prefs,
    audioService: audio,
  );
  await state.setSymbol('WINFUT');
  state.selectAllAgents();
  state.updatePositions(_buildPositions());
  state.updateOrders(_buildOrders());
  state.updateHistory(_buildHistory());
  await state.refreshVerifierState();
  return _Env(
    api: api,
    tradingApi: tradingApi,
    signalR: signalR,
    prefs: prefs,
    audio: audio,
    state: state,
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
}

Future<void> _capture(WidgetTester tester, String name) async {
  await expectLater(
    find.byKey(_goldenRoot),
    matchesGoldenFile('goldens/$name.png'),
  );
}

Future<void> _openDrawerTab(
    WidgetTester tester, _Env env, String tooltip, String name) async {
  await tester.pumpWidget(_buildApp(env));
  await _settle(tester);
  await tester.tap(find.byTooltip(tooltip));
  await _settle(tester);
  await _capture(tester, name);
  env.state.reset();
}

Future<void> _cancelTimers(WidgetTester tester, _Env env) async {
  await tester.pumpWidget(const SizedBox.shrink());
  await tester.pump(const Duration(seconds: 6));
  env.state.reset();
}

void main() {
  setUpAll(() async {
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
    env.state.setTradingPanelVisible(false);
    await tester.pumpWidget(_buildApp(env));
    await _settle(tester);
    await _capture(tester, 'overview');
    env.state.reset();
  });

  testWidgets('golden - aba Bubbles', (tester) async {
    final env = await _createEnv();
    env.state.setTradingPanelVisible(false);
    await _openDrawerTab(tester, env, 'Notificações de bubbles', 'drawer_bubbles');
  });

  testWidgets('golden - aba Estrutura', (tester) async {
    final env = await _createEnv();
    env.state.setTradingPanelVisible(false);
    await _openDrawerTab(tester, env, 'Notificações de estrutura', 'drawer_estrutura');
  });

  testWidgets('golden - aba Volume Profile', (tester) async {
    final env = await _createEnv();
    env.state.setTradingPanelVisible(false);
    await _openDrawerTab(tester, env, 'Volume Profile', 'drawer_volume_profile');
  });

  testWidgets('golden - aba Trading Data', (tester) async {
    final env = await _createEnv();
    env.state.setTradingPanelVisible(false);
    await _openDrawerTab(tester, env, 'Trading Data', 'drawer_trading_data');
  });

  testWidgets('golden - aba Verifier', (tester) async {
    final env = await _createEnv();
    env.state.setTradingPanelVisible(false);
    await _openDrawerTab(tester, env, 'Verificador', 'drawer_verifier');
  });

  testWidgets('golden - painel de trading', (tester) async {
    final env = await _createEnv();
    env.state.setTradingPanelVisible(true);
    env.state.setTradingAccountExpanded(true);
    env.state.setTradingOrdersExpanded(false);
    env.state.setTradingPositionsExpanded(true);
    env.state.setTradingHistoryExpanded(true);
    await tester.pumpWidget(_buildApp(env));
    await _settle(tester);
    await tester.pump(const Duration(milliseconds: 200));
    await _capture(tester, 'trading_panel');
    await _cancelTimers(tester, env);
  });
}

Future<void> _loadFonts() async {
  final root = Platform.environment['FLUTTER_ROOT'];
  final fontsDir = (root != null && root.isNotEmpty)
      ? '$root/bin/cache/artifacts/material_fonts'
      : 'C:/tools/flutter/bin/cache/artifacts/material_fonts';
  final loader = FontLoader('Roboto');
  for (final name in ['roboto-regular.ttf', 'roboto-bold.ttf', 'roboto-medium.ttf']) {
    final bytes = await File('$fontsDir/$name').readAsBytes();
    final data = ByteData.view(bytes.buffer, bytes.offsetInBytes, bytes.lengthInBytes);
    loader.addFont(Future.value(data));
  }
  await loader.load();

  final iconsLoader = FontLoader('MaterialIcons');
  final iconBytes = await File('$fontsDir/materialicons-regular.otf').readAsBytes();
  final iconData = ByteData.view(
      iconBytes.buffer, iconBytes.offsetInBytes, iconBytes.lengthInBytes);
  iconsLoader.addFont(Future.value(iconData));
  await iconsLoader.load();
}
