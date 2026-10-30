import 'dart:convert';
import 'dart:io';
import 'dart:math' as math;

import 'package:b3wm_flutter/models/bar_storage_item.dart';
import 'package:b3wm_flutter/models/bubble_storage_item.dart';
import 'package:b3wm_flutter/models/extreme_storage_item.dart';
import 'package:b3wm_flutter/models/pivot_storage_item.dart';
import 'package:b3wm_flutter/models/structure_storage_item.dart';
import 'package:b3wm_flutter/models/volume_level.dart';
import 'package:b3wm_flutter/models/volume_level_storage_item.dart';
import 'package:b3wm_flutter/models/vwap_history_point.dart';
import 'package:b3wm_flutter/services/strategy_service.dart';

class ReadmeSessionData {
  final DateTime session;
  final DateTime dailyFrom;
  final DateTime dailyTo;
  final Map<String, Object> prefs;
  final List<BarStorageItem> bars;
  final List<BarStorageItem> dailyBars;
  final List<BubbleStorageItem> bubbles;
  final VolumeLevelStorageItem volume;
  final List<StructureStorageItem> structures;
  final List<StructureStorageItem> structuresDaily;
  final ExtremeStorageItem extreme;
  final ExtremeStorageItem extremeDaily;
  final List<VwapHistoryPoint> vwap;
  final StrategyLogDay strategyLog;
  final List<StrategyInfo> strategies;
  final List<VolumeLevel> dailyProfile;
  final PivotStorageItem? pivotIntraday;
  final PivotStorageItem? pivotDaily;

  const ReadmeSessionData({
    required this.session,
    required this.dailyFrom,
    required this.dailyTo,
    required this.prefs,
    required this.bars,
    required this.dailyBars,
    required this.bubbles,
    required this.volume,
    required this.structures,
    required this.structuresDaily,
    required this.extreme,
    required this.extremeDaily,
    required this.vwap,
    required this.strategyLog,
    required this.strategies,
    required this.dailyProfile,
    required this.pivotIntraday,
    required this.pivotDaily,
  });

  ReadmeSessionData withPrefs(Map<String, Object> value) => ReadmeSessionData(
        session: session,
        dailyFrom: dailyFrom,
        dailyTo: dailyTo,
        prefs: value,
        bars: bars,
        dailyBars: dailyBars,
        bubbles: bubbles,
        volume: volume,
        structures: structures,
        structuresDaily: structuresDaily,
        extreme: extreme,
        extremeDaily: extremeDaily,
        vwap: vwap,
        strategyLog: strategyLog,
        strategies: strategies,
        dailyProfile: dailyProfile,
        pivotIntraday: pivotIntraday,
        pivotDaily: pivotDaily,
      );
}

const _fixtureCandidates = [
  'test/fixtures/readme_session.json',
  'B3WM.Flutter/test/fixtures/readme_session.json',
];

const _defaultStrategies = [
  StrategyInfo(
    name: 'JevAnalysis',
    description:
        'Análise Jev/TypeSafe do contexto (bolhas, volume, candles, topos/vales, pivots)',
    detailsMd: '## JevAnalysis\n'
        'Análise Jev/TypeSafe do contexto de mercado (bolhas, volume, candles, topos/vales, pivots).\n\n'
        '- **Gatilho:** fechamento do candle do TF da sessão com pelo menos uma bolha (envia todas as bolhas do candle).\n'
        '- **TF:** o TF vigente no PLAY.\n'
        '- **Parâmetros fixos no código:** `LookbackCandles=60`, `LookbackBubbles=30`.\n'
        '- **Saída informativa:** viés `comprar|vender|lateral` e proximidade técnica.\n'
        '- **Foto da tela:** o PLAY congela os filtros da tela.\n'
        '- **Riscos:** depende da IA; log-only; para mudar filtros é preciso STOP/PLAY.',
  ),
];

Future<ReadmeSessionData> loadReadmeSessionData() async {
  final fixture = _loadFixture();
  final prefs = _tryStabilizePrefs(_loadPrefs());
  final dataDir = _findDataDir();
  if (dataDir != null) {
    try {
      return _loadFromDisk(
        dataDir,
        prefs ?? _stabilizePrefs(fixture.prefs),
      );
    } catch (e) {
      stderr.writeln(
          '[readme-data] dados reais indisponíveis ($e); usando fixture.');
    }
  }
  return prefs == null ? fixture : fixture.withPrefs(_stabilizePrefs(prefs));
}

ReadmeSessionData _loadFixture() {
  final file = _findFixture();
  final raw = _camelize(jsonDecode(file.readAsStringSync())) as Map;
  return _parseBundle(Map<String, dynamic>.from(raw));
}

File _findFixture() {
  for (final path in _fixtureCandidates) {
    final file = File(path);
    if (file.existsSync()) return file;
  }
  throw StateError(
      'Fixture não encontrada: ${_fixtureCandidates.join(' ou ')}');
}

String? _findDataDir() {
  final candidate = Platform.environment['B3WM_DATA_DIR'];
  final candidates = <String>[
    if (candidate != null && candidate.isNotEmpty) candidate,
    '../B3WM/Data',
    'B3WM/Data',
    'Data',
    '../../B3WM/Data',
  ];
  for (final path in candidates) {
    final probe =
        File('$path/WINFUT_CandleService_2MIN_2026-10-28.json');
    if (probe.existsSync()) return path;
  }
  return null;
}

Map<String, Object>? _loadPrefs() {
  for (final file in _prefsCandidates()) {
    if (!file.existsSync()) continue;
    final raw = jsonDecode(file.readAsStringSync());
    if (raw is Map) {
      return {
        for (final e in raw.entries)
          e.key.toString(): _prefValue(e.value),
      };
    }
  }
  return null;
}

Object _prefValue(dynamic value) {
  if (value is String || value is bool || value is num) return value;
  if (value is List) return value.map((e) => e.toString()).toList();
  return value.toString();
}

List<File> _prefsCandidates() {
  final files = <File>[];
  final explicit = Platform.environment['B3WM_PREFS_JSON'];
  if (explicit != null && explicit.isNotEmpty) files.add(File(explicit));

  final appData = Platform.environment['APPDATA'];
  if (appData != null && appData.isNotEmpty) {
    files.add(File(
        '$appData${Platform.pathSeparator}com.example${Platform.pathSeparator}b3wm_flutter${Platform.pathSeparator}shared_preferences.json'));
  }

  final home = Platform.environment['HOME'];
  if (home != null && home.isNotEmpty) {
    files.add(File(
        '$home/.local/share/com.example/b3wm_flutter/shared_preferences.json'));
    files.add(File(
        '$home/.config/com.example/b3wm_flutter/shared_preferences.json'));
  }

  final wslUsers = Directory('/mnt/c/Users');
  if (wslUsers.existsSync()) {
    for (final entry in wslUsers.listSync(followLinks: false)) {
      if (entry is Directory) {
        files.add(File(
            '${entry.path}/AppData/Roaming/com.example/b3wm_flutter/shared_preferences.json'));
      }
    }
  }
  return files;
}

ReadmeSessionData _loadFromDisk(String dataDir, Map<String, Object> prefs) {
  final session = DateTime(2026, 10, 28);
  // Perfil/extremos/estruturas 1D usam a janela curta (sem o salto de
  // rolagem de contrato); os candles 1D cobrem o histórico disponível.
  final dailyFrom = DateTime(2026, 10, 13);
  final dailyBarsFrom = DateTime(2026, 6, 9);
  final dailyTo = session;
  final config = _winfutConfig(prefs);
  final distance =
      (config['structureRangeUpd'] as num?)?.toDouble() ?? 455.0;

  final bars = _readList('$dataDir/WINFUT_CandleService_2MIN_2026-10-28.json')
      .map((e) => _bar(e))
      .toList();
  final bubbles =
      _readList('$dataDir/WINFUT_BubbleService_2026-10-28.json')
          .map((e) => BubbleStorageItem.fromJson(_map(e)))
          .toList();
  final volume = VolumeLevelStorageItem.fromJson(
      _readMap('$dataDir/WINFUT_VolumeService_2026-10-28.json'));

  final structurePath = _findStructureFile(dataDir, distance);
  final structures = structurePath == null
      ? <StructureStorageItem>[]
      : _readList(structurePath)
          .map((e) => StructureStorageItem.fromJson(_map(e)))
          .toList();

  final extreme = _readExtreme(
      '$dataDir/WINFUT_ExtremeService_2026-10-28.json');
  final extremeDaily = ExtremeStorageItem(
    symbol: extreme.symbol,
    date: session,
    periodFrom: dailyFrom,
    periodTo: dailyTo,
    extremes: extreme.extremes,
    noiseSensitivity: extreme.noiseSensitivity,
    minimumProminence: extreme.minimumProminence,
    topCount: extreme.topCount,
    valleyCount: extreme.valleyCount,
    profilePointCount: extreme.profilePointCount,
  );

  final vwap = _readList('$dataDir/WINFUT_VwapService_2026-10-28.json')
      .map((e) => VwapHistoryPoint.fromJson(_map(e)))
      .toList();

  final strategyLog = StrategyLogDay.fromJson(
      _readMap('$dataDir/WINFUT_Strategy_JevAnalysis_2026-10-28.json'));

  final dailyBars = _readDailyBars(dataDir, dailyBarsFrom, dailyTo);
  final dailyProfile = _aggregateDailyProfile(dataDir, dailyFrom, dailyTo);
  final structuresDaily = _readDailyStructures(dataDir, dailyFrom, dailyTo);
  final pivots = _computePivots(dailyBars, session, config);

  return ReadmeSessionData(
    session: session,
    dailyFrom: dailyFrom,
    dailyTo: dailyTo,
    prefs: prefs,
    bars: bars,
    dailyBars: dailyBars,
    bubbles: bubbles,
    volume: volume,
    structures: structures,
    structuresDaily: structuresDaily,
    extreme: extreme,
    extremeDaily: extremeDaily,
    vwap: vwap,
    strategyLog: strategyLog,
    strategies: _defaultStrategies,
    dailyProfile: dailyProfile,
    pivotIntraday: pivots.$1,
    pivotDaily: pivots.$2,
  );
}

ReadmeSessionData _parseBundle(Map<String, dynamic> raw) {
  final session = DateTime.parse(raw['session'] as String);
  final dailyFrom = DateTime.parse(raw['dailyFrom'] as String);
  final dailyTo = DateTime.parse(raw['dailyTo'] as String);
  final prefs = _stabilizePrefs({
    for (final e in (raw['prefs'] as Map).entries)
      e.key.toString(): _prefValue(e.value),
  });
  final dailyBars = (raw['dailyBars'] as List)
      .map((e) => _bar(e))
      .toList()
    ..sort((a, b) => a.date.compareTo(b.date));
  final config = _winfutConfig(prefs);
  final pivots = _computePivots(dailyBars, session, config);

  return ReadmeSessionData(
    session: session,
    dailyFrom: dailyFrom,
    dailyTo: dailyTo,
    prefs: prefs,
    bars: (raw['bars'] as List).map((e) => _bar(e)).toList(),
    dailyBars: dailyBars,
    bubbles: (raw['bubbles'] as List)
        .map((e) => BubbleStorageItem.fromJson(_map(e)))
        .toList(),
    volume: VolumeLevelStorageItem.fromJson(_map(raw['volume'])),
    structures: (raw['structures'] as List)
        .map((e) => StructureStorageItem.fromJson(_map(e)))
        .toList(),
    structuresDaily: (raw['structuresDaily'] as List)
        .map((e) => StructureStorageItem.fromJson(_map(e)))
        .toList(),
    extreme: ExtremeStorageItem.fromJson(_map(raw['extreme'])),
    extremeDaily: ExtremeStorageItem.fromJson(_map(raw['extremeDaily'])),
    vwap: (raw['vwap'] as List)
        .map((e) => VwapHistoryPoint.fromJson(_map(e)))
        .toList(),
    strategyLog: StrategyLogDay.fromJson(_map(raw['strategyLog'])),
    strategies: (raw['strategies'] as List)
        .map((e) => StrategyInfo.fromJson(_map(e)))
        .toList(),
    dailyProfile: (raw['dailyProfile'] as List)
        .map((e) => VolumeLevel.fromJson(_map(e)))
        .toList(),
    pivotIntraday: pivots.$1,
    pivotDaily: pivots.$2,
  );
}

// O viewport (zoom/pan) salvo no app muda a cada execução e depende da
// geometria da janela. Fixá-lo garante goldens reproduzíveis. Os valores
// abaixo reproduzem o enquadramento completo da sessão (intraday e 1D).
const _viewportOverride = <String, Object>{
  'xZoom': 0.6357421875,
  'panX': 96.4818816674099,
  'panY': -60.47258257536392,
  'yZoom': 1.0,
};

const _dailyViewportOverride = <String, Object>{
  'xZoom': 1.823709408615519,
  'panX': -387.3903611319947,
  'panY': -271.6292402597037,
  'yZoom': 6.488296399286461,
};

Map<String, Object>? _tryStabilizePrefs(Map<String, Object>? prefs) {
  if (prefs == null) return null;
  final result = Map<String, Object>.of(prefs);
  final raw = result['flutter.b3wm_Config_WINFUT'];
  if (raw is String && raw.isNotEmpty) {
    final decoded = jsonDecode(raw);
    if (decoded is Map) {
      final config = Map<String, dynamic>.from(decoded);
      config.addAll(_viewportOverride);
      final daily = config['daily'];
      if (daily is Map) {
        final dailyConfig = Map<String, dynamic>.from(daily)
          ..addAll(_dailyViewportOverride);
        config['daily'] = dailyConfig;
      }
      result['flutter.b3wm_Config_WINFUT'] = jsonEncode(config);
    }
  }
  return result;
}

Map<String, Object> _stabilizePrefs(Map<String, Object> prefs) =>
    _tryStabilizePrefs(prefs)!;

Map<String, dynamic> _winfutConfig(Map<String, Object> prefs) {
  final raw = prefs['flutter.b3wm_Config_WINFUT'];
  if (raw is String && raw.isNotEmpty) {
    final decoded = jsonDecode(raw);
    if (decoded is Map) return Map<String, dynamic>.from(decoded);
  }
  return <String, dynamic>{};
}

List<dynamic> _readList(String path) {
  final raw = jsonDecode(File(path).readAsStringSync());
  if (raw is List) return raw;
  throw StateError('Esperava lista em $path');
}

Map<String, dynamic> _readMap(String path) {
  final raw = jsonDecode(File(path).readAsStringSync());
  if (raw is Map) return _map(raw);
  throw StateError('Esperava objeto em $path');
}

Map<String, dynamic> _map(dynamic raw) =>
    Map<String, dynamic>.from(_camelize(raw) as Map);

BarStorageItem _bar(dynamic raw) =>
    BarStorageItem.fromJson(_map(raw));

ExtremeStorageItem _readExtreme(String path) =>
    ExtremeStorageItem.fromJson(_readMap(path));

String? _findStructureFile(String dataDir, double distance) {
  final direct = File(
      '$dataDir/WINFUT_StructureService_2MIN_${_distanceLabel(distance)}_2026-10-28.json');
  if (direct.existsSync()) return direct.path;

  final candidates = Directory(dataDir)
      .listSync()
      .whereType<File>()
      .where((f) =>
          f.path.contains('WINFUT_StructureService_2MIN_') &&
          f.path.endsWith('_2026-10-28.json'))
      .toList()
    ..sort((a, b) => a.path.compareTo(b.path));
  return candidates.isEmpty ? null : candidates.first.path;
}

String _distanceLabel(double value) =>
    value == value.roundToDouble() ? value.toInt().toString() : '$value';

List<BarStorageItem> _readDailyBars(
    String dataDir, DateTime from, DateTime to) {
  final bars = <BarStorageItem>[];
  final pattern = RegExp(r'WINFUT_CandleService_1440MIN_(\d{4}-\d{2}-\d{2})\.json$');
  for (final file in Directory(dataDir).listSync().whereType<File>()) {
    final match = pattern.firstMatch(file.path);
    if (match == null) continue;
    final day = DateTime.parse(match.group(1)!);
    if (day.isBefore(from) || day.isAfter(to)) continue;
    for (final raw in _readList(file.path)) {
      bars.add(_bar(raw));
    }
  }
  bars.sort((a, b) => a.date.compareTo(b.date));
  return bars;
}

List<StructureStorageItem> _readDailyStructures(
    String dataDir, DateTime from, DateTime to) {
  final files = Directory(dataDir)
      .listSync()
      .whereType<File>()
      .where((f) =>
          f.path.contains('WINFUT_StructureService_1440MIN_') &&
          f.path.endsWith('.json'))
      .toList()
    ..sort((a, b) => a.path.compareTo(b.path));
  for (final file in files.reversed) {
    try {
      final list = _readList(file.path)
          .map((e) => StructureStorageItem.fromJson(_map(e)))
          .where((s) =>
              !s.date.isBefore(from) &&
              !s.date.isAfter(to.add(const Duration(days: 1))))
          .toList();
      if (list.isNotEmpty) return list;
    } catch (_) {
      continue;
    }
  }
  return [];
}

List<VolumeLevel> _aggregateDailyProfile(
    String dataDir, DateTime from, DateTime to) {
  final totals = <double, List<int>>{};
  final pattern = RegExp(r'WINFUT_VolumeService_(\d{4}-\d{2}-\d{2})\.json$');
  for (final file in Directory(dataDir).listSync().whereType<File>()) {
    final match = pattern.firstMatch(file.path);
    if (match == null) continue;
    final day = DateTime.parse(match.group(1)!);
    if (day.isBefore(from) || day.isAfter(to)) continue;
    final item = VolumeLevelStorageItem.fromJson(_readMap(file.path));
    for (final level in item.volumes) {
      final values = totals.putIfAbsent(level.price, () => [0, 0, 0]);
      values[0] += level.total;
      values[1] += level.buyVolume;
      values[2] += level.sellVolume;
    }
  }
  final prices = totals.keys.toList()..sort();
  return [
    for (final price in prices)
      if (totals[price]![0] > 0)
        VolumeLevel(
          price: price,
          total: totals[price]![0],
          buyVolume: totals[price]![1],
          sellVolume: totals[price]![2],
        ),
  ];
}

(PivotStorageItem?, PivotStorageItem?) _computePivots(
    List<BarStorageItem> dailyBars, DateTime session,
    Map<String, dynamic> config) {
  final dailyConfig = config['daily'] is Map
      ? Map<String, dynamic>.from(config['daily'] as Map)
      : <String, dynamic>{};
  final intradayLineCount =
      (config['pivotLineCount'] as num?)?.toInt() ?? 3;
  final dailyLineCount =
      (dailyConfig['pivotLineCount'] as num?)?.toInt() ?? 2;

  BarStorageItem? previous;
  for (final bar in dailyBars) {
    if (!bar.date.isBefore(session)) continue;
    if (previous == null || bar.date.isAfter(previous.date)) previous = bar;
  }
  final pivotIntraday = previous == null
      ? null
      : _pivotFromHlc(previous, intradayLineCount, 'D-1');

  final monday = _startOfWeekMonday(session);
  final previousWeek = dailyBars
      .where((b) => b.date.isBefore(monday))
      .toList()
    ..sort((a, b) => a.date.compareTo(b.date));
  PivotStorageItem? pivotDaily;
  if (previousWeek.isNotEmpty) {
    final week = previousWeek.length > 5
        ? previousWeek.sublist(previousWeek.length - 5)
        : previousWeek;
    final high = week.map((b) => b.high).reduce(math.max);
    final low = week.map((b) => b.low).reduce(math.min);
    final close = week.last.close;
    pivotDaily = _pivotFromValues(
      high: high,
      low: low,
      close: close,
      date: week.last.date,
      lineCount: dailyLineCount,
      source: 'Semana anterior',
    );
  }
  return (pivotIntraday, pivotDaily);
}

PivotStorageItem _pivotFromHlc(
        BarStorageItem bar, int lineCount, String source) =>
    _pivotFromValues(
      high: bar.high,
      low: bar.low,
      close: bar.close,
      date: bar.date,
      lineCount: lineCount,
      source: source,
    );

PivotStorageItem _pivotFromValues({
  required double high,
  required double low,
  required double close,
  required DateTime date,
  required int lineCount,
  required String source,
}) {
  final n = lineCount.clamp(2, 5);
  final p = (high + low + close) / 3.0;
  final range = high - low;
  final r1 = 2 * p - low;
  final s1 = 2 * p - high;
  final r2 = p + range;
  final s2 = p - range;
  final r3 = high + 2 * (p - low);
  final s3 = low - 2 * (high - p);
  final r4 = r3 + range;
  final s4 = s3 - range;
  final r5 = r4 + range;
  final s5 = s4 - range;
  final levels = <PivotLevel>[
    PivotLevel(key: 'P', value: p),
    PivotLevel(key: 'R1', value: r1),
    PivotLevel(key: 'S1', value: s1),
    PivotLevel(key: 'R2', value: r2),
    PivotLevel(key: 'S2', value: s2),
    if (n >= 3) ...[
      PivotLevel(key: 'R3', value: r3),
      PivotLevel(key: 'S3', value: s3),
    ],
    if (n >= 4) ...[
      PivotLevel(key: 'R4', value: r4),
      PivotLevel(key: 'S4', value: s4),
    ],
    if (n >= 5) ...[
      PivotLevel(key: 'R5', value: r5),
      PivotLevel(key: 'S5', value: s5),
    ],
  ];
  return PivotStorageItem(
    symbol: 'WINFUT',
    date: date,
    source: source,
    high: high,
    low: low,
    close: close,
    lineCount: n,
    levels: levels,
  );
}

DateTime _startOfWeekMonday(DateTime date) {
  final day = DateTime(date.year, date.month, date.day);
  final offset = (day.weekday + 6) % 7;
  return day.subtract(Duration(days: offset));
}

dynamic _camelize(dynamic value) {
  if (value is List) return value.map(_camelize).toList();
  if (value is Map) {
    return {
      for (final entry in value.entries)
        _camelKey(entry.key.toString()): _camelize(entry.value),
    };
  }
  return value;
}

String _camelKey(String key) {
  if (key.isEmpty) return key;
  return key[0].toLowerCase() + key.substring(1);
}
