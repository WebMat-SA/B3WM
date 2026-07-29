import 'dart:async';
import 'dart:math';
import 'package:flutter/material.dart';
import 'package:flutter/gestures.dart';
import 'package:flutter/services.dart';
import 'package:provider/provider.dart';
import 'chart_data.dart';
import 'chart_painter.dart';
import 'chart_fixed_painter.dart';
import '../../../services/trading_service.dart';
import '../../../services/state_service.dart';

class MapFlowChart extends StatefulWidget {
  final ChartData data;
  const MapFlowChart({super.key, required this.data});

  @override
  // ignore: library_private_types_in_public_api
  _MapFlowChartState createState() => _MapFlowChartState();
}

class _MapFlowChartState extends State<MapFlowChart>
    with SingleTickerProviderStateMixin {
  final TransformationController _controller = TransformationController();
  final FocusNode _focusNode = FocusNode();
  double _yZoom = 1.0;
  double? _hoverY;
  BubblePoint? _hoveredBubble;
  Offset? _hoverPos;
  bool _initialFitDone = false;
  double _lastCandleAreaWidth = 0;
  double _lastVirtualCandleWidth = 0;
  double _lastFitAreaWidth = 0;
  double _lastCandleAreaHeight = 0;
  Matrix4? _gestureStartMatrix;
  double _gestureStartScale = 1;
  Offset _gestureAccumDelta = Offset.zero;
  double _minAllowedScale = 0.7;
  final Set<int> _closingTickets = {};
  final Set<int> _cancellingTickets = {};
  late final AnimationController _loadingCtrl;

  @override
  void initState() {
    super.initState();
    _yZoom = widget.data.yZoom;
    _controller.addListener(_onTransformChanged);
    _loadingCtrl = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 800),
    )..addListener(() {
      if (mounted) setState(() {});
    });
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
    if (widget.data.candles.length > oldWidget.data.candles.length && _initialFitDone) {
      final areaW = _lastCandleAreaWidth;
      if (areaW > 0) {
        final viewerW = areaW - ChartFixedPainter.rightReserved;
        if (viewerW <= 0) return;
        const stepX = 6.0 + 2.0;
        final vw = (widget.data.candles.length * stepX).clamp(200, 50000).toDouble();
        final t = _controller.value.getTranslation();
        final tx = t.x;
        final ty = t.y;
        final s = _controller.value[0];
        final rightOffset = viewerW - (tx + vw * s);
        const defaultRightMargin = 0.15;
        if (rightOffset < viewerW * defaultRightMargin) {
          final newTx = viewerW * (1 - defaultRightMargin) - vw * s;
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
    _loadingCtrl.dispose();
    super.dispose();
  }

  void _dismissTooltip() {
    _hoveredBubble = null;
    _hoverPos = null;
    _hoverY = null;
  }

  void _fitChart(double areaWidth, double virtualWidth, double areaHeight) {
    final viewerW = areaWidth - ChartFixedPainter.rightReserved;
    if (viewerW <= 0) return;
    final leftMargin = viewerW * 0.02;
    final rightMargin = viewerW * 0.15;
    final fitWidth = viewerW - leftMargin - rightMargin;
    final fitScale = max(0.1, min(1.0, fitWidth / virtualWidth));
    _minAllowedScale = max(0.7, fitScale);
    final scale = _minAllowedScale;

    final m = Matrix4.identity();
    final tx = (viewerW - rightMargin) - virtualWidth * scale;
    m.setTranslationRaw(tx, 0.0, 0.0);
    m[0] = scale;
    m[5] = scale;
    _controller.value = m;
  }

  void _resetChart() {
    setState(() {
      _yZoom = 1.0;
    });
    _fitChart(_lastCandleAreaWidth, _lastVirtualCandleWidth, _lastCandleAreaHeight);
  }

  void _onInteractionEnd() {
    _centerLastCandleY();
  }

  void _handleScroll(PointerScrollEvent event) {
    setState(() {
      _yZoom = (_yZoom * (event.scrollDelta.dy < 0 ? 1.25 : 1 / 1.25)).clamp(0.9, 10.0);
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
    _controller.value = m;
  }

  void _handleHover(PointerEvent event, double candleAreaWidth, double candleAreaHeight, double stepX) {
    if (!_initialFitDone) return;

    final localX = event.localPosition.dx;
    final localY = event.localPosition.dy;

    final m = _controller.value;
    final scaleX = m[0];
    final scaleY = m[5];
    final tx = m.getTranslation().x;
    final ty = m.getTranslation().y;

    final childX = (localX - tx) / scaleX;
    final childY = (localY - ty) / scaleY;

    final data = widget.data;

    BubblePoint? hit;

    void checkBubbles(List<BubblePoint> bubbles) {
      for (final b in bubbles) {
        final bx = b.candleIndex.clamp(0, data.candles.length - 1) * stepX + stepX / 2;
        final by = _toChildY(b.price, candleAreaHeight);
        var r = sqrt(b.amount) * 1.5;
        r = r.clamp(data.bubbleSizeMin, data.bubbleSizeMax);
        if (r < 2) continue;
        final dx = childX - bx;
        final dy = childY - by;
        if (dx * dx + dy * dy <= r * r) {
          hit = b;
          return;
        }
      }
    }

    checkBubbles(data.blueBubbles);
    if (hit == null) checkBubbles(data.redBubbles);

    setState(() {
      _hoverY = localY;
      _hoveredBubble = hit;
      _hoverPos = hit != null ? event.localPosition : null;
    });
  }

  void _handleTap(PointerDownEvent event, double candleAreaWidth, double candleAreaHeight) {
    final m = _controller.value;
    final childX = (event.localPosition.dx - m.getTranslation().x) / m[0];
    final childY = (event.localPosition.dy - m.getTranslation().y) / m[5];

    const btnSize = 14.0;
    for (final pos in widget.data.positions) {
      if (_closingTickets.contains(pos.ticket)) continue;
      final y = _toChildY(pos.priceOpen, candleAreaHeight);
      if (Rect.fromLTWH(2, y - btnSize / 2, btnSize, btnSize).contains(Offset(childX, childY))) {
        _closePosition(pos.ticket);
        return;
      }
    }

    for (final order in widget.data.orders) {
      if (_cancellingTickets.contains(order.ticket)) continue;
      final y = _toChildY(order.priceOpen, candleAreaHeight);
      if (Rect.fromLTWH(2, y - btnSize / 2, btnSize, btnSize).contains(Offset(childX, childY))) {
        _cancelOrder(order.ticket);
        return;
      }
    }
  }

  void _cleanupStaleTickets() {
    bool changed = false;
    final posTickets = widget.data.positions.map((p) => p.ticket).toSet();
    _closingTickets.removeWhere((t) {
      if (!posTickets.contains(t)) {
        changed = true;
        return true;
      }
      return false;
    });
    final ordTickets = widget.data.orders.map((o) => o.ticket).toSet();
    _cancellingTickets.removeWhere((t) {
      if (!ordTickets.contains(t)) {
        changed = true;
        return true;
      }
      return false;
    });
    if (changed) {
      _stopLoadingAnimationIfNeeded();
    }
  }

  Future<void> _closePosition(int ticket) async {
    if (!_closingTickets.add(ticket)) return;
    _startLoadingAnimation();
    setState(() {});
    bool success = false;
    try {
      final api = context.read<TradingApiService>();
      final result = await api.closePosition(ticket);
      if (result != null && mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(result.success
                ? 'Position $ticket closed @ ${result.price.toStringAsFixed(2)}'
                : 'Failed to close: ${result.message}'),
            backgroundColor: result.success ? Colors.green : Colors.red,
          ),
        );
        success = result.success;
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Error: $e'), backgroundColor: Colors.red),
        );
      }
    }
    if (!success && mounted) {
      _closingTickets.remove(ticket);
      _stopLoadingAnimationIfNeeded();
      setState(() {});
    }
  }

  Future<void> _cancelOrder(int ticket) async {
    if (!_cancellingTickets.add(ticket)) return;
    _startLoadingAnimation();
    setState(() {});
    bool success = false;
    try {
      final api = context.read<TradingApiService>();
      final result = await api.cancelOrder(ticket);
      if (result != null && mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(result.success
                ? 'Order $ticket cancelled'
                : 'Failed to cancel: ${result.message}'),
            backgroundColor: result.success ? Colors.green : Colors.red,
          ),
        );
        success = result.success;
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Error: $e'), backgroundColor: Colors.red),
        );
      }
    }
    if (!success && mounted) {
      _cancellingTickets.remove(ticket);
      _stopLoadingAnimationIfNeeded();
      setState(() {});
    }
  }

  void _startLoadingAnimation() {
    if (!_loadingCtrl.isAnimating) _loadingCtrl.repeat();
  }

  void _stopLoadingAnimationIfNeeded() {
    if (_closingTickets.isEmpty && _cancellingTickets.isEmpty) {
      _loadingCtrl.stop();
    }
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

  double _toChildY(double price, double chartHeight) {
    final range = widget.data.priceRange;
    if (range <= 0) return chartHeight / 2;
    final padding = range * 0.25;
    final minP = widget.data.minPrice - padding;
    final maxP = widget.data.maxPrice + padding;
    final center = (minP + maxP) / 2;
    final halfRange = (maxP - minP) / 2;
    final zoomedHalf = halfRange / _yZoom;
    final adjustedMin = center - zoomedHalf;
    final adjustedMax = center + zoomedHalf;
    final adjustedRange = adjustedMax - adjustedMin;
    if (adjustedRange <= 0) return chartHeight / 2;
    return chartHeight - ((price - adjustedMin) / adjustedRange) * chartHeight;
  }

  @override
  Widget build(BuildContext context) {
    _cleanupStaleTickets();
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
                  onPointerDown: (event) {
                    _focusNode.requestFocus();
                    _handleTap(event, candleAreaWidth, candleAreaHeight);
                  },
                  onPointerSignal: (event) {
                    if (event is PointerScrollEvent) {
                      _handleScroll(event);
                    }
                  },
                  child: MouseRegion(
                    onHover: (e) => _handleHover(e, candleAreaWidth, candleAreaHeight, stepX),
                    onExit: (e) { if (_hoverY != null || _hoveredBubble != null) setState(() { _hoverY = null; _hoveredBubble = null; _hoverPos = null; }); },
                    child: InteractiveViewer(
                        transformationController: _controller,
                        constrained: false,
                        boundaryMargin: const EdgeInsets.all(double.infinity),
                        minScale: _minAllowedScale,
                        maxScale: 5.0,
                        onInteractionEnd: (_) => _onInteractionEnd(),

                        child: CustomPaint(
                          size: Size(virtualCandleWidth, candleAreaHeight),
                          painter: ChartPainter(
                            data: data,
                            candleWidth: candleWidth,
                            candleSpacing: candleSpacing,
                            yZoom: _yZoom,
                            closingTickets: _closingTickets,
                            cancellingTickets: _cancellingTickets,
                            loadingAnimation: _loadingCtrl.value,
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
              child: Tooltip(
                message: 'Press space',
                preferBelow: false,
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
          ),
          if (_hoveredBubble != null && _hoverPos != null)
            Positioned(
              left: ChartFixedPainter.marginLeft + _hoverPos!.dx + 12,
              top: ChartFixedPainter.marginTop + _hoverPos!.dy - 20,
              child: _buildBubbleTooltip(_hoveredBubble!),
            ),
        ],
      );
    },
  );
 }

  Widget _buildBubbleTooltip(BubblePoint bubble) {
    final color = bubble.isBuy ? widget.data.colorBuyer : widget.data.colorSeller;
    final dt = bubble.date;
    final timeStr = '${dt.day.toString().padLeft(2, '0')}/${dt.month.toString().padLeft(2, '0')} '
        '${dt.hour.toString().padLeft(2, '0')}:${dt.minute.toString().padLeft(2, '0')}:${dt.second.toString().padLeft(2, '0')}';
    return _buildTooltip(
      bubble.agentName,
      [
        _TooltipRow('Qtd', bubble.originalAmount.toStringAsFixed(0)),
        _TooltipRow('Preço', bubble.price.toStringAsFixed(1)),
        _TooltipRow('Hora', timeStr),
      ],
      titleColor: color,
    );
  }

  Widget _buildTooltip(String title, List<_TooltipRow> rows, {Color? titleColor}) {
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
              style: TextStyle(color: titleColor ?? Colors.white, fontSize: 11, fontWeight: FontWeight.bold),
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
