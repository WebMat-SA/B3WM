import 'dart:convert';
import 'package:http/http.dart' as http;
import '../app_config.dart';
import '../app_log.dart';

class StrategyInfo {
  final String name;
  final String description;

  const StrategyInfo({
    required this.name,
    required this.description,
  });

  factory StrategyInfo.fromJson(Map<String, dynamic> json) {
    return StrategyInfo(
      name: json['name'] as String? ?? '',
      description: json['description'] as String? ?? '',
    );
  }
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

  /// Avaliação avulsa (botão "avaliar agora"): sem sessão, só log.
  Future<Map<String, dynamic>?> evaluate({
    required String strategy,
    required String symbol,
    required Map<String, dynamic> screenConfig,
  }) async {
    try {
      final response = await _client.post(
        Uri.parse('$_baseUrl/api/Strategy/Evaluate'),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode({
          'strategy': strategy,
          'symbol': symbol,
          'screenConfig': screenConfig,
        }),
      );
      if (response.statusCode != 200) {
        logD(
            '[strategy] Evaluate HTTP ${response.statusCode}: ${response.body}');
        return null;
      }
      return jsonDecode(response.body) as Map<String, dynamic>;
    } catch (e) {
      logD('[strategy] Evaluate error: $e');
      return null;
    }
  }

  void dispose() {
    _client.close();
  }
}
