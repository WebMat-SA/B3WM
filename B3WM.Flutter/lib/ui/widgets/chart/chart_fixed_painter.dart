import 'dart:math';
import 'package:flutter/material.dart';
import 'chart_data.dart';
import 'chart_price_scale.dart';
import 'measure_ruler.dart';
import '../../../models/defaults.dart';
import '../../../models/trade_models.dart';

class ChartFixedPainter extends CustomPainter {
  final ChartData data;
  final double candleAreaWidth;
  final double candleAreaHeight;
  final double yZoom;

  /// Escala preço↔Y do range de candles visíveis no viewport X. Compartilhada
  /// com o [ChartPainter] para manter candles e overlays no mesmo eixo.
  final ChartPriceScale priceScale;
  final TransformationController controller;
  final Set<int> closingTickets;
  final Set<int> cancellingTickets;
  final Set<int> hiddenOrderTickets;

  /// Posição/camada de saída que está sendo arrastada: a linha original é
  /// escondida para não duplicar com o preview do arrasto.
  final int? hiddenExitPositionTicket;
  final String? hiddenExitKind;

  /// Tickets cujo "X" está sob o mouse, para desenhar sombra de hover.
  final int? hoveredClosePositionTicket;
  final int? hoveredCloseOrderTicket;
  final int? hoveredCloseExitTicket;
  final String? hoveredCloseExitKind;

  /// Saídas (`ticket:tp` / `ticket:sl`) em processo de cancelamento.
  final Set<String> cancellingExitKeys;
  final double loadingAnimation;

  static const double marginLeft = 8;
  static const double marginRight = 8;
  static const double marginTop = 8;
  static const double marginBottom = 24;
  static const double profileWidth = 60;
  static const double rightLabelMargin = 64;
  static const double rightReserved = 168;
  static const double bottomLabelHeight = 20;
  static const double candleStep = 8.0;

  static const Color textColor = Color(0xFFaaaaaa);
  static const Color tradeBuyColor = Color(0xFF2ECC71);
  static const Color tradeSellColor = Color(0xFFE74C3C);

  static const double tradeChipLeft = 3;
  static const double tradeChipHeight = 20;
  static const double tradeChipRadius = 5;
  static const double tradeChipAccentWidth = 3;
  static const double tradeChipPaddingLeft = 6;
  static const double tradeChipCloseSize = 12;
  static const double tradeChipCloseGap = 6;
  static const double tradeChipRightPad = 8;
  static const double tradeChipCloseHitSize = 18;

  /// Centro local (relativo ao Listener do gráfico) do X dentro do chip.
  static double get tradeChipCloseCenterLocalX =>
      tradeChipLeft +
      tradeChipAccentWidth +
      tradeChipPaddingLeft +
      tradeChipCloseSize / 2;

  static double get _tradeChipTextLeftLocal =>
      tradeChipCloseCenterLocalX + tradeChipCloseSize / 2 + tradeChipCloseGap;

  final double? hoverY;
  final int? hoverCandleIndex;

  ChartFixedPainter({
    required this.data,
    required this.candleAreaWidth,
    required this.candleAreaHeight,
    this.yZoom = 1.0,
    required this.priceScale,
    required this.controller,
    this.hoverY,
    this.hoverCandleIndex,
    this.closingTickets = const {},
    this.cancellingTickets = const {},
    this.hiddenOrderTickets = const {},
    this.hiddenExitPositionTicket,
    this.hiddenExitKind,
    this.hoveredClosePositionTicket,
    this.hoveredCloseOrderTicket,
    this.hoveredCloseExitTicket,
    this.hoveredCloseExitKind,
    this.cancellingExitKeys = const {},
    this.loadingAnimation = 0,
  });

  double get chartRight => marginLeft + candleAreaWidth;
  double get chartHeight => candleAreaHeight;

  double get _scaleY {
    final v = controller.value[5];
    return v.isFinite && v > 0 ? v : 1.0;
  }

  double get _transY {
    final v = controller.value.getTranslation().y;
    return v.isFinite ? v : 0.0;
  }

  double _toChildY(double price) {
    return priceScale.childY(price, chartHeight);
  }

  double _priceToY(double price) {
    return marginTop + _toChildY(price) * _scaleY + _transY;
  }

  double _yToPrice(double y) {
    final childY = (y - marginTop - _transY) / _scaleY;
    return priceScale.priceAtChildY(childY, chartHeight);
  }

  @override
  void paint(Canvas canvas, Size size) {
    if (data.candles.isEmpty) {
      return;
    }
    _drawBackground(canvas, size);
    _drawGridLines(canvas);
    _drawXAxis(canvas);
    _drawMarkArea(canvas);
    _drawDaySeparators(canvas);
    canvas.save();
    canvas.clipRect(
        Rect.fromLTWH(marginLeft, marginTop, candleAreaWidth, chartHeight));
    _drawVolumeProfile(canvas, size);
    _drawExtremeLines(canvas);
    _drawPivotLines(canvas);
    _drawVwapLines(canvas);
    _drawMarkLine(canvas);
    _drawPositionLines(canvas);
    _drawPositionExitLines(canvas);
    _drawOrderLines(canvas);
    canvas.restore();
    _drawYAxis(canvas);
    if (hoverY != null) _drawHoverLine(canvas, hoverY!);
    if (hoverCandleIndex != null) _drawHoverXLabel(canvas, hoverCandleIndex!);
    _drawWatermark(canvas, size);
  }

  void _drawBackground(Canvas canvas, Size size) {
    final paint = Paint()..color = const Color(0xFF1e1e1e);
    canvas.drawRect(Rect.fromLTWH(0, 0, size.width, size.height), paint);

    final chartPaint = Paint()..color = const Color(0xFF252525);
    canvas.drawRect(
        Rect.fromLTWH(marginLeft, marginTop, candleAreaWidth - rightLabelMargin,
            chartHeight),
        chartPaint);
  }

  void _drawGridLines(Canvas canvas) {
    final paint = Paint()
      ..color = const Color(0x12808080)
      ..strokeWidth = 0.5;

    const lines = 8;
    for (int i = 0; i <= lines; i++) {
      final y = marginTop + (chartHeight / lines) * i;
      canvas.drawLine(Offset(marginLeft, y),
          Offset(chartRight - rightLabelMargin, y), paint);
    }
  }

  double _snapToTick(double price, double tick) {
    return (price / tick).round() * tick;
  }

  void _drawYAxis(Canvas canvas) {
    const lines = 8;
    final tick = Defaults.tickSize(data.symbol);
    for (int i = 0; i <= lines; i++) {
      if (i == 0 || i == lines) continue;
      final y = marginTop + (chartHeight / lines) * i;
      final price = _snapToTick(_yToPrice(y), tick);
      final label = price.toStringAsFixed(_decimalPlaces(data.symbol));

      final tp = TextPainter(
        text: TextSpan(
            text: label, style: const TextStyle(color: textColor, fontSize: 9)),
        textDirection: TextDirection.ltr,
      )..layout();
      tp.paint(canvas, Offset(chartRight - tp.width - 4, y - tp.height / 2));
    }
  }

  void _drawXAxis(Canvas canvas) {
    final y = marginTop + chartHeight + 2;
    final m = controller.value;
    final tx = m.getTranslation().x;
    final scaleX = m[0];

    final visibleStart = -tx / scaleX;
    final visibleEnd = (candleAreaWidth - tx) / scaleX;

    final firstIdx =
        max(0, ((visibleStart - candleStep / 2) / candleStep).floor());
    final lastIdx = min(data.dates.length - 1,
        ((visibleEnd - candleStep / 2) / candleStep).ceil());

    final count = lastIdx - firstIdx + 1;
    if (count <= 0) return;
    final screenStep = candleStep * scaleX;
    const minLabelSpacing = 50.0;
    final step = max(1, (minLabelSpacing / screenStep).ceil());

    for (int i = firstIdx; i <= lastIdx; i += step) {
      final childX = i * candleStep + candleStep / 2;
      final screenX = marginLeft + childX * scaleX + tx;
      if (screenX < marginLeft || screenX > chartRight) continue;

      final fmt = data.dates[i];
      // Diário (1440): candles sempre à 00:00 — mostra a data em vez da hora.
      final label = data.timeFrame == 1440
          ? '${fmt.day.toString().padLeft(2, '0')}/${fmt.month.toString().padLeft(2, '0')}'
          : '${fmt.hour.toString().padLeft(2, '0')}:${fmt.minute.toString().padLeft(2, '0')}';
      final tp = TextPainter(
        text: TextSpan(
            text: label, style: const TextStyle(color: textColor, fontSize: 9)),
        textDirection: TextDirection.ltr,
      )..layout();
      tp.paint(canvas, Offset(screenX - tp.width / 2, y));
    }
  }

  void _drawVolumeProfile(Canvas canvas, Size size) {
    if (data.volumeProfile.isEmpty) return;

    final profileLeft = chartRight - profileWidth - rightLabelMargin;
    final profileRight = chartRight - rightLabelMargin;
    final profileW = (profileRight - profileLeft) * data.profileSizeH;

    double maxVol = 0;
    for (final v in data.volumeProfile) {
      if (v.total > maxVol) maxVol = v.total;
    }
    if (maxVol <= 0) return;
    final priceRange = priceScale.priceRange;
    if (!priceRange.isFinite || priceRange <= 0) return;

    for (final v in data.volumeProfile) {
      final y = _priceToY(v.price);
      final barHeight =
          (v.step / priceRange) * chartHeight * data.profileSizeV * _scaleY;
      final barWidth = (v.total / maxVol) * profileW;

      final color = v.isPoc
          ? const Color(0xFFFF6400).withOpacity(data.profileOpacity)
          : const Color(0xFF6496ff).withOpacity(data.profileOpacity);

      final paint = Paint()..color = color;
      canvas.drawRect(
          Rect.fromLTWH(profileRight - barWidth, y - barHeight / 2, barWidth,
              max(barHeight, 1.0)),
          paint);

      final borderPaint = Paint()
        ..color = color.withOpacity(data.profileOpacity * 0.6)
        ..style = PaintingStyle.stroke
        ..strokeWidth = 0.5;
      canvas.drawRect(
          Rect.fromLTWH(profileRight - barWidth, y - barHeight / 2, barWidth,
              max(barHeight, 1.0)),
          borderPaint);
    }
  }

  void _drawMarkArea(Canvas canvas) {
    if (!data.profileVisible) return;
    if (data.rangeStart <= 0 && data.rangeEnd >= data.candles.length) return;

    final m = controller.value;
    final scaleX = m[0];
    final tx = m.getTranslation().x;

    final stepX = ChartFixedPainter.candleStep;
    final x1 = marginLeft +
        (data.rangeStart.clamp(0, data.candles.length - 1) * stepX +
                stepX / 2) *
            scaleX +
        tx -
        stepX / 2 * scaleX;
    final x2 = marginLeft +
        (data.rangeEnd.clamp(0, data.candles.length - 1) * stepX + stepX / 2) *
            scaleX +
        tx +
        stepX / 2 * scaleX;

    final paint = Paint()..color = const Color(0x08c8c8c8);
    canvas.drawRect(
        Rect.fromLTRB(x1.clamp(marginLeft, chartRight), marginTop,
            x2.clamp(marginLeft, chartRight), marginTop + chartHeight),
        paint);
  }

  void _drawDaySeparators(Canvas canvas) {
    final m = controller.value;
    final scaleX = m[0];
    final tx = m.getTranslation().x;

    final stepX = ChartFixedPainter.candleStep;

    final paint = Paint()
      ..color = const Color(0x26c8c8c8)
      ..strokeWidth = 1;

    for (final idx in data.daySeparatorIndices) {
      final childX = idx * stepX + stepX / 2;
      final screenX = marginLeft + childX * scaleX + tx;
      if (screenX < marginLeft || screenX > chartRight) continue;

      canvas.drawLine(Offset(screenX, marginTop),
          Offset(screenX, marginTop + chartHeight), paint);

      if (idx < data.dates.length) {
        final d = data.dates[idx];
        final label =
            '${d.day.toString().padLeft(2, '0')}/${d.month.toString().padLeft(2, '0')}';
        final tp = TextPainter(
          text: TextSpan(
            text: label,
            style: const TextStyle(color: Color(0x66c8c8c8), fontSize: 8),
          ),
          textDirection: TextDirection.ltr,
        )..layout();
        tp.paint(canvas, Offset(screenX - tp.width / 2, marginTop + 2));
      }
    }
  }

  int _decimalPlaces(String symbol) {
    final tick = Defaults.tickSize(symbol);
    if (tick == tick.roundToDouble()) return 0;
    final str = tick.toStringAsFixed(10);
    final dot = str.indexOf('.');
    return str.substring(dot + 1).replaceAll(RegExp(r'0+$'), '').length;
  }

  void _drawExtremeLines(Canvas canvas) {
    for (final ex in data.extremeLayers) {
      if (ex.visible) _drawExtremeGroup(canvas, ex);
    }
  }

  void _drawExtremeGroup(Canvas canvas, ExtremeLineData ex) {
    void drawLines(List<double> prices, Color color) {
      final paint = Paint()
        ..color = color.withOpacity(ex.opacity)
        ..strokeWidth = 1;
      for (final price in prices) {
        final y = _priceToY(price);
        if (y < marginTop || y > marginTop + chartHeight) continue;
        _drawDashedLine(canvas, Offset(marginLeft, y),
            Offset(chartRight - rightLabelMargin, y), paint);
      }
    }

    drawLines(ex.topPrices, ex.topColor);
    drawLines(ex.valleyPrices, ex.valleyColor);
  }

  /// Níveis do Pivot Tradicional (issue #14): linhas horizontais sólidas
  /// (diferente do tracejado de topos/vales) com cores próprias — P cinza,
  /// R ciano, S roxo — e tag com a chave do nível.
  void _drawPivotLines(Canvas canvas) {
    for (final pv in data.pivotLayers) {
      if (pv.visible) _drawPivotGroup(canvas, pv);
    }
  }

  void _drawPivotGroup(Canvas canvas, PivotLineData pv) {
    void drawLevel(String key, double price, Color color) {
      final y = _priceToY(price);
      if (y < marginTop || y > marginTop + chartHeight) return;
      final paint = Paint()
        ..color = color.withOpacity(pv.opacity)
        ..strokeWidth = 1.2;
      canvas.drawLine(Offset(marginLeft, y),
          Offset(chartRight - rightLabelMargin, y), paint);

      final tp = TextPainter(
        text: TextSpan(
          text: key,
          style: TextStyle(
            color: color.withOpacity(pv.opacity),
            fontSize: 9,
            fontWeight: FontWeight.bold,
          ),
        ),
        textDirection: TextDirection.ltr,
      )..layout();
      tp.paint(canvas, Offset(marginLeft + 4, y - tp.height - 1));
    }

    if (pv.pivot.isFinite) {
      drawLevel('P', pv.pivot, const Color(0xFFBDBDBD));
    }
    for (int i = 0; i < pv.resistances.length; i++) {
      drawLevel('R${i + 1}', pv.resistances[i], const Color(0xFF4DD0E1));
    }
    for (int i = 0; i < pv.supports.length; i++) {
      drawLevel('S${i + 1}', pv.supports[i], const Color(0xFFCE93D8));
    }
  }

  void _drawVwapLines(Canvas canvas) {
    final vwapPoints = data.vwapPoints;
    if (vwapPoints.isEmpty) return;

    final m = controller.value;
    final scaleX = m[0];
    final tx = m.getTranslation().x;

    final paint = Paint()
      ..color = data.vwapColor.withOpacity(data.vwapOpacity)
      ..strokeWidth = 1.5
      ..style = PaintingStyle.stroke;

    Offset? prev;
    DateTime? prevDay;
    for (final vwap in vwapPoints) {
      final y = _priceToY(vwap.price);
      if (y < marginTop || y > marginTop + chartHeight) {
        prev = null;
        continue;
      }

      final currentDay =
          DateTime(vwap.date.year, vwap.date.month, vwap.date.day);
      if (prevDay != null && currentDay != prevDay) {
        prev = null;
      }
      prevDay = currentDay;

      final x = marginLeft + vwap.startCandleIndex * candleStep * scaleX + tx;
      final pt = Offset(x, y);

      if (prev != null) {
        canvas.drawLine(prev, pt, paint);
      }
      prev = pt;
    }
  }

  void _drawMarkLine(Canvas canvas) {
    final y = _priceToY(data.lastPrice);

    final paint = Paint()
      ..color = const Color(0xFFaaaaaa)
      ..strokeWidth = 1;
    _drawDashedLine(
        canvas, Offset(marginLeft, y), Offset(chartRight, y), paint);

    final label = data.lastPrice.toStringAsFixed(_decimalPlaces(data.symbol));
    final textPainter = TextPainter(
      text: TextSpan(
        text: label,
        style: const TextStyle(
          color: Color(0xFF444444),
          fontSize: 10,
          backgroundColor: Color(0xFFaaaaaa),
        ),
      ),
      textDirection: TextDirection.ltr,
    )..layout();

    final labelRect = Rect.fromLTWH(chartRight - textPainter.width - 8,
        y - textPainter.height / 2, textPainter.width + 8, textPainter.height);
    canvas.drawRRect(
        RRect.fromRectAndRadius(labelRect, const Radius.circular(4)),
        Paint()..color = const Color(0xFFaaaaaa));
    textPainter.paint(canvas,
        Offset(chartRight - textPainter.width - 4, y - textPainter.height / 2));
  }

  void _drawHoverXLabel(Canvas canvas, int index) {
    final m = controller.value;
    final tx = m.getTranslation().x;
    final scaleX = m[0];

    final childX = index * candleStep + candleStep / 2;
    final screenX = marginLeft + childX * scaleX + tx;
    if (screenX < marginLeft || screenX > chartRight) return;

    final d = data.dates[index];
    final label = data.timeFrame == 1440
        ? '${d.day.toString().padLeft(2, '0')}/${d.month.toString().padLeft(2, '0')}/${d.year}'
        : '${d.hour.toString().padLeft(2, '0')}:${d.minute.toString().padLeft(2, '0')}';
    final y = marginTop + chartHeight + bottomLabelHeight / 2;

    final tp = TextPainter(
      text: TextSpan(
        text: label,
        style: const TextStyle(
          color: Color(0xFF444444),
          fontSize: 10,
        ),
      ),
      textDirection: TextDirection.ltr,
    )..layout();

    final labelRect = Rect.fromLTWH(
      screenX - tp.width / 2 - 4,
      y - tp.height / 2 - 2,
      tp.width + 8,
      tp.height + 4,
    );
    canvas.drawRRect(
      RRect.fromRectAndRadius(labelRect, const Radius.circular(4)),
      Paint()..color = const Color(0xFFaaaaaa),
    );
    tp.paint(canvas, Offset(screenX - tp.width / 2, y - tp.height / 2));
  }

  void _drawHoverLine(Canvas canvas, double chartY) {
    final canvasY = marginTop + chartY;
    final tick = Defaults.tickSize(data.symbol);
    final price = _snapToTick(_yToPrice(canvasY), tick);

    final paint = Paint()
      ..color = const Color(0xFFaaaaaa)
      ..strokeWidth = 1;
    _drawDashedLine(canvas, Offset(marginLeft, canvasY),
        Offset(chartRight, canvasY), paint);

    final label = price.toStringAsFixed(_decimalPlaces(data.symbol));
    final textPainter = TextPainter(
      text: TextSpan(
        text: label,
        style: const TextStyle(
          color: Color(0xFF444444),
          fontSize: 10,
          backgroundColor: Color(0xFFaaaaaa),
        ),
      ),
      textDirection: TextDirection.ltr,
    )..layout();

    final labelRect = Rect.fromLTWH(
        chartRight - textPainter.width - 8,
        canvasY - textPainter.height / 2,
        textPainter.width + 8,
        textPainter.height);
    canvas.drawRRect(
        RRect.fromRectAndRadius(labelRect, const Radius.circular(4)),
        Paint()..color = const Color(0xFFaaaaaa));
    textPainter.paint(
        canvas,
        Offset(chartRight - textPainter.width - 4,
            canvasY - textPainter.height / 2));
  }

  void _drawWatermark(Canvas canvas, Size size) {
    final tfLabel = data.timeFrame == 1440 ? '1D' : '${data.timeFrame}min';
    final centerText = '${data.symbol}\n$tfLabel';
    final centerTp = TextPainter(
      text: TextSpan(
        text: centerText,
        style: const TextStyle(
          color: Color(0x13c8c8c8),
          fontSize: 60,
          fontWeight: FontWeight.bold,
        ),
      ),
      textAlign: TextAlign.center,
      textDirection: TextDirection.ltr,
    )..layout();
    centerTp.paint(
        canvas,
        Offset((marginLeft + chartRight) / 2 - centerTp.width / 2,
            marginTop + chartHeight / 2 - centerTp.height / 2));

    if (data.remainingSeconds > 0) {
      final min = data.remainingSeconds ~/ 60;
      final sec = data.remainingSeconds % 60;
      final timeStr =
          '${min.toString().padLeft(2, '0')}:${sec.toString().padLeft(2, '0')}';
      final timeTp = TextPainter(
        text: TextSpan(
          text: '🕒 $timeStr',
          style: const TextStyle(
            color: Color(0x8cc8c8c8),
            fontSize: 11,
            fontWeight: FontWeight.bold,
          ),
        ),
        textDirection: TextDirection.ltr,
      )..layout();
      timeTp.paint(
          canvas,
          Offset(size.width - marginRight - timeTp.width,
              marginTop + chartHeight - timeTp.height));
    }
  }

  void _drawPositionLines(Canvas canvas) {
    for (final pos in data.positions) {
      final y = _priceToY(pos.priceOpen);
      if (y < marginTop || y > marginTop + chartHeight) continue;

      final isBuy = pos.type == 'buy';
      final color = isBuy ? tradeBuyColor : tradeSellColor;

      final glow = Paint()
        ..color = color.withValues(alpha: 0.12)
        ..strokeWidth = 5;
      canvas.drawLine(Offset(marginLeft, y), Offset(chartRight, y), glow);

      final paint = Paint()
        ..color = color.withValues(alpha: 0.9)
        ..strokeWidth = 1.6;
      canvas.drawLine(Offset(marginLeft, y), Offset(chartRight, y), paint);

      final last = data.lastPrice > 0 ? data.lastPrice : pos.priceCurrent;
      final points = last > 0 ? pos.pointsAt(last) : 0.0;
      _drawTradeChip(
        canvas,
        y: y,
        color: color,
        volume: pos.volume,
        metric: last > 0
            ? formatTradePoints(points, data.symbol, showSign: true)
            : '',
        metricColor: points >= 0 ? tradeBuyColor : tradeSellColor,
        isLoading: closingTickets.contains(pos.ticket),
        closeHovered: hoveredClosePositionTicket == pos.ticket,
      );
    }
  }

  void _drawPositionExitLines(Canvas canvas) {
    // Gain/loss só no intraday e com as ordens em aberto visíveis (mesmo
    // gate do botão C/V usado para o arrasto).
    if (data.timeFrame >= 1440 || !data.openOrdersVisible) return;
    for (final pos in data.positions) {
      // A saída é sempre contrária à entrada: posição comprada fecha com
      // venda (vermelho) e posição vendida fecha com compra (verde).
      final exitColor = pos.type == 'buy' ? tradeSellColor : tradeBuyColor;
      if (pos.tp > 0) {
        _drawPositionExitLine(
          canvas,
          pos: pos,
          price: pos.tp,
          kind: 'tp',
          color: exitColor,
        );
      }
      if (pos.sl > 0) {
        _drawPositionExitLine(
          canvas,
          pos: pos,
          price: pos.sl,
          kind: 'sl',
          color: exitColor,
        );
      }
    }
  }

  void _drawPositionExitLine(
    Canvas canvas, {
    required PositionInfo pos,
    required double price,
    required String kind,
    required Color color,
  }) {
    if (hiddenExitPositionTicket == pos.ticket && hiddenExitKind == kind) {
      return;
    }
    final y = _priceToY(price);
    if (y < marginTop || y > marginTop + chartHeight) return;

    final paint = Paint()
      ..color = color.withValues(alpha: 0.7)
      ..strokeWidth = 1.3;
    _drawDashedLine(
        canvas, Offset(marginLeft, y), Offset(chartRight, y), paint);

    final key = '${pos.ticket}:$kind';
    final hovered =
        hoveredCloseExitTicket == pos.ticket && hoveredCloseExitKind == kind;
    final points = (price - pos.priceOpen).abs();
    _drawTradeChip(
      canvas,
      y: y,
      color: color,
      volume: pos.volume,
      metric: formatTradePoints(points, data.symbol),
      metricColor: color,
      isLoading: cancellingExitKeys.contains(key),
      showClose: true,
      closeHovered: hovered,
    );
  }

  void _drawOrderLines(Canvas canvas) {
    for (final order in data.orders) {
      if (hiddenOrderTickets.contains(order.ticket)) continue;
      final y = _priceToY(order.priceOpen);
      if (y < marginTop || y > marginTop + chartHeight) continue;

      final isBuy = order.type.startsWith('buy');
      final color = isBuy ? tradeBuyColor : tradeSellColor;

      final paint = Paint()
        ..color = color.withValues(alpha: 0.58)
        ..strokeWidth = 1.2;
      _drawDashedLine(
          canvas, Offset(marginLeft, y), Offset(chartRight, y), paint);

      final last = data.lastPrice;
      _drawTradeChip(
        canvas,
        y: y,
        color: color,
        volume: order.volume,
        metric: last > 0
            ? formatTradePoints(order.distancePointsAt(last), data.symbol)
            : '',
        metricColor: const Color(0xFFD0D0D0),
        isLoading: cancellingTickets.contains(order.ticket),
        isOrder: true,
        closeHovered: hoveredCloseOrderTicket == order.ticket,
      );
    }
  }

  void _drawTradeChip(
    Canvas canvas, {
    required double y,
    required Color color,
    required double volume,
    required String metric,
    required Color metricColor,
    required bool isLoading,
    bool isOrder = false,
    bool showClose = true,
    bool closeHovered = false,
  }) {
    final spans = <InlineSpan>[
      TextSpan(
        text: volume.toStringAsFixed(2),
        style: const TextStyle(
          color: Colors.white,
          fontSize: 9.5,
          fontWeight: FontWeight.w700,
          fontFamily: 'Roboto',
        ),
      ),
    ];
    if (metric.isNotEmpty) {
      spans.add(const TextSpan(
        text: '  ',
        style: TextStyle(fontSize: 9.5, fontFamily: 'Roboto'),
      ));
      spans.add(TextSpan(
        text: metric,
        style: TextStyle(
          color: metricColor,
          fontSize: 9.5,
          fontWeight: FontWeight.w600,
          fontFamily: 'Roboto',
        ),
      ));
    }
    final tp = TextPainter(
      text: TextSpan(children: spans),
      textDirection: TextDirection.ltr,
      maxLines: 1,
    )..layout();

    final left = marginLeft + tradeChipLeft;
    final textLeftLocal = showClose
        ? _tradeChipTextLeftLocal
        : tradeChipLeft + tradeChipAccentWidth + tradeChipPaddingLeft;
    final width =
        (textLeftLocal - tradeChipLeft) + tp.width + tradeChipRightPad;
    final top = (y - tradeChipHeight / 2)
        .clamp(marginTop + 1, marginTop + chartHeight - tradeChipHeight - 1)
        .toDouble();
    final rect = Rect.fromLTWH(left, top, width, tradeChipHeight);
    final rrect =
        RRect.fromRectAndRadius(rect, const Radius.circular(tradeChipRadius));

    canvas.drawRRect(
      rrect.shift(const Offset(0, 1.5)),
      Paint()..color = Colors.black.withValues(alpha: 0.35),
    );
    canvas.drawRRect(
      rrect,
      Paint()..color = const Color(0xFF171717).withValues(alpha: 0.94),
    );
    canvas.drawRRect(
      rrect,
      Paint()..color = color.withValues(alpha: isOrder ? 0.10 : 0.16),
    );
    canvas.drawRRect(
      RRect.fromRectAndRadius(
        Rect.fromLTWH(
          left + 1,
          top + 3,
          tradeChipAccentWidth,
          tradeChipHeight - 6,
        ),
        const Radius.circular(2),
      ),
      Paint()..color = color.withValues(alpha: isOrder ? 0.7 : 1),
    );
    canvas.drawRRect(
      rrect,
      Paint()
        ..style = PaintingStyle.stroke
        ..strokeWidth = 1
        ..color = color.withValues(alpha: isOrder ? 0.55 : 0.85),
    );

    if (showClose) {
      _drawTradeChipClose(
        canvas,
        Offset(
          left + (tradeChipCloseCenterLocalX - tradeChipLeft),
          top + tradeChipHeight / 2,
        ),
        isLoading,
        hovered: closeHovered,
      );
    }
    tp.paint(
      canvas,
      Offset(
        left + (textLeftLocal - tradeChipLeft),
        top + (tradeChipHeight - tp.height) / 2,
      ),
    );
  }

  void _drawTradeChipClose(Canvas canvas, Offset center, bool isLoading,
      {bool hovered = false}) {
    if (isLoading) {
      _drawTradeChipSpinner(canvas, center);
      return;
    }
    const radius = tradeChipCloseSize / 2;
    if (hovered) {
      canvas.drawCircle(
        center + const Offset(0, 1.5),
        radius + 2,
        Paint()
          ..color = Colors.black.withValues(alpha: 0.55)
          ..maskFilter = const MaskFilter.blur(BlurStyle.normal, 3),
      );
      canvas.drawCircle(
        center,
        radius + 1.5,
        Paint()..color = Colors.white.withValues(alpha: 0.18),
      );
    }
    canvas.drawCircle(
      center,
      radius,
      Paint()..color = Colors.black.withValues(alpha: hovered ? 0.6 : 0.35),
    );
    final paint = Paint()
      ..color = Colors.white.withValues(alpha: hovered ? 1.0 : 0.8)
      ..strokeWidth = 1.3
      ..strokeCap = StrokeCap.round;
    const m = 3.4;
    canvas.drawLine(
        center + const Offset(-m, -m), center + const Offset(m, m), paint);
    canvas.drawLine(
        center + const Offset(m, -m), center + const Offset(-m, m), paint);
  }

  void _drawTradeChipSpinner(Canvas canvas, Offset center) {
    const radius = tradeChipCloseSize / 2;
    canvas.drawCircle(
      center,
      radius,
      Paint()..color = Colors.grey.withValues(alpha: 0.5),
    );
    final arcPaint = Paint()
      ..color = Colors.white
      ..strokeWidth = 1.6
      ..style = PaintingStyle.stroke
      ..strokeCap = StrokeCap.round;
    final rect = Rect.fromCircle(center: center, radius: radius - 2);
    final startAngle = loadingAnimation * 2 * pi - pi / 2;
    const sweepAngle = 4 * pi / 3;
    canvas.drawArc(rect, startAngle, sweepAngle, false, arcPaint);
  }

  void _drawDashedLine(Canvas canvas, Offset p1, Offset p2, Paint paint) {
    final dx = p2.dx - p1.dx;
    final dy = p2.dy - p1.dy;
    final length = sqrt(dx * dx + dy * dy);
    if (length <= 0) return;
    const dashLen = 6.0;
    const gapLen = 4.0;
    final total = dashLen + gapLen;
    final count = (length / total).floor();

    final ux = dx / length;
    final uy = dy / length;

    for (int i = 0; i < count; i++) {
      final start = Offset(p1.dx + i * total * ux, p1.dy + i * total * uy);
      final end = Offset(start.dx + dashLen * ux, start.dy + dashLen * uy);
      canvas.drawLine(start, end, paint);
    }
  }

  @override
  bool shouldRepaint(covariant ChartFixedPainter oldDelegate) => true;
}
