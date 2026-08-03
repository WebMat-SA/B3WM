import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'package:b3wm_flutter/models/bar_storage_item.dart';
import 'package:b3wm_flutter/models/volume_level.dart';
import 'package:b3wm_flutter/models/volume_level_storage_item.dart';
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

VolumeLevel _lvl(double price, int total) =>
    VolumeLevel(price: price, total: total, buyVolume: 0, sellVolume: 0);

BarStorageItem _bar(int i, List<VolumeLevel>? levels) => BarStorageItem(
      date: DateTime(2026, 1, 1, 9, i * 2),
      symbol: 'WINFUT',
      timeFrame: 2,
      open: 100000,
      high: 100100,
      low: 99900,
      close: 100050,
      volume: 10,
      volumeLevel: levels,
    );

void main() {
  late StateService service;
  late _NoopSignalRService signalR;

  setUp(() async {
    SharedPreferences.setMockInitialValues({});
    final prefs = PreferencesService();
    await prefs.init();
    final api = _stubApi();
    signalR = _NoopSignalRService(
        hubUrl: 'http://test.local/hub', apiService: api);
    service = StateService(
      apiService: api,
      signalRService: signalR,
      preferencesService: prefs,
      audioService: AudioService(),
    );
  });

  test('com 2 barras o range completo mostra o cumulativo (não um Diff)',
      () async {
    signalR.onCloseBar!(_bar(0, [_lvl(1000, 10)]));
    signalR.onCloseBar!(_bar(1, [_lvl(1000, 10), _lvl(1001, 5)]));

    expect(service.barsTimeFrameFilter.length, 2);
    expect(service.filteredVolumeLevels, isNull);
    expect(service.volumeFilterActive, isFalse);
  });

  test('range [0, end] parcial inclui o volume do primeiro candle', () async {
    signalR.onCloseBar!(_bar(0, [_lvl(1000, 10)]));
    signalR.onCloseBar!(_bar(1, [_lvl(1000, 10), _lvl(1001, 5)]));
    signalR.onCloseBar!(
        _bar(2, [_lvl(1000, 10), _lvl(1001, 5), _lvl(1002, 3)]));

    service.applyVolumeFilter(0, 2);

    final levels = service.filteredVolumeLevels!;
    expect(levels.length, 3);
    expect(levels.firstWhere((v) => v.price == 1000).total, 10);
    expect(levels.firstWhere((v) => v.price == 1001).total, 5);
    expect(levels.firstWhere((v) => v.price == 1002).total, 3);
  });

  test(
      'range [max-1, max] sem snapshot na referência mostra o Diff (não o volume todo)',
      () async {
    // barra 1 (referência) sem snapshot; só a 0 e a última têm.
    signalR.onCloseBar!(_bar(0, [_lvl(1000, 5)]));
    signalR.onCloseBar!(_bar(1, null));
    signalR.onCloseBar!(_bar(2, [_lvl(1000, 10), _lvl(1001, 5)]));

    service.applyVolumeFilter(2, 3);

    final levels = service.filteredVolumeLevels!;
    expect(levels.firstWhere((v) => v.price == 1000).total, 5);
    expect(levels.firstWhere((v) => v.price == 1001).total, 5);
  });

  test('range [start>0, end] subtrai a referência anterior (Diff)', () async {
    signalR.onCloseBar!(_bar(0, [_lvl(1000, 10)]));
    signalR.onCloseBar!(_bar(1, [_lvl(1000, 10), _lvl(1001, 5)]));
    signalR.onCloseBar!(
        _bar(2, [_lvl(1000, 10), _lvl(1001, 5), _lvl(1002, 3)]));

    service.applyVolumeFilter(1, 3);

    final levels = service.filteredVolumeLevels!;
    expect(levels.firstWhere((v) => v.price == 1000).total, 0);
    expect(levels.firstWhere((v) => v.price == 1001).total, 5);
    expect(levels.firstWhere((v) => v.price == 1002).total, 3);
  });

  test('volume update mantém cumulativo em range completo', () async {
    signalR.onCloseBar!(_bar(0, [_lvl(1000, 10)]));
    signalR.onCloseBar!(_bar(1, [_lvl(1000, 10), _lvl(1001, 5)]));

    signalR.onVolumeUpdate!(VolumeLevelStorageItem(
      id: 1,
      date: DateTime(2026, 1, 1, 9, 4),
      symbol: 'WINFUT',
      timeFrame: 2,
      volumes: [_lvl(1000, 12), _lvl(1001, 6)],
    ));

    expect(service.filteredVolumeLevels, isNull);
    expect(service.volumeLevels.length, 2);
  });

  test(
      'close bar ao vivo sem VolumeLevel preserva o snapshot cumulativo (CloneBar)',
      () async {
    signalR.onCloseBar!(_bar(0, [_lvl(1000, 10)]));
    signalR.onCloseBar!(_bar(1, [_lvl(1000, 10), _lvl(1001, 5)]));
    signalR.onCloseBar!(
        _bar(2, [_lvl(1000, 10), _lvl(1001, 5), _lvl(1002, 3)]));

    signalR.onCloseBar!(_bar(2, null));

    expect(service.barsTimeFrameFilter[2].volumeLevel!.length, 3);

    service.applyVolumeFilter(2, 3);
    final levels = service.filteredVolumeLevels!;
    expect(levels.firstWhere((v) => v.price == 1000).total, 0);
    expect(levels.firstWhere((v) => v.price == 1001).total, 0);
    expect(levels.firstWhere((v) => v.price == 1002).total, 3);
  });

  test('current bar ao vivo sem VolumeLevel preserva o snapshot cumulativo',
      () async {
    signalR.onCloseBar!(_bar(0, [_lvl(1000, 10)]));
    signalR.onCloseBar!(_bar(1, [_lvl(1000, 10), _lvl(1001, 5)]));
    signalR.onCloseBar!(
        _bar(2, [_lvl(1000, 10), _lvl(1001, 5), _lvl(1002, 3)]));

    signalR.onCurrentBar!(_bar(2, null));

    expect(service.barsTimeFrameFilter[2].volumeLevel!.length, 3);

    service.applyVolumeFilter(2, 3);
    final levels = service.filteredVolumeLevels!;
    expect(levels.firstWhere((v) => v.price == 1002).total, 3);
  });

  test('volume update mantém primeiro candle incluso em range parcial',
      () async {
    signalR.onCloseBar!(_bar(0, [_lvl(1000, 10)]));
    signalR.onCloseBar!(_bar(1, [_lvl(1000, 10), _lvl(1001, 5)]));
    signalR.onCloseBar!(
        _bar(2, [_lvl(1000, 10), _lvl(1001, 5), _lvl(1002, 3)]));

    service.applyVolumeFilter(0, 2);
    signalR.onVolumeUpdate!(VolumeLevelStorageItem(
      id: 1,
      date: DateTime(2026, 1, 1, 9, 6),
      symbol: 'WINFUT',
      timeFrame: 2,
      volumes: [_lvl(1000, 12), _lvl(1001, 6), _lvl(1002, 4)],
    ));

    final levels = service.filteredVolumeLevels!;
    expect(levels.firstWhere((v) => v.price == 1000).total, 10);
    expect(levels.firstWhere((v) => v.price == 1001).total, 5);
    expect(levels.firstWhere((v) => v.price == 1002).total, 3);
  });
}
