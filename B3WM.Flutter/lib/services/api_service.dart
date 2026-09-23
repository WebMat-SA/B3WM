import 'dart:convert';
import 'package:flutter/foundation.dart';
import 'package:http/http.dart' as http;
import '../models/bar_storage_item.dart';
import '../models/bubble_storage_item.dart';
import '../models/volume_level.dart';
import '../models/volume_level_storage_item.dart';
import '../models/structure_storage_item.dart';
import '../models/indicator_value.dart';
import '../models/verifier_config.dart';
import '../models/verifier_state.dart';
import '../models/verifier_log_day.dart';
import '../models/extreme_storage_item.dart';
import '../models/pivot_storage_item.dart';

class ApiService {
  final http.Client _client;
  final String _baseUrl;

  ApiService({http.Client? client, String? baseUrl})
      : _client = client ?? http.Client(),
        _baseUrl = baseUrl ?? 'https://localhost:5002';

  Future<List<BarStorageItem>> getBars(String symbol, DateTime date) async {
    final dateStr = _formatDate(date);
    final response = await _client.get(
      Uri.parse('$_baseUrl/api/Data/GetBar/$symbol/$dateStr'),
    );
    if (response.statusCode != 200) return [];
    final list = jsonDecode(response.body) as List;
    return list
        .map((e) => BarStorageItem.fromJson(e as Map<String, dynamic>))
        .toList();
  }

  Future<List<BarStorageItem>> getBarRange(
      String symbol, DateTime startDate, DateTime endDate, int timeFrame) async {
    final startStr = _formatDate(startDate);
    final endStr = _formatDate(endDate);
    final response = await _client.get(
      Uri.parse(
          '$_baseUrl/api/Data/GetBarRange/$symbol/$startStr/$endStr/$timeFrame'),
    );
    if (response.statusCode != 200) return [];
    final list = jsonDecode(response.body) as List;
    return list
        .map((e) => BarStorageItem.fromJson(e as Map<String, dynamic>))
        .toList();
  }

  Future<List<BubbleStorageItem>> getBubbles(
      String symbol, DateTime date) async {
    final dateStr = _formatDate(date);
    final response = await _client.get(
      Uri.parse('$_baseUrl/api/Data/GetBubble/$symbol/$dateStr'),
    );
    if (response.statusCode != 200) return [];
    final list = jsonDecode(response.body) as List? ?? [];
    return list
        .map((e) => BubbleStorageItem.fromJson(e as Map<String, dynamic>))
        .toList();
  }

  Future<List<BubbleStorageItem>> getBubbleRange(
      String symbol, DateTime startDate, DateTime endDate) async {
    final startStr = _formatDate(startDate);
    final endStr = _formatDate(endDate);
    final response = await _client.get(
      Uri.parse(
          '$_baseUrl/api/Data/GetBubbleRange/$symbol/$startStr/$endStr'),
    );
    if (response.statusCode != 200) return [];
    final list = jsonDecode(response.body) as List? ?? [];
    return list
        .map((e) => BubbleStorageItem.fromJson(e as Map<String, dynamic>))
        .toList();
  }

  Future<VolumeLevelStorageItem?> getVolume(
      String symbol, DateTime date) async {
    final dateStr = _formatDate(date);
    final response = await _client.get(
      Uri.parse('$_baseUrl/api/Data/GetVolume/$symbol/$dateStr'),
    );
    if (response.statusCode != 200) return null;
    final json = jsonDecode(response.body) as Map<String, dynamic>?;
    if (json == null) return null;
    return VolumeLevelStorageItem.fromJson(json);
  }

  Future<List<StructureStorageItem>> getStructure(
      String symbol, DateTime date, double minDistance) async {
    final dateStr = _formatDate(date);
    final response = await _client.get(
      Uri.parse(
          '$_baseUrl/api/Data/GetStructure/$symbol/$dateStr/${minDistance.toStringAsFixed(1)}'),
    );
    if (response.statusCode != 200) return [];
    final list = jsonDecode(response.body) as List? ?? [];
    return list
        .map((e) => StructureStorageItem.fromJson(e as Map<String, dynamic>))
        .toList();
  }

  Future<List<StructureStorageItem>> getStructureRange(
      String symbol, DateTime startDate, DateTime endDate, double minDistance) async {
    final startStr = _formatDate(startDate);
    final endStr = _formatDate(endDate);
    final response = await _client.get(
      Uri.parse(
          '$_baseUrl/api/Data/GetStructureRange/$symbol/$startStr/$endStr/${minDistance.toStringAsFixed(1)}'),
    );
    if (response.statusCode != 200) return [];
    final list = jsonDecode(response.body) as List? ?? [];
    return list
        .map((e) => StructureStorageItem.fromJson(e as Map<String, dynamic>))
        .toList();
  }

  Future<List<StructureStorageItem>> setStructureDistance(
      String symbol, double minDistance) async {
    final response = await _client.get(
      Uri.parse(
          '$_baseUrl/api/Data/SetStructureDistance/$symbol/${minDistance.toStringAsFixed(1)}'),
    );
    if (response.statusCode != 200) return [];
    final list = jsonDecode(response.body) as List? ?? [];
    return list
        .map((e) => StructureStorageItem.fromJson(e as Map<String, dynamic>))
        .toList();
  }

  /// Distância de estrutura de um timeframe específico (seção diária usa 1440,
  /// issue #10). Não toca nos demais timeframes.
  Future<List<StructureStorageItem>> setStructureDistanceForTimeFrame(
      String symbol, int timeFrame, double minDistance) async {
    final response = await _client.get(
      Uri.parse(
          '$_baseUrl/api/Data/SetStructureDistanceForTimeFrame/$symbol/$timeFrame/${minDistance.toStringAsFixed(1)}'),
    );
    if (response.statusCode != 200) return [];
    final list = jsonDecode(response.body) as List? ?? [];
    return list
        .map((e) => StructureStorageItem.fromJson(e as Map<String, dynamic>))
        .toList();
  }

  /// Histórico de estruturas de um timeframe (âncora diária usa 1440, issue #10).
  Future<List<StructureStorageItem>> getStructureHistory(
      String symbol, int timeFrame, double minDistance,
      {int days = 90}) async {
    final response = await _client.get(
      Uri.parse(
          '$_baseUrl/api/Data/GetStructureHistory/$symbol/$timeFrame/${minDistance.toStringAsFixed(1)}?days=$days'),
    );
    if (response.statusCode != 200) return [];
    final list = jsonDecode(response.body) as List? ?? [];
    return list
        .map((e) => StructureStorageItem.fromJson(e as Map<String, dynamic>))
        .toList();
  }

  Future<List<BarStorageItem>> getLiveBarsSince(
      String symbol, int timeFrame, DateTime since) async {
    final sinceStr = since.toIso8601String();
    final response = await _client.get(
      Uri.parse(
          '$_baseUrl/api/Data/GetLiveBarsSince/$symbol/$timeFrame?since=$sinceStr'),
    );
    if (response.statusCode != 200) return [];
    final list = jsonDecode(response.body) as List? ?? [];
    return list
        .map((e) => BarStorageItem.fromJson(e as Map<String, dynamic>))
        .toList();
  }

  Future<List<BubbleStorageItem>> getLiveBubblesSince(
      String symbol, DateTime since) async {
    final sinceStr = since.toIso8601String();
    final response = await _client.get(
      Uri.parse(
          '$_baseUrl/api/Data/GetLiveBubblesSince/$symbol?since=$sinceStr'),
    );
    if (response.statusCode != 200) return [];
    final list = jsonDecode(response.body) as List? ?? [];
    return list
        .map((e) => BubbleStorageItem.fromJson(e as Map<String, dynamic>))
        .toList();
  }

  Future<List<IndicatorValue>> evaluateIndicators(
      String symbol, DateTime date) async {
    final dateStr = _formatDate(date);
    final response = await _client.get(
      Uri.parse('$_baseUrl/api/indicator/evaluate/$symbol/$dateStr'),
    );
    if (response.statusCode != 200) return [];
    final list = jsonDecode(response.body) as List? ?? [];
    return list
        .map((e) => IndicatorValue.fromJson(e as Map<String, dynamic>))
        .toList();
  }

  Future<DateTime> findLastDateWithData(String symbol) async {
    for (int i = 1; i <= 30; i++) {
      final date = DateTime.now().subtract(Duration(days: i));
      final bars = await getBars(symbol, date);
      if (bars.isNotEmpty) return date;
    }
    return DateTime.now();
  }

  Future<int> startVerifier(VerifierConfig config) async {
    try {
      final response = await _client.post(
        Uri.parse('$_baseUrl/api/Verifier/Start'),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode(config.toJson()),
      );
      if (response.statusCode != 200) {
        debugPrint('[verifier] Start HTTP ${response.statusCode}: ${response.body}');
      }
      return response.statusCode;
    } catch (e) {
      debugPrint('[verifier] Start error: $e');
      return 0;
    }
  }

  Future<int> stopVerifier(String symbol, int timeFrame) async {
    try {
      final response = await _client.post(Uri.parse(
          '$_baseUrl/api/Verifier/Stop?symbol=$symbol&timeFrame=$timeFrame'));
      if (response.statusCode != 200) {
        debugPrint('[verifier] Stop HTTP ${response.statusCode}: ${response.body}');
      }
      return response.statusCode;
    } catch (e) {
      debugPrint('[verifier] Stop error: $e');
      return 0;
    }
  }

  Future<int> resetVerifier(String symbol, int timeFrame) async {
    try {
      final response = await _client.post(Uri.parse(
          '$_baseUrl/api/Verifier/Reset?symbol=$symbol&timeFrame=$timeFrame'));
      if (response.statusCode != 200) {
        debugPrint('[verifier] Reset HTTP ${response.statusCode}: ${response.body}');
      }
      return response.statusCode;
    } catch (e) {
      debugPrint('[verifier] Reset error: $e');
      return 0;
    }
  }

  Future<VerifierState?> getVerifierState(String symbol, int timeFrame) async {
    try {
      final response = await _client.get(Uri.parse(
          '$_baseUrl/api/Verifier/State?symbol=$symbol&timeFrame=$timeFrame'));
      if (response.statusCode != 200) return null;
      return VerifierState.fromJson(jsonDecode(response.body) as Map<String, dynamic>);
    } catch (e) {
      return null;
    }
  }

  Future<List<VerifierLogDay>> exportVerifier(
    String symbol, int timeFrame, {
    DateTime? from,
    DateTime? to,
  }) async {
    try {
      final fromStr = from != null ? '&from=${_formatDate(from)}' : '';
      final toStr = to != null ? '&to=${_formatDate(to)}' : '';
      final response = await _client.get(Uri.parse(
          '$_baseUrl/api/Verifier/Export?symbol=$symbol&timeFrame=$timeFrame$fromStr$toStr'));
      if (response.statusCode != 200) return [];
      final list = jsonDecode(response.body) as List? ?? [];
      return list
          .map((e) => VerifierLogDay.fromJson(e as Map<String, dynamic>))
          .toList();
    } catch (e) {
      return [];
    }
  }

  Future<ExtremeStorageItem?> getExtreme(
      String symbol, DateTime date, {DateTime? from, DateTime? to}) async {
    final dateStr = _formatDate(date);
    final dateParam = 'date=${Uri.encodeQueryComponent(dateStr)}';
    final fromStr = from != null ? '&from=${Uri.encodeQueryComponent(from.toIso8601String())}' : '';
    final toStr = to != null ? '&to=${Uri.encodeQueryComponent(to.toIso8601String())}' : '';
    final response = await _client.get(
      Uri.parse('$_baseUrl/api/Data/GetExtreme/$symbol?$dateParam$fromStr$toStr'),
    );
    if (response.statusCode != 200) return null;
    final json = jsonDecode(response.body) as Map<String, dynamic>?;
    if (json == null) return null;
    return ExtremeStorageItem.fromJson(json);
  }

  /// Monta a URL de SetExtremePeriod sem disparar a requisição (diagnóstico).
  String setExtremePeriodDebugUrl(
    String symbol, {
    DateTime? date,
    DateTime? from,
    DateTime? to,
  }) {
    final dateStr =
        date != null ? 'date=${Uri.encodeQueryComponent(_formatDate(date))}&' : '';
    final fromStr = from != null ? 'from=${Uri.encodeQueryComponent(from.toIso8601String())}&' : '';
    final toStr = to != null ? 'to=${Uri.encodeQueryComponent(to.toIso8601String())}' : '';
    return '$_baseUrl/api/Data/SetExtremePeriod/$symbol?$dateStr$fromStr$toStr';
  }

  Future<ExtremeStorageItem?> setExtremePeriod(
    String symbol, {
    DateTime? date,
    DateTime? from,
    DateTime? to,
  }) async {
    final dateStr =
        date != null ? 'date=${Uri.encodeQueryComponent(_formatDate(date))}&' : '';
    final fromStr = from != null ? 'from=${Uri.encodeQueryComponent(from.toIso8601String())}&' : '';
    final toStr = to != null ? 'to=${Uri.encodeQueryComponent(to.toIso8601String())}' : '';
    final response = await _client.get(
      Uri.parse('$_baseUrl/api/Data/SetExtremePeriod/$symbol?$dateStr$fromStr$toStr'),
    );
    if (response.statusCode != 200) return null;
    final json = jsonDecode(response.body) as Map<String, dynamic>?;
    if (json == null) return null;
    return ExtremeStorageItem.fromJson(json);
  }

  Future<ExtremeStorageItem?> setExtremeConfig(
    String symbol, {
    DateTime? date,
    DateTime? from,
    DateTime? to,
    double? noiseSensitivity,
    double? minimumProminence,
  }) async {
    final dateStr =
        date != null ? 'date=${Uri.encodeQueryComponent(_formatDate(date))}&' : '';
    final fromStr = from != null ? 'from=${Uri.encodeQueryComponent(from.toIso8601String())}&' : '';
    final toStr = to != null ? 'to=${Uri.encodeQueryComponent(to.toIso8601String())}&' : '';
    final ns = noiseSensitivity != null
        ? 'noiseSensitivity=${noiseSensitivity.toStringAsFixed(3)}&'
        : '';
    final mp = minimumProminence != null
        ? 'minimumProminence=${minimumProminence.toStringAsFixed(3)}'
        : '';
    final response = await _client.get(
      Uri.parse('$_baseUrl/api/Data/SetExtremeConfig/$symbol?$dateStr$fromStr$toStr$ns$mp'),
    );
    if (response.statusCode != 200) return null;
    final json = jsonDecode(response.body) as Map<String, dynamic>?;
    if (json == null) return null;
    return ExtremeStorageItem.fromJson(json);
  }

  /// Overlay diário estático (issue #10): perfil agregado multi-dia.
  /// Rota nova e isolada — não toca no fluxo intraday (GetExtreme/SetExtreme*).
  Future<ExtremeStorageItem?> getExtremeDaily(
    String symbol, {
    DateTime? from,
    DateTime? to,
    double? noiseSensitivity,
    double? minimumProminence,
  }) async {
    final fromStr = from != null ? 'from=${Uri.encodeQueryComponent(_formatDate(from))}&' : '';
    final toStr = to != null ? 'to=${Uri.encodeQueryComponent(_formatDate(to))}&' : '';
    final ns = noiseSensitivity != null
        ? 'noiseSensitivity=${noiseSensitivity.toStringAsFixed(3)}&'
        : '';
    final mp = minimumProminence != null
        ? 'minimumProminence=${minimumProminence.toStringAsFixed(3)}'
        : '';
    final response = await _client.get(
      Uri.parse('$_baseUrl/api/Data/GetExtremeDaily/$symbol?$fromStr$toStr$ns$mp'),
    );
    if (response.statusCode != 200) return null;
    final json = jsonDecode(response.body) as Map<String, dynamic>?;
    if (json == null) return null;
    return ExtremeStorageItem.fromJson(json);
  }

  /// Volume Profile diário (issue #12): perfil agregado multi-dia somado no
  /// backend a partir dos arquivos diários (fonte 100% diária).
  Future<List<VolumeLevel>> getDailyProfile(
    String symbol, {
    DateTime? from,
    DateTime? to,
  }) async {
    final fromStr = from != null ? 'from=${Uri.encodeQueryComponent(_formatDate(from))}&' : '';
    final toStr = to != null ? 'to=${Uri.encodeQueryComponent(_formatDate(to))}' : '';
    final response = await _client.get(
      Uri.parse('$_baseUrl/api/Data/GetDailyProfile/$symbol?$fromStr$toStr'),
    );
    if (response.statusCode != 200) return [];
    final list = jsonDecode(response.body) as List? ?? [];
    return list
        .map((e) => VolumeLevel.fromJson(e as Map<String, dynamic>))
        .toList();
  }

  String _formatDate(DateTime date) =>
      '${date.year.toString().padLeft(4, '0')}-${date.month.toString().padLeft(2, '0')}-${date.day.toString().padLeft(2, '0')}';

  /// Pivot Tradicional do Profit no splitter superior (issue #14): fonte HLC
  /// de D-1, linhas só sobre a sessão exibida. Rota pura, sem estado ao vivo.
  Future<PivotStorageItem?> getPivotIntraday(
    String symbol, {
    int? lineCount,
  }) async {
    final lc = lineCount != null ? 'lineCount=$lineCount' : '';
    final response = await _client.get(
      Uri.parse('$_baseUrl/api/Data/GetPivotIntraday/$symbol?$lc'),
    );
    if (response.statusCode != 200) return null;
    final json = jsonDecode(response.body) as Map<String, dynamic>?;
    if (json == null) return null;
    return PivotStorageItem.fromJson(json);
  }

  /// Pivot Tradicional do Profit no widget diário (issue #14): fonte HLC da
  /// semana anterior (fiel ao Profit). Rota pura, sem estado ao vivo.
  Future<PivotStorageItem?> getPivotDaily(
    String symbol, {
    int? lineCount,
  }) async {
    final lc = lineCount != null ? 'lineCount=$lineCount' : '';
    final response = await _client.get(
      Uri.parse('$_baseUrl/api/Data/GetPivotDaily/$symbol?$lc'),
    );
    if (response.statusCode != 200) return null;
    final json = jsonDecode(response.body) as Map<String, dynamic>?;
    if (json == null) return null;
    return PivotStorageItem.fromJson(json);
  }

  void dispose() {
    _client.close();
  }
}
