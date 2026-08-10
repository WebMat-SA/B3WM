class VolumeLevel {
  final double price;
  final int total;
  final int buyVolume;
  final int sellVolume;

  int get delta => buyVolume - sellVolume;

  VolumeLevel({
    required this.price,
    required this.total,
    required this.buyVolume,
    required this.sellVolume,
  });

  factory VolumeLevel.fromJson(Map<String, dynamic> json) {
    return VolumeLevel(
      price: (json['price'] as num).toDouble(),
      total: (json['total'] as num).toInt(),
      buyVolume: (json['buyVolume'] as num).toInt(),
      sellVolume: (json['sellVolume'] as num).toInt(),
    );
  }

  Map<String, dynamic> toJson() => {
        'price': price,
        'total': total,
        'buyVolume': buyVolume,
        'sellVolume': sellVolume,
      };
}
