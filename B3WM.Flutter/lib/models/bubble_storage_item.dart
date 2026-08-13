import 'ticks2.dart';

class BubbleStorageItem {
  final double price;
  final int agent;
  final double amount;
  final DateTime date;
  final ActionType actionType;
  final String symbol;

  BubbleStorageItem({
    required this.price,
    required this.agent,
    required this.amount,
    required this.date,
    required this.actionType,
    required this.symbol,
  });

  factory BubbleStorageItem.fromJson(Map<String, dynamic> json) {
    return BubbleStorageItem(
      price: (json['price'] as num).toDouble(),
      agent: json['agent'] as int,
      amount: (json['amount'] as num).toDouble(),
      date: DateTime.parse(json['date'] as String),
      actionType: ActionType.fromValue(json['actionType']),
      symbol: json['symbol'] as String,
    );
  }

  Map<String, dynamic> toJson() => {
        'price': price,
        'agent': agent,
        'amount': amount,
        'date': date.toIso8601String(),
        'actionType': actionType.value,
        'symbol': symbol,
      };
}
