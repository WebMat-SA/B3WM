import 'dart:convert';
import 'package:http/http.dart' as http;
import '../app_config.dart';
import '../app_log.dart';
import '../models/bubble_storage_item.dart';

class StrategyInfo {
  final String name;
  final String description;
  final String detailsMd;

  const StrategyInfo({
    required this.name,
    required this.description,
    this.detailsMd = '',
  });

  factory StrategyInfo.fromJson(Map<String, dynamic> json) {
    return StrategyInfo(
      name: json['name'] as String? ?? '',
      description: json['description'] as String? ?? '',
      detailsMd: json['detailsMd'] as String? ?? '',
    );
  }
}

/// Um item do relatório da sessão: cada avaliação (gatilho) e cada
/// execução paper geram um. Exibido sob a sessão aberta na aba.
class StrategyDecisionItem {
  final String kind; // avaliacao | execucao
  /// Data/hora completa canônica (p/ timeline X). Nulo só em log muito antigo.
  final DateTime? at;
  final String time;
  final String event;
  final String side;
  final double confidence;
  final double encerrar;
  final bool shouldTrade;
  /// Confiança por criteria (JEV — informativo). Vazio no resto.
  final Map<String, double> criteria;
  final String reason;
  final int stateChars;
  final String snapshotHash;
  final String positionBefore;
  final String action;

  const StrategyDecisionItem({
    required this.kind,
    this.at,
    required this.time,
    required this.event,
    required this.side,
    required this.confidence,
    required this.encerrar,
    required this.shouldTrade,
    this.criteria = const {},
    required this.reason,
    required this.stateChars,
    required this.snapshotHash,
    required this.positionBefore,
    required this.action,
  });

  factory StrategyDecisionItem.fromJson(Map<String, dynamic> json) =>
      StrategyDecisionItem(
        kind: json['kind'] as String? ?? 'avaliacao',
        at: _parseAt(json['at']) ?? _parseAt(json['At']),
        time: json['time'] as String? ?? '',
        event: json['event'] as String? ?? '',
        side: json['side'] as String? ?? '',
        confidence: (json['confidence'] as num?)?.toDouble() ?? 0,
        encerrar: (json['encerrar'] as num?)?.toDouble() ?? 0,
        shouldTrade: json['shouldTrade'] as bool? ?? false,
        criteria: ((json['criteria'] as Map?) ?? const {}).map(
            (k, v) => MapEntry(k.toString(), (v as num?)?.toDouble() ?? 0)),
        reason: json['reason'] as String? ?? '',
        stateChars: (json['stateChars'] as num?)?.toInt() ?? 0,
        snapshotHash: json['snapshotHash'] as String? ?? '',
        positionBefore: json['positionBefore'] as String? ?? 'flat',
        action: json['action'] as String? ?? '',
      );
}

class StrategySession {
  final String sessionId;
  final String strategy;
  final String symbol;
  final bool paused;
  final String snapshotHash;
  final String? position;
  final int decisions;
  final double realizedPts;
  final String? lastDecision;
  final List<StrategyDecisionItem> reportItems;

  const StrategySession({
    required this.sessionId,
    required this.strategy,
    required this.symbol,
    required this.paused,
    required this.snapshotHash,
    required this.position,
    required this.decisions,
    required this.realizedPts,
    required this.lastDecision,
    required this.reportItems,
  });

  factory StrategySession.fromJson(Map<String, dynamic> json) =>
      StrategySession(
        sessionId: json['sessionId'] as String? ?? '',
        strategy: json['strategy'] as String? ?? '',
        symbol: json['symbol'] as String? ?? '',
        paused: json['paused'] as bool? ?? false,
        snapshotHash: json['snapshotHash'] as String? ?? '',
        position: json['position'] as String?,
        decisions: (json['decisions'] as num?)?.toInt() ?? 0,
        realizedPts: (json['realizedPts'] as num?)?.toDouble() ?? 0,
        lastDecision: json['lastDecision'] as String?,
        reportItems: (((json['reportItems'] as List?) ??
                    json['recentDecisions'] as List?) ??
                [])
            .map((e) => StrategyDecisionItem.fromJson(e as Map<String, dynamic>))
            .toList(),
      );
}

/// Parede BRT (UTC-3, sem horário de verão desde 2019): base comum entre
/// `At` (absoluto, com ou sem offset) e as datas das barras (naive, já em
/// parede BRT). Devolve naive-local com os componentes da parede BRT —
/// imune ao fuso da máquina (gráfico e eixo X usam esses componentes).
DateTime b3Wall(DateTime at) {
  final u = at.toUtc().subtract(const Duration(hours: 3));
  return DateTime(
      u.year, u.month, u.day, u.hour, u.minute, u.second, u.millisecond);
}

/// Parse tolerante de data/hora completa (novo campo `at`/`At`).
/// Com offset/Z: absoluto. Sem offset (backend serializa Unspecified sem
/// offset): parede BRT. Retorna null se ausente/inválido (log muito antigo).
DateTime? _parseAt(dynamic v) {
  if (v == null) return null;
  if (v is DateTime) return v;
  if (v is String && v.isNotEmpty) {
    final hasZone =
        RegExp(r'([Zz]|[+-]\d{2}:?\d{2})$').hasMatch(v);
    final parsed = DateTime.tryParse(v);
    if (parsed == null) return null;
    if (hasZone) return parsed;
    return DateTime.utc(parsed.year, parsed.month, parsed.day, parsed.hour,
            parsed.minute, parsed.second, parsed.millisecond)
        .add(const Duration(hours: 3));
  }
  return null;
}

/// Item cru do timeline (com data completa; sem At não aparece na faixa).
class StrategyTimelineItem {
  final DateTime at;
  final String kind; // avaliacao | execucao
  final String event; // bubble | candle_close | exec | ...
  final String side;
  final double confidence;
  final double encerrar;
  final String action;
  final Map<String, double> criteria;
  final String reason;
  final String snapshotHash;

  const StrategyTimelineItem({
    required this.at,
    required this.kind,
    required this.event,
    required this.side,
    required this.confidence,
    required this.encerrar,
    required this.action,
    this.criteria = const {},
    required this.reason,
    required this.snapshotHash,
  });

  factory StrategyTimelineItem.fromDecision(StrategyDecisionItem d,
          {DateTime? at}) =>
      StrategyTimelineItem(
        at: at ?? d.at!,
        kind: d.kind,
        event: d.event,
        side: d.side,
        confidence: d.confidence,
        encerrar: d.encerrar,
        action: d.action,
        criteria: d.criteria,
        reason: d.reason,
        snapshotHash: d.snapshotHash,
      );
}

/// Paper trade com data completa (timeline/histórico).
class StrategyPaperTrade {
  final String side;
  final DateTime? entryAt;
  final String entryTime;
  final double entry;
  final DateTime? exitAt;
  final String exitTime;
  final double exit;
  final double pts;
  final String exitReason;
  final double entryConf;

  const StrategyPaperTrade({
    required this.side,
    this.entryAt,
    required this.entryTime,
    required this.entry,
    this.exitAt,
    required this.exitTime,
    required this.exit,
    required this.pts,
    required this.exitReason,
    required this.entryConf,
  });

  factory StrategyPaperTrade.fromJson(Map<String, dynamic> json) =>
      StrategyPaperTrade(
        side: json['side'] as String? ?? '',
        entryAt: _parseAt(json['entryAt']) ?? _parseAt(json['EntryAt']),
        entryTime: json['entryTime'] as String? ?? '',
        entry: (json['entry'] as num?)?.toDouble() ?? 0,
        exitAt: _parseAt(json['exitAt']) ?? _parseAt(json['ExitAt']),
        exitTime: json['exitTime'] as String? ?? '',
        exit: (json['exit'] as num?)?.toDouble() ?? 0,
        pts: (json['pts'] as num?)?.toDouble() ?? 0,
        exitReason: json['exitReason'] as String? ?? '',
        entryConf: (json['entryConf'] as num?)?.toDouble() ?? 0,
      );
}

/// Log diário persistido (`GET api/Strategy/Log/{symbol}/{date}`).
class StrategyLogDay {
  final DateTime? date;
  final String symbol;
  final String strategy;
  final String snapshotHash;
  final List<StrategyDecisionItem> decisions;
  final List<StrategyPaperTrade> paperTrades;
  final double realizedPts;

  const StrategyLogDay({
    this.date,
    required this.symbol,
    required this.strategy,
    required this.snapshotHash,
    required this.decisions,
    required this.paperTrades,
    required this.realizedPts,
  });

  factory StrategyLogDay.fromJson(Map<String, dynamic> json) => StrategyLogDay(
        date: _parseAt(json['date']) ?? _parseAt(json['Date']),
        symbol: json['symbol'] as String? ?? '',
        strategy: json['strategy'] as String? ?? '',
        snapshotHash: json['snapshotHash'] as String? ?? '',
        decisions: ((json['decisions'] as List?) ?? (json['Decisions'] as List?) ?? [])
            .whereType<Map>()
            .map((e) => StrategyDecisionItem.fromJson(
                e.map((k, v) => MapEntry(k.toString(), v))))
            .toList(),
        paperTrades: ((json['paperTrades'] as List?) ?? (json['PaperTrades'] as List?) ?? [])
            .whereType<Map>()
            .map((e) => StrategyPaperTrade.fromJson(
                e.map((k, v) => MapEntry(k.toString(), v))))
            .toList(),
        realizedPts: (json['realizedPts'] as num?)?.toDouble() ??
            (json['RealizedPts'] as num?)?.toDouble() ??
            0,
      );
}

class StrategyService {
  final http.Client _client;
  final String _baseUrl;

  StrategyService({http.Client? client, String? baseUrl})
      : _client = client ?? http.Client(),
        _baseUrl = baseUrl ?? AppConfig.apiBaseUrl;

  Future<List<StrategyInfo>> list() async {
    try {
      final response =
          await _client.get(Uri.parse('$_baseUrl/api/Strategy/List'));
      if (response.statusCode != 200) return [];
      final list = jsonDecode(response.body) as List? ?? [];
      return list
          .map((e) => StrategyInfo.fromJson(e as Map<String, dynamic>))
          .toList();
    } catch (e) {
      logD('[strategy] List error: $e');
      return [];
    }
  }

  /// PLAY: envia strategy + símbolo + foto da tela. Config 100% no código
  /// da strategy. A partir daqui o backend executa sozinho (pode fechar o
  /// app); filtros travam.
  Future<Map<String, dynamic>?> arm({
    required String strategy,
    required String symbol,
    required Map<String, dynamic> screenConfig,
    DateTime? displayDate,
  }) async {
    try {
      final response = await _client.post(
        Uri.parse('$_baseUrl/api/Strategy/Arm'),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode({
          'strategy': strategy,
          'symbol': symbol,
          'screenConfig': screenConfig,
          if (displayDate != null)
            'displayDate': displayDate.toIso8601String(),
        }),
      );
      if (response.statusCode != 200) {
        logD(
            '[strategy] Arm HTTP ${response.statusCode}: ${response.body}');
        return null;
      }
      return jsonDecode(response.body) as Map<String, dynamic>;
    } catch (e) {
      logD('[strategy] Arm error: $e');
      return null;
    }
  }

  Future<bool> pause(String sessionId, bool paused) async {
    try {
      final response = await _client.post(Uri.parse(
          '$_baseUrl/api/Strategy/Pause?sessionId=$sessionId&paused=$paused'));
      return response.statusCode == 200;
    } catch (e) {
      logD('[strategy] Pause error: $e');
      return false;
    }
  }

  Future<Map<String, dynamic>?> stop(String sessionId) async {
    try {
      final response = await _client.post(Uri.parse(
          '$_baseUrl/api/Strategy/Stop?sessionId=$sessionId'));
      if (response.statusCode != 200) return null;
      return jsonDecode(response.body) as Map<String, dynamic>;
    } catch (e) {
      logD('[strategy] Stop error: $e');
      return null;
    }
  }

  Future<List<StrategySession>> state() async {
    try {
      final response =
          await _client.get(Uri.parse('$_baseUrl/api/Strategy/State'));
      if (response.statusCode != 200) return [];
      final list = jsonDecode(response.body) as List? ?? [];
      return list
          .map((e) => StrategySession.fromJson(e as Map<String, dynamic>))
          .toList();
    } catch (e) {
      logD('[strategy] State error: $e');
      return [];
    }
  }

  /// Trigger manual: reenvia a consulta da sessão como se o gatilho da
  /// strategy tivesse sido preenchido (a bubble clicada no gráfico é o evento).
  /// Retorna `fired`, `no_decision` ou null em falha.
  Future<String?> manualTrigger(
      String sessionId, BubbleStorageItem bubble) async {
    try {
      final response = await _client.post(
        Uri.parse('$_baseUrl/api/Strategy/ManualTrigger'),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode({
          'sessionId': sessionId,
          'bubble': bubble.toJson(),
        }),
      );
      if (response.statusCode != 200) {
        logD(
            '[strategy] ManualTrigger HTTP ${response.statusCode}: ${response.body}');
        return null;
      }
      final body = jsonDecode(response.body) as Map<String, dynamic>?;
      return body?['status'] as String?;
    } catch (e) {
      logD('[strategy] ManualTrigger error: $e');
      return null;
    }
  }

  Future<List<StrategyLogDay>> log(String symbol, DateTime date) async {
    try {
      final ds =
          '${date.year.toString().padLeft(4, '0')}-${date.month.toString().padLeft(2, '0')}-${date.day.toString().padLeft(2, '0')}';
      final response = await _client
          .get(Uri.parse('$_baseUrl/api/Strategy/Log/$symbol/$ds'));
      if (response.statusCode != 200) return [];
      final list = jsonDecode(response.body) as List? ?? [];
      return list
          .whereType<Map>()
          .map((e) => StrategyLogDay.fromJson(
              e.map((k, v) => MapEntry(k.toString(), v))))
          .toList();
    } catch (e) {
      logD('[strategy] Log error: $e');
      return [];
    }
  }

  void dispose() {
    _client.close();
  }
}
