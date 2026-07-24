import 'dart:math';
import 'package:flutter/material.dart';
import 'chart_data.dart';

class ChartPainter extends CustomPainter {
  final ChartData data;
  final double candleWidth;
  final double candleSpacing;

  static const Color candleUpColor = Color(0xFFFFFFFF);
  static const Color candleDownColor = Color(0xFF000000);
  static const Color candleBorderColor = Color(0xFFFFFFFF);
  static const Color gridColor = Color(0x08000000);
  static const Color daySepColor = Color(0x26c8c8c8);

  ChartPainter({
    required this.data,
    this.candleWidth = 6.0,
    this.candleSpacing = 2.0,
  });

  @override
  void paint(Canvas canvas, Size size) {
    if (data.candles.isEmpty) return;

    final chartHeight = size.height;
    final chartWidth = size.width;

    if (chartWidth <= 0 || chartHeight <= 0) return;

    final stepX = candleWidth + candleSpacing;

    _drawGridLines(canvas, size, chartWidth, chartHeight);
    _drawDaySeparators(canvas, size, chartWidth, chartHeight, stepX);
    _drawMarkArea(canvas, size, chartWidth, chartHeight, stepX);
    _drawStructureLines(canvas, size, chartWidth, chartHeight, stepX);
    _drawForecastLine(canvas, size, chartWidth, chartHeight, stepX);
    _drawCandles(canvas, size, chartWidth, chartHeight, stepX);
    _drawBubbles(canvas, size, chartWidth, chartHeight, stepX);
    _drawIndicatorLines(canvas, size, chartWidth, chartHeight, stepX);
    _drawIndicatorMarkers(canvas, size, chartWidth, chartHeight, stepX);
  }

  double _priceToY(double price, double chartHeight) {
    final range = data.priceRange;
    if (range <= 0) return chartHeight / 2;
    final padding = range * 0.25;
    final minP = data.minPrice - padding;
    final maxP = data.maxPrice + padding;
    final adjustedRange = maxP - minP;
    if (adjustedRange <= 0) return chartHeight / 2;
    return chartHeight - ((price - minP) / adjustedRange) * chartHeight;
  }

  double _yToPrice(double y, double chartHeight) {
    final range = data.priceRange;
    if (range <= 0) return 0;
    final padding = range * 0.25;
    final minP = data.minPrice - padding;
    final maxP = data.maxPrice + padding;
    final adjustedRange = maxP - minP;
    if (adjustedRange <= 0) return 0;
    return minP + ((chartHeight - y) / chartHeight) * adjustedRange;
  }

  double _indexToX(int index, double stepX) {
    return index * stepX + stepX / 2;
  }

  void _drawGridLines(Canvas canvas, Size size, double chartWidth, double chartHeight) {
    final paint = Paint()
      ..color = gridColor
      ..strokeWidth = 0.5;

    const lines = 8;
    for (int i = 0; i <= lines; i++) {
      final y = (chartHeight / lines) * i;
      canvas.drawLine(Offset(0, y), Offset(chartWidth, y), paint);
    }

    for (int i = 0; i <= 10; i++) {
      final x = (chartWidth / 10) * i;
      canvas.drawLine(Offset(x, 0), Offset(x, chartHeight), paint);
    }
  }

  void _drawDaySeparators(
      Canvas canvas, Size size, double chartWidth, double chartHeight, double stepX) {
    final paint = Paint()
      ..color = daySepColor
      ..strokeWidth = 1;

    for (final idx in data.daySeparatorIndices) {
      final x = _indexToX(idx, stepX);
      if (x >= 0 && x <= chartWidth) {
        canvas.drawLine(Offset(x, 0), Offset(x, chartHeight), paint);

        if (idx < data.dates.length) {
          final d = data.dates[idx];
          final label = '${d.day.toString().padLeft(2, '0')}/${d.month.toString().padLeft(2, '0')}';
          final tp = TextPainter(
            text: TextSpan(
              text: label,
              style: const TextStyle(color: Color(0x66c8c8c8), fontSize: 8),
            ),
            textDirection: TextDirection.ltr,
          )..layout();
          tp.paint(canvas, Offset(x - tp.width / 2, 2));
        }
      }
    }
  }

  void _drawMarkArea(
      Canvas canvas, Size size, double chartWidth, double chartHeight, double stepX) {
    if (data.rangeStart <= 0 && data.rangeEnd >= data.candles.length) return;

    final x1 = _indexToX(data.rangeStart.clamp(0, data.candles.length - 1), stepX) - stepX / 2;
    final x2 = _indexToX(data.rangeEnd.clamp(0, data.candles.length - 1), stepX) + stepX / 2;

    final paint = Paint()..color = const Color(0x08c8c8c8);
    canvas.drawRect(
        Rect.fromLTRB(max(x1, 0), 0, min(x2, chartWidth), chartHeight),
        paint);
  }

  void _drawStructureLines(
      Canvas canvas, Size size, double chartWidth, double chartHeight, double stepX) {
    final s = data.structures;
    if (s == null || !s.visible) return;

    void drawLine(List<double?> vals, Color color, bool dashed) {
      final paint = Paint()
        ..color = color.withOpacity(s.opacity)
        ..strokeWidth = 1;

      Offset? prev;
      for (int i = 0; i < vals.length; i++) {
        final v = vals[i];
        if (v == null) {
          prev = null;
          continue;
        }
        final x = _indexToX(i, stepX);
        if (x < 0 || x > chartWidth) { prev = null; continue; }
        final y = _priceToY(v, chartHeight);
        final pt = Offset(x, y);
        if (prev != null) {
          if (dashed) {
            _drawDashedLine(canvas, prev, pt, paint);
          } else {
            canvas.drawLine(prev, pt, paint);
          }
        }
        prev = pt;
      }
    }

    drawLine(s.upBorder, const Color(0xFF9696ff), true);
    drawLine(s.downBorder, const Color(0xFFff9696), true);

    if (s.auxVisible) {
      drawLine(s.upAuxBorder, const Color(0xFF5555aa), false);
      drawLine(s.downAuxBorder, const Color(0xFFaa5555), false);
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

  void _drawForecastLine(
      Canvas canvas, Size size, double chartWidth, double chartHeight, double stepX) {
    final f = data.forecast;
    if (f == null || !f.visible) return;

    final paint = Paint()
      ..color = const Color(0xFFffaa00)
      ..strokeWidth = 2;

    Offset? prev;
    for (int i = 0; i < f.values.length; i++) {
      final v = f.values[i];
      if (v == null) {
        prev = null;
        continue;
      }
      final x = _indexToX(i, stepX);
      if (x < 0 || x > chartWidth) { prev = null; continue; }
      final y = _priceToY(v, chartHeight);
      final pt = Offset(x, y);
      if (prev != null) canvas.drawLine(prev, pt, paint);
      prev = pt;
    }

    if (f.currentVwap != null) {
      final y = _priceToY(f.currentVwap!, chartHeight);
      final mlPaint = Paint()
        ..color = const Color(0xB3ffaa00)
        ..strokeWidth = 1;

      _drawDashedLine(canvas, Offset(0, y), Offset(chartWidth, y), mlPaint);

      final label = '${f.currentVwap!.toStringAsFixed(2)}';
      final textPainter = TextPainter(
        text: TextSpan(
          text: label,
          style: const TextStyle(color: Colors.white, fontSize: 10, backgroundColor: Color(0xFFffaa00)),
        ),
        textDirection: TextDirection.ltr,
      )..layout();
      textPainter.paint(canvas, Offset(chartWidth - textPainter.width - 4, y - textPainter.height / 2));
    }
  }

  void _drawCandles(
      Canvas canvas, Size size, double chartWidth, double chartHeight, double stepX) {
    final bodyWidth = candleWidth * 0.8;

    for (int i = 0; i < data.candles.length; i++) {
      final c = data.candles[i];
      final x = _indexToX(i, stepX);
      final yOpen = _priceToY(c.open, chartHeight);
      final yClose = _priceToY(c.close, chartHeight);
      final yHigh = _priceToY(c.high, chartHeight);
      final yLow = _priceToY(c.low, chartHeight);

      if (x < -candleWidth || x > chartWidth + candleWidth) continue;

      final isUp = c.isUp;
      final bodyColor = isUp ? candleUpColor : candleDownColor;

      final wickPaint = Paint()
        ..color = candleBorderColor
        ..strokeWidth = 1;
      canvas.drawLine(Offset(x, yHigh), Offset(x, yLow), wickPaint);

      final bodyTop = min(yOpen, yClose);
      final bodyBottom = max(yOpen, yClose);
      final bodyHeight = max(bodyBottom - bodyTop, 1.0);

      final bodyPaint = Paint()..color = bodyColor;
      canvas.drawRect(
          Rect.fromCenter(center: Offset(x, bodyTop + bodyHeight / 2), width: bodyWidth, height: bodyHeight),
          bodyPaint);

      final borderPaint = Paint()
        ..color = candleBorderColor
        ..style = PaintingStyle.stroke
        ..strokeWidth = 0.5;
      canvas.drawRect(
          Rect.fromCenter(center: Offset(x, bodyTop + bodyHeight / 2), width: bodyWidth, height: bodyHeight),
          borderPaint);
    }
  }

  void _drawBubbles(
      Canvas canvas, Size size, double chartWidth, double chartHeight, double stepX) {
    for (final b in data.redBubbles) {
      final x = _indexToX(b.candleIndex.clamp(0, data.candles.length - 1), stepX);
      final y = _priceToY(b.price, chartHeight);
      final r = sqrt(b.amount) * 1.5;
      if (r < 2) continue;
      final fillPaint = Paint()
        ..color = data.colorSeller.withOpacity(data.bubbleOpacity)
        ..style = PaintingStyle.fill;
      canvas.drawCircle(Offset(x, y), r, fillPaint);
      final strokeOpacity = (data.bubbleOpacity + 0.3).clamp(0.0, 1.0);
      final strokePaint = Paint()
        ..color = data.colorSeller.withOpacity(strokeOpacity)
        ..style = PaintingStyle.stroke
        ..strokeWidth = 0.1;
      canvas.drawCircle(Offset(x, y), r, strokePaint);
    }

    for (final b in data.blueBubbles) {
      final x = _indexToX(b.candleIndex.clamp(0, data.candles.length - 1), stepX);
      final y = _priceToY(b.price, chartHeight);
      final r = sqrt(b.amount) * 1.5;
      if (r < 2) continue;
      final fillPaint = Paint()
        ..color = data.colorBuyer.withOpacity(data.bubbleOpacity)
        ..style = PaintingStyle.fill;
      canvas.drawCircle(Offset(x, y), r, fillPaint);
      final strokeOpacity = (data.bubbleOpacity + 0.3).clamp(0.0, 1.0);
      final strokePaint = Paint()
        ..color = data.colorBuyer.withOpacity(strokeOpacity)
        ..style = PaintingStyle.stroke
        ..strokeWidth = 0.1;
      canvas.drawCircle(Offset(x, y), r, strokePaint);
    }
  }

  void _drawIndicatorLines(
      Canvas canvas, Size size, double chartWidth, double chartHeight, double stepX) {
    for (final entry in data.indicatorLines.entries) {
      final line = entry.value;
      if (!line.visible) continue;

      final color = _parseHex(line.color);
      final paint = Paint()
        ..color = color.withOpacity(line.opacity)
        ..strokeWidth = 1;

      Offset? prev;
      for (int i = 0; i < line.values.length; i++) {
        final v = line.values[i];
        if (v == null) {
          prev = null;
          continue;
        }
        final x = _indexToX(i, stepX);
        if (x < 0 || x > chartWidth) { prev = null; continue; }
        final y = _priceToY(v, chartHeight);
        final pt = Offset(x, y);
        if (prev != null) canvas.drawLine(prev, pt, paint);
        prev = pt;
      }
    }
  }

  void _drawIndicatorMarkers(
      Canvas canvas, Size size, double chartWidth, double chartHeight, double stepX) {
    for (final m in data.indicatorMarkers) {
      if (!m.visible) continue;
      final x = _indexToX(m.index, stepX);
      if (x < 0 || x > chartWidth) continue;
      final y = _priceToY(m.value, chartHeight);

      final color = _parseHex(m.color);
      final paint = Paint()..color = color;

      final path = Path()
        ..moveTo(x, y - 8)
        ..lineTo(x - 6, y)
        ..lineTo(x + 6, y)
        ..close();

      canvas.drawPath(path, paint..style = PaintingStyle.fill);
    }
  }

  Color _parseHex(String hex) {
    hex = hex.replaceAll('#', '');
    if (hex.length == 6) hex = 'FF$hex';
    return Color(int.parse(hex, radix: 16));
  }

  @override
  bool shouldRepaint(covariant ChartPainter oldDelegate) => true;
}
