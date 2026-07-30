import 'dart:math';
import 'package:flutter/material.dart';
import 'chart_data.dart';
import '../../../models/defaults.dart';

class ChartFixedPainter extends CustomPainter {
  final ChartData data;
  final double candleAreaWidth;
  final double candleAreaHeight;
  final double yZoom;
  final TransformationController controller;
  final Set<int> closingTickets;
  final Set<int> cancellingTickets;
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

  final double? hoverY;
  final int? hoverCandleIndex;

  ChartFixedPainter({
    required this.data,
    required this.candleAreaWidth,
    required this.candleAreaHeight,
    this.yZoom = 1.0,
    required this.controller,
    this.hoverY,
    this.hoverCandleIndex,
    this.closingTickets = const {},
    this.cancellingTickets = const {},
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
    final range = data.priceRange;
    if (range <= 0) return chartHeight / 2;
    final padding = range * 0.25;
    final minP = data.minPrice - padding;
    final maxP = data.maxPrice + padding;
    final center = (minP + maxP) / 2;
    final halfRange = (maxP - minP) / 2;
    final zoomedHalf = halfRange / yZoom;
    final adjustedMin = center - zoomedHalf;
    final adjustedMax = center + zoomedHalf;
    final adjustedRange = adjustedMax - adjustedMin;
    if (adjustedRange <= 0) return chartHeight / 2;
    return chartHeight - ((price - adjustedMin) / adjustedRange) * chartHeight;
  }

  double _priceToY(double price) {
    return marginTop + _toChildY(price) * _scaleY + _transY;
  }

  double _yToPrice(double y) {
    final childY = (y - marginTop - _transY) / _scaleY;
    final range = data.priceRange;
    if (range <= 0) return 0;
    final padding = range * 0.25;
    final minP = data.minPrice - padding;
    final maxP = data.maxPrice + padding;
    final center = (minP + maxP) / 2;
    final halfRange = (maxP - minP) / 2;
    final zoomedHalf = halfRange / yZoom;
    final adjustedMin = center - zoomedHalf;
    final adjustedMax = center + zoomedHalf;
    final adjustedRange = adjustedMax - adjustedMin;
    if (adjustedRange <= 0) return 0;
    return adjustedMin + ((chartHeight - childY) / chartHeight) * adjustedRange;
  }

  @override
  void paint(Canvas canvas, Size size) {
    if (data.candles.isEmpty) {
      return;
    }
    _drawBackground(canvas, size);
    _drawGridLines(canvas);
    _drawXAxis(canvas);
    canvas.save();
    canvas.clipRect(Rect.fromLTWH(marginLeft, marginTop, candleAreaWidth, chartHeight));
    _drawVolumeProfile(canvas, size);
    _drawMarkLine(canvas);
    _drawPositionLines(canvas);
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
        Rect.fromLTWH(marginLeft, marginTop, candleAreaWidth, chartHeight), chartPaint);
  }

  void _drawGridLines(Canvas canvas) {
    final paint = Paint()
      ..color = const Color(0x12808080)
      ..strokeWidth = 0.5;

    const lines = 8;
    for (int i = 0; i <= lines; i++) {
      final y = marginTop + (chartHeight / lines) * i;
      canvas.drawLine(Offset(marginLeft, y), Offset(chartRight, y), paint);
    }
  }

  double _snapToTick(double price, double tick) {
    return (price / tick).round() * tick;
  }

  void _drawYAxis(Canvas canvas) {
    const lines = 8;
    final tick = Defaults.tickSize(data.symbol);
    for (int i = 0; i <= lines; i++) {
      final y = marginTop + (chartHeight / lines) * i;
      final price = _snapToTick(_yToPrice(y), tick);
      final label = price.toStringAsFixed(_decimalPlaces(data.symbol));

      final tp = TextPainter(
        text: TextSpan(text: label, style: const TextStyle(color: textColor, fontSize: 9)),
        textDirection: TextDirection.ltr,
      )..layout();
      tp.paint(canvas, Offset(marginLeft + 2, y - tp.height / 2));
    }

    for (int i = 0; i <= lines; i++) {
      if (i == 0 || i == lines) continue;
      final y = marginTop + (chartHeight / lines) * i;
      final price = _snapToTick(_yToPrice(y), tick);
      final label = price.toStringAsFixed(_decimalPlaces(data.symbol));

      final tp = TextPainter(
        text: TextSpan(text: label, style: const TextStyle(color: textColor, fontSize: 9)),
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
    final step = max(1, (count / 10).round());

    for (int i = firstIdx; i <= lastIdx; i += step) {
      final childX = i * candleStep + candleStep / 2;
      final screenX = marginLeft + childX * scaleX + tx;
      if (screenX < marginLeft || screenX > chartRight) continue;

      final fmt = data.dates[i];
      final label =
          '${fmt.hour.toString().padLeft(2, '0')}:${fmt.minute.toString().padLeft(2, '0')}';
      final tp = TextPainter(
        text: TextSpan(text: label, style: const TextStyle(color: textColor, fontSize: 9)),
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

    for (final v in data.volumeProfile) {
      final y = _priceToY(v.price);
      final barHeight = (v.step / data.priceRange) * chartHeight * data.profileSizeV * _scaleY;
      final barWidth = (v.total / maxVol) * profileW;

      final color = v.isPoc
          ? const Color(0xFFFF6400).withOpacity(data.profileOpacity)
          : const Color(0xFF6496ff).withOpacity(data.profileOpacity);

      final paint = Paint()..color = color;
      canvas.drawRect(
          Rect.fromLTWH(
              profileRight - barWidth, y - barHeight / 2, barWidth, max(barHeight, 1.0)),
          paint);

      final borderPaint = Paint()
        ..color = color.withOpacity(data.profileOpacity * 0.6)
        ..style = PaintingStyle.stroke
        ..strokeWidth = 0.5;
      canvas.drawRect(
          Rect.fromLTWH(
              profileRight - barWidth, y - barHeight / 2, barWidth, max(barHeight, 1.0)),
          borderPaint);
    }
  }

  int _decimalPlaces(String symbol) {
    final tick = Defaults.tickSize(symbol);
    if (tick == tick.roundToDouble()) return 0;
    final str = tick.toStringAsFixed(10);
    final dot = str.indexOf('.');
    return str.substring(dot + 1).replaceAll(RegExp(r'0+$'), '').length;
  }

  void _drawMarkLine(Canvas canvas) {
    final y = _priceToY(data.lastPrice);

    final paint = Paint()
      ..color = const Color(0xFFaaaaaa)
      ..strokeWidth = 1;
    _drawDashedLine(canvas, Offset(marginLeft, y), Offset(chartRight, y), paint);

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

    final labelRect = Rect.fromLTWH(
        chartRight - textPainter.width - 8, y - textPainter.height / 2,
        textPainter.width + 8, textPainter.height);
    canvas.drawRRect(
        RRect.fromRectAndRadius(labelRect, const Radius.circular(4)),
        Paint()..color = const Color(0xFFaaaaaa));
    textPainter.paint(canvas, Offset(chartRight - textPainter.width - 4, y - textPainter.height / 2));
  }

  void _drawHoverXLabel(Canvas canvas, int index) {
    final m = controller.value;
    final tx = m.getTranslation().x;
    final scaleX = m[0];

    final childX = index * candleStep + candleStep / 2;
    final screenX = marginLeft + childX * scaleX + tx;
    if (screenX < marginLeft || screenX > chartRight) return;

    final d = data.dates[index];
    final label = '${d.hour.toString().padLeft(2, '0')}:${d.minute.toString().padLeft(2, '0')}';
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
      screenX - tp.width / 2 - 4, y - tp.height / 2 - 2,
      tp.width + 8, tp.height + 4,
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
    _drawDashedLine(canvas, Offset(marginLeft, canvasY), Offset(chartRight, canvasY), paint);

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
        chartRight - textPainter.width - 8, canvasY - textPainter.height / 2,
        textPainter.width + 8, textPainter.height);
    canvas.drawRRect(
        RRect.fromRectAndRadius(labelRect, const Radius.circular(4)),
        Paint()..color = const Color(0xFFaaaaaa));
    textPainter.paint(canvas, Offset(chartRight - textPainter.width - 4, canvasY - textPainter.height / 2));
  }

  void _drawWatermark(Canvas canvas, Size size) {
    final centerText = '${data.symbol}\n${data.timeFrame}min';
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
        Offset(
            (marginLeft + chartRight) / 2 - centerTp.width / 2,
            marginTop + chartHeight / 2 - centerTp.height / 2));

    if (data.remainingSeconds > 0) {
      final min = data.remainingSeconds ~/ 60;
      final sec = data.remainingSeconds % 60;
      final timeStr = '${min.toString().padLeft(2, '0')}:${sec.toString().padLeft(2, '0')}';
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
          Offset(
              size.width - marginRight - timeTp.width,
              marginTop + chartHeight - timeTp.height));
    }
  }

  void _drawPositionLines(Canvas canvas) {
    for (final pos in data.positions) {
      final y = _priceToY(pos.priceOpen);
      if (y < marginTop || y > marginTop + chartHeight) continue;

      final isBuy = pos.type == 'buy';
      final color = isBuy ? Colors.green : Colors.red;

      final paint = Paint()
        ..color = color.withOpacity(0.8)
        ..strokeWidth = 2;
      canvas.drawLine(Offset(marginLeft, y), Offset(chartRight, y), paint);

      _drawCloseButton(canvas, y, Colors.red, closingTickets.contains(pos.ticket));

      const btnWithGap = 20.0;
      final label = '${isBuy ? 'B' : 'S'} ${pos.volume.toStringAsFixed(2)}';
      final tp = TextPainter(
        text: TextSpan(
          text: label,
          style: const TextStyle(
              color: Colors.white, fontSize: 10, fontWeight: FontWeight.bold),
        ),
        textDirection: TextDirection.ltr,
      )..layout();

      final bgRect = Rect.fromLTWH(
        marginLeft + btnWithGap - 4,
        y - tp.height / 2 - 2,
        tp.width + 8,
        tp.height + 4,
      );
      canvas.drawRRect(
        RRect.fromRectAndRadius(bgRect, const Radius.circular(3)),
        Paint()..color = color.withOpacity(0.8),
      );
      tp.paint(canvas, Offset(marginLeft + btnWithGap, y - tp.height / 2));
    }
  }

  void _drawOrderLines(Canvas canvas) {
    for (final order in data.orders) {
      final y = _priceToY(order.priceOpen);
      if (y < marginTop || y > marginTop + chartHeight) continue;

      final isBuy = order.type.startsWith('buy');
      final color = isBuy ? Colors.green : Colors.red;

      final paint = Paint()
        ..color = color.withOpacity(0.6)
        ..strokeWidth = 1.5;
      _drawDashedLine(canvas, Offset(marginLeft, y), Offset(chartRight, y), paint);

      _drawCloseButton(canvas, y, Colors.red, cancellingTickets.contains(order.ticket));

      const btnWithGap = 20.0;
      final typeLabel = _orderTypeShort(order.type);
      final label = '$typeLabel ${order.volume.toStringAsFixed(2)}';
      final tp = TextPainter(
        text: TextSpan(
          text: label,
          style: const TextStyle(color: Colors.white, fontSize: 10),
        ),
        textDirection: TextDirection.ltr,
      )..layout();

      final bgRect = Rect.fromLTWH(
        marginLeft + btnWithGap - 4,
        y - tp.height / 2 - 2,
        tp.width + 8,
        tp.height + 4,
      );
      canvas.drawRRect(
        RRect.fromRectAndRadius(bgRect, const Radius.circular(3)),
        Paint()..color = color.withOpacity(0.6),
      );
      tp.paint(canvas, Offset(marginLeft + btnWithGap, y - tp.height / 2));
    }
  }

  void _drawCloseButton(Canvas canvas, double cy, Color color, bool isLoading) {
    if (isLoading) {
      _drawLoadingSpinner(canvas, cy);
      return;
    }
    const size = 14.0;
    final r = Rect.fromLTWH(marginLeft + 2, cy - size / 2, size, size);
    canvas.drawCircle(r.center, size / 2, Paint()..color = color);
    final xPaint = Paint()
      ..color = Colors.white
      ..strokeWidth = 1.5
      ..strokeCap = StrokeCap.round;
    const m = 4.0;
    canvas.drawLine(Offset(r.left + m, r.top + m), Offset(r.right - m, r.bottom - m), xPaint);
    canvas.drawLine(Offset(r.right - m, r.top + m), Offset(r.left + m, r.bottom - m), xPaint);
  }

  void _drawLoadingSpinner(Canvas canvas, double cy) {
    const size = 14.0;
    final cx = marginLeft + 2.0 + size / 2;
    final center = Offset(cx, cy);
    final bgPaint = Paint()..color = Colors.grey.withOpacity(0.5);
    canvas.drawCircle(center, size / 2, bgPaint);
    final arcPaint = Paint()
      ..color = Colors.white
      ..strokeWidth = 2
      ..style = PaintingStyle.stroke
      ..strokeCap = StrokeCap.round;
    final rect = Rect.fromCircle(center: center, radius: size / 2 - 2);
    final startAngle = loadingAnimation * 2 * pi - pi / 2;
    const sweepAngle = 4 * pi / 3;
    canvas.drawArc(rect, startAngle, sweepAngle, false, arcPaint);
  }

  String _orderTypeShort(String type) {
    switch (type) {
      case 'buy_limit':
        return 'Buy Limit';
      case 'sell_limit':
        return 'Sell Limit';
      case 'buy_stop':
        return 'Buy Stop';
      case 'sell_stop':
        return 'Sell Stop';
      case 'buy_stop_limit':
        return 'Buy S/L';
      case 'sell_stop_limit':
        return 'Sell S/L';
      default:
        return type;
    }
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
