import 'package:flutter/material.dart';
import 'chart_data.dart';
import 'chart_painter.dart';
import 'chart_fixed_painter.dart';

class MapFlowChart extends StatefulWidget {
  final ChartData data;

  const MapFlowChart({super.key, required this.data});

  @override
  State<MapFlowChart> createState() => _MapFlowChartState();
}

class _MapFlowChartState extends State<MapFlowChart> {
  final TransformationController _controller = TransformationController();

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final data = widget.data;
    if (data.candles.isEmpty) return const SizedBox();

    const candleWidth = 6.0;
    const candleSpacing = 2.0;
    final stepX = candleWidth + candleSpacing;
    final virtualCandleWidth =
        (data.candles.length * stepX).clamp(200, 50000).toDouble();

    return LayoutBuilder(
      builder: (context, constraints) {
        final fullWidth = constraints.maxWidth;
        final fullHeight = constraints.maxHeight;
        final candleAreaWidth =
            fullWidth - ChartFixedPainter.marginLeft - ChartFixedPainter.profileWidth - ChartFixedPainter.marginRight;
        final candleAreaHeight =
            fullHeight - ChartFixedPainter.marginTop - ChartFixedPainter.marginBottom;

        if (candleAreaWidth <= 0 || candleAreaHeight <= 0) {
          return const SizedBox();
        }

        return Stack(
          children: [
            Positioned.fill(
              child: CustomPaint(
                painter: ChartFixedPainter(
                  data: data,
                  candleAreaWidth: candleAreaWidth,
                  candleAreaHeight: candleAreaHeight,
                ),
              ),
            ),
            Positioned(
              left: ChartFixedPainter.marginLeft,
              top: ChartFixedPainter.marginTop,
              width: candleAreaWidth,
              height: candleAreaHeight,
              child: ClipRect(
                child: InteractiveViewer(
                  transformationController: _controller,
                  constrained: false,
                  boundaryMargin: const EdgeInsets.all(double.infinity),
                  minScale: 0.5,
                  maxScale: 5.0,
                  child: CustomPaint(
                    size: Size(virtualCandleWidth, candleAreaHeight),
                    painter: ChartPainter(
                      data: data,
                      candleWidth: candleWidth,
                      candleSpacing: candleSpacing,
                    ),
                  ),
                ),
              ),
            ),
          ],
        );
      },
    );
  }
}
