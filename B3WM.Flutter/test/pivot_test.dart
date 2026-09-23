import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'package:b3wm_flutter/models/pivot_storage_item.dart';
import 'package:b3wm_flutter/models/symbol_config.dart';
import 'package:b3wm_flutter/services/api_service.dart';
import 'package:b3wm_flutter/services/audio_service.dart';
import 'package:b3wm_flutter/services/preferences_service.dart';
import 'package:b3wm_flutter/services/signalr_service.dart';
import 'package:b3wm_flutter/services/state_service.dart';
import 'package:b3wm_flutter/ui/widgets/chart/chart_data.dart';
import 'package:b3wm_flutter/ui/widgets/chart/daily_chart_data.dart';

class _NoopSignalRService extends SignalRService {
  _NoopSignalRService({required super.hubUrl, required super.apiService});

  @override
  Future<void> startConnection(String symbol, int? timeFrame) async {}

  @override
  Future<void> stopConnection() async {}
}

Map<String, dynamic> _pivotJson(String source) => {
      'symbol': 'WINFUT',
      'date': '2026-09-11T00:00:00.000',
      'source': source,
      'high': 190000.0,
      'low': 188000.0,
      'close': 189000.0,
      'lineCount': 2,
      'levels': [
        {'key': 'P', 'value': 189000.0},
        {'key': 'R1', 'value': 190000.0},
        {'key': 'S1', 'value': 188000.0},
        {'key': 'R2', 'value': 191000.0},
        {'key': 'S2', 'value': 187000.0},
      ],
    };

/// Mock com rotas de pivot + respostas vazias para o fluxo intraday/diário.
ApiService _pivotApi() => ApiService(
      baseUrl: 'http://test.local',
      client: MockClient((request) async {
        final path = request.url.path;
        if (path.contains('GetPivotIntraday')) {
          return http.Response(jsonEncode(_pivotJson('D-1')), 200);
        }
        if (path.contains('GetPivotDaily')) {
          return http.Response(jsonEncode(_pivotJson('W-1')), 200);
        }
        if (path.contains('GetBarRange')) {
          return http.Response(
              jsonEncode([
                {
                  'date': '2026-09-11T00:00:00.000',
                  'symbol': 'WINFUT',
                  'timeFrame': 1440,
                  'open': 189000.0,
                  'high': 190000.0,
                  'low': 188000.0,
                  'close': 189000.0,
                  'volume': 1000,
                },
              ]),
              200);
        }
        if (path.contains('GetStructureHistory')) {
          return http.Response(jsonEncode([]), 200);
        }
        if (path.contains('GetDailyProfile')) {
          return http.Response(jsonEncode([]), 200);
        }
        if (path.contains('GetExtremeDaily')) {
          return http.Response(
              jsonEncode({
                'symbol': 'WINFUT',
                'date': '2026-09-11T00:00:00.000',
                'extremes': [],
                'statistics': {
                  'topCount': 0,
                  'valleyCount': 0,
                  'pointCount': 0
                },
              }),
              200);
        }
        return http.Response('[]', 200);
      }),
    );

StateService _createPivotService(PreferencesService prefs) {
  final api = _pivotApi();
  return StateService(
    apiService: api,
    signalRService: _NoopSignalRService(
        hubUrl: 'http://test.local/hub', apiService: api),
    preferencesService: prefs,
    audioService: AudioService(),
  );
}

void main() {
  group('PivotStorageItem', () {
    test('fromJson parseia snapshot tradicional', () {
      final item = PivotStorageItem.fromJson(_pivotJson('D-1'));
      expect(item.symbol, 'WINFUT');
      expect(item.source, 'D-1');
      expect(item.lineCount, 2);
      expect(item.levels.length, 5);
      expect(item.levelValue('P'), 189000.0);
      expect(item.levelValue('R1'), 190000.0);
      expect(item.levelValue('S1'), 188000.0);
    });

    test('fromJson tolera payload vazio', () {
      final item = PivotStorageItem.fromJson({});
      expect(item.levels, isEmpty);
      expect(item.lineCount, 2);
    });
  });

  group('PivotLineData', () {
    test('fromLevels agrupa P/R/S e ordena', () {
      final data = PivotLineData.fromLevels(
        const [
          (key: 'S2', value: 187000.0),
          (key: 'R2', value: 191000.0),
          (key: 'P', value: 189000.0),
          (key: 'S1', value: 188000.0),
          (key: 'R1', value: 190000.0),
        ],
        visible: true,
        opacity: 0.7,
      );
      expect(data.pivot, 189000.0);
      expect(data.resistances, [190000.0, 191000.0]);
      expect(data.supports, [187000.0, 188000.0]);
      expect(data.visible, isTrue);
      expect(data.opacity, 0.7);
    });
  });

  group('Pivot config', () {
    test('defaults intraday + daily', () {
      final cfg = SymbolConfig.withDefaults('WINFUT');
      expect(cfg.pivotVisible, isTrue);
      expect(cfg.pivotOpacity, 0.7);
      expect(cfg.pivotLineCount, 2);
      expect(cfg.daily.pivotVisible, isTrue);
      expect(cfg.daily.pivotOpacity, 0.7);
      expect(cfg.daily.pivotLineCount, 2);
    });

    test('round-trip preserva pivot', () {
      final cfg = SymbolConfig.withDefaults('WINFUT')
        ..pivotVisible = false
        ..pivotOpacity = 0.35
        ..pivotLineCount = 4
        ..daily.pivotOpacity = 0.9
        ..daily.pivotLineCount = 5;
      final restored = SymbolConfig.fromJson(cfg.toJson(), symbol: 'WINFUT');
      expect(restored.pivotVisible, isFalse);
      expect(restored.pivotOpacity, 0.35);
      expect(restored.pivotLineCount, 4);
      expect(restored.daily.pivotOpacity, 0.9);
      expect(restored.daily.pivotLineCount, 5);
    });

    test('fromJson usa default quando ausente e limita 2–5', () {
      final missing = SymbolConfig.fromJson({'timeFrame': 2}, symbol: 'WINFUT');
      expect(missing.pivotVisible, isTrue);
      expect(missing.pivotLineCount, 2);
      expect(missing.daily.pivotLineCount, 2);
      final clamped = SymbolConfig.fromJson(
          {'timeFrame': 2, 'pivotLineCount': 99}, symbol: 'WINFUT');
      expect(clamped.pivotLineCount, 5);
    });
  });

  group('ApiService pivot', () {
    test('getPivotIntraday parseia D-1', () async {
      final data = await _pivotApi().getPivotIntraday('WINFUT', lineCount: 2);
      expect(data, isNotNull);
      expect(data!.source, 'D-1');
      expect(data.levels.length, 5);
    });

    test('getPivotDaily parseia W-1', () async {
      final data = await _pivotApi().getPivotDaily('WINFUT', lineCount: 2);
      expect(data, isNotNull);
      expect(data!.source, 'W-1');
      expect(data.levels.length, 5);
    });
  });

  group('StateService pivot (issue #14)', () {
    test('setters limitam, salvam e recarregam via loaders', () async {
      SharedPreferences.setMockInitialValues({});
      final prefs = PreferencesService();
      await prefs.init();

      final service = _createPivotService(prefs);
      await service.setSymbol('WINFUT');

      // setSymbol → loadData já carregou o pivot intraday (fonte D-1).
      expect(service.pivotIntraday, isNotNull);
      expect(service.pivotIntraday!.source, 'D-1');

      service.setPivotVisible(false);
      expect(service.pivotVisible, isFalse);
      service.setPivotOpacity(0.25);
      expect(service.pivotOpacity, 0.25);
      service.setPivotLineCount(99);
      expect(service.pivotLineCount, 5);
      service.setPivotLineCount(0);
      expect(service.pivotLineCount, 2);

      service.setDailyPivotVisible(false);
      expect(service.dailyPivotVisible, isFalse);
      service.setDailyPivotOpacity(0.1);
      expect(service.dailyPivotOpacity, 0.1);
      service.setDailyPivotLineCount(4);
      expect(service.dailyPivotLineCount, 4);

      await service.loadDailyPivot();
      expect(service.dailyPivot, isNotNull);
      expect(service.dailyPivot!.source, 'W-1');

      // Chart intraday e diário recebem os níveis com a opacidade vigente.
      service.setPivotVisible(true);
      service.setPivotOpacity(0.42);
      final intraday = buildChartData(service);
      expect(intraday.pivots, isNotNull);
      expect(intraday.pivots!.opacity, 0.42);
      expect(intraday.pivots!.pivot, 189000.0);
      expect(intraday.pivots!.resistances, [190000.0, 191000.0]);
      expect(intraday.pivots!.supports, [187000.0, 188000.0]);

      service.setDailyPivotVisible(true);
      final daily = buildDailyChartData(service);
      expect(daily.pivots, isNotNull);
      expect(daily.pivots!.pivot, 189000.0);

      // Limpar esconde as linhas (ChartData.pivots continua nulo-safe).
      service.clearPivotIntraday();
      expect(service.pivotIntraday, isNull);
      service.clearDailyPivot();
      expect(service.dailyPivot, isNull);

      // Persistência no mesmo Config_$symbol após recriar.
      service.reset();
      final reloaded = _createPivotService(prefs);
      await reloaded.setSymbol('WINFUT');
      expect(reloaded.pivotVisible, isTrue);
      expect(reloaded.pivotOpacity, 0.42);
      expect(reloaded.pivotLineCount, 2);
      expect(reloaded.dailyPivotLineCount, 4);
      reloaded.reset();
    });
  });
}
