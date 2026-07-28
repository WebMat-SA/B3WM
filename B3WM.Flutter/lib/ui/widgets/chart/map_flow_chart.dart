import 'dart:async';
import 'dart:math';
import 'package:flutter/material.dart';
import 'package:flutter/gestures.dart';
import 'chart_data.dart';
import 'chart_painter.dart';
import 'chart_fixed_painter.dart';

class MapFlowChart extends StatefulWidget {
  final ChartData data;
  const MapFlowChart({super.key, required this.data});

  @override
  // ignore: library_private_types_in_public_api
  _MapFlowChartState createState() => _MapFlowChartState();
}

class _MapFlowChartState extends State<MapFlowChart> {
  final TransformationController _controller = TransformationController();
  double _yZoom = 1.0;
  OverlayEntry? _tooltipOverlay;
  Timer? _tapTimer;
  bool _initialFitDone = false;
  double _lastCandleAreaWidth = 0;
  double _lastVirtualCandleWidth = 0;

  @override
  void initState() {
    super.initState();
    _yZoom = widget.data.yZoom;
    _controller.addListener(_onTransformChanged);
  }

  void _onTransformChanged() {
    if (mounted) setState(() {});
  }

  @override
  void didUpdateWidget(MapFlowChart oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (widget.data.yZoom != oldWidget.data.yZoom && _yZoom != widget.data.yZoom) {
      _yZoom = widget.data.yZoom;
    }
  }

  @override
  void dispose() {
    _dismissTooltip();
    _controller.removeListener(_onTransformChanged);
    _controller.dispose();
    super.dispose();
  }

  void _dismissTooltip() {
    _tapTimer?.cancel();
    _tooltipOverlay?.remove();
    _tooltipOverlay = null;
  }

  void _snapYTransform() {
    final m = _controller.value;
    if (!m.getTranslation().x.isFinite) return;
    final tx = m.getTranslation().x;
    final scaleX = m[0];
    final ty = m.getTranslation().y;
    if (ty != 0.0 || m[5] != 1.0) {
      final corrected = Matrix4.identity();
      corrected.setTranslationRaw(tx, 0.0, 0.0);
      corrected[0] = scaleX;
      _controller.value = corrected;
    }
  }

  void _fitChart(double areaWidth, double virtualWidth) {
    final rightMargin = areaWidth * 0.15;
    final leftMargin = areaWidth * 0.02;
    final fitWidth = areaWidth - leftMargin - rightMargin;
    final scale = max(0.1, fitWidth / virtualWidth);
    final m = Matrix4.identity();
    m.setTranslationRaw(leftMargin, 0.0, 0.0);
    m[0] = scale;
    _controller.value = m;
  }

  void _handleScroll(PointerScrollEvent event) {
    setState(() {
      _yZoom = (_yZoom * (event.scrollDelta.dy < 0 ? 1.25 : 1 / 1.25)).clamp(0.3, 10.0);
    });
  }

  void _handleTap(TapDownDetails details, double candleAreaWidth, double candleAreaHeight, double stepX) {
  }

  void _handleHover(PointerEvent event, double candleAreaWidth, double candleAreaHeight, double stepX) {}

  double _yToPrice(double y, double chartHeight) {
    final range = widget.data.priceRange;
    if (range <= 0) return 0;
    final padding = range * 0.25;
    final minP = widget.data.minPrice - padding;
    final maxP = widget.data.maxPrice + padding;
    final center = (minP + maxP) / 2;
    final halfRange = (maxP - minP) / 2;
    final zoomedHalf = halfRange / _yZoom;
    final adjustedMin = center - zoomedHalf;
    final adjustedMax = center + zoomedHalf;
    final adjustedRange = adjustedMax - adjustedMin;
    if (adjustedRange <= 0) return 0;
    return adjustedMin + ((chartHeight - y) / chartHeight) * adjustedRange;
  }

  @override
  Widget build(BuildContext context) {
    final data = widget.data;
    if (data.candles.isEmpty) {
      return const SizedBox();
    }

    const candleWidth = 6.0;
    const candleSpacing = 2.0;
    const stepX = candleWidth + candleSpacing;
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

        _lastCandleAreaWidth = candleAreaWidth;
        _lastVirtualCandleWidth = virtualCandleWidth;

        if (!_initialFitDone && data.candles.isNotEmpty) {
          _initialFitDone = true;
          WidgetsBinding.instance.addPostFrameCallback((_) {
            if (!mounted) return;
            _fitChart(candleAreaWidth, virtualCandleWidth);
          });
        }

        return Stack(
          fit: StackFit.expand,
          children: [
            Positioned.fill(
              child: CustomPaint(
                painter: ChartFixedPainter(
                  data: data,
                  candleAreaWidth: candleAreaWidth,
                  candleAreaHeight: candleAreaHeight,
                  yZoom: _yZoom,
                  controller: _controller,
                ),
              ),
            ),
            Positioned(
              left: ChartFixedPainter.marginLeft,
              top: ChartFixedPainter.marginTop,
              width: candleAreaWidth,
              height: candleAreaHeight,
              child: ClipRect(
                child: Listener(
                  onPointerSignal: (event) {
                    if (event is PointerScrollEvent) {
                      _handleScroll(event);
                    }
                  },
                  child: MouseRegion(
                    onHover: (e) => _handleHover(e, candleAreaWidth, candleAreaHeight, stepX),
                    child: GestureDetector(
                      onTapDown: (details) => _handleTap(details, candleAreaWidth, candleAreaHeight, stepX),
                      child: InteractiveViewer(
                        transformationController: _controller,
                        constrained: false,
                        boundaryMargin: const EdgeInsets.all(double.infinity),
                        minScale: 0.5,
                        maxScale: 5.0,
                        onInteractionEnd: (_) => _snapYTransform(),
                        child: CustomPaint(
                          size: Size(virtualCandleWidth, candleAreaHeight),
                          painter: ChartPainter(
                            data: data,
                            candleWidth: candleWidth,
                            candleSpacing: candleSpacing,
                            yZoom: _yZoom,
                          ),
                        ),
                      ),
                    ),
                  ),
                ),
              ),
            ),
            Positioned(
              right: 4.0,
              top: 4.0,
              child: GestureDetector(
                onTap: () {
                  setState(() {
                    _yZoom = 1.0;
                  });
                  _fitChart(_lastCandleAreaWidth, _lastVirtualCandleWidth);
                },
                child: Container(
                  padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
                  decoration: BoxDecoration(
                    color: Colors.black54,
                    borderRadius: BorderRadius.circular(4),
                  ),
                  child: const Text(
                    '⟲',
                    style: TextStyle(color: Colors.white70, fontSize: 12),
                  ),
                ),
              ),
            ),
          ],
        );
      },
    );
  }

  Widget _buildTooltip(String title, List<_TooltipRow> rows) {
    return Container(
      constraints: const BoxConstraints(maxWidth: 180),
      padding: const EdgeInsets.all(8),
      decoration: BoxDecoration(
        color: const Color(0xDD2d2d2d),
        borderRadius: BorderRadius.circular(6),
        border: Border.all(color: Colors.white24, width: 0.5),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          Padding(
            padding: const EdgeInsets.only(bottom: 4),
            child: Text(
              title,
              style: const TextStyle(color: Colors.white, fontSize: 11, fontWeight: FontWeight.bold),
            ),
          ),
          for (final row in rows)
            Padding(
              padding: const EdgeInsets.symmetric(vertical: 1),
              child: Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text('${row.label}: ', style: const TextStyle(color: Colors.grey, fontSize: 10)),
                  Text(row.value, style: const TextStyle(color: Colors.white70, fontSize: 10)),
                ],
              ),
            ),
        ],
      ),
    );
  }
}

class _TooltipRow {
  final String label;
  final String value;
  const _TooltipRow(this.label, this.value);
}
