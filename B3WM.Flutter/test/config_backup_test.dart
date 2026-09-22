import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'package:b3wm_flutter/models/config_backup.dart';
import 'package:b3wm_flutter/models/symbol_config.dart';
import 'package:b3wm_flutter/services/api_service.dart';
import 'package:b3wm_flutter/services/audio_service.dart';
import 'package:b3wm_flutter/services/config_file_service.dart';
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
        // Uma estrutura válida evita os 4 retries x 2s do _fetchStructures
        // e acelera os setSymbol/loadData dos testes.
        if (request.url.path.contains('GetStructure')) {
          return http.Response(
              jsonEncode([
                {
                  'date': '2026-09-21T09:00:00.000',
                  'symbol': 'WINFUT',
                  'timeFrame': 2,
                  'upBorder': 100.0,
                  'downBorder': 90.0,
                  'upAuxBorder': 101.0,
                  'downAuxBorder': 89.0,
                }
              ]),
              200);
        }
        return http.Response('[]', 200);
      }),
    );

StateService _createService(PreferencesService prefs) {
  final api = _stubApi();
  return StateService(
    apiService: api,
    signalRService:
        _NoopSignalRService(hubUrl: 'http://test.local/hub', apiService: api),
    preferencesService: prefs,
    audioService: AudioService(),
  );
}

Future<PreferencesService> _freshPrefs() async {
  SharedPreferences.setMockInitialValues({});
  final prefs = PreferencesService();
  await prefs.init();
  return prefs;
}

void main() {
  group('ConfigBackup', () {
    test('round-trip preserva configs por símbolo', () {
      final winfut = SymbolConfig.withDefaults('WINFUT')
        ..bubbleVisible = false
        ..thresholdBubble = 750
        ..selectedAgents = [1, 2]
        ..agentThresholds = {1: 500}
        ..knownAgents = [1, 2, 3]
        ..daily.panelFraction = 0.6;
      final wdofut = SymbolConfig.withDefaults('WDOFUT');

      final backup = ConfigBackup(
        activeSymbol: 'WINFUT',
        symbols: {'WINFUT': winfut, 'WDOFUT': wdofut},
      );
      final restored =
          ConfigBackup.fromJson(jsonDecode(backup.toPrettyJson()));
      expect(restored.activeSymbol, 'WINFUT');
      expect(restored.symbols.keys.toSet(), {'WINFUT', 'WDOFUT'});
      final r = restored.symbols['WINFUT']!;
      expect(r.bubbleVisible, isFalse);
      expect(r.thresholdBubble, 750);
      expect(r.selectedAgents, [1, 2]);
      expect(r.agentThresholds, {1: 500});
      expect(r.knownAgents, [1, 2, 3]);
      expect(r.daily.panelFraction, closeTo(0.6, 1e-9));
    });

    test('rejeita formato/versão/symbols inválidos', () {
      Map<String, dynamic> base() => ConfigBackup(
            activeSymbol: 'WINFUT',
            symbols: {'WINFUT': SymbolConfig.withDefaults('WINFUT')},
          ).toJson();

      expect(() => ConfigBackup.fromJson({}),
          throwsA(isA<FormatException>()));
      expect(() => ConfigBackup.fromJson({...base(), 'format': 'x'}),
          throwsA(isA<FormatException>()));
      expect(() => ConfigBackup.fromJson({...base(), 'version': 999}),
          throwsA(isA<FormatException>()));
      expect(() => ConfigBackup.fromJson({...base(), 'symbols': <String, dynamic>{}}),
          throwsA(isA<FormatException>()));
      expect(
          () => ConfigBackup.fromJson({
                ...base(),
                'symbols': {
                  'WINFUT': {'timeFrame': 'tipo-errado'}
                }
              }),
          throwsA(isA<FormatException>()));
      expect(
          () => ConfigBackup.fromJson({
                ...base(),
                'symbols': {'WINFUT': 'não-mapa'}
              }),
          throwsA(isA<FormatException>()));
    });

    test('suggestedFileName segue o padrão', () {
      final name = ConfigBackup.suggestedFileName(DateTime(2026, 9, 21, 8, 5, 9));
      expect(name, 'b3wm_config_20260921_080509.json');
    });

    test('ConfigFileService encode/decode round-trip', () {
      final backup = ConfigBackup(
        activeSymbol: 'WDOFUT',
        symbols: {'WDOFUT': SymbolConfig.withDefaults('WDOFUT')},
      );
      final text = ConfigFileService.encode(backup);
      final decoded = ConfigFileService.decode(text);
      expect(ConfigBackup.fromJson(decoded).activeSymbol, 'WDOFUT');
      expect(() => ConfigFileService.decode('não-json{{{'),
          throwsA(isA<FormatException>()));
    });
  });

  group('StateService backup', () {
    test('export contém todos os símbolos + ativo', () async {
      final prefs = await _freshPrefs();
      final service = _createService(prefs);
      await service.setSymbol('WINFUT');
      service.setBubbleVisible(false);
      service.setThresholdBubble(750);
      await service.setSymbol('WDOFUT');

      final backup = await service.exportAllConfigs();
      expect(backup.symbols.keys.toSet(), {'WINFUT', 'WDOFUT'});
      expect(backup.activeSymbol, 'WDOFUT');
      expect(backup.symbols['WINFUT']!.bubbleVisible, isFalse);
      expect(backup.symbols['WINFUT']!.thresholdBubble, 750);
      // Pré-seleção sobrevive ao pretty JSON (caminho real do arquivo).
      final viaFile = ConfigBackup.fromJson(
          jsonDecode(backup.toPrettyJson()) as Map<String, dynamic>);
      expect(viaFile.symbols['WINFUT']!.bubbleVisible, isFalse);
      service.reset();
    });

    test('import substitui tudo e pré-seleciona no símbolo ativo', () async {
      final prefs = await _freshPrefs();
      final service = _createService(prefs);
      await service.setSymbol('WINFUT');
      service.setBubbleVisible(false);
      service.setThresholdBubble(750);
      service.toggleAgent(42);
      await service.setSymbol('WDOFUT');
      final backup = await service.exportAllConfigs();
      final text = backup.toPrettyJson();
      service.reset();

      // Aparelho "zerado" com símbolo extra local.
      final prefs2 = await _freshPrefs();
      final fresh = _createService(prefs2);
      await fresh.setSymbol('WINFUT');
      expect(fresh.bubbleVisible, isTrue);
      await fresh.setSymbol('EXTRA');
      fresh.setBubbleVisible(false);

      await fresh.importAllConfigs(
          jsonDecode(text) as Map<String, dynamic>);
      // Semântica substituir-tudo: EXTRA removido.
      expect(fresh.knownConfigSymbols().toSet(), {'WINFUT', 'WDOFUT'});
      expect(fresh.symbol, 'WDOFUT'); // activeSymbol restaurado
      await fresh.setSymbol('WINFUT');
      expect(fresh.bubbleVisible, isFalse);
      expect(fresh.thresholdBubble, 750);
      expect(fresh.selectedAgents, contains(42));
      fresh.reset();
    });

    test('import inválido não altera nada', () async {
      final prefs = await _freshPrefs();
      final service = _createService(prefs);
      await service.setSymbol('WINFUT');
      service.setBubbleVisible(false);

      expect(() => service.importAllConfigs({}),
          throwsA(isA<FormatException>()));
      // Estado intacto após a rejeição.
      expect(service.bubbleVisible, isFalse);
      expect(service.knownConfigSymbols(), contains('WINFUT'));
      service.reset();
    });
  });
}
