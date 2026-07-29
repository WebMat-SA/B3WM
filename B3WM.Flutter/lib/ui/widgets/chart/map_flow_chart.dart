import 'dart:async';
import 'dart:math';
import 'package:flutter/material.dart';
import 'package:flutter/gestures.dart';
import 'package:flutter/services.dart';
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
  final FocusNode _focusNode = FocusNode();
  double _yZoom = 1.0;
  double? _hoverY;
  OverlayEntry? _tooltipOverlay;
  Timer? _tapTimer;
  bool _initialFitDone = false;
  double _lastCandleAreaWidth = 0;
  double _lastVirtualCandleWidth = 0;
  double _lastFitAreaWidth = 0;
  double _lastCandleAreaHeight = 0;
  bool _isUpdating = false;
  double _lastScale = 0;
  double _lastTy = 0;

  @override
  void initState() {
    super.initState();
    _yZoom = widget.data.yZoom;
    _controller.addListener(_onTransformChanged);
  }

  void _onTransformChanged() {
    if (_isUpdating) return;
    _clampTransform();
    if (mounted) setState(() {});
  }

  void _clampTransform() {
    final areaW = _lastCandleAreaWidth;
    final areaH = _lastCandleAreaHeight;
    final vw = _lastVirtualCandleWidth;
    if (areaW <= 0 || areaH <= 0 || vw <= 0) return;

    final m = Matrix4.copy(_controller.value);
    final s = m.getMaxScaleOnAxis();
    final tx = m.getTranslation().x;
    final ty = m.getTranslation().y;

    final fitTx = areaW * 0.02;
    final rawTxMin = areaW * 0.85 - vw * s;
    final rawTxMax = areaW - ChartFixedPainter.rightReserved - vw * s;
    final txMin = min(rawTxMin, fitTx);
    final txMax = max(rawTxMax, fitTx);
    final clampedTx = tx.clamp(txMin, txMax);

    double clampedTy = ty;
    if (_lastScale > 0) {
      final delta = s / _lastScale;
      if (delta > 1.005 || delta < 0.995) {
        clampedTy = _lastTy;
      } else {
        _lastTy = ty;
      }
    }

    final tyMin = -areaH * s;
    final tyMax = areaH;
    clampedTy = clampedTy.clamp(tyMin, tyMax);

    if (clampedTx != tx || clampedTy != ty) {
      _isUpdating = true;
      m.setTranslationRaw(clampedTx, clampedTy, m.getTranslation().z);
      _controller.value = m;
      _isUpdating = false;
    }

    _lastScale = s;
  }

  @override
  void didUpdateWidget(MapFlowChart oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (widget.data.yZoom != oldWidget.data.yZoom && _yZoom != widget.data.yZoom) {
      _yZoom = widget.data.yZoom;
    }
    if (widget.data.candles.length > oldWidget.data.candles.length && _initialFitDone) {
      final areaW = _lastCandleAreaWidth;
      if (areaW > 0) {
        const stepX = 6.0 + 2.0;
        final vw = (widget.data.candles.length * stepX).clamp(200, 50000).toDouble();
        final t = _controller.value.getTranslation();
        final tx = t.x;
        final ty = t.y;
        final s = _controller.value[0];
        final rightOffset = areaW - (tx + vw * s);
        const defaultRightMargin = 0.15;
        if (rightOffset < areaW * defaultRightMargin) {
          final newTx = areaW * (1 - defaultRightMargin) - vw * s;
          final m = _controller.value.clone();
          m.setTranslationRaw(newTx, ty, 0.0);
          _controller.value = m;
        }
      }
    }
  }

  @override
  void dispose() {
    _dismissTooltip();
    _controller.removeListener(_onTransformChanged);
    _controller.dispose();
    _focusNode.dispose();
    super.dispose();
  }

  void _dismissTooltip() {
    _tapTimer?.cancel();
    _tooltipOverlay?.remove();
    _tooltipOverlay = null;
    if (_hoverY != null) _hoverY = null;
  }

  void _fitChart(double areaWidth, double virtualWidth, double areaHeight) {
    final rightMargin = areaWidth * 0.15;
    final leftMargin = areaWidth * 0.02;
    final fitWidth = areaWidth - leftMargin - rightMargin;
    final scale = max(0.1, min(1.0, fitWidth / virtualWidth));

    final m = Matrix4.identity();
    m.setTranslationRaw(leftMargin, 0.0, 0.0);
    m[0] = scale;
    m[5] = scale;
    _lastScale = scale;
    _lastTy = 0.0;
    _controller.value = m;
  }

  void _resetChart() {
    setState(() {
      _yZoom = 1.0;
    });
    _fitChart(_lastCandleAreaWidth, _lastVirtualCandleWidth, _lastCandleAreaHeight);
  }

  void _handleScroll(PointerScrollEvent event) {
    setState(() {
      _yZoom = (_yZoom * (event.scrollDelta.dy < 0 ? 1.25 : 1 / 1.25)).clamp(0.3, 10.0);
    });
    _centerLastCandleY();
  }

  void _centerLastCandleY() {
    final areaH = _lastCandleAreaHeight;
    if (areaH <= 0) return;
    final range = widget.data.priceRange;
    if (range <= 0) return;

    final padding = range * 0.25;
    final minP = widget.data.minPrice - padding;
    final maxP = widget.data.maxPrice + padding;
    final center = (minP + maxP) / 2;
    final halfRange = (maxP - minP) / 2;
    final zoomedHalf = halfRange / _yZoom;
    final adjustedMin = center - zoomedHalf;
    final adjustedMax = center + zoomedHalf;
    final adjustedRange = adjustedMax - adjustedMin;
    if (adjustedRange <= 0) return;

    final lastPrice = widget.data.lastPrice;
    final yLast = areaH - ((lastPrice - adjustedMin) / adjustedRange) * areaH;

    final m = Matrix4.copy(_controller.value);
    final s = m.getMaxScaleOnAxis();
    final tx = m.getTranslation().x;
    final ty = areaH / 2 - yLast * s;
    m.setTranslationRaw(tx, ty, m.getTranslation().z);
    _lastTy = ty;
    _lastScale = s;
    _isUpdating = true;
    _controller.value = m;
    _isUpdating = false;
  }

  void _handleTap(TapDownDetails details, double candleAreaWidth, double candleAreaHeight, double stepX) {
  }

  void _handleHover(PointerEvent event, double candleAreaWidth, double candleAreaHeight, double stepX) {
    if (!_initialFitDone) return;
    setState(() {
      _hoverY = event.localPosition.dy;
    });
  }

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
            fullWidth - ChartFixedPainter.marginLeft - ChartFixedPainter.marginRight;
        final candleAreaHeight =
            fullHeight - ChartFixedPainter.marginTop - ChartFixedPainter.marginBottom;

        if (candleAreaWidth <= 0 || candleAreaHeight <= 0) {
          return const SizedBox();
        }

        _lastCandleAreaWidth = candleAreaWidth;
        _lastVirtualCandleWidth = virtualCandleWidth;
        _lastCandleAreaHeight = candleAreaHeight;

        if (!_initialFitDone && data.candles.isNotEmpty) {
          _initialFitDone = true;
          WidgetsBinding.instance.addPostFrameCallback((_) {
            if (!mounted) return;
            _fitChart(candleAreaWidth, virtualCandleWidth, candleAreaHeight);
          });
        } else if (_initialFitDone && candleAreaWidth != _lastFitAreaWidth) {
          _lastFitAreaWidth = candleAreaWidth;
          WidgetsBinding.instance.addPostFrameCallback((_) {
            if (!mounted) return;
            _fitChart(candleAreaWidth, virtualCandleWidth, candleAreaHeight);
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
                  hoverY: _hoverY,
                ),
              ),
            ),
            Positioned(
              left: ChartFixedPainter.marginLeft,
              top: ChartFixedPainter.marginTop,
              width: max(0.0, candleAreaWidth - ChartFixedPainter.rightReserved),
              height: candleAreaHeight,
              child: ClipRect(
                child: Focus(
                  focusNode: _focusNode,
                  autofocus: true,
                  onKeyEvent: (node, event) {
                    if (event is KeyDownEvent && event.logicalKey == LogicalKeyboardKey.space) {
                      _resetChart();
                      return KeyEventResult.handled;
                    }
                    return KeyEventResult.ignored;
                  },
                  child: Listener(
                  onPointerSignal: (event) {
                    if (event is PointerScrollEvent) {
                      _handleScroll(event);
                    }
                  },
                  child: MouseRegion(
                    onHover: (e) => _handleHover(e, candleAreaWidth, candleAreaHeight, stepX),
                    onExit: (e) { if (_hoverY != null) setState(() => _hoverY = null); },
                    child: GestureDetector(
                      onTapDown: (details) {
                        _focusNode.requestFocus();
                        _handleTap(details, candleAreaWidth, candleAreaHeight, stepX);
                      },
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
                            yZoom: _yZoom,
                          ),
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
                onTap: _resetChart,
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
