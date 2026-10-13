import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:shared_preferences/shared_preferences.dart';

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

void main() {
  late StateService service;

  setUp(() async {
    SharedPreferences.setMockInitialValues({});
    final prefs = PreferencesService();
    await prefs.init();
    final api = ApiService(
      baseUrl: 'http://test.local',
      client: MockClient((_) async => http.Response('[]', 200)),
    );
    service = StateService(
      apiService: api,
      signalRService:
          _NoopSignalRService(hubUrl: 'http://test.local/hub', apiService: api),
      preferencesService: prefs,
      audioService: AudioService(),
    );
  });

  test('com estratégia armada o timeframe não muda', () async {
    expect(service.timeFrame, 2);
    service.setStrategiesArmed(true);
    await service.setTimeFrame(5);
    expect(service.timeFrame, 2);
  });

  test('sem estratégia armada o timeframe muda', () async {
    service.setStrategiesArmed(false);
    await service.setTimeFrame(5);
    expect(service.timeFrame, 5);
  });

  test('migração 1440 -> 2 segue valendo sem trava', () async {
    service.setStrategiesArmed(false);
    await service.setTimeFrame(1440);
    expect(service.timeFrame, 2);
  });
}
