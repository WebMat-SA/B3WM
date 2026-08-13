import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'package:b3wm_flutter/models/bubble_storage_item.dart';
import 'package:b3wm_flutter/models/defaults.dart';
import 'package:b3wm_flutter/models/symbol_config.dart';
import 'package:b3wm_flutter/models/ticks2.dart';
import 'package:b3wm_flutter/services/api_service.dart';
import 'package:b3wm_flutter/services/audio_service.dart';
import 'package:b3wm_flutter/services/preferences_service.dart';
import 'package:b3wm_flutter/services/signalr_service.dart';
import 'package:b3wm_flutter/services/state_service.dart';

class _NoopSignalRService extends SignalRService {
  _NoopSignalRService({required super.hubUrl, required super.apiService});

  @override
  Future<void> startConnection(String symbol, int? timeFrame) async {}

  @override
  Future<void> stopConnection() async {}
}

ApiService _stubApi() => ApiService(
      baseUrl: 'http://test.local',
      client: MockClient((request) async {
        if (request.url.path.contains('GetVolume')) {
          return http.Response('null', 200);
        }
        return http.Response('[]', 200);
      }),
    );

ApiService _apiWithBubbles(List<BubbleStorageItem> bubbles) => ApiService(
      baseUrl: 'http://test.local',
      client: MockClient((request) async {
        if (request.url.path.contains('GetVolume')) {
          return http.Response('null', 200);
        }
        if (request.url.path.contains('GetBubble')) {
          return http.Response(
              jsonEncode(bubbles.map((b) => b.toJson()).toList()), 200);
        }
        return http.Response('[]', 200);
      }),
    );

StateService _createService(PreferencesService prefs) {
  final api = _stubApi();
  return StateService(
    apiService: api,
    signalRService: _NoopSignalRService(
        hubUrl: 'http://test.local/hub', apiService: api),
    preferencesService: prefs,
    audioService: AudioService(),
  );
}

void main() {
  group('SymbolConfig', () {
    test('seedFromLegacy aplica valores legados dentro dos limites do símbolo',
        () {
      final legacy = SymbolConfig.withDefaults('WINFUT')
        ..thresholdBubble = 350
        ..structureRangeUpd = 300;

      final winfut = SymbolConfig.seedFromLegacy('WINFUT', legacy);
      expect(winfut.thresholdBubble, 350);
      expect(winfut.structureRangeUpd, 300);

      final wdofut = SymbolConfig.seedFromLegacy('WDOFUT', legacy);
      expect(wdofut.thresholdBubble, Defaults.thresholdBubbleSize('WDOFUT'));
      expect(
          wdofut.structureRangeUpd, Defaults.minDistanceUpdateBorder('WDOFUT'));
    });

    test('fromJson usa default per-symbol quando a chave está ausente', () {
      final wdofut = SymbolConfig.fromJson({'timeFrame': 2}, symbol: 'WDOFUT');
      expect(wdofut.thresholdBubble, Defaults.thresholdBubbleSize('WDOFUT'));
      expect(
          wdofut.structureRangeUpd, Defaults.minDistanceUpdateBorder('WDOFUT'));

      final winfut = SymbolConfig.fromJson({'timeFrame': 2}, symbol: 'WINFUT');
      expect(winfut.thresholdBubble, Defaults.thresholdBubbleSize('WINFUT'));
    });

    test('toJson/fromJson preserva valores setados', () {
      final cfg = SymbolConfig.withDefaults('WINFUT')
        ..bubbleVisible = false
        ..thresholdBubble = 750
        ..selectedAgents = [1, 2]
        ..agentThresholds = {1: 500}
        ..knownAgents = [1, 2, 3];
      final restored = SymbolConfig.fromJson(cfg.toJson(), symbol: 'WINFUT');
      expect(restored.bubbleVisible, isFalse);
      expect(restored.thresholdBubble, 750);
      expect(restored.selectedAgents, [1, 2]);
      expect(restored.agentThresholds, {1: 500});
      expect(restored.knownAgents, [1, 2, 3]);
    });

    test('trading data flags round-trip e defaults', () {
      final defaults = SymbolConfig.withDefaults('WINFUT');
      expect(defaults.tradingHistoryVisible, isTrue);
      expect(defaults.positionVisible, isTrue);
      expect(defaults.openOrdersVisible, isTrue);

      final cfg = SymbolConfig.withDefaults('WINFUT')
        ..tradingHistoryVisible = false
        ..positionVisible = false
        ..openOrdersVisible = false;
      final restored = SymbolConfig.fromJson(cfg.toJson(), symbol: 'WINFUT');
      expect(restored.tradingHistoryVisible, isFalse);
      expect(restored.positionVisible, isFalse);
      expect(restored.openOrdersVisible, isFalse);

      final missing = SymbolConfig.fromJson({'timeFrame': 2}, symbol: 'WINFUT');
      expect(missing.tradingHistoryVisible, isTrue);
      expect(missing.positionVisible, isTrue);
      expect(missing.openOrdersVisible, isTrue);
    });

    test('trading drawer flags round-trip e defaults', () {
      final defaults = SymbolConfig.withDefaults('WINFUT');
      expect(defaults.tradingPanelVisible, isTrue);
      expect(defaults.tradingAccountExpanded, isFalse);
      expect(defaults.tradingOrdersExpanded, isFalse);
      expect(defaults.tradingPositionsExpanded, isFalse);
      expect(defaults.tradingHistoryExpanded, isFalse);

      final cfg = SymbolConfig.withDefaults('WINFUT')
        ..tradingPanelVisible = false
        ..tradingAccountExpanded = true
        ..tradingOrdersExpanded = true
        ..tradingPositionsExpanded = true
        ..tradingHistoryExpanded = true;
      final restored = SymbolConfig.fromJson(cfg.toJson(), symbol: 'WINFUT');
      expect(restored.tradingPanelVisible, isFalse);
      expect(restored.tradingAccountExpanded, isTrue);
      expect(restored.tradingOrdersExpanded, isTrue);
      expect(restored.tradingPositionsExpanded, isTrue);
      expect(restored.tradingHistoryExpanded, isTrue);

      final missing = SymbolConfig.fromJson({'timeFrame': 2}, symbol: 'WINFUT');
      expect(missing.tradingPanelVisible, isTrue);
      expect(missing.tradingAccountExpanded, isFalse);
      expect(missing.tradingOrdersExpanded, isFalse);
      expect(missing.tradingPositionsExpanded, isFalse);
      expect(missing.tradingHistoryExpanded, isFalse);
    });
  });

  group('StateService persistence', () {
    test('configs antigas globais são aplicadas a todos os símbolos', () async {
      SharedPreferences.setMockInitialValues({
        'b3wm_TimeFrame': 5,
        'b3wm_ThresholdBubble': 350,
        'b3wm_StructureRangeUpd': 300.0,
        'b3wm_BubbleVisible': false,
        'b3wm_ProfileVisible': false,
        'b3wm_StructureVisible': false,
      });
      final prefs = PreferencesService();
      await prefs.init();

      final winfut = _createService(prefs);
      await winfut.setSymbol('WINFUT');
      expect(winfut.timeFrame, 5);
      expect(winfut.thresholdBubble, 350);
      expect(winfut.structureRangeUpd, 300);
      expect(winfut.bubbleVisible, isFalse);
      winfut.reset();

      final wdofut = _createService(prefs);
      await wdofut.setSymbol('WDOFUT');
      expect(wdofut.timeFrame, 5);
      expect(wdofut.thresholdBubble, Defaults.thresholdBubbleSize('WDOFUT'));
      expect(wdofut.structureRangeUpd, Defaults.minDistanceUpdateBorder('WDOFUT'));
      expect(wdofut.bubbleVisible, isFalse);
      wdofut.reset();
    });

    test('config setada persiste ao recriar StateService', () async {
      SharedPreferences.setMockInitialValues({});
      final prefs = PreferencesService();
      await prefs.init();

      final service = _createService(prefs);
      await service.setSymbol('WINFUT');
      service.setBubbleVisible(false);
      service.setThresholdBubble(750);
      service.setBubbleSoundEnabled(false);
      service.setTradingHistoryVisible(false);
      service.setPositionVisible(false);
      service.setOpenOrdersVisible(false);
      service.setTradingPanelVisible(false);
      service.setTradingAccountExpanded(true);
      service.setTradingOrdersExpanded(true);
      service.setTradingPositionsExpanded(true);
      service.setTradingHistoryExpanded(true);
      service.reset();

      final reloaded = _createService(prefs);
      await reloaded.setSymbol('WINFUT');
      expect(reloaded.bubbleVisible, isFalse);
      expect(reloaded.thresholdBubble, 750);
      expect(reloaded.bubbleSoundEnabled, isFalse);
      expect(reloaded.tradingHistoryVisible, isFalse);
      expect(reloaded.positionVisible, isFalse);
      expect(reloaded.openOrdersVisible, isFalse);
      expect(reloaded.tradingPanelVisible, isFalse);
      expect(reloaded.tradingAccountExpanded, isTrue);
      expect(reloaded.tradingOrdersExpanded, isTrue);
      expect(reloaded.tradingPositionsExpanded, isTrue);
      expect(reloaded.tradingHistoryExpanded, isTrue);
      reloaded.reset();
    });

    test('configurações não vazam entre símbolos', () async {
      SharedPreferences.setMockInitialValues({});
      final prefs = PreferencesService();
      await prefs.init();

      final service = _createService(prefs);
      await service.setSymbol('WINFUT');
      service.setBubbleVisible(false);
      service.setTradingHistoryVisible(false);
      service.setTradingPanelVisible(false);
      service.setTradingOrdersExpanded(true);
      service.reset();

      final wdofut = _createService(prefs);
      await wdofut.setSymbol('WDOFUT');
      expect(wdofut.bubbleVisible, isTrue);
      expect(wdofut.tradingHistoryVisible, isTrue);
      expect(wdofut.tradingPanelVisible, isTrue);
      expect(wdofut.tradingOrdersExpanded, isFalse);
      wdofut.reset();

      final reloaded = _createService(prefs);
      await reloaded.setSymbol('WINFUT');
      expect(reloaded.bubbleVisible, isFalse);
      expect(reloaded.tradingHistoryVisible, isFalse);
      expect(reloaded.tradingPanelVisible, isFalse);
      expect(reloaded.tradingOrdersExpanded, isTrue);
      reloaded.reset();
    });

    test('agente novo é persistido no selectedAgents', () async {
      SharedPreferences.setMockInitialValues({});
      final prefs = PreferencesService();
      await prefs.init();

      final api = _stubApi();
      final signalR = _NoopSignalRService(
          hubUrl: 'http://test.local/hub', apiService: api);
      final service = StateService(
        apiService: api,
        signalRService: signalR,
        preferencesService: prefs,
        audioService: AudioService(),
      );
      await service.setSymbol('WINFUT');

      signalR.onNewBubble!(BubbleStorageItem(
        price: 1,
        agent: 42,
        amount: 100,
        date: DateTime.now(),
        actionType: ActionType.buy,
        symbol: 'WINFUT',
      ));
      expect(service.selectedAgents, contains(42));
      service.reset();

      final reloaded = _createService(prefs);
      await reloaded.setSymbol('WINFUT');
      expect(reloaded.selectedAgents, contains(42));
      reloaded.reset();
    });

    test('loadData carrega bubbles com resposta real da API (actionType string)',
        () async {
      SharedPreferences.setMockInitialValues({});
      final prefs = PreferencesService();
      await prefs.init();

      final api = ApiService(
        baseUrl: 'http://test.local',
        client: MockClient((request) async {
          if (request.url.path.contains('GetVolume')) {
            return http.Response('null', 200);
          }
          if (request.url.path.contains('GetBubble')) {
            return http.Response(jsonEncode([
              {
                'price': 170400,
                'agent': 85,
                'amount': 267,
                'date': '2026-08-13T09:03:19.473',
                'actionType': 'Sale',
                'symbol': 'WINFUT',
              },
              {
                'price': 170305,
                'agent': 85,
                'amount': 550,
                'date': '2026-08-13T09:03:31.973',
                'actionType': 'Buy',
                'symbol': 'WINFUT',
              },
            ]), 200);
          }
          return http.Response('[]', 200);
        }),
      );
      final signalR = _NoopSignalRService(
          hubUrl: 'http://test.local/hub', apiService: api);
      final service = StateService(
        apiService: api,
        signalRService: signalR,
        preferencesService: prefs,
        audioService: AudioService(),
      );
      await service.setSymbol('WINFUT');

      expect(service.bubbles.length, 2);
      expect(service.bubbles.map((b) => b.actionType).toSet(),
          {ActionType.sale, ActionType.buy});
      expect(service.allBubbleAgents, contains(85));
      service.reset();
    });

    test('bubbles do dia são auto-selecionados no load', () async {
      SharedPreferences.setMockInitialValues({});
      final prefs = PreferencesService();
      await prefs.init();

      BubbleStorageItem bubble(int agent) => BubbleStorageItem(
            price: 1,
            agent: agent,
            amount: 100,
            date: DateTime.now(),
            actionType: ActionType.buy,
            symbol: 'WINFUT',
          );

      final api = _apiWithBubbles([bubble(42), bubble(43)]);
      final signalR = _NoopSignalRService(
          hubUrl: 'http://test.local/hub', apiService: api);
      final service = StateService(
        apiService: api,
        signalRService: signalR,
        preferencesService: prefs,
        audioService: AudioService(),
      );
      await service.setSymbol('WINFUT');

      expect(service.selectedAgents, contains(42));
      expect(service.selectedAgents, contains(43));
      service.reset();

      final reloaded = _createService(prefs);
      await reloaded.setSymbol('WINFUT');
      expect(reloaded.selectedAgents, contains(42));
      expect(reloaded.selectedAgents, contains(43));
      reloaded.reset();
    });

    test('agentes selecionados persistem visíveis ao recarregar outro dia',
        () async {
      SharedPreferences.setMockInitialValues({});
      final prefs = PreferencesService();
      await prefs.init();

      BubbleStorageItem bubble(int agent) => BubbleStorageItem(
            price: 1,
            agent: agent,
            amount: 100,
            date: DateTime.now(),
            actionType: ActionType.buy,
            symbol: 'WINFUT',
          );

      final api = _apiWithBubbles([bubble(43)]);
      final signalR = _NoopSignalRService(
          hubUrl: 'http://test.local/hub', apiService: api);
      final service = StateService(
        apiService: api,
        signalRService: signalR,
        preferencesService: prefs,
        audioService: AudioService(),
      );
      await service.setSymbol('WINFUT');

      signalR.onNewBubble!(bubble(42));
      expect(service.selectedAgents, contains(42));

      await service.loadData();
      expect(service.allBubbleAgents, contains(42));
      expect(service.allBubbleAgents, contains(43));
      expect(service.selectedAgents, contains(42));
      expect(service.selectedAgents, contains(43));
      service.reset();
    });

    test('agente desmarcado não é reselecionado ao reaparecer em outro dia',
        () async {
      SharedPreferences.setMockInitialValues({});
      final prefs = PreferencesService();
      await prefs.init();

      BubbleStorageItem bubble(int agent) => BubbleStorageItem(
            price: 1,
            agent: agent,
            amount: 100,
            date: DateTime.now(),
            actionType: ActionType.buy,
            symbol: 'WINFUT',
          );

      final api = _stubApi();
      final signalR = _NoopSignalRService(
          hubUrl: 'http://test.local/hub', apiService: api);
      final service = StateService(
        apiService: api,
        signalRService: signalR,
        preferencesService: prefs,
        audioService: AudioService(),
      );
      await service.setSymbol('WINFUT');

      signalR.onNewBubble!(bubble(42));
      expect(service.selectedAgents, contains(42));

      service.toggleAgent(42);
      expect(service.selectedAgents, isNot(contains(42)));

      await service.loadData();
      expect(service.allBubbleAgents, contains(42));

      signalR.onNewBubble!(bubble(42));
      expect(service.selectedAgents, isNot(contains(42)));
      service.reset();
    });
  });

  group('BubbleStorageItem parsing', () {
    test('fromJson aceita actionType como número (SignalR/arquivos)', () {
      final b = BubbleStorageItem.fromJson({
        'price': 170400,
        'agent': 85,
        'amount': 267,
        'date': '2026-08-13T09:03:19.473',
        'actionType': 2,
        'symbol': 'WINFUT',
      });
      expect(b.actionType, ActionType.sale);
    });

    test('fromJson aceita actionType como string (API com JsonStringEnumConverter)',
        () {
      final b = BubbleStorageItem.fromJson({
        'price': 170400,
        'agent': 85,
        'amount': 267,
        'date': '2026-08-13T09:03:19.473',
        'actionType': 'Sale',
        'symbol': 'WINFUT',
      });
      expect(b.actionType, ActionType.sale);
    });
  });
}
