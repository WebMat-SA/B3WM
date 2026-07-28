import 'dart:math';
import 'package:flutter/material.dart';
import 'chart_data.dart';

class ChartFixedPainter extends CustomPainter {
  final ChartData data;
  final double candleAreaWidth;
  final double candleAreaHeight;
  final double yZoom;
  final TransformationController controller;

  static const double marginLeft = 8;
  static const double marginRight = 8;
  static const double marginTop = 8;
  static const double marginBottom = 24;
  static const double profileWidth = 60;
  static const double candleStep = 8.0;

  static const Color textColor = Color(0xFFaaaaaa);

  ChartFixedPainter({
    required this.data,
    required this.candleAreaWidth,
    required this.candleAreaHeight,
    this.yZoom = 1.0,
    required this.controller,
  });

  double get chartRight => marginLeft + candleAreaWidth;
  double get chartHeight => candleAreaHeight;

  double _priceToY(double price) {
    final range = data.priceRange;
    if (range <= 0) return marginTop + chartHeight / 2;
    final padding = range * 0.25;
    final minP = data.minPrice - padding;
    final maxP = data.maxPrice + padding;
    final center = (minP + maxP) / 2;
    final halfRange = (maxP - minP) / 2;
    final zoomedHalf = halfRange / yZoom;
    final adjustedMin = center - zoomedHalf;
    final adjustedMax = center + zoomedHalf;
    final adjustedRange = adjustedMax - adjustedMin;
    if (adjustedRange <= 0) return marginTop + chartHeight / 2;
    return marginTop + chartHeight - ((price - adjustedMin) / adjustedRange) * chartHeight;
  }

  double _yToPrice(double y) {
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
    return adjustedMin + ((marginTop + chartHeight - y) / chartHeight) * adjustedRange;
  }

  @override
  void paint(Canvas canvas, Size size) {
    if (data.candles.isEmpty) {
      return;
    }
    _drawBackground(canvas, size);
    _drawYAxis(canvas);
    _drawXAxis(canvas);
    _drawVolumeProfile(canvas, size);
    _drawMarkLine(canvas);
    _drawWatermark(canvas);
  }

  void _drawBackground(Canvas canvas, Size size) {
    final paint = Paint()..color = const Color(0xFF1e1e1e);
    canvas.drawRect(Rect.fromLTWH(0, 0, size.width, size.height), paint);

    final chartPaint = Paint()..color = const Color(0xFF252525);
    canvas.drawRect(
        Rect.fromLTWH(marginLeft, marginTop, candleAreaWidth, chartHeight), chartPaint);
  }

  void _drawYAxis(Canvas canvas) {
    const lines = 8;
    for (int i = 0; i <= lines; i++) {
      final y = marginTop + (chartHeight / lines) * i;
      final price = _yToPrice(y);
      final label = price.round().toString();

      final tp = TextPainter(
        text: TextSpan(text: label, style: const TextStyle(color: textColor, fontSize: 9)),
        textDirection: TextDirection.ltr,
      )..layout();
      tp.paint(canvas, Offset(marginLeft + 2, y - tp.height / 2));
    }

    for (int i = 0; i <= lines; i++) {
      final y = marginTop + (chartHeight / lines) * i;
      final price = _yToPrice(y);
      final label = price.round().toString();

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

    final profileLeft = chartRight + 2;
    final profileRight = size.width - marginRight;
    final profileW = (profileRight - profileLeft) * data.profileSizeH;

    double maxVol = 0;
    for (final v in data.volumeProfile) {
      if (v.total > maxVol) maxVol = v.total;
    }
    if (maxVol <= 0) return;

    for (final v in data.volumeProfile) {
      final y = _priceToY(v.price);
      final barHeight = (v.step / data.priceRange) * chartHeight * data.profileSizeV;
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

  void _drawMarkLine(Canvas canvas) {
    final y = _priceToY(data.lastPrice);

    final paint = Paint()
      ..color = const Color(0xFFaaaaaa)
      ..strokeWidth = 1;
    _drawDashedLine(canvas, Offset(marginLeft, y), Offset(chartRight, y), paint);

    final label = data.lastPrice.toStringAsFixed(2);
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

  void _drawWatermark(Canvas canvas) {
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
            fontSize: 14,
            fontWeight: FontWeight.bold,
          ),
        ),
        textDirection: TextDirection.ltr,
      )..layout();
      timeTp.paint(
          canvas,
          Offset(
              chartRight - timeTp.width - 8,
              marginTop + chartHeight - timeTp.height - 4));
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
