import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'package:b3wm_flutter/services/api_service.dart';
import 'package:b3wm_flutter/services/audio_service.dart';
import 'package:b3wm_flutter/services/preferences_service.dart';
import 'package:b3wm_flutter/services/signalr_service.dart';
import 'package:b3wm_flutter/services/state_service.dart';
import 'package:b3wm_flutter/services/strategy_service.dart';

class _NoopSignalRService extends SignalRService {
  _NoopSignalRService({required super.hubUrl, required super.apiService});

  @override
  Future<void> startConnection(String symbol, int? timeFrame) async {}

  @override
  Future<void> stopConnection() async {}
}

class _FakeStrategyService extends StrategyService {
  _FakeStrategyService()
      : super(
          baseUrl: 'http://test.local',
          client: MockClient((_) async => http.Response('[]', 200)),
        );

  List<StrategySession> sessions = [];

  @override
  Future<List<StrategySession>> state() async => sessions;
}

StrategySession _session(
  String strategy,
  String symbol, {
  int timeFrame = 2,
  bool paused = false,
}) =>
    StrategySession(
      sessionId: '$strategy-$symbol-$timeFrame',
      strategy: strategy,
      symbol: symbol,
      triggerTimeFrame: timeFrame,
      paused: paused,
      snapshotHash: 'snap-$strategy-$symbol-$timeFrame',
      position: null,
      decisions: 0,
      realizedPts: 0,
      lastDecision: null,
      reportItems: const [],
    );

void main() {
  late StateService service;
  late _FakeStrategyService strategy;

  setUp(() async {
    SharedPreferences.setMockInitialValues({});
    final prefs = PreferencesService();
    await prefs.init();
    final api = ApiService(
      baseUrl: 'http://test.local',
      client: MockClient((_) async => http.Response('[]', 200)),
    );
    strategy = _FakeStrategyService();
    service = StateService(
      apiService: api,
      signalRService:
          _NoopSignalRService(hubUrl: 'http://test.local/hub', apiService: api),
      preferencesService: prefs,
      audioService: AudioService(),
      strategyService: strategy,
    );
  });

  tearDown(() => service.dispose());

  test('sessão ativa trava o timeframe apenas do próprio símbolo', () async {
    await service.setSymbol('WINFUT');
    strategy.sessions = [_session('JevAnalysis', 'WINFUT')];
    await service.refreshStrategySessions();

    expect(service.configLocked, isTrue);
    expect(service.isSymbolLocked('winfut'), isTrue);
    await service.setTimeFrame(5);
    expect(service.timeFrame, 2);
  });

  test('sessão em outro símbolo não trava o símbolo atual', () async {
    await service.setSymbol('WDOFUT');
    strategy.sessions = [_session('JevAnalysis', 'WINFUT')];
    await service.refreshStrategySessions();

    expect(service.configLocked, isFalse);
    expect(service.isSymbolLocked('WDOFUT'), isFalse);
    await service.setTimeFrame(5);
    expect(service.timeFrame, 5);
  });

  test('pausar a sessão destrava apenas o símbolo dela', () async {
    await service.setSymbol('WDOFUT');
    strategy.sessions = [
      _session('JevAnalysis', 'WINFUT'),
      _session('JevAnalysis', 'WDOFUT'),
    ];
    await service.refreshStrategySessions();
    expect(service.isSymbolLocked('WINFUT'), isTrue);
    expect(service.isSymbolLocked('WDOFUT'), isTrue);
    await service.setTimeFrame(5);
    expect(service.timeFrame, 2);

    strategy.sessions = [
      _session('JevAnalysis', 'WINFUT'),
      _session('JevAnalysis', 'WDOFUT', paused: true),
    ];
    await service.refreshStrategySessions();
    expect(service.configLocked, isFalse);
    await service.setTimeFrame(5);
    expect(service.timeFrame, 5);

    await service.setSymbol('WINFUT');
    expect(service.configLocked, isTrue);
  });

  test('identidade de sessão usa strategy + símbolo + timeframe', () async {
    await service.setSymbol('WINFUT');
    strategy.sessions = [
      _session('JevAnalysis', 'WINFUT', timeFrame: 2),
      _session('SampleRule', 'WINFUT', timeFrame: 5),
    ];
    await service.refreshStrategySessions();

    expect(service.activeSessionKeys, hasLength(2));
    expect(
        service.hasActiveSession(
            strategy: 'JevAnalysis', symbol: 'WINFUT', timeFrame: 2),
        isTrue);
    expect(
        service.hasActiveSession(
            strategy: 'SampleRule', symbol: 'WINFUT', timeFrame: 5),
        isTrue);
    expect(
        service.hasActiveSession(
            strategy: 'JevAnalysis', symbol: 'WINFUT', timeFrame: 5),
        isFalse);
    expect(
        service.hasActiveSession(
            strategy: 'JevAnalysis', symbol: 'WDOFUT', timeFrame: 2),
        isFalse);
  });

  test('sem sessão o timeframe muda', () async {
    strategy.sessions = [];
    await service.refreshStrategySessions();
    await service.setTimeFrame(5);
    expect(service.timeFrame, 5);
  });

  test('migração 1440 -> 2 segue valendo sem trava', () async {
    await service.setTimeFrame(1440);
    expect(service.timeFrame, 2);
  });
}
