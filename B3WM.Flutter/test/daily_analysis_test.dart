import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'package:b3wm_flutter/models/bar_storage_item.dart';
import 'package:b3wm_flutter/models/daily_analysis_config.dart';
import 'package:b3wm_flutter/models/defaults.dart';
import 'package:b3wm_flutter/models/symbol_config.dart';
import 'package:b3wm_flutter/services/api_service.dart';
import 'package:b3wm_flutter/services/audio_service.dart';
import 'package:b3wm_flutter/services/preferences_service.dart';
import 'package:b3wm_flutter/services/signalr_service.dart';
import 'package:b3wm_flutter/services/state_service.dart';
import 'package:b3wm_flutter/ui/widgets/chart/daily_chart_data.dart';

class _NoopSignalRService extends SignalRService {
  _NoopSignalRService({required super.hubUrl, required super.apiService});

  @override
  Future<void> startConnection(String symbol, int? timeFrame) async {}

  @override
  Future<void> stopConnection() async {}
}

Map<String, dynamic> _bar(String date, double base) => {
      'date': date,
      'symbol': 'WINFUT',
      'timeFrame': 1440,
      'open': base,
      'high': base + 100,
      'low': base - 100,
      'close': base + 10,
      'volume': 1000,
    };

Map<String, dynamic> _structure(String date, double up, double down) => {
      'date': date,
      'symbol': 'WINFUT',
      'timeFrame': 1440,
      'upBorder': up,
      'downBorder': down,
      'upAuxBorder': up + 50,
      'downAuxBorder': down - 50,
    };

/// Mock com rotas diárias + respostas vazias para o fluxo intraday.
ApiService _dailyApi() => ApiService(
      baseUrl: 'http://test.local',
      client: MockClient((request) async {
        final path = request.url.path;
        if (path.contains('GetStructureHistory')) {
          return http.Response(
              jsonEncode([
                _structure('2026-08-01T00:00:00.000', 100000, 99000),
                _structure('2026-08-10T00:00:00.000', 101000, 99000),
              ]),
              200);
        }
        if (path.contains('GetBarRange')) {
          return http.Response(
              jsonEncode([
                _bar('2026-08-10T00:00:00.000', 100000),
                _bar('2026-08-11T00:00:00.000', 100500),
                _bar('2026-08-12T00:00:00.000', 101000),
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
                {
                  'price': 101000.0,
                  'total': 9000,
                  'buyVolume': 4000,
                  'sellVolume': 5000
                },
              ]),
              200);
        }
        if (path.contains('GetExtremeDaily')) {
          return http.Response(
              jsonEncode({
                'symbol': 'WINFUT',
                'date': '2026-08-12T00:00:00.000',
                'periodFrom': '2026-08-10T00:00:00.000',
                'periodTo': '2026-08-12T00:00:00.000',
                'config': {
                  'noiseSensitivity': 3.0,
                  'minimumProminence': 0.15
                },
                'statistics': {
                  'topCount': 1,
                  'valleyCount': 1,
                  'pointCount': 2
                },
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
                  {
                    'position': 99000.0,
                    'value': 1000.0,
                    'type': 'Valley',
                    'prominence': 0.4,
                    'strength': 1.0,
                    'confidence': 1.0,
                    'width': 2.0,
                    'isEdge': false,
                  },
                ],
              }),
              200);
        }
        if (path.contains('GetVolume')) return http.Response('null', 200);
        if (path.contains('SetExtremeConfig')) {
          return http.Response('null', 200);
        }
        return http.Response('[]', 200);
      }),
    );

StateService _createDailyService(PreferencesService prefs) {
  final api = _dailyApi();
  return StateService(
    apiService: api,
    signalRService: _NoopSignalRService(
        hubUrl: 'http://test.local/hub', apiService: api),
    preferencesService: prefs,
    audioService: AudioService(),
  );
}

void main() {
  group('DailyAnalysisConfig', () {
    test('defaults por símbolo (Range 1D)', () {
      expect(DailyAnalysisConfig.withDefaults('WINFUT').structureRangeUpd,
          Defaults.minDistanceUpdateBorderDaily('WINFUT'));
      expect(DailyAnalysisConfig.withDefaults('WDOFUT').structureRangeUpd,
          Defaults.minDistanceUpdateBorderDaily('WDOFUT'));
      expect(DailyAnalysisConfig.withDefaults('WINFUT').windowDays, 0);
      expect(DailyAnalysisConfig.withDefaults('WINFUT').profileVisible,
          isTrue);
    });

    test('SymbolConfig round-trip preserva bloco daily', () {
      final cfg = SymbolConfig.withDefaults('WINFUT')
        ..daily.structureRangeUpd = 1500
        ..daily.profileOpacity = 0.9
        ..daily.extremeNoiseSensitivity = 5.0
        ..daily.windowDays = 30
        ..daily.panelVisible = true
        ..daily.panelFraction = 0.6;
      final restored = SymbolConfig.fromJson(cfg.toJson(), symbol: 'WINFUT');
      expect(restored.daily.structureRangeUpd, 1500);
      expect(restored.daily.profileOpacity, 0.9);
      expect(restored.daily.extremeNoiseSensitivity, 5.0);
      expect(restored.daily.windowDays, 30);
      expect(restored.daily.panelVisible, isTrue);
      expect(restored.daily.panelFraction, 0.6);
      expect(cfg.toJson(), contains('daily'));
      expect(cfg.toJson().containsKey('structureRangeUpdDaily'), isFalse);
      expect(cfg.toJson().containsKey('dailyExtremeVisible'), isFalse);
    });

    test('fromJson migra chaves flat da #10', () {
      final cfg = SymbolConfig.fromJson({
        'timeFrame': 2,
        'structureRangeUpdDaily': 1500,
        'dailyStructureVisible': false,
        'dailyExtremeVisible': false,
        'dailyExtremeOpacity': 0.7,
        'dailyExtremeNoiseSensitivity': 5.0,
        'dailyExtremeMinimumProminence': 0.3,
      }, symbol: 'WINFUT');
      expect(cfg.daily.structureRangeUpd, 1500);
      expect(cfg.daily.structureVisible, isFalse);
      expect(cfg.daily.extremeVisible, isFalse);
      expect(cfg.daily.extremeOpacity, 0.7);
      expect(cfg.daily.extremeNoiseSensitivity, 5.0);
      expect(cfg.daily.extremeMinimumProminence, 0.3);
    });

    test('fromJson migra timeFrame 1440 para intraday', () {
      final cfg =
          SymbolConfig.fromJson({'timeFrame': 1440}, symbol: 'WINFUT');
      expect(cfg.timeFrame, isNot(1440));
    });
  });

  group('ApiService.getDailyProfile', () {
    test('parseia sem janela (from/to nulos)', () async {
      final api = _dailyApi();
      final levels = await api.getDailyProfile('WINFUT');
      expect(levels.length, 2);
    });

    test('parseia com janela from/to', () async {
      final api = _dailyApi();
      final levels = await api.getDailyProfile(
        'WINFUT',
        from: DateTime(2026, 8, 1),
        to: DateTime(2026, 8, 12),
      );
      expect(levels.length, 2);
      expect(levels.first.price, 100000.0);
      expect(levels.first.total, 5000);
      expect(levels.first.delta, 1000);
    });
  });

  group('StateService daily (issue #12)', () {
    test('loadDailyAll carrega bars + perfil + extremos isolados', () async {
      SharedPreferences.setMockInitialValues({});
      final prefs = PreferencesService();
      await prefs.init();

      final service = _createDailyService(prefs);
      await service.setSymbol('WINFUT');

      // Intraday intacto e vazio (mock sem dados intraday).
      expect(service.barsTimeFrameFilter, isEmpty);
      expect(service.dailyBars, isEmpty);

      await service.loadDailyAll();

      expect(service.dailyBars.length, 3);
      expect(service.dailyProfileLevels.length, 2);
      expect(service.dailyExtremes, isNotNull);
      expect(service.dailyExtremes!.extremes.length, 2);
      expect(service.dailyAnchor, isNotNull);
      expect(service.dailyStructureChanges.length, 1);
      expect(service.dailyStructureChanges.first.isUp, isTrue);

      final data = buildDailyChartData(service);
      expect(data.candles.length, 3);
      expect(data.timeFrame, 1440);
      expect(data.structures, isNotNull);
      expect(data.extremes, isNotNull);
      expect(data.extremes!.topPrices, [101000.0]);
      expect(data.extremes!.valleyPrices, [99000.0]);
      expect(data.volumeProfile.length, 2);
      // POC = nível de maior volume.
      expect(
          data.volumeProfile
              .firstWhere((v) => v.isPoc)
              .price,
          101000.0);
      // Sem contaminação do intraday.
      expect(data.redBubbles, isEmpty);
      expect(data.blueBubbles, isEmpty);
      expect(data.vwapPoints, isEmpty);
      service.reset();
    });

    test('setDailyWindowDays persiste e recarrega na janela por dias',
        () async {
      SharedPreferences.setMockInitialValues({});
      final prefs = PreferencesService();
      await prefs.init();

      final service = _createDailyService(prefs);
      await service.setSymbol('WINFUT');
      await service.loadDailyAll();

      await service.setDailyWindowDays(30);
      expect(service.dailyWindowDays, 30);
      final now = DateTime.now();
      final today = DateTime(now.year, now.month, now.day);
      expect(service.dailyProfileFrom, today.subtract(const Duration(days: 30)));
      expect(service.dailyProfileTo, today);

      // Recria e confere persistência no mesmo Config_$symbol.
      service.reset();
      final reloaded = _createDailyService(prefs);
      await reloaded.setSymbol('WINFUT');
      expect(reloaded.dailyWindowDays, 30);
      reloaded.reset();
    });

    test('setDailyPanelFraction limita 0.3–0.7 e persiste', () async {
      SharedPreferences.setMockInitialValues({});
      final prefs = PreferencesService();
      await prefs.init();

      final service = _createDailyService(prefs);
      await service.setSymbol('WINFUT');
      expect(service.dailyPanelVisible, isFalse);

      service.setDailyPanelVisible(true);
      service.setDailyPanelFraction(0.9);
      expect(service.dailyPanelFraction, 0.7);
      service.setDailyPanelFraction(0.1);
      expect(service.dailyPanelFraction, 0.3);
      service.setDailyPanelFraction(0.6);
      service.reset();

      final reloaded = _createDailyService(prefs);
      await reloaded.setSymbol('WINFUT');
      expect(reloaded.dailyPanelVisible, isTrue);
      expect(reloaded.dailyPanelFraction, 0.6);
      reloaded.reset();
    });

    test('arrasto: preview não salva, commit persiste p/ restart', () async {
      SharedPreferences.setMockInitialValues({});
      final prefs = PreferencesService();
      await prefs.init();

      final service = _createDailyService(prefs);
      await service.setSymbol('WINFUT');
      expect(service.dailyPanelFraction, 0.5);

      // Pixels do arrasto: mudam a UI sem tocar o disco.
      service.previewDailyPanelFraction(0.9);
      service.previewDailyPanelFraction(0.65);
      expect(service.dailyPanelFraction, 0.65);

      service.reset();
      final restarted = _createDailyService(prefs);
      await restarted.setSymbol('WINFUT');
      expect(restarted.dailyPanelFraction, 0.5);
      restarted.reset();

      // Fim do gesto: commit persiste; restart restaura.
      service.previewDailyPanelFraction(0.65);
      await service.commitDailyPanelFraction();
      service.reset();
      final restored = _createDailyService(prefs);
      await restored.setSymbol('WINFUT');
      expect(restored.dailyPanelFraction, 0.65);
      restored.reset();
    });

    test('barra diária de hoje acompanha os ticks ao vivo', () async {
      SharedPreferences.setMockInitialValues({});
      final prefs = PreferencesService();
      await prefs.init();

      final now = DateTime.now();
      final today = DateTime(now.year, now.month, now.day);
      final yesterday = today.subtract(const Duration(days: 1));
      String iso(DateTime d) =>
          '${d.year.toString().padLeft(4, '0')}-${d.month.toString().padLeft(2, '0')}-${d.day.toString().padLeft(2, '0')}T'
          '${d.hour.toString().padLeft(2, '0')}:${d.minute.toString().padLeft(2, '0')}:00.000';

      final api = ApiService(
        baseUrl: 'http://test.local',
        client: MockClient((request) async {
          final path = request.url.path;
          if (path.contains('GetBarRange')) {
            return http.Response(
                jsonEncode([
                  _bar(iso(yesterday), 100000),
                  _bar(iso(today), 101000),
                ]),
                200);
          }
          if (path.contains('GetStructureHistory')) {
            return http.Response('[]', 200);
          }
          if (path.contains('GetDailyProfile')) {
            return http.Response('[]', 200);
          }
          if (path.contains('GetExtremeDaily')) {
            return http.Response('null', 200);
          }
          if (path.contains('GetVolume')) return http.Response('null', 200);
          if (path.contains('SetExtremeConfig')) {
            return http.Response('null', 200);
          }
          // Intraday vazio: o ao vivo chega via SignalR abaixo.
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
      await service.loadDailyAll();
      expect(service.dailyBars.length, 2);

      BarStorageItem liveBar(
              int hour, double open, double high, double low, double close,
              int volume) =>
          BarStorageItem(
            date: DateTime(now.year, now.month, now.day, hour),
            symbol: 'WINFUT',
            timeFrame: 2,
            open: open,
            high: high,
            low: low,
            close: close,
            volume: volume,
          );

      // Primeiro candle do dia via close-bar.
      signalR.onCloseBar!(liveBar(9, 101000, 101050, 100900, 101020, 1000));
      var todayBar = service.dailyBars.last;
      expect(todayBar.open, 101000);
      expect(todayBar.close, 101020);
      expect(todayBar.high, 101050);
      expect(todayBar.low, 100900);
      expect(todayBar.volume, 1000);
      expect(buildDailyChartData(service).lastPrice, 101020);

      // Segundo candle com nova máxima via current-bar (tick em andamento).
      signalR.onCurrentBar!(liveBar(10, 101020, 101200, 101000, 101150, 2000));
      todayBar = service.dailyBars.last;
      expect(todayBar.open, 101000);
      expect(todayBar.high, 101200);
      expect(todayBar.low, 100900);
      expect(todayBar.close, 101150);
      expect(todayBar.volume, 3000);
      expect(buildDailyChartData(service).lastPrice, 101150);

      // Histórico preservado: ontem intacto, contagem estável.
      expect(service.dailyBars.length, 2);
      expect(service.dailyBars.first.open, 100000);
      service.reset();
    });

    test('onDailyBar atualiza o candle diário como o 2min', () async {
      SharedPreferences.setMockInitialValues({});
      final prefs = PreferencesService();
      await prefs.init();

      final api = _dailyApi();
      final signalR = _NoopSignalRService(
          hubUrl: 'http://test.local/hub', apiService: api);
      final service = StateService(
        apiService: api,
        signalRService: signalR,
        preferencesService: prefs,
        audioService: AudioService(),
      );
      await service.setSymbol('WINFUT');
      await service.loadDailyAll();
      final baseCount = service.dailyBars.length;
      expect(baseCount, greaterThan(0));

      final now = DateTime.now();
      BarStorageItem dailySnapshot(double close, int volume) =>
          BarStorageItem(
            date: DateTime(now.year, now.month, now.day, 10, 30),
            symbol: 'WINFUT',
            timeFrame: 1440,
            open: 100000,
            high: 101000,
            low: 99000,
            close: close,
            volume: volume,
          );

      // Snapshot ao vivo: acrescenta a barra de hoje e move o last price.
      signalR.onDailyBar!(dailySnapshot(100500, 5000));
      expect(service.dailyBars.length, baseCount + 1);
      expect(service.dailyBars.last.close, 100500);
      expect(buildDailyChartData(service).lastPrice, 100500);

      // Snapshot seguinte: substitui (não duplica) e atualiza o close.
      signalR.onDailyBar!(dailySnapshot(100700, 6000));
      expect(service.dailyBars.length, baseCount + 1);
      expect(service.dailyBars.last.close, 100700);
      expect(service.dailyBars.last.open, 100000);
      expect(buildDailyChartData(service).lastPrice, 100700);

      // Snapshot vazio (serviço recém-iniciado) e timeframe errado: ignora.
      signalR.onDailyBar!(BarStorageItem(
        date: DateTime(1, 1, 1),
        symbol: 'WINFUT',
        timeFrame: 1440,
        open: 0,
        high: 0,
        low: 0,
        close: 0,
        volume: 0,
      ));
      signalR.onDailyBar!(BarStorageItem(
        date: DateTime(now.year, now.month, now.day, 10, 30),
        symbol: 'WINFUT',
        timeFrame: 2,
        open: 1,
        high: 1,
        low: 1,
        close: 1,
        volume: 1,
      ));
      expect(service.dailyBars.length, baseCount + 1);
      expect(service.dailyBars.last.close, 100700);
      service.reset();
    });

    test('onDailyBar respeita o lazy com painel fechado', () async {
      SharedPreferences.setMockInitialValues({});
      final prefs = PreferencesService();
      await prefs.init();

      final api = _dailyApi();
      final signalR = _NoopSignalRService(
          hubUrl: 'http://test.local/hub', apiService: api);
      final service = StateService(
        apiService: api,
        signalRService: signalR,
        preferencesService: prefs,
        audioService: AudioService(),
      );
      await service.setSymbol('WINFUT');
      expect(service.dailyBars, isEmpty);

      final now = DateTime.now();
      signalR.onDailyBar!(BarStorageItem(
        date: DateTime(now.year, now.month, now.day, 10, 30),
        symbol: 'WINFUT',
        timeFrame: 1440,
        open: 100000,
        high: 101000,
        low: 99000,
        close: 100500,
        volume: 5000,
      ));
      expect(service.dailyBars, isEmpty);
      service.reset();
    });

    test('separadores do diário caem em viradas de mês', () async {      SharedPreferences.setMockInitialValues({});
      final prefs = PreferencesService();
      await prefs.init();

      Map<String, dynamic> daily(String date, double base) => {
            'date': date,
            'symbol': 'WINFUT',
            'timeFrame': 1440,
            'open': base,
            'high': base + 100,
            'low': base - 100,
            'close': base + 10,
            'volume': 1000,
          };
      final api = ApiService(
        baseUrl: 'http://test.local',
        client: MockClient((request) async {
          final path = request.url.path;
          if (path.contains('GetBarRange')) {
            return http.Response(
                jsonEncode([
                  daily('2026-08-31T00:00:00.000', 100000),
                  daily('2026-09-01T00:00:00.000', 101000),
                  daily('2026-09-02T00:00:00.000', 102000),
                ]),
                200);
          }
          if (path.contains('GetStructureHistory')) {
            return http.Response('[]', 200);
          }
          if (path.contains('GetDailyProfile')) {
            return http.Response('[]', 200);
          }
          if (path.contains('GetExtremeDaily')) {
            return http.Response('null', 200);
          }
          if (path.contains('GetVolume')) return http.Response('null', 200);
          if (path.contains('SetExtremeConfig')) {
            return http.Response('null', 200);
          }
          return http.Response('[]', 200);
        }),
      );
      final service = StateService(
        apiService: api,
        signalRService: _NoopSignalRService(
            hubUrl: 'http://test.local/hub', apiService: api),
        preferencesService: prefs,
        audioService: AudioService(),
      );
      await service.setSymbol('WINFUT');
      await service.loadDailyAll();

      final data = buildDailyChartData(service);
      expect(data.dates.map((d) => d.month).toList(), [8, 9, 9]);
      // Só a virada ago→set é marcada.
      expect(data.daySeparatorIndices, [1]);
      service.reset();
    });
  });

  group('Range estrutura: diário vs intraday isolados', () {
    test('ranges intraday e diário persistem independentes', () async {
      final cfg = SymbolConfig.withDefaults('WINFUT')
        ..structureRangeUpd = 300
        ..daily.structureRangeUpd = 1500;
      final restored = SymbolConfig.fromJson(cfg.toJson(), symbol: 'WINFUT');
      expect(restored.structureRangeUpd, 300);
      expect(restored.daily.structureRangeUpd, 1500);

      // Mudar um não toca no outro.
      restored.daily.structureRangeUpd = 2000;
      expect(restored.structureRangeUpd, 300);
      restored.structureRangeUpd = 400;
      expect(restored.daily.structureRangeUpd, 2000);
    });

    test('confirm intraday usa SetStructureDistance; diário usa TF 1440',
        () async {
      SharedPreferences.setMockInitialValues({});
      final prefs = PreferencesService();
      await prefs.init();

      final requested = <String>[];
      Map<String, dynamic> structureJson(int tf) => {
            'date': '2026-08-12T10:00:00.000',
            'symbol': 'WINFUT',
            'timeFrame': tf,
            'upBorder': 100000.0,
            'downBorder': 99000.0,
            'upAuxBorder': 100050.0,
            'downAuxBorder': 98950.0,
          };
      final api = ApiService(
        baseUrl: 'http://test.local',
        client: MockClient((request) async {
          final url = request.url.toString();
          requested.add(url);
          if (url.contains('SetStructureDistanceForTimeFrame')) {
            return http.Response(jsonEncode([structureJson(1440)]), 200);
          }
          if (url.contains('SetStructureDistance')) {
            // Backend antigo podia incluir o 1440 na resposta: o app deve
            // descartar para o intraday.
            return http.Response(
                jsonEncode([structureJson(2), structureJson(1440)]), 200);
          }
          if (url.contains('GetStructureHistory')) {
            return http.Response('[]', 200);
          }
          if (url.contains('GetDailyProfile')) {
            return http.Response('[]', 200);
          }
          if (url.contains('GetExtremeDaily')) {
            return http.Response('null', 200);
          }
          if (url.contains('GetVolume')) return http.Response('null', 200);
          if (url.contains('SetExtremeConfig')) {
            return http.Response('null', 200);
          }
          return http.Response('[]', 200);
        }),
      );
      final service = StateService(
        apiService: api,
        signalRService: _NoopSignalRService(
            hubUrl: 'http://test.local/hub', apiService: api),
        preferencesService: prefs,
        audioService: AudioService(),
      );
      await service.setSymbol('WINFUT');
      service.setStructureRangeUpd(300);
      service.setDailyStructureRangeUpd(1500);

      await service.confirmStructureRangeUpd();
      expect(
          requested.any((u) =>
              u.contains('SetStructureDistance/WINFUT/300.0')),
          isTrue);
      // Intraday nunca armazena 1440.
      expect(service.structures.any((s) => s.timeFrame == 1440), isFalse);
      expect(
          service.structuresTimeFrameFilter
              .every((s) => s.timeFrame == service.timeFrame),
          isTrue);

      await service.confirmStructureRangeUpdDaily();
      expect(
          requested.any((u) => u.contains(
              'SetStructureDistanceForTimeFrame/WINFUT/1440/1500.0')),
          isTrue);
      // Confirm diário não toca na lista intraday.
      expect(service.structures.any((s) => s.timeFrame == 1440), isFalse);
      service.reset();
    });
  });
}
