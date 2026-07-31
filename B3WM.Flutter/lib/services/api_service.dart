import 'dart:convert';
import 'package:http/http.dart' as http;
import '../models/bar_storage_item.dart';
import '../models/bubble_storage_item.dart';
import '../models/volume_level_storage_item.dart';
import '../models/structure_storage_item.dart';
import '../models/indicator_value.dart';

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

  String _formatDate(DateTime date) =>
      '${date.year.toString().padLeft(4, '0')}-${date.month.toString().padLeft(2, '0')}-${date.day.toString().padLeft(2, '0')}';

  void dispose() {
    _client.close();
  }
}
