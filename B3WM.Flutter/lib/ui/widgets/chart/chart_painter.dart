import 'dart:math';
import 'package:flutter/material.dart';
import 'chart_data.dart';

class ChartPainter extends CustomPainter {
  final ChartData data;
  final double candleWidth;
  final double candleSpacing;
  final double yZoom;

  /// Razão `scaleY/scaleX` da matriz (1.0 = uniforme). O InteractiveViewer
  /// escala o canvas filho; com zoom só no X (roda), círculos virariam
  /// elipses. Bubbles e marcadores compensam desenhando elipse
  /// (`rx = r*xyAspect`) para seguirem redondos na tela. Com escala uniforme
  /// o resultado é idêntico ao `drawCircle` anterior.

  final double xyAspect;

  /// Índice do candle sob o cursor (eixo X). Quando não nulo, esse candle é
  /// desenhado ligeiramente maior (+25% de largura) mantendo o centro em X,
  /// com pavio e borda mais destacados.
  final int? hoverCandleIndex;

  static const Color candleUpColor = Color(0xFFFFFFFF);
  static const Color candleDownColor = Color(0xFF000000);
  static const Color candleBorderColor = Color(0xFFFFFFFF);
  static const Color daySepColor = Color(0x26c8c8c8);

  ChartPainter({
    required this.data,
    this.candleWidth = 6.0,
    this.candleSpacing = 2.0,
    this.yZoom = 1.0,
    this.xyAspect = 1.0,
    this.hoverCandleIndex,
  });

  @override
  void paint(Canvas canvas, Size size) {
    if (data.candles.isEmpty) return;

    final chartHeight = size.height;
    final chartWidth = size.width;

    if (chartWidth <= 0 || chartHeight <= 0) return;

    final stepX = candleWidth + candleSpacing;

    _drawDaySeparators(canvas, size, chartWidth, chartHeight, stepX);
    _drawMarkArea(canvas, size, chartWidth, chartHeight, stepX);
    _drawStructureLines(canvas, size, chartWidth, chartHeight, stepX);
    _drawCandles(canvas, size, chartWidth, chartHeight, stepX);
    _drawBubbles(canvas, size, chartWidth, chartHeight, stepX);
    _drawHistoryMarkers(canvas, size, chartWidth, chartHeight, stepX);
  }

  double _priceToY(double price, double chartHeight) {
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

  double _indexToX(int index, double stepX) {
    return index * stepX + stepX / 2;
  }

  void _drawDaySeparators(
      Canvas canvas, Size size, double chartWidth, double chartHeight, double stepX) {
    // Moved to ChartFixedPainter to fill full visible height without clipping
  }

  void _drawMarkArea(
      Canvas canvas, Size size, double chartWidth, double chartHeight, double stepX) {
    // Moved to ChartFixedPainter to fill full visible height without clipping
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

    drawLine(s.upBorder, const Color(0xFF9696ff), false);
    drawLine(s.downBorder, const Color(0xFFff9696), false);

    if (s.auxVisible) {
      drawLine(s.upAuxBorder, const Color(0xFF5555aa), true);
      drawLine(s.downAuxBorder, const Color(0xFFaa5555), true);
    }
  }

  // Note: extreme lines and VWAP are drawn only by ChartFixedPainter
  // (full-width, screen-space with pan/zoom applied) to avoid double-draw.

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

  void _drawCandles(
      Canvas canvas, Size size, double chartWidth, double chartHeight, double stepX) {
    final bodyWidth = candleWidth * 0.8;
    // +25% de largura no hover, com teto para não invadir os vizinhos.
    final hoverBodyWidth = min(bodyWidth * 1.25, stepX * 0.95);
    final hoverIndex = (hoverCandleIndex != null &&
            hoverCandleIndex! >= 0 &&
            hoverCandleIndex! < data.candles.length)
        ? hoverCandleIndex!
        : null;

    void drawCandle(int i, {required bool isHover}) {
      final c = data.candles[i];
      final x = _indexToX(i, stepX);
      final yOpen = _priceToY(c.open, chartHeight);
      final yClose = _priceToY(c.close, chartHeight);
      final yHigh = _priceToY(c.high, chartHeight);
      final yLow = _priceToY(c.low, chartHeight);

      if (x < -candleWidth || x > chartWidth + candleWidth) return;

      final isUp = c.isUp;
      final bodyColor = isUp ? candleUpColor : candleDownColor;
      final width = isHover ? hoverBodyWidth : bodyWidth;

      final bodyTop = min(yOpen, yClose);
      final bodyBottom = max(yOpen, yClose);
      final bodyHeight = max(bodyBottom - bodyTop, 1.0);
      final bodyCenter = Offset(x, bodyTop + bodyHeight / 2);
      final bodyRect =
          Rect.fromCenter(center: bodyCenter, width: width, height: bodyHeight);

      if (isHover) {
        // Halo iluminado: branco máximo com blur atrás do candle para
        // "brilhar" além do branco puro (que já é o teto da cor).
        final haloPaint = Paint()
          ..color = const Color(0xFFFFFFFF).withValues(alpha: 0.85)
          ..style = PaintingStyle.stroke
          ..strokeWidth = 4.0
          ..maskFilter = const MaskFilter.blur(BlurStyle.normal, 5.0);
        canvas.drawRect(
          Rect.fromCenter(
              center: bodyCenter, width: width + 2.0, height: bodyHeight + 2.0),
          haloPaint,
        );
        final wickHaloPaint = Paint()
          ..color = const Color(0xFFFFFFFF).withValues(alpha: 0.85)
          ..strokeWidth = 4.0
          ..maskFilter = const MaskFilter.blur(BlurStyle.normal, 5.0);
        canvas.drawLine(Offset(x, yHigh), Offset(x, yLow), wickHaloPaint);
      }

      final wickPaint = Paint()
        ..color = const Color(0xFFFFFFFF)
        ..strokeWidth = isHover ? 2.0 : 1;
      canvas.drawLine(Offset(x, yHigh), Offset(x, yLow), wickPaint);

      final bodyPaint = Paint()..color = bodyColor;
      canvas.drawRect(bodyRect, bodyPaint);

      final borderPaint = Paint()
        ..color = const Color(0xFFFFFFFF)
        ..style = PaintingStyle.stroke
        ..strokeWidth = isHover ? 1.5 : 0.5;
      canvas.drawRect(bodyRect, borderPaint);
    }

    for (int i = 0; i < data.candles.length; i++) {
      if (i == hoverIndex) continue;
      drawCandle(i, isHover: false);
    }
    // Hover por último para ficar por cima em caso de sobreposição leve.
    // O centro em X é preservado pelo `Rect.fromCenter`.
    if (hoverIndex != null) drawCandle(hoverIndex, isHover: true);
  }

  void _drawAspectCircle(Canvas canvas, Offset center, double r, Paint paint) {
    if ((xyAspect - 1.0).abs() < 1e-6) {
      canvas.drawCircle(center, r, paint);
    } else {
      canvas.drawOval(
        Rect.fromCenter(
            center: center, width: 2 * r * xyAspect, height: 2 * r),
        paint,
      );
    }
  }

  void _drawBubbles(
      Canvas canvas, Size size, double chartWidth, double chartHeight, double stepX) {
    for (final b in data.redBubbles) {
      final x = _indexToX(b.candleIndex.clamp(0, data.candles.length - 1), stepX);
      final y = _priceToY(b.price, chartHeight);
      final r = data.bubbleRadius(b.amount);
      if (r < 2) continue;
      final fillPaint = Paint()
        ..color = data.colorSeller.withOpacity(data.bubbleOpacity)
        ..style = PaintingStyle.fill;
      _drawAspectCircle(canvas, Offset(x, y), r, fillPaint);
      final strokeOpacity = (data.bubbleOpacity + 0.3).clamp(0.0, 1.0);
      final strokePaint = Paint()
        ..color = data.colorSeller.withOpacity(strokeOpacity)
        ..style = PaintingStyle.stroke
        ..strokeWidth = 0.1;
      _drawAspectCircle(canvas, Offset(x, y), r, strokePaint);
    }

    for (final b in data.blueBubbles) {
      final x = _indexToX(b.candleIndex.clamp(0, data.candles.length - 1), stepX);
      final y = _priceToY(b.price, chartHeight);
      final r = data.bubbleRadius(b.amount);
      if (r < 2) continue;
      final fillPaint = Paint()
        ..color = data.colorBuyer.withOpacity(data.bubbleOpacity)
        ..style = PaintingStyle.fill;
      _drawAspectCircle(canvas, Offset(x, y), r, fillPaint);
      final strokeOpacity = (data.bubbleOpacity + 0.3).clamp(0.0, 1.0);
      final strokePaint = Paint()
        ..color = data.colorBuyer.withOpacity(strokeOpacity)
        ..style = PaintingStyle.stroke
        ..strokeWidth = 0.1;
      _drawAspectCircle(canvas, Offset(x, y), r, strokePaint);
    }
  }

  void _drawHistoryMarkers(
      Canvas canvas, Size size, double chartWidth, double chartHeight, double stepX) {
    if (data.historyPoints.isEmpty) return;

    for (final hp in data.historyPoints) {
      final x = _indexToX(hp.candleIndex.clamp(0, data.candles.length - 1), stepX);
      if (x < 0 || x > chartWidth) continue;
      final y = _priceToY(hp.price, chartHeight);

      final color = hp.isBuy ? const Color(0xFF4caf50) : const Color(0xFFf44336);
      _drawTriangle(canvas, Offset(x, y), 7.0, hp.isBuy, color);
    }
  }

  void _drawTriangle(Canvas canvas, Offset center, double size, bool pointUp, Color color) {
    const half = 0.866;
    // Compensa o esticamento horizontal do zoom X para o marcador seguir
    // com a mesma forma na tela (idêntico quando a escala é uniforme).
    final sizeX = size * xyAspect;
    final path = Path();
    if (pointUp) {
      path.moveTo(center.dx, center.dy - size);
      path.lineTo(center.dx + sizeX * half, center.dy + size * 0.5);
      path.lineTo(center.dx - sizeX * half, center.dy + size * 0.5);
    } else {
      path.moveTo(center.dx, center.dy + size);
      path.lineTo(center.dx + sizeX * half, center.dy - size * 0.5);
      path.lineTo(center.dx - sizeX * half, center.dy - size * 0.5);
    }
    path.close();

    canvas.drawPath(path, Paint()..color = color);
    canvas.drawPath(
      path,
      Paint()
        ..color = Colors.white
        ..style = PaintingStyle.stroke
        ..strokeWidth = 0.8,
    );
  }

  @override
  bool shouldRepaint(covariant ChartPainter oldDelegate) => true;
}
