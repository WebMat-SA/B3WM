import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'package:b3wm_flutter/services/api_service.dart';
import 'package:b3wm_flutter/services/audio_service.dart';
import 'package:b3wm_flutter/services/preferences_service.dart';
import 'package:b3wm_flutter/services/signalr_service.dart';
import 'package:b3wm_flutter/services/state_service.dart';
import 'package:b3wm_flutter/models/symbol_config.dart';

class _NoopSignalRService extends SignalRService {
  _NoopSignalRService({required super.hubUrl, required super.apiService});
  @override
  Future<void> startConnection(String symbol, int? timeFrame) async {}
  @override
  Future<void> stopConnection() async {}
}

void main() {
  test('repro old config', () async {
    final oldConfig = {
      'timeFrame': 2,
      'bubbleVisible': true,
      'bubbleSize': 1.0,
      'bubbleOpacity': 0.7,
      'bubbleSizeMin': 20,
      'bubbleSizeMax': 100,
      'thresholdBubble': 250,
      'profileVisible': true,
      'profileSizeH': 1.0,
      'profileSizeV': 1.0,
      'profileOpacity': 0.5,
      'profileAutoByPriceStructure': false,
      'structureVisible': true,
      'structureAuxVisible': true,
      'structureOpacity': 0.8,
      'structureRangeUpd': 250.0,
      'colorBuyer': '#4488ff',
      'colorSeller': '#ff4444',
      'selectedAgents': [1, 2],
      'agentThresholds': {'1': 500},
      'bubbleAmountFilter': true,
      'bubbleAgentsFilter': true,
      'bubbleSoundEnabled': true,
      'bubbleSoundVolume': 0.5,
      'tradingHistoryVisible': true,
      'positionVisible': true,
      'openOrdersVisible': true,
      'tradingPanelVisible': true,
      'tradingAccountExpanded': false,
      'tradingOrdersExpanded': false,
      'tradingPositionsExpanded': false,
      'tradingHistoryExpanded': false,
    };
    SharedPreferences.setMockInitialValues({
      'b3wm_Config_WINFUT': jsonEncode(oldConfig),
    });
    final prefs = PreferencesService();
    await prefs.init();
    final api = ApiService(
      baseUrl: 'http://test.local',
      client: MockClient((request) async => http.Response('[]', 200)),
    );
    final signalR = _NoopSignalRService(hubUrl: 'http://test.local/hub', apiService: api);
    final service = StateService(
      apiService: api,
      signalRService: signalR,
      preferencesService: prefs,
      audioService: AudioService(),
    );
    await service.setSymbol('WINFUT');
    // access getters
    print('selectedAgents: ${service.selectedAgents}');
    print('allBubbleAgents: ${service.allBubbleAgents}');
    print('knownAgents via config: ${service.agentThresholds}');
    service.reset();
  });
}
