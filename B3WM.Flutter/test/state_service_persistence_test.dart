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
        ..agentThresholds = {1: 500};
      final restored = SymbolConfig.fromJson(cfg.toJson(), symbol: 'WINFUT');
      expect(restored.bubbleVisible, isFalse);
      expect(restored.thresholdBubble, 750);
      expect(restored.selectedAgents, [1, 2]);
      expect(restored.agentThresholds, {1: 500});
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
      service.reset();

      final reloaded = _createService(prefs);
      await reloaded.setSymbol('WINFUT');
      expect(reloaded.bubbleVisible, isFalse);
      expect(reloaded.thresholdBubble, 750);
      expect(reloaded.bubbleSoundEnabled, isFalse);
      reloaded.reset();
    });

    test('configurações não vazam entre símbolos', () async {
      SharedPreferences.setMockInitialValues({});
      final prefs = PreferencesService();
      await prefs.init();

      final service = _createService(prefs);
      await service.setSymbol('WINFUT');
      service.setBubbleVisible(false);
      service.reset();

      final wdofut = _createService(prefs);
      await wdofut.setSymbol('WDOFUT');
      expect(wdofut.bubbleVisible, isTrue);
      wdofut.reset();

      final reloaded = _createService(prefs);
      await reloaded.setSymbol('WINFUT');
      expect(reloaded.bubbleVisible, isFalse);
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
  });
}
