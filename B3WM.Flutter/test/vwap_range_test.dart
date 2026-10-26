import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'package:b3wm_flutter/models/bar_storage_item.dart';
import 'package:b3wm_flutter/models/volume_level.dart';
import 'package:b3wm_flutter/models/vwap_history_point.dart';
import 'package:b3wm_flutter/services/api_service.dart';
import 'package:b3wm_flutter/services/audio_service.dart';
import 'package:b3wm_flutter/services/preferences_service.dart';
import 'package:b3wm_flutter/services/signalr_service.dart';
import 'package:b3wm_flutter/services/state_service.dart';
import 'package:b3wm_flutter/ui/widgets/chart/chart_data.dart';

class _NoopSignalRService extends SignalRService {
  _NoopSignalRService({required super.hubUrl, required super.apiService});

  @override
  Future<void> startConnection(String symbol, int? timeFrame) async {}

  @override
  Future<void> stopConnection() async {}
}

ApiService _stubApi() => ApiService(
      baseUrl: 'http://test.local',
      client: MockClient((request) async => http.Response('[]', 200)),
    );

VolumeLevel _lvl(double price, int total) =>
    VolumeLevel(price: price, total: total, buyVolume: 0, sellVolume: 0);

BarStorageItem _bar(DateTime date, List<VolumeLevel>? levels) =>
    BarStorageItem(
      date: date,
      symbol: 'WINFUT',
      timeFrame: 2,
      open: 100000,
      high: 100100,
      low: 99900,
      close: 100050,
      volume: 10,
      volumeLevel: levels,
    );

VwapHistoryPoint _vwap(DateTime minute, double vwap) => VwapHistoryPoint(
      minute: minute,
      vwap: vwap,
      sumPv: vwap,
      sumV: 1,
      symbol: '',
      day: DateTime(2026, 1, 1),
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

  test('filtro de datas do volume não corta a VWAP (dia todo)', () async {
    final dates = [0, 2, 4, 6].map((m) => DateTime(2026, 1, 1, 9, m));
    var i = 0;
    for (final d in dates) {
      signalR.onCloseBar!(_bar(d, [_lvl(1000.0 + i, 10)]));
      signalR.onVwapUpdate!(_vwap(d, 100000.0 + i));
      i++;
    }
    expect(service.barsTimeFrameFilter.length, 4);

    // Janela parcial do TimeRangeSlider (só vale para o volume profile).
    service.applyVolumeFilter(2, 4);
    expect(service.dateRangeStart, 2);
    expect(service.dateRangeEnd, 4);

    final data = buildChartData(service);
    // VWAP cheia: um ponto por candle, do primeiro ao último.
    expect(data.vwapPoints.length, 4);
    expect(data.vwapPoints.map((p) => p.startCandleIndex).toList(),
        [0, 1, 2, 3]);
    expect(data.rangeStart, 2);
    expect(data.rangeEnd, 4);
  });

  test('historico multi-dia (intraday): VWAP de dias anteriores tambem e plotada',
      () async {
    // Dias arbitrarios: o "atual" vem de GetBar/GetVwapHistory e o "historico"
    // de GetBarRange/GetVwapRange. O mock ignora as datas pedidas.
    final dayA = DateTime(2026, 1, 1);
    final dayB = DateTime(2026, 10, 15);

    Map<String, dynamic> barJson(DateTime d, int m) => {
          'date': DateTime(d.year, d.month, d.day, 9, m).toIso8601String(),
          'symbol': 'WINFUT',
          'timeFrame': 2,
          'open': 100000.0,
          'high': 100100.0,
          'low': 99900.0,
          'close': 100050.0,
          'volume': 10,
        };
    Map<String, dynamic> vwapJson(DateTime d, int m, double v) => {
          'minute': DateTime(d.year, d.month, d.day, 9, m).toIso8601String(),
          'vwap': v,
          'sumPv': v,
          'sumV': 1,
          'symbol': 'WINFUT',
          'day': DateTime(d.year, d.month, d.day).toIso8601String(),
        };

    final api = ApiService(
      baseUrl: 'http://test.local',
      client: MockClient((request) async {
        final path = request.url.path;
        if (path.contains('GetVolume')) return http.Response('', 404);
        if (path.contains('GetVwapHistory')) {
          return http.Response(
              jsonEncode([vwapJson(dayA, 0, 100), vwapJson(dayA, 2, 101)]),
              200);
        }
        if (path.contains('GetVwapRange')) {
          return http.Response(
              jsonEncode([vwapJson(dayB, 0, 200), vwapJson(dayB, 2, 201)]),
              200);
        }
        if (path.contains('GetBarRange')) {
          return http.Response(
              jsonEncode([barJson(dayB, 0), barJson(dayB, 2)]), 200);
        }
        if (path.contains('/GetBar/')) {
          return http.Response(
              jsonEncode([barJson(dayA, 0), barJson(dayA, 2)]), 200);
        }
        if (path.contains('GetExtreme') || path.contains('GetPivotIntraday')) {
          return http.Response('null', 200);
        }
        return http.Response('[]', 200);
      }),
    );
    SharedPreferences.setMockInitialValues({});
    final prefs = PreferencesService();
    await prefs.init();
    final svc = StateService(
      apiService: api,
      signalRService:
          _NoopSignalRService(hubUrl: 'http://test.local/hub', apiService: api),
      preferencesService: prefs,
      audioService: AudioService(),
    );

    await svc.setSymbol('WINFUT');
    expect(svc.barsTimeFrameFilter.length, 2);
    expect(await svc.loadPreviousHistory(), isTrue);
    expect(svc.barsTimeFrameFilter.length, 4);

    final data = buildChartData(svc);
    // Um ponto de VWAP por candle, incluindo os do dia historico.
    expect(data.vwapPoints.length, 4);
    expect(data.vwapPoints.map((p) => p.price).toSet(),
        {100.0, 101.0, 200.0, 201.0});
  });
}
