import 'dart:convert';
import 'package:http/http.dart' as http;
import '../models/trade_models.dart';

class TradingApiService {
  final http.Client _client;
  final String _baseUrl;

  TradingApiService({http.Client? client, String? baseUrl})
      : _client = client ?? http.Client(),
        _baseUrl = baseUrl ?? 'https://localhost:5002';

  Future<OrderResult?> placeMarketOrder(MarketOrderRequest request) async {
    final response = await _client.post(
      Uri.parse('$_baseUrl/api/trade/order-market'),
      headers: {'Content-Type': 'application/json'},
      body: jsonEncode(request.toJson()),
    );
    if (response.statusCode != 200) return null;
    return OrderResult.fromJson(
        jsonDecode(response.body) as Map<String, dynamic>);
  }

  Future<OrderResult?> closePosition(int positionTicket) async {
    final response = await _client.post(
      Uri.parse('$_baseUrl/api/trade/order-close'),
      headers: {'Content-Type': 'application/json'},
      body: jsonEncode({'position_ticket': positionTicket}),
    );
    if (response.statusCode != 200) return null;
    return OrderResult.fromJson(
        jsonDecode(response.body) as Map<String, dynamic>);
  }

  Future<AccountInfo?> getAccountInfo() async {
    final response = await _client.get(
      Uri.parse('$_baseUrl/api/trade/account'),
    );
    if (response.statusCode != 200) return null;
    return AccountInfo.fromJson(
        jsonDecode(response.body) as Map<String, dynamic>);
  }

  Future<List<PositionInfo>> getPositions({String? symbol}) async {
    final url = symbol != null
        ? '$_baseUrl/api/trade/positions/$symbol'
        : '$_baseUrl/api/trade/positions';
    final response = await _client.get(Uri.parse(url));
    if (response.statusCode != 200) return [];
    final list = jsonDecode(response.body) as List? ?? [];
    return list
        .map((e) => PositionInfo.fromJson(e as Map<String, dynamic>))
        .toList();
  }

  Future<List<HistoryDeal>> getHistory(String symbol,
      {String fromDate = '', String toDate = ''}) async {
    var url = '$_baseUrl/api/trade/history?symbol=$symbol';
    if (fromDate.isNotEmpty) url += '&from_date=$fromDate';
    if (toDate.isNotEmpty) url += '&to_date=$toDate';
    final response = await _client.get(Uri.parse(url));
    if (response.statusCode != 200) return [];
    final list = jsonDecode(response.body) as List? ?? [];
    return list
        .map((e) => HistoryDeal.fromJson(e as Map<String, dynamic>))
        .toList();
  }

  Future<OrderResult?> cancelOrder(int orderTicket) async {
    final response = await _client.post(
      Uri.parse('$_baseUrl/api/trade/order-cancel'),
      headers: {'Content-Type': 'application/json'},
      body: jsonEncode({'position_ticket': orderTicket}),
    );
    if (response.statusCode != 200) return null;
    return OrderResult.fromJson(
        jsonDecode(response.body) as Map<String, dynamic>);
  }

  Future<List<OrderInfo>> getOpenOrders() async {
    final response =
        await _client.get(Uri.parse('$_baseUrl/api/trade/orders'));
    if (response.statusCode != 200) return [];
    final list = jsonDecode(response.body) as List? ?? [];
    return list
        .map((e) => OrderInfo.fromJson(e as Map<String, dynamic>))
        .toList();
  }

  Future<SymbolInfo?> getSymbolInfo(String symbol) async {
    final response =
        await _client.get(Uri.parse('$_baseUrl/api/trade/symbol/$symbol'));
    if (response.statusCode != 200) return null;
    return SymbolInfo.fromJson(
        jsonDecode(response.body) as Map<String, dynamic>);
  }

  void dispose() {
    _client.close();
  }
}
