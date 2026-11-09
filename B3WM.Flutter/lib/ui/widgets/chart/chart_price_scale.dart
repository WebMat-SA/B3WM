import 'chart_data.dart';

/// Escala preço↔Y baseada apenas no range dos candles VISÍVEIS (eixo X).
///
/// A normalização mantém os extremos visíveis em coordenadas fixas do child
/// (`H/6` e `5H/6` com o padding de 25% em cada lado), então o zoom X — que
/// troca quais candles estão visíveis — preserva as margens em pixels
/// `ytop`/`ybottom`. A assimetria vem do pan (`ty`); o Y-zoom (`yZoom`) é o
/// único que altera essas margens junto com a escala.
class ChartPriceScale {
  /// Padding aplicado acima do `max` e abaixo do `min` visíveis. Com 0.25 em
  /// cada lado os extremos caem em `H/6` e `5H/6` do child (paridade visual
  /// com a versão anterior, que usava o range global).
  static const double paddingRatio = 0.25;

  final double min;
  final double max;
  final double yZoom;

  const ChartPriceScale({
    required this.min,
    required this.max,
    this.yZoom = 1.0,
  });

  factory ChartPriceScale.fallback(ChartData data, {double yZoom = 1.0}) {
    return ChartPriceScale(
      min: data.minPrice,
      max: data.maxPrice,
      yZoom: yZoom,
    );
  }

  double get priceRange => max - min;

  bool get isValid => priceRange.isFinite && priceRange > 0;

  /// Mesma escala com outro Y-zoom (usado para ancorar o arrasto da coluna Y).
  ChartPriceScale rezoom(double zoom) =>
      ChartPriceScale(min: min, max: max, yZoom: zoom);

  double get _paddedMin => min - priceRange * paddingRatio;
  double get _paddedMax => max + priceRange * paddingRatio;
  double get _center => (_paddedMin + _paddedMax) / 2;
  double get _halfRange => (_paddedMax - _paddedMin) / 2;

  double _adjustedRange() {
    final z = yZoom.isFinite && yZoom > 0 ? yZoom : 1.0;
    return 2 * (_halfRange / z);
  }

  double _adjustedMin() {
    final z = yZoom.isFinite && yZoom > 0 ? yZoom : 1.0;
    return _center - (_halfRange / z);
  }

  /// Preço → Y no espaço do child (0 no topo, [height] na base).
  double childY(double price, double height) {
    if (!isValid || !height.isFinite || height <= 0) return height / 2;
    final range = _adjustedRange();
    if (!range.isFinite || range <= 0) return height / 2;
    return height - ((price - _adjustedMin()) / range) * height;
  }

  /// Y no espaço do child → preço (inverso de [childY]).
  double priceAtChildY(double childY, double height) {
    if (!isValid || !height.isFinite || height <= 0) return 0;
    final range = _adjustedRange();
    if (!range.isFinite || range <= 0) return 0;
    return _adjustedMin() + ((height - childY) / height) * range;
  }
}
