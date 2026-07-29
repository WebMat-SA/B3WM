class MarketOrderRequest {
  final String symbol;
  final double volume;
  final String type;
  final double? sl;
  final double? tp;
  final String comment;
  final int magic;
  final int deviation;

  MarketOrderRequest({
    required this.symbol,
    required this.volume,
    required this.type,
    this.sl,
    this.tp,
    this.comment = '',
    this.magic = 0,
    this.deviation = 10,
  });

  Map<String, dynamic> toJson() => {
        'symbol': symbol,
        'volume': volume,
        'type': type,
        'sl': sl,
        'tp': tp,
        'comment': comment,
        'magic': magic,
        'deviation': deviation,
      };
}

class OrderResult {
  final bool success;
  final int retcode;
  final String retcodeName;
  final int orderTicket;
  final double price;
  final double volume;
  final String message;

  OrderResult({
    required this.success,
    this.retcode = 0,
    this.retcodeName = '',
    this.orderTicket = 0,
    this.price = 0,
    this.volume = 0,
    this.message = '',
  });

  factory OrderResult.fromJson(Map<String, dynamic> json) => OrderResult(
        success: json['success'] as bool? ?? false,
        retcode: json['retcode'] as int? ?? 0,
        retcodeName: json['retcode_name'] as String? ?? '',
        orderTicket: json['order_ticket'] as int? ?? 0,
        price: (json['price'] as num?)?.toDouble() ?? 0,
        volume: (json['volume'] as num?)?.toDouble() ?? 0,
        message: json['message'] as String? ?? '',
      );
}

class OrderInfo {
  final int ticket;
  final String symbol;
  final String type;
  final double volume;
  final double priceOpen;
  final double sl;
  final double tp;
  final String timeSetup;
  final String timeExpiration;
  final String state;
  final String comment;
  final int magic;

  OrderInfo({
    required this.ticket,
    required this.symbol,
    required this.type,
    required this.volume,
    required this.priceOpen,
    this.sl = 0,
    this.tp = 0,
    this.timeSetup = '',
    this.timeExpiration = '',
    this.state = '',
    this.comment = '',
    this.magic = 0,
  });

  factory OrderInfo.fromJson(Map<String, dynamic> json) => OrderInfo(
        ticket: json['ticket'] as int? ?? 0,
        symbol: json['symbol'] as String? ?? '',
        type: json['type'] as String? ?? '',
        volume: (json['volume'] as num?)?.toDouble() ?? 0,
        priceOpen: (json['price_open'] as num?)?.toDouble() ?? 0,
        sl: (json['sl'] as num?)?.toDouble() ?? 0,
        tp: (json['tp'] as num?)?.toDouble() ?? 0,
        timeSetup: json['time_setup'] as String? ?? '',
        timeExpiration: json['time_expiration'] as String? ?? '',
        state: json['state'] as String? ?? '',
        comment: json['comment'] as String? ?? '',
        magic: json['magic'] as int? ?? 0,
      );
}

class AccountInfo {
  final int login;
  final double balance;
  final double equity;
  final double profit;
  final double margin;
  final double marginFree;
  final double marginLevel;
  final int leverage;
  final String currency;
  final String server;
  final bool tradeAllowed;
  final String name;

  AccountInfo({
    required this.login,
    required this.balance,
    required this.equity,
    required this.profit,
    required this.margin,
    required this.marginFree,
    required this.marginLevel,
    required this.leverage,
    required this.currency,
    required this.server,
    required this.tradeAllowed,
    required this.name,
  });

  factory AccountInfo.fromJson(Map<String, dynamic> json) => AccountInfo(
        login: json['login'] as int? ?? 0,
        balance: (json['balance'] as num?)?.toDouble() ?? 0,
        equity: (json['equity'] as num?)?.toDouble() ?? 0,
        profit: (json['profit'] as num?)?.toDouble() ?? 0,
        margin: (json['margin'] as num?)?.toDouble() ?? 0,
        marginFree: (json['margin_free'] as num?)?.toDouble() ?? 0,
        marginLevel: (json['margin_level'] as num?)?.toDouble() ?? 0,
        leverage: json['leverage'] as int? ?? 0,
        currency: json['currency'] as String? ?? '',
        server: json['server'] as String? ?? '',
        tradeAllowed: json['trade_allowed'] as bool? ?? false,
        name: json['name'] as String? ?? '',
      );
}

class PositionInfo {
  final int ticket;
  final String symbol;
  final String type;
  final double volume;
  final double priceOpen;
  final double sl;
  final double tp;
  final double priceCurrent;
  final double profit;
  final double swap;
  final double commission;
  final int magic;
  final String comment;
  final String time;

  PositionInfo({
    required this.ticket,
    required this.symbol,
    required this.type,
    required this.volume,
    required this.priceOpen,
    this.sl = 0,
    this.tp = 0,
    this.priceCurrent = 0,
    this.profit = 0,
    this.swap = 0,
    this.commission = 0,
    this.magic = 0,
    this.comment = '',
    this.time = '',
  });

  factory PositionInfo.fromJson(Map<String, dynamic> json) => PositionInfo(
        ticket: json['ticket'] as int? ?? 0,
        symbol: json['symbol'] as String? ?? '',
        type: json['type'] as String? ?? '',
        volume: (json['volume'] as num?)?.toDouble() ?? 0,
        priceOpen: (json['price_open'] as num?)?.toDouble() ?? 0,
        sl: (json['sl'] as num?)?.toDouble() ?? 0,
        tp: (json['tp'] as num?)?.toDouble() ?? 0,
        priceCurrent: (json['price_current'] as num?)?.toDouble() ?? 0,
        profit: (json['profit'] as num?)?.toDouble() ?? 0,
        swap: (json['swap'] as num?)?.toDouble() ?? 0,
        commission: (json['commission'] as num?)?.toDouble() ?? 0,
        magic: json['magic'] as int? ?? 0,
        comment: json['comment'] as String? ?? '',
        time: json['time'] as String? ?? '',
      );
}

class HistoryDeal {
  final int ticket;
  final String symbol;
  final String type;
  final double volume;
  final double price;
  final double profit;
  final String time;
  final String comment;
  final int magic;

  HistoryDeal({
    required this.ticket,
    required this.symbol,
    required this.type,
    required this.volume,
    required this.price,
    required this.profit,
    required this.time,
    this.comment = '',
    this.magic = 0,
  });

  factory HistoryDeal.fromJson(Map<String, dynamic> json) => HistoryDeal(
        ticket: json['ticket'] as int? ?? 0,
        symbol: json['symbol'] as String? ?? '',
        type: json['type'] as String? ?? '',
        volume: (json['volume'] as num?)?.toDouble() ?? 0,
        price: (json['price'] as num?)?.toDouble() ?? 0,
        profit: (json['profit'] as num?)?.toDouble() ?? 0,
        time: json['time'] as String? ?? '',
        comment: json['comment'] as String? ?? '',
        magic: json['magic'] as int? ?? 0,
      );

  Map<String, dynamic> toJson() => {
        'ticket': ticket,
        'symbol': symbol,
        'type': type,
        'volume': volume,
        'price': price,
        'profit': profit,
        'time': time,
        'comment': comment,
        'magic': magic,
      };
}

class SymbolInfo {
  final String symbol;
  final double bid;
  final double ask;
  final int spread;
  final int digits;

  SymbolInfo({
    required this.symbol,
    required this.bid,
    required this.ask,
    required this.spread,
    this.digits = 0,
  });

  factory SymbolInfo.fromJson(Map<String, dynamic> json) => SymbolInfo(
        symbol: json['symbol'] as String? ?? '',
        bid: (json['bid'] as num?)?.toDouble() ?? 0,
        ask: (json['ask'] as num?)?.toDouble() ?? 0,
        spread: json['spread'] as int? ?? 0,
        digits: json['digits'] as int? ?? 0,
      );
}
