import 'dart:async';
import 'dart:math';
import 'package:flutter/material.dart';
import 'package:flutter/gestures.dart';
import 'package:flutter/services.dart';
import 'package:provider/provider.dart';
import 'chart_data.dart';
import 'chart_painter.dart';
import 'chart_fixed_painter.dart';
import 'measure_ruler.dart';
import 'strategy_report_overlay.dart';
import '../../../services/trading_service.dart';
import '../../../services/state_service.dart';

class MapFlowChart extends StatefulWidget {
  final ChartData data;

  /// Exibe o overlay do relatório da sessão. Desligado no gráfico diário.
  final bool showStrategyOverlay;
  const MapFlowChart(
      {super.key, required this.data, this.showStrategyOverlay = true});

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
  int? _hoverCandleIndex;
  BubblePoint? _hoveredBubble;
  HistoryDealPoint? _hoveredHistory;
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
  bool _isClamping = false;
  final Set<int> _closingTickets = {};
  final Set<int> _cancellingTickets = {};
  late final AnimationController _loadingCtrl;

  // Régua de medição (issue #20): toggle 📏 + Ctrl+drag medem, drag puro dá pan.
  // O InteractiveViewer aceita todos os botões por padrão, então enquanto
  // mede (`_isMeasuring`) o pan é suprimido via `_panEnabled = false` + o
  // bloqueio de translação em `_onTransformChanged`. Fora da medição o pan
  // esquerdo/direito e o touch seguem livres.
  bool _panEnabled = true;
  bool _isMeasuring = false;
  bool _rulerMode = false;
  int? _measureIdx1;
  int? _measureIdx2;
  double? _measurePrice1;
  double? _measurePrice2;
  Offset? _measureCursorLocal;
  Offset? _measureDownLocal;
  Matrix4? _measureBlockMatrix;
  Offset? _pendingTapDownLocal;

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
    if (_isMeasuring && _measureBlockMatrix != null) {
      // Bloqueia o pan do botão esquerdo durante a medição.
      final cur = _controller.value;
      final blocked = _measureBlockMatrix!;
      final ctx = cur.getTranslation().x;
      final cty = cur.getTranslation().y;
      final btx = blocked.getTranslation().x;
      final bty = blocked.getTranslation().y;
      if (ctx != btx || cty != bty) {
        if (!_isClamping) {
          _isClamping = true;
          final m = cur.clone();
          m.setTranslationRaw(btx, bty, cur.getTranslation().z);
          _controller.value = m;
          _isClamping = false;
          return;
        }
      }
    }
    if (!_isClamping) _clampPan();
    if (mounted) setState(() {});
  }

  void _clampPan() {
    final m = _controller.value;
    final s = m.getMaxScaleOnAxis();
    final tx = m.getTranslation().x;
    final ty = m.getTranslation().y;

    final childW = _lastVirtualCandleWidth;
    final childH = _lastCandleAreaHeight;
    final viewerW = _lastCandleAreaWidth - ChartFixedPainter.rightReserved;
    final viewerH = _lastCandleAreaHeight;

    if (childW <= 0 || childH <= 0 || viewerW <= 0 || viewerH <= 0) return;

    const margin = 200.0;
    final minX = -margin - childW * s;
    final maxX = viewerW + margin;
    final minY = -margin - childH * s;
    final maxY = viewerH + margin;

    final clampedX = tx.clamp(minX, maxX);
    final clampedY = ty.clamp(minY, maxY);

    if (clampedX != tx || clampedY != ty) {
      _isClamping = true;
      m.setTranslationRaw(clampedX, clampedY, 0.0);
      _controller.value = m;
      _isClamping = false;
    }
  }

  @override
  void didUpdateWidget(MapFlowChart oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (widget.data.yZoom != oldWidget.data.yZoom && _yZoom != widget.data.yZoom) {
      _yZoom = widget.data.yZoom;
    }
    if (widget.data.symbol != oldWidget.data.symbol ||
        widget.data.timeFrame != oldWidget.data.timeFrame) {
      _isMeasuring = false;
      _panEnabled = true;
      _clearMeasure();
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
    _hoveredHistory = null;
    _hoverPos = null;
    _hoverY = null;
  }

  void _fitChart(double areaWidth, double virtualWidth, double areaHeight) {
    final viewerW = areaWidth - ChartFixedPainter.rightReserved;
    if (viewerW <= 0) return;
    final leftMargin = viewerW * 0.10;
    final rightMargin = viewerW * 0.15;
    final fitWidth = viewerW - leftMargin - rightMargin;
    final fitScale = max(0.1, min(1.0, fitWidth / virtualWidth));
    _minAllowedScale = fitScale;
    final scale = fitScale;

    final m = Matrix4.identity();
    final tx = (viewerW - virtualWidth * scale) / 2;
    final ty = max(0.0, (areaHeight - areaHeight * scale) / 2);
    m.setTranslationRaw(tx, ty, 0.0);
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
  }

  void _handleScroll(PointerScrollEvent event) {
    // Durante a medição o zoom por roda é ignorado para não deslocar a
    // referência (o bloqueio de translação em `_onTransformChanged`
    // reverteria o ajuste de `ty` do zoom).
    if (_isMeasuring) return;
    final areaH = _lastCandleAreaHeight;
    final range = widget.data.priceRange;
    if (areaH <= 0 || range <= 0) return;

    final m = _controller.value;
    final scaleY = (m[5].isFinite && m[5] > 0) ? m[5] : 1.0;
    final ty = m.getTranslation().y.isFinite ? m.getTranslation().y : 0.0;

    final childY = (event.localPosition.dy - ty) / scaleY;
    final price = _yToPrice(childY, areaH);

    final newZoom =
        (_yZoom * (event.scrollDelta.dy < 0 ? 1.25 : 1 / 1.25)).clamp(0.9, 10.0);
    if (newZoom == _yZoom) return;

    final newChildY = _toChildY(price, areaH, newZoom);
    final newTy = ty + (childY - newChildY) * scaleY;

    setState(() {
      _yZoom = newZoom;
    });

    final mm = Matrix4.copy(_controller.value);
    mm.setTranslationRaw(mm.getTranslation().x, newTy, mm.getTranslation().z);
    _controller.value = mm;
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
    // Esconde o hover (linha horizontal + bubble) enquanto a régua está
    // sendo arrastada para não poluir o tooltip de medição.
    if (_isMeasuring) return;

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

    HistoryDealPoint? hitHistory;
    if (hit == null && data.historyPoints.isNotEmpty) {
      const hitRadius = 11.0;
      for (final hp in data.historyPoints) {
        final hx = hp.candleIndex.clamp(0, data.candles.length - 1) * stepX + stepX / 2;
        final hy = _toChildY(hp.price, candleAreaHeight);
        final dx = childX - hx;
        final dy = childY - hy;
        if (dx * dx + dy * dy <= hitRadius * hitRadius) {
          hitHistory = hp;
          break;
        }
      }
    }

    final candleIndex = (childX / stepX).round().clamp(0, data.candles.length - 1);

    setState(() {
      _hoverY = localY;
      _hoverCandleIndex = candleIndex;
      _hoveredBubble = hit;
      _hoveredHistory = hitHistory;
      _hoverPos = (hit ?? hitHistory) != null ? event.localPosition : null;
    });
  }

  void _handleTap(PointerEvent event, double candleAreaWidth, double candleAreaHeight) {
    final m = _controller.value;
    final scaleY = m[5];
    final ty = m.getTranslation().y;

    const btnSize = 14.0;
    for (final pos in widget.data.positions) {
      if (_closingTickets.contains(pos.ticket)) continue;
      final y = _toChildY(pos.priceOpen, candleAreaHeight);
      final screenPosY = y * scaleY + ty;
      if (Rect.fromLTWH(2, screenPosY - btnSize / 2, btnSize, btnSize).contains(Offset(event.localPosition.dx, event.localPosition.dy))) {
        _closePosition(pos.ticket);
        return;
      }
    }

    for (final order in widget.data.orders) {
      if (_cancellingTickets.contains(order.ticket)) continue;
      final y = _toChildY(order.priceOpen, candleAreaHeight);
      final screenPosY = y * scaleY + ty;
      if (Rect.fromLTWH(2, screenPosY - btnSize / 2, btnSize, btnSize).contains(Offset(event.localPosition.dx, event.localPosition.dy))) {
        _cancelOrder(order.ticket);
        return;
      }
    }
  }

  bool _isLeftMouseDown(PointerDownEvent event) {
    return event.kind == PointerDeviceKind.mouse &&
        (event.buttons & kPrimaryButton) != 0;
  }

  bool _isRightMouseDown(PointerDownEvent event) {
    return event.kind == PointerDeviceKind.mouse &&
        (event.buttons & kSecondaryButton) != 0;
  }

  bool get _isCtrlPressed => HardwareKeyboard.instance.isControlPressed;

  void _toggleRulerMode() {
    setState(() {
      _rulerMode = !_rulerMode;
      // Ao sair do modo régua, limpa medição em andamento/finalizada para
      // não deixar régua órfã sem contexto de modo.
      if (!_rulerMode) {
        _isMeasuring = false;
        _panEnabled = true;
        _clearMeasure();
      }
    });
  }

  int _localToCandleIndex(double localX, double stepX) {
    final m = _controller.value;
    final scaleX = m[0];
    final tx = m.getTranslation().x;
    final childX = (localX - tx) / scaleX;
    return (childX / stepX).round().clamp(0, widget.data.candles.length - 1);
  }

  double _localToPrice(double localY, double candleAreaHeight) {
    final m = _controller.value;
    final scaleY = m[5];
    final ty = m.getTranslation().y;
    final childY = (localY - ty) / scaleY;
    return _yToPrice(childY, candleAreaHeight);
  }

  Offset _dataToLocal(int index, double price, double stepX, double candleAreaHeight) {
    final m = _controller.value;
    final scaleX = m[0];
    final scaleY = m[5];
    final tx = m.getTranslation().x;
    final ty = m.getTranslation().y;
    final childX = index * stepX + stepX / 2;
    final childY = _toChildY(price, candleAreaHeight);
    return Offset(childX * scaleX + tx, childY * scaleY + ty);
  }

  bool _hasMeasure() {
    return _measureIdx1 != null &&
        _measureIdx2 != null &&
        _measurePrice1 != null &&
        _measurePrice2 != null;
  }

  void _clearMeasure() {
    _measureIdx1 = null;
    _measureIdx2 = null;
    _measurePrice1 = null;
    _measurePrice2 = null;
    _measureCursorLocal = null;
    _measureDownLocal = null;
    _measureBlockMatrix = null;
  }

  MeasureResult? _currentMeasure() {
    if (!_hasMeasure()) return null;
    return computeMeasureFromCandles(
      candles: widget.data.candles,
      idx1: _measureIdx1!,
      idx2: _measureIdx2!,
      price1: _measurePrice1!,
      price2: _measurePrice2!,
      timeFrame: widget.data.timeFrame,
    );
  }

  void _onMeasureDown(PointerDownEvent event, double stepX, double candleAreaHeight) {
    final idx = _localToCandleIndex(event.localPosition.dx, stepX);
    final price = _localToPrice(event.localPosition.dy, candleAreaHeight);
    setState(() {
      _measureIdx1 = idx;
      _measureIdx2 = idx;
      _measurePrice1 = price;
      _measurePrice2 = price;
      _measureCursorLocal = event.localPosition;
      _measureDownLocal = event.localPosition;
      _isMeasuring = true;
      _panEnabled = false;
      _measureBlockMatrix = _controller.value.clone();
      // Esconde hover/bubbles para não competir com a régua.
      _hoverY = null;
      _hoverCandleIndex = null;
      _hoveredBubble = null;
      _hoveredHistory = null;
      _hoverPos = null;
    });
  }

  void _onMeasureMove(PointerEvent event, double stepX, double candleAreaHeight) {
    if (!_isMeasuring) return;
    final idx = _localToCandleIndex(event.localPosition.dx, stepX);
    final price = _localToPrice(event.localPosition.dy, candleAreaHeight);
    setState(() {
      _measureIdx2 = idx;
      _measurePrice2 = price;
      _measureCursorLocal = event.localPosition;
    });
  }

  void _onMeasureUp(PointerUpEvent event, double candleAreaHeight) {
    if (!_isMeasuring) return;
    final down = _measureDownLocal;
    final dist = down == null
        ? double.infinity
        : (event.localPosition - down).distance;
    const clickThreshold = 4.0;
    if (dist < clickThreshold) {
      // Clique simples: limpa a régua e repassa como tap (fechar
      // posição/cancelar ordem) para preservar `_handleTap`.
      setState(() {
        _isMeasuring = false;
        _panEnabled = true;
        _clearMeasure();
      });
      _handleTap(event, 0, candleAreaHeight);
    } else {
      // Soltou após arrastar: a régua some junto (só visível durante o drag).
      setState(() {
        _isMeasuring = false;
        _panEnabled = true;
        _clearMeasure();
      });
    }
  }

  void _onMeasureCancel() {
    if (!_isMeasuring) return;
    setState(() {
      _isMeasuring = false;
      _panEnabled = true;
      _clearMeasure();
    });
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

  double _yToPrice(double y, double chartHeight, [double? zoom]) {
    final range = widget.data.priceRange;
    if (range <= 0) return 0;
    final z = zoom ?? _yZoom;
    final padding = range * 0.25;
    final minP = widget.data.minPrice - padding;
    final maxP = widget.data.maxPrice + padding;
    final center = (minP + maxP) / 2;
    final halfRange = (maxP - minP) / 2;
    final zoomedHalf = halfRange / z;
    final adjustedMin = center - zoomedHalf;
    final adjustedMax = center + zoomedHalf;
    final adjustedRange = adjustedMax - adjustedMin;
    if (adjustedRange <= 0) return 0;
    return adjustedMin + ((chartHeight - y) / chartHeight) * adjustedRange;
  }

  double _toChildY(double price, double chartHeight, [double? zoom]) {
    final range = widget.data.priceRange;
    if (range <= 0) return chartHeight / 2;
    final z = zoom ?? _yZoom;
    final padding = range * 0.25;
    final minP = widget.data.minPrice - padding;
    final maxP = widget.data.maxPrice + padding;
    final center = (minP + maxP) / 2;
    final halfRange = (maxP - minP) / 2;
    final zoomedHalf = halfRange / z;
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

        final measure = _currentMeasure();
        Offset? rulerStart;
        Offset? rulerEnd;
        if (measure != null && _hasMeasure()) {
          rulerStart =
              _dataToLocal(_measureIdx1!, _measurePrice1!, stepX, candleAreaHeight);
          rulerEnd =
              _dataToLocal(_measureIdx2!, _measurePrice2!, stepX, candleAreaHeight);
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
                    hoverCandleIndex: _hoverCandleIndex,
                    closingTickets: _closingTickets,
                    cancellingTickets: _cancellingTickets,
                    loadingAnimation: _loadingCtrl.value,
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
                    if (event is KeyDownEvent && event.logicalKey == LogicalKeyboardKey.escape) {
                      if (_hasMeasure() || _isMeasuring) {
                        setState(() {
                          _isMeasuring = false;
                          _panEnabled = true;
                          _clearMeasure();
                        });
                        return KeyEventResult.handled;
                      }
                      if (_rulerMode) {
                        setState(() {
                          _rulerMode = false;
                        });
                        return KeyEventResult.handled;
                      }
                    }
                    return KeyEventResult.ignored;
                  },
                  child: Listener(
                  onPointerDown: (event) {
                    _focusNode.requestFocus();
                    if (_isLeftMouseDown(event)) {
                      // Modo régua (toggle 📏) ou Ctrl+drag medem; drag puro
                      // mantém pan esquerdo original + tap para fechar/cancelar.
                      if (_rulerMode || _isCtrlPressed) {
                        _pendingTapDownLocal = null;
                        _onMeasureDown(event, stepX, candleAreaHeight);
                      } else {
                        // Próximo clique limpa régua anterior (paridade com
                        // "manter visível até o próximo clique/Esc").
                        if (_hasMeasure()) {
                          setState(() {
                            _clearMeasure();
                          });
                        }
                        _pendingTapDownLocal = event.localPosition;
                      }
                    } else if (_isRightMouseDown(event)) {
                      // Botão direito: sempre pan via InteractiveViewer.
                      // Não mede nem fecha posição.
                      _pendingTapDownLocal = null;
                    } else {
                      // Touch/stylus: em modo régua o arrasto mede (acesso
                      // mobile ao recurso); fora dele mantém pan + tap para
                      // fechar/cancelar.
                      if (_rulerMode) {
                        _pendingTapDownLocal = null;
                        _onMeasureDown(event, stepX, candleAreaHeight);
                      } else {
                        if (_hasMeasure()) {
                          setState(() {
                            _clearMeasure();
                          });
                        }
                        _pendingTapDownLocal = event.localPosition;
                      }
                    }
                  },
                  onPointerMove: (event) {
                    if (_isMeasuring) {
                      _onMeasureMove(event, stepX, candleAreaHeight);
                    }
                  },
                  onPointerUp: (event) {
                    if (_isMeasuring) {
                      _onMeasureUp(event, candleAreaHeight);
                    } else if (_pendingTapDownLocal != null) {
                      final dist = (event.localPosition - _pendingTapDownLocal!).distance;
                      _pendingTapDownLocal = null;
                      if (dist < 6.0) {
                        _handleTap(event, candleAreaWidth, candleAreaHeight);
                      }
                      if (mounted) setState(() {});
                    } else if (event.kind == PointerDeviceKind.mouse &&
                        (event.buttons & kSecondaryButton) == 0) {
                      // Soltou o botão esquerdo sem ter medido (ex: clique que
                      // começou fora e terminou dentro): garante pan religado.
                      if (!_panEnabled && mounted) {
                        setState(() {
                          _panEnabled = true;
                          _measureBlockMatrix = null;
                        });
                      }
                    }
                  },
                  onPointerCancel: (_) => _onMeasureCancel(),
                  onPointerSignal: (event) {
                    if (event is PointerScrollEvent) {
                      _handleScroll(event);
                    }
                  },
                  child: MouseRegion(
                    onHover: (e) => _handleHover(e, candleAreaWidth, candleAreaHeight, stepX),
                    onExit: (e) { if (_hoverY != null || _hoveredBubble != null || _hoveredHistory != null) setState(() { _hoverY = null; _hoverCandleIndex = null; _hoveredBubble = null; _hoveredHistory = null; _hoverPos = null; }); },
                    child: InteractiveViewer(
                        transformationController: _controller,
                        constrained: false,
                        boundaryMargin: const EdgeInsets.all(double.infinity),
                        minScale: _minAllowedScale,
                        maxScale: 5.0,
                        panEnabled: _panEnabled,
                        onInteractionEnd: (_) => _onInteractionEnd(),

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
            if (measure != null && rulerStart != null && rulerEnd != null)
              Positioned(
                left: ChartFixedPainter.marginLeft,
                top: ChartFixedPainter.marginTop,
                width: max(0.0, candleAreaWidth - ChartFixedPainter.rightReserved),
                height: candleAreaHeight,
                child: IgnorePointer(
                  child: CustomPaint(
                    painter: _RulerPainter(
                      start: rulerStart,
                      end: rulerEnd,
                      color: measure.isUp ? Colors.green : Colors.red,
                    ),
                  ),
                ),
              ),
            Positioned(
              right: 4.0,
              top: 4.0,
              child: Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Tooltip(
                    message: _rulerMode
                        ? 'Régua ativa — arraste para medir (Esc sai)'
                        : 'Régua de medição (Ctrl+arrastar)',
                    preferBelow: false,
                    child: GestureDetector(
                      onTap: _toggleRulerMode,
                      child: Container(
                        padding: const EdgeInsets.symmetric(
                            horizontal: 6, vertical: 2),
                        decoration: BoxDecoration(
                          color: _rulerMode
                              ? Colors.blue.withValues(alpha: 0.85)
                              : Colors.black54,
                          borderRadius: BorderRadius.circular(4),
                          border: _rulerMode
                              ? Border.all(color: Colors.white70, width: 1)
                              : null,
                        ),
                        child: Text(
                          '📏',
                          style: TextStyle(
                            color: _rulerMode
                                ? Colors.white
                                : Colors.white70,
                            fontSize: 12,
                          ),
                        ),
                      ),
                    ),
                  ),
                  const SizedBox(width: 4),
                  Tooltip(
                    message: 'Press space',
                    preferBelow: false,
                    child: GestureDetector(
                      onTap: _resetChart,
                      child: Container(
                        padding: const EdgeInsets.symmetric(
                            horizontal: 6, vertical: 2),
                        decoration: BoxDecoration(
                          color: Colors.black54,
                          borderRadius: BorderRadius.circular(4),
                        ),
                        child: const Text(
                          '⟲',
                          style:
                              TextStyle(color: Colors.white70, fontSize: 12),
                        ),
                      ),
                    ),
                  ),
                ],
              ),
            ),
            // Relatório da sessão ativa sobre o gráfico (só com sessão
            // armada; altura máxima = altura do gráfico, com rolagem).
            if (widget.showStrategyOverlay)
              StrategyReportOverlay(
                maxHeight: candleAreaHeight - 16,
              ),
          if (_hoveredBubble != null && _hoverPos != null)
            Positioned(
              left: ChartFixedPainter.marginLeft + _hoverPos!.dx + 12,
              top: ChartFixedPainter.marginTop + _hoverPos!.dy - 20,
              child: _buildBubbleTooltip(_hoveredBubble!),
            ),
          if (_hoveredHistory != null && _hoverPos != null)
            Positioned(
              left: ChartFixedPainter.marginLeft + _hoverPos!.dx + 12,
              top: ChartFixedPainter.marginTop + _hoverPos!.dy - 20,
              child: _buildHistoryTooltip(_hoveredHistory!),
            ),
          if (measure != null && _measureCursorLocal != null)
            Positioned(
              left: _measureTooltipLeft(
                  candleAreaWidth, _measureCursorLocal!.dx),
              top: _measureTooltipTop(
                  candleAreaHeight, _measureCursorLocal!.dy),
              child: IgnorePointer(
                child: _buildMeasureTooltip(measure),
              ),
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

  double _measureTooltipLeft(double candleAreaWidth, double cursorDx) {
    // Tooltip à direita do cursor, com clamp para não vazar da área.
    const tooltipWidth = 210.0;
    final viewerW =
        max(0.0, candleAreaWidth - ChartFixedPainter.rightReserved);
    var left =
        ChartFixedPainter.marginLeft + cursorDx + 12;
    if (left + tooltipWidth > ChartFixedPainter.marginLeft + viewerW) {
      left = ChartFixedPainter.marginLeft + cursorDx - tooltipWidth - 12;
    }
    return max(ChartFixedPainter.marginLeft, left);
  }

  double _measureTooltipTop(double candleAreaHeight, double cursorDy) {
    const tooltipHeight = 130.0;
    var top = ChartFixedPainter.marginTop + cursorDy - 20;
    if (top + tooltipHeight >
        ChartFixedPainter.marginTop + candleAreaHeight) {
      top = ChartFixedPainter.marginTop +
          candleAreaHeight -
          tooltipHeight -
          4;
    }
    return max(ChartFixedPainter.marginTop, top);
  }

  Widget _buildMeasureTooltip(MeasureResult m) {
    final color = m.isUp ? Colors.green : Colors.red;
    final symbol = widget.data.symbol;
    return _buildTooltip(
      '${formatPts(m.pts)} ${formatPct(m.pct)}',
      [
        _TooltipRow('Diferença', formatDiferenca(m.diferenca)),
        _TooltipRow('Ponto 1', formatRulerPrice(m.p1, symbol)),
        _TooltipRow('Ponto 2', formatRulerPrice(m.p2, symbol)),
        _TooltipRow('Intervalo', m.intervaloLabel),
        _TooltipRow('Candles', '${m.candles}'),
      ],
      titleColor: color,
    );
  }

  Widget _buildHistoryTooltip(HistoryDealPoint hp) {
    final deal = hp.deal;
    final color = hp.isBuy ? Colors.green : Colors.red;
    return _buildTooltip(
      hp.isBuy ? 'Buy' : 'Sell',
      [
        _TooltipRow('Preço', deal.price.toStringAsFixed(2)),
        _TooltipRow('Hora', deal.time),
        _TooltipRow('Volume', deal.volume.toStringAsFixed(2)),
        _TooltipRow(
            'P/L', '${deal.profit >= 0 ? '+' : ''}${deal.profit.toStringAsFixed(2)}'),
      ],
      titleColor: color,
    );
  }

  Widget _buildTooltip(String title, List<_TooltipRow> rows, {Color? titleColor}) {
    return Container(
      constraints: const BoxConstraints(maxWidth: 210),
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
                  Flexible(
                    child: Text(
                      row.value,
                      style: const TextStyle(color: Colors.white70, fontSize: 10),
                      overflow: TextOverflow.ellipsis,
                    ),
                  ),
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

class _RulerPainter extends CustomPainter {
  final Offset start;
  final Offset end;
  final Color color;

  const _RulerPainter({
    required this.start,
    required this.end,
    required this.color,
  });

  @override
  void paint(Canvas canvas, Size size) {
    final linePaint = Paint()
      ..color = color
      ..strokeWidth = 1.5
      ..style = PaintingStyle.stroke;
    canvas.drawLine(start, end, linePaint);

    final dotFill = Paint()..color = color;
    final dotBorder = Paint()
      ..color = Colors.white
      ..style = PaintingStyle.stroke
      ..strokeWidth = 1.2;
    const r = 4.0;
    canvas.drawCircle(start, r, dotFill);
    canvas.drawCircle(start, r, dotBorder);
    canvas.drawCircle(end, r, dotFill);
    canvas.drawCircle(end, r, dotBorder);
  }

  @override
  bool shouldRepaint(covariant _RulerPainter oldDelegate) {
    return oldDelegate.start != start ||
        oldDelegate.end != end ||
        oldDelegate.color != color;
  }
}
