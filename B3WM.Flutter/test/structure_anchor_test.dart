import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'package:b3wm_flutter/models/bar_storage_item.dart';
import 'package:b3wm_flutter/models/structure_change_item.dart';
import 'package:b3wm_flutter/models/structure_storage_item.dart';
import 'package:b3wm_flutter/models/volume_level.dart';
import 'package:b3wm_flutter/services/api_service.dart';
import 'package:b3wm_flutter/services/audio_service.dart';
import 'package:b3wm_flutter/services/preferences_service.dart';
import 'package:b3wm_flutter/services/signalr_service.dart';
import 'package:b3wm_flutter/services/state_service.dart';

BarStorageItem bar(DateTime date,
        {double high = 0, double low = 0, double close = 0}) =>
    BarStorageItem(
      date: date,
      symbol: 'WINFUT',
      timeFrame: 2,
      open: close,
      high: high,
      low: low,
      close: close,
      volume: 10,
    );

BarStorageItem day(DateTime date, {double high = 0, double low = 0}) =>
    BarStorageItem(
      date: date,
      symbol: 'WINFUT',
      timeFrame: 1440,
      open: low,
      high: high,
      low: low,
      close: low,
      volume: 10,
    );

StructureStorageItem printAt(DateTime date,
        {double upAux = 0, double downAux = 0}) =>
    StructureStorageItem(
      date: date,
      symbol: 'WINFUT',
      timeFrame: 2,
      upBorder: 0,
      downBorder: 0,
      upAuxBorder: upAux,
      downAuxBorder: downAux,
    );

StructureStorageItem dayPrintAt(DateTime date,
        {double upAux = 0, double downAux = 0}) =>
    StructureStorageItem(
      date: date,
      symbol: 'WINFUT',
      timeFrame: 1440,
      upBorder: 0,
      downBorder: 0,
      upAuxBorder: upAux,
      downAuxBorder: downAux,
    );

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

BarStorageItem _vbar(int i, List<VolumeLevel>? levels,
        {double high = 100100, double low = 99900}) =>
    BarStorageItem(
      date: DateTime(2026, 1, 1, 9, i * 2),
      symbol: 'WINFUT',
      timeFrame: 2,
      open: 100000,
      high: high,
      low: low,
      close: 100050,
      volume: 10,
      volumeLevel: levels,
    );

void main() {
  group('resolveExtremeBarIndex (primeiro toque E, incluso)', () {
    test('topo: volta da confirmação ao candle que marcou o aux', () {
      final bars = [
        bar(DateTime(2026, 1, 1, 9, 0), high: 100, low: 90, close: 95),
        bar(DateTime(2026, 1, 1, 9, 2), high: 110, low: 95, close: 100),
        bar(DateTime(2026, 1, 1, 9, 4), high: 105, low: 92, close: 93),
      ];
      final anchor = StructureChangeItem(
        date: bars[2].date, // confirmação por distanciamento
        isUp: true,
        oldValue: 100,
        newValue: 110,
      );
      expect(
        StateService.resolveExtremeBarIndex(
          bars: bars,
          structures: const [],
          symbol: 'WINFUT',
          timeFrame: 2,
          anchor: anchor,
          confirmationIndex: 2,
          tolerance: 2.5,
        ),
        1,
      );
    });

    test('reteste: ancora no PRIMEIRO toque (E), não no reteste', () {
      final bars = [
        bar(DateTime(2026, 1, 1, 9, 0), high: 110, low: 90, close: 100),
        bar(DateTime(2026, 1, 1, 9, 2), high: 108, low: 95, close: 100),
        bar(DateTime(2026, 1, 1, 9, 4), high: 110, low: 96, close: 101),
        bar(DateTime(2026, 1, 1, 9, 6), high: 104, low: 92, close: 93),
      ];
      final anchor = StructureChangeItem(
        date: bars[3].date,
        isUp: true,
        oldValue: 100,
        newValue: 110,
      );
      expect(
        StateService.resolveExtremeBarIndex(
          bars: bars,
          structures: const [],
          symbol: 'WINFUT',
          timeFrame: 2,
          anchor: anchor,
          confirmationIndex: 3,
          tolerance: 2.5,
        ),
        0,
      );
    });

    test('fundo com reteste: primeiro toque incluso', () {
      final bars = [
        bar(DateTime(2026, 1, 1, 9, 0), high: 100, low: 90, close: 95),
        bar(DateTime(2026, 1, 1, 9, 2), high: 98, low: 80, close: 90),
        bar(DateTime(2026, 1, 1, 9, 4), high: 99, low: 80, close: 92),
        bar(DateTime(2026, 1, 1, 9, 6), high: 100, low: 85, close: 97),
      ];
      final anchor = StructureChangeItem(
        date: bars[3].date,
        isUp: false,
        oldValue: 90,
        newValue: 80,
      );
      expect(
        StateService.resolveExtremeBarIndex(
          bars: bars,
          structures: const [],
          symbol: 'WINFUT',
          timeFrame: 2,
          anchor: anchor,
          confirmationIndex: 3,
          tolerance: 2.5,
        ),
        1,
      );
    });

    test('série aux manda: primeiro print que alcançou o valor', () {
      final bars = [
        bar(DateTime(2026, 1, 1, 9, 0), high: 100, low: 90, close: 95),
        bar(DateTime(2026, 1, 1, 9, 2), high: 110, low: 95, close: 100),
        bar(DateTime(2026, 1, 1, 9, 4), high: 110, low: 96, close: 101),
        bar(DateTime(2026, 1, 1, 9, 6), high: 104, low: 92, close: 93),
      ];
      final prints = [
        printAt(DateTime(2026, 1, 1, 9, 0), upAux: 100),
        printAt(DateTime(2026, 1, 1, 9, 2), upAux: 110), // marcou aqui
        printAt(DateTime(2026, 1, 1, 9, 4), upAux: 110),
        printAt(DateTime(2026, 1, 1, 9, 6), upAux: 110),
      ];
      final anchor = StructureChangeItem(
        date: bars[3].date,
        isUp: true,
        oldValue: 100,
        newValue: 110,
      );
      expect(
        StateService.resolveExtremeBarIndex(
          bars: bars,
          structures: prints,
          symbol: 'WINFUT',
          timeFrame: 2,
          anchor: anchor,
          confirmationIndex: 3,
          tolerance: 2.5,
        ),
        1,
      );
    });

    test('preço não encontrado: fallback na confirmação', () {
      final bars = [
        bar(DateTime(2026, 1, 1, 9, 0), high: 100, low: 90, close: 95),
        bar(DateTime(2026, 1, 1, 9, 2), high: 101, low: 91, close: 96),
      ];
      final anchor = StructureChangeItem(
        date: bars[1].date,
        isUp: true,
        oldValue: 100,
        newValue: 999, // nunca tocado
      );
      expect(
        StateService.resolveExtremeBarIndex(
          bars: bars,
          structures: const [],
          symbol: 'WINFUT',
          timeFrame: 2,
          anchor: anchor,
          confirmationIndex: 1,
          tolerance: 2.5,
        ),
        1,
      );
    });

    test('lowerBound: não atravessa a perna anterior', () {
      final bars = [
        bar(DateTime(2026, 1, 1, 9, 0), high: 110, low: 90, close: 100),
        bar(DateTime(2026, 1, 1, 9, 2), high: 100, low: 90, close: 95),
        bar(DateTime(2026, 1, 1, 9, 4), high: 100, low: 90, close: 95),
        bar(DateTime(2026, 1, 1, 9, 6), high: 100, low: 90, close: 95),
        bar(DateTime(2026, 1, 1, 9, 8), high: 100, low: 90, close: 95),
        bar(DateTime(2026, 1, 1, 9, 10), high: 110, low: 95, close: 100),
        bar(DateTime(2026, 1, 1, 9, 12), high: 105, low: 92, close: 93),
      ];
      final anchor = StructureChangeItem(
        date: bars[6].date,
        isUp: true,
        oldValue: 100,
        newValue: 110,
      );
      expect(
        StateService.resolveExtremeBarIndex(
          bars: bars,
          structures: const [],
          symbol: 'WINFUT',
          timeFrame: 2,
          anchor: anchor,
          confirmationIndex: 6,
          lowerBound: 3,
          tolerance: 2.5,
        ),
        5, // ignora o 110 antigo do índice 0
      );
    });
  });

  group('oppositeResetDate (limite inferior da busca)', () {
    test('pula mudança same-side intermediária', () {
      // Ordem mais-recente-primeiro: L (topo), A (fundo/âncora), P (fundo
      // same-side intermediário), O (topo = reset do aux da âncora).
      DateTime d(int h, int m) => DateTime(2026, 1, 1, 9, h, m);
      final changes = [
        StructureChangeItem(
            date: d(9, 8), isUp: true, oldValue: 100, newValue: 112),
        StructureChangeItem(
            date: d(9, 6), isUp: false, oldValue: 90, newValue: 80),
        StructureChangeItem(
            date: d(9, 4), isUp: false, oldValue: 95, newValue: 85),
        StructureChangeItem(
            date: d(9, 0), isUp: true, oldValue: 100, newValue: 110),
      ];
      // A regra antiga (mudança imediatamente anterior) devolveria d(9,4);
      // a correta é o reset oposto d(9,0).
      expect(StateService.oppositeResetDate(changes, 1), d(9, 0));
    });

    test('sem oposto anterior: null (busca sem limite)', () {
      final changes = [
        StructureChangeItem(
            date: DateTime(2026, 1, 1, 9, 6),
            isUp: false,
            oldValue: 90,
            newValue: 80),
        StructureChangeItem(
            date: DateTime(2026, 1, 1, 9, 4),
            isUp: false,
            oldValue: 95,
            newValue: 85),
      ];
      expect(StateService.oppositeResetDate(changes, 0), isNull);
    });

    test('E anterior à mudança imediatamente anterior ainda é achado', () {
      // E=1 (fundo 80), P same-side confirma em 2, R=3 retesta, C=4 confirma
      // a âncora. Com o limite antigo (lo=2) o E ficava cortado e o start
      // caía em 3 (1 candle depois do fundo real, como no print).
      final bars = [
        bar(DateTime(2026, 1, 1, 9, 0), high: 100, low: 90, close: 95),
        bar(DateTime(2026, 1, 1, 9, 2), high: 98, low: 80, close: 90),
        bar(DateTime(2026, 1, 1, 9, 4), high: 99, low: 85, close: 92),
        bar(DateTime(2026, 1, 1, 9, 6), high: 97, low: 80, close: 91),
        bar(DateTime(2026, 1, 1, 9, 8), high: 100, low: 88, close: 97),
      ];
      final anchor = StructureChangeItem(
        date: bars[4].date,
        isUp: false,
        oldValue: 90,
        newValue: 80,
      );
      int resolve(int lo) {
        return StateService.resolveExtremeBarIndex(
          bars: bars,
          structures: const [],
          symbol: 'WINFUT',
          timeFrame: 2,
          anchor: anchor,
          confirmationIndex: 4,
          lowerBound: lo,
          tolerance: 2.5,
        );
      }
      expect(resolve(2), 3); // regra antiga: corta o E, cai no reteste
      expect(resolve(0), 1); // reset oposto: acha o fundo real
    });
  });

  group('confirmationBarIndex', () {

    test('última barra com date <= changeDate', () {
      final bars = [
        bar(DateTime(2026, 1, 1, 9, 0)),
        bar(DateTime(2026, 1, 1, 9, 2)),
        bar(DateTime(2026, 1, 1, 9, 4)),
      ];
      expect(
        StateService.confirmationBarIndex(bars, DateTime(2026, 1, 1, 9, 3)),
        1,
      );
    });
  });

  group('resolveDailyAnchorDay (primeiro toque)', () {
    test('volta ao dia do primeiro toque, mesmo com reteste', () {
      final bars = [
        day(DateTime(2026, 1, 1), high: 100, low: 90),
        day(DateTime(2026, 1, 2), high: 110, low: 95),
        day(DateTime(2026, 1, 3), high: 110, low: 96),
        day(DateTime(2026, 1, 4), high: 104, low: 93),
      ];
      final anchor = StructureChangeItem(
        date: DateTime(2026, 1, 4, 18, 0), // confirmação dias depois
        isUp: true,
        oldValue: 100,
        newValue: 110,
      );
      expect(
        StateService.resolveDailyAnchorDay(
          dailyBars: bars,
          history: const [],
          symbol: 'WINFUT',
          anchor: anchor,
        ),
        DateTime(2026, 1, 2),
      );
    });

    test('histórico aux: primeiro dia que alcançou o valor', () {
      final bars = [
        day(DateTime(2026, 1, 1), high: 100, low: 90),
        day(DateTime(2026, 1, 2), high: 110, low: 95),
        day(DateTime(2026, 1, 3), high: 110, low: 96),
        day(DateTime(2026, 1, 4), high: 104, low: 93),
      ];
      final hist = [
        dayPrintAt(DateTime(2026, 1, 1), upAux: 100),
        dayPrintAt(DateTime(2026, 1, 2), upAux: 110), // marcou aqui
        dayPrintAt(DateTime(2026, 1, 3), upAux: 110),
        dayPrintAt(DateTime(2026, 1, 4), upAux: 110),
      ];
      final anchor = StructureChangeItem(
        date: DateTime(2026, 1, 4, 18, 0),
        isUp: true,
        oldValue: 100,
        newValue: 110,
      );
      expect(
        StateService.resolveDailyAnchorDay(
          dailyBars: bars,
          history: hist,
          symbol: 'WINFUT',
          anchor: anchor,
        ),
        DateTime(2026, 1, 2),
      );
    });

    test('sem barras nem histórico: fallback no dia da confirmação', () {
      final anchor = StructureChangeItem(
        date: DateTime(2026, 1, 4, 18, 0),
        isUp: true,
        oldValue: 100,
        newValue: 110,
      );
      expect(
        StateService.resolveDailyAnchorDay(
          dailyBars: const [],
          history: const [],
          symbol: 'WINFUT',
          anchor: anchor,
        ),
        DateTime(2026, 1, 4),
      );
    });
  });

  group('primeiros candles da perna entram no perfil (regressão)', () {
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

    test('volume nascido entre E e R-1 aparece (regra R apagaria)', () async {
      // E=1 faz o fundo (low 99900), 2 carrega volume novo, R=3 retesta,
      // C=4 confirma. O nível 2000 nasce no candle 2 (entre E e R): com
      // start=R ele seria cancelado no Diff (cumulativo(R-1) já o contém);
      // com start=E ele fica.
      signalR.onCloseBar!(_vbar(0, [_lvl(1000, 10)], low: 100000));
      signalR.onCloseBar!(_vbar(1, [_lvl(1000, 10), _lvl(1001, 5)]));
      signalR.onCloseBar!(
          _vbar(2, [_lvl(1000, 10), _lvl(1001, 5), _lvl(2000, 7)],
              low: 99950));
      signalR.onCloseBar!(
          _vbar(3, [_lvl(1000, 10), _lvl(1001, 5), _lvl(2000, 7)]));
      signalR.onCloseBar!(
          _vbar(4, [_lvl(1000, 10), _lvl(1001, 5), _lvl(2000, 7), _lvl(2001, 3)],
              low: 99960));

      final bars = service.barsTimeFrameFilter;
      final anchor = StructureChangeItem(
        date: bars[4].date,
        isUp: false,
        oldValue: 100000,
        newValue: 99900, // low de E e R
      );
      final e = StateService.resolveExtremeBarIndex(
        bars: bars,
        structures: const [],
        symbol: 'WINFUT',
        timeFrame: 2,
        anchor: anchor,
        confirmationIndex: 4,
        tolerance: 2.5,
      );
      expect(e, 1); // primeiro toque

      service.applyVolumeFilter(e, bars.length);
      expect(service.dateRangeStart, e);
      final levels = service.filteredVolumeLevels!;
      // Janela [1..4]: tudo da perna entra, inclusive o nível do candle 2.
      expect(levels.firstWhere((v) => v.price == 2000).total, 7);
      expect(levels.firstWhere((v) => v.price == 2001).total, 3);
      expect(levels.firstWhere((v) => v.price == 1001).total, 5);
    });

    test('contraste: com start=R o nível de E..R-1 some (o defeito)', () async {
      signalR.onCloseBar!(_vbar(0, [_lvl(1000, 10)], low: 100000));
      signalR.onCloseBar!(_vbar(1, [_lvl(1000, 10), _lvl(1001, 5)]));
      signalR.onCloseBar!(
          _vbar(2, [_lvl(1000, 10), _lvl(1001, 5), _lvl(2000, 7)],
              low: 99950));
      signalR.onCloseBar!(
          _vbar(3, [_lvl(1000, 10), _lvl(1001, 5), _lvl(2000, 7)]));
      signalR.onCloseBar!(
          _vbar(4, [_lvl(1000, 10), _lvl(1001, 5), _lvl(2000, 7), _lvl(2001, 3)],
              low: 99960));

      final bars = service.barsTimeFrameFilter;
      // Simulando a regra antiga (último toque = 3): o nível 2000, nascido
      // no candle 2, já está no snapshot de referência (bars[2]) e zera.
      service.applyVolumeFilter(3, bars.length);
      final levels = service.filteredVolumeLevels!;
      expect(levels.firstWhere((v) => v.price == 2000).total, 0);
      // ...enquanto com start=E ele aparece:
      service.applyVolumeFilter(1, bars.length);
      final fixed = service.filteredVolumeLevels!;
      expect(fixed.firstWhere((v) => v.price == 2000).total, 7);
    });
  });
}
