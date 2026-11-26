import 'dart:convert';
import 'package:http/http.dart' as http;
import '../models/trade_models.dart';
import '../app_config.dart';

class TradingApiService {
  final http.Client _client;
  final String _baseUrl;

  TradingApiService({http.Client? client, String? baseUrl})
      : _client = client ?? http.Client(),
        _baseUrl = baseUrl ?? AppConfig.apiBaseUrl;

  Future<OrderResult?> placeMarketOrder(MarketOrderRequest request) async {
    return _postOrder('/api/trade/order-market', request.toJson());
  }

  Future<OrderResult?> placeLimitOrder(LimitOrderRequest request) async {
    return _postOrder('/api/trade/order-limit', request.toJson());
  }

  Future<OrderResult?> closePosition(int positionTicket) async {
    return _postOrder('/api/trade/order-close', {
      'position_ticket': positionTicket,
    });
  }

  /// POST de ordens no backend. Em falha de transporte/HTTP devolve um
  /// [OrderResult] com `success=false` e a mensagem real do servidor, para a
  /// UI não mostrar apenas "falha ao enviar" sem motivo.
  Future<OrderResult?> _postOrder(
      String path, Map<String, dynamic> body) async {
    final response = await _client
        .post(
          Uri.parse('$_baseUrl$path'),
          headers: {'Content-Type': 'application/json'},
          body: jsonEncode(body),
        )
        .timeout(const Duration(seconds: 15));
    if (response.statusCode == 200) {
      return OrderResult.fromJson(
          jsonDecode(response.body) as Map<String, dynamic>);
    }
    final detail = response.body.trim();
    return OrderResult(
      success: false,
      retcode: response.statusCode,
      retcodeName: 'http_${response.statusCode}',
      message: 'HTTP ${response.statusCode}'
          '${detail.isEmpty ? '' : ': $detail'}',
    );
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

  Future<OrderResult?> modifyPendingOrder(
      ModifyPendingOrderRequest request) async {
    return _postOrder('/api/trade/order-modify-pending', request.toJson());
  }

  Future<OrderResult?> modifyPosition(ModifyPositionRequest request) async {
    return _postOrder('/api/trade/order-modify', request.toJson());
  }

  Future<OrderResult?> cancelOrder(int orderTicket) async {
    return _postOrder('/api/trade/order-cancel', {
      'position_ticket': orderTicket,
    });
  }

  Future<List<OrderInfo>> getOpenOrders() async {
    final response = await _client.get(Uri.parse('$_baseUrl/api/trade/orders'));
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
