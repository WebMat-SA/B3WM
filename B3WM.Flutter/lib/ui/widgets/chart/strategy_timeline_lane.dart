import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../../services/state_service.dart';
import '../../../services/strategy_service.dart' show b3Wall;
import 'chart_data.dart';
import 'jev_tooltip_data.dart';

/// Cor do ícone ⓘ: a mesma do relatório antigo (comprar azul, vender
/// vermelho, execução laranja, resto cinza).
Color strategyPointColor(StrategyPoint p) {
  if (p.kind == 'execucao') return Colors.orange;
  switch (p.side) {
    case 'comprar':
      return Colors.lightBlue;
    case 'vender':
      return Colors.redAccent;
    case 'lateral':
      return Colors.grey;
    default:
      return Colors.grey;
  }
}

Color _jevSideColor(String side) {
  switch (JevTooltipData.normalizeSide(side)) {
    case 'comprar':
      return Colors.lightBlue;
    case 'vender':
      return Colors.redAccent;
    default:
      return Colors.grey;
  }
}

/// Grupo de chamadas no mesmo candle (toque mostra todas).
class _LaneGroup {
  final int candleIndex;
  final List<StrategyPoint> items;
  _LaneGroup(this.candleIndex, this.items);
}

/// Faixa timeline de estratégias: ícones ⓘ sincronizados com o X dos
/// candles (mesma matemática `childX * scaleX + tx` do controller).
/// Sobrepõe a faixa inferior da área de candles (não mexe na geometria
/// dos painters). Mouse-over abre o tooltip (igual ao bubble info).
class StrategyTimelineLane extends StatefulWidget {
  final List<StrategyPoint> points;
  final TransformationController controller;
  final double width;
  final double stepX;

  /// Notifica o candle sob o ⓘ em hover (null ao sair), para o gráfico
  /// destacar o candle correspondente.
  final ValueChanged<int?>? onHoverCandle;

  const StrategyTimelineLane({
    super.key,
    required this.points,
    required this.controller,
    required this.width,
    this.stepX = 8.0,
    this.onHoverCandle,
  });

  @override
  State<StrategyTimelineLane> createState() => _StrategyTimelineLaneState();
}

class _StrategyTimelineLaneState extends State<StrategyTimelineLane> {
  /// screenX (local) de cada grupo no último paint (p/ hit-test).
  List<({double x, _LaneGroup group})> _layout = [];

  @override
  void initState() {
    super.initState();
    // A faixa dirige o polling das sessões (antes era o overlay).
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) context.read<StateService>().ensureStrategyPolling();
    });
  }

  List<_LaneGroup> get _groups {
    final byIndex = <int, List<StrategyPoint>>{};
    for (final p in widget.points) {
      byIndex.putIfAbsent(p.candleIndex, () => []).add(p);
    }
    final gs = byIndex.entries
        .map((e) => _LaneGroup(e.key, e.value))
        .toList()
      ..sort((a, b) => a.candleIndex.compareTo(b.candleIndex));
    return gs;
  }

  _LaneGroup? _hovered;
  OverlayEntry? _tipEntry;
  Offset? _lastLocal;

  @override
  void dispose() {
    _hideTip();
    super.dispose();
  }

  void _updateHover(Offset local) {
    _LaneGroup? hit;
    var best = 16.0;
    double? hitX;
    for (final e in _layout) {
      // Ícone centrado em y=13 (lane de 26px).
      final dist = (Offset(e.x, 13) - local).distance;
      if (dist < best) {
        best = dist;
        hit = e.group;
        hitX = e.x;
      }
    }
    if (hit != _hovered) {
      _hovered = hit;
      widget.onHoverCandle?.call(hit?.candleIndex);
      if (hit == null) {
        _hideTip();
      } else {
        _showTip(hit, hitX!);
      }
    } else if (hit != null) {
      // Move junto do cursor se o layout mudou sob ele (pan/zoom).
      _showTip(hit, hitX!);
    }
  }

  void _clearHover() {
    _hovered = null;
    _lastLocal = null;
    widget.onHoverCandle?.call(null);
    _hideTip();
  }

  bool _revalidateScheduled = false;

  /// Revalida o hover fora da fase de paint (pan/zoom move os ícones sob
  /// o cursor parado): agenda p/ o próximo frame.
  void _scheduleRevalidate() {
    if (_revalidateScheduled) return;
    _revalidateScheduled = true;
    WidgetsBinding.instance.addPostFrameCallback((_) {
      _revalidateScheduled = false;
      if (!mounted || _lastLocal == null || _hovered == null) return;
      _updateHover(_lastLocal!);
    });
  }

  void _hideTip() {
    _tipEntry?.remove();
    _tipEntry = null;
  }

  /// Tooltip em Overlay (fora do layout): flutua sobre o gráfico e nunca
  /// empurra o splitter — mesmo comportamento visual do bubble info.
  void _showTip(_LaneGroup group, double iconLocalX) {
    if (!mounted) return;
    final box = context.findRenderObject() as RenderBox?;
    if (box == null || !box.hasSize) return;
    final screen = MediaQuery.of(context).size;
    final global = box.localToGlobal(Offset(iconLocalX, 13));
    const cardW = 340.0;
    final maxLeft =
        (screen.width - cardW - 8).clamp(8.0, double.infinity);
    final left = (global.dx + 12).clamp(8.0, maxLeft);

    // Abre preferencialmente acima do ícone; se não houver altura, abaixo.
    const gap = 26.0;
    final spaceAbove = global.dy - gap - 8;
    final spaceBelow = screen.height - global.dy - gap - 8;
    final above = spaceAbove >= spaceBelow || spaceBelow < 120;
    final maxHeight = (above ? spaceAbove : spaceBelow)
        .clamp(80.0, screen.height);
    final top = above ? null : (global.dy + gap).clamp(8.0, screen.height - 8);
    final bottom =
        above ? (screen.height - global.dy + gap).clamp(8.0, screen.height - 8) : null;

    _hideTip();
    _tipEntry = OverlayEntry(
      builder: (_) => Positioned(
        left: left,
        top: top,
        bottom: bottom,
        width: cardW,
        // Material transparente: sem ele o texto no Overlay ganha
        // sublinhado amarelo (estilo default de debug).
        child: IgnorePointer(
          child: Material(
            color: Colors.transparent,
            child: ConstrainedBox(
              constraints: BoxConstraints(maxHeight: maxHeight),
              // Em telas baixas o cartão é cortado com elegância (sem
              // empurrar o layout do gráfico).
              child: SingleChildScrollView(
                physics: const NeverScrollableScrollPhysics(),
                child: _buildHoverCard(group),
              ),
            ),
          ),
        ),
      ),
    );
    Overlay.of(context).insert(_tipEntry!);
  }

  String _hhmm(DateTime at) {
    final w = b3Wall(at);
    return '${w.hour.toString().padLeft(2, '0')}:${w.minute.toString().padLeft(2, '0')}';
  }

  String _ddmmhhmm(DateTime at) {
    final w = b3Wall(at);
    return '${w.day.toString().padLeft(2, '0')}/${w.month.toString().padLeft(2, '0')} ${_hhmm(at)}';
  }

  /// Tooltip do hover (mesmo padrão do bubble info em `map_flow_chart.dart`:
  /// abre no mouse-over, sem click).
  Widget _buildHoverCard(_LaneGroup group) {
    return Container(
      constraints: const BoxConstraints(maxWidth: 340),
      padding: const EdgeInsets.fromLTRB(12, 10, 12, 10),
      decoration: BoxDecoration(
        color: const Color(0xF22B2B2B),
        borderRadius: BorderRadius.circular(10),
        border: Border.all(color: Colors.white24, width: 0.5),
        boxShadow: const [
          BoxShadow(
            color: Colors.black54,
            blurRadius: 10,
            offset: Offset(0, 4),
          ),
        ],
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          for (var i = 0; i < group.items.length; i++) ...[
            if (i > 0)
              const Padding(
                padding: EdgeInsets.symmetric(vertical: 8),
                child: Divider(height: 1, color: Colors.white12),
              ),
            _buildPointDetails(group.items[i]),
          ],
        ],
      ),
    );
  }

  Widget _buildPointDetails(StrategyPoint p) {
    final data = JevTooltipData.fromPoint(p);
    final sideColor =
        data.isJev ? _jevSideColor(data.vies) : strategyPointColor(p);
    final sideLabel = data.isJev
        ? JevTooltipData.sideLabel(data.vies)
        : p.side.toUpperCase();
    final badge = data.isJev
        ? (sideLabel == 'LATERAL'
            ? '$sideLabel · sem sinal'
            : '$sideLabel · confiança '
                '${(data.direcaoConfidence * 100).round()}%')
        : '$sideLabel ${p.confidence.toStringAsFixed(2)}';
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        Row(
          children: [
            Text(
              _ddmmhhmm(p.at),
              style: const TextStyle(
                color: Colors.white,
                fontSize: 12,
                fontWeight: FontWeight.bold,
                decoration: TextDecoration.none,
              ),
            ),
            const Spacer(),
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 7, vertical: 2),
              decoration: BoxDecoration(
                color: sideColor.withValues(alpha: 0.18),
                borderRadius: BorderRadius.circular(4),
                border: Border.all(color: sideColor.withValues(alpha: 0.7)),
              ),
              child: Text(
                badge,
                style: TextStyle(
                  color: sideColor,
                  fontSize: 10,
                  fontWeight: FontWeight.bold,
                  decoration: TextDecoration.none,
                ),
              ),
            ),
          ],
        ),
        const SizedBox(height: 3),
        Text(
          '${p.kind == 'execucao' ? 'EXECUÇÃO' : 'AVALIAÇÃO'}'
          ' · ${_eventLabel(p.event)}'
          '${p.action.isNotEmpty ? ' · ${p.action}' : ''}',
          style: const TextStyle(
            color: Colors.white54,
            fontSize: 9.5,
            letterSpacing: 0.2,
            decoration: TextDecoration.none,
          ),
        ),
        if (data.isJev) ...[
          const SizedBox(height: 8),
          Row(
            children: [
              Expanded(child: _biasTile(data, sideColor)),
              const SizedBox(width: 6),
              Expanded(
                child: _summaryTile(
                  'OPERÁVEL',
                  data.operabilidade,
                  'qualidade do contexto',
                ),
              ),
            ],
          ),
          _skepticStrip(data),
          if (data.proximidade != null)
            Padding(
              padding: const EdgeInsets.only(top: 6),
              child: Row(
                children: [
                  const Text(
                    'Proximidade',
                    style: TextStyle(
                      color: Colors.white54,
                      fontSize: 9.5,
                      decoration: TextDecoration.none,
                    ),
                  ),
                  const Spacer(),
                  Text(
                    '${data.proximidade!.toStringAsFixed(2)}'
                    '${data.proximidadeRef.isEmpty ? '' : ' · ${JevTooltipData.refLabel(data.proximidadeRef)}'}'
                    '${data.proximidadeTicks == null ? '' : ' · ${data.proximidadeTicks!.toStringAsFixed(0)}t'}',
                    style: const TextStyle(
                      color: Colors.white70,
                      fontSize: 9.5,
                      fontWeight: FontWeight.w600,
                      decoration: TextDecoration.none,
                    ),
                  ),
                ],
              ),
            ),
          _indicatorSection(
            'DIREÇÃO',
            data.direction,
            directional: true,
            subtitle: 'baixo = venda · centro = lateral · alto = compra\n'
                '↑/↓ = confiança de alta/baixa · número à direita = saldo',
          ),
          _indicatorSection(
            'OPERABILIDADE',
            data.operability,
            subtitle: '0% = contexto ruim · 100% = favorável',
          ),
        ] else if (p.reason.isNotEmpty) ...[
          const SizedBox(height: 8),
          Text(
            p.reason,
            style: const TextStyle(
              color: Colors.white70,
              fontSize: 10.5,
              height: 1.35,
              fontFamily: 'monospace',
              decoration: TextDecoration.none,
            ),
          ),
        ],
      ],
    );
  }

  Widget _biasTile(JevTooltipData data, Color color) {
    final label = JevTooltipData.sideLabel(data.vies);
    final lateral = label == 'LATERAL';
    final caption = lateral
        ? (data.direcaoMotivo.isEmpty ? 'sem sinal' : data.direcaoMotivo)
        : 'confiança ${(data.direcaoConfidence * 100).round()}%';
    final net = data.direcaoNet;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 6),
      decoration: BoxDecoration(
        color: Colors.white.withValues(alpha: 0.05),
        borderRadius: BorderRadius.circular(6),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          const Text(
            'VIÉS',
            style: TextStyle(
              color: Colors.white54,
              fontSize: 8.5,
              letterSpacing: 0.5,
              decoration: TextDecoration.none,
            ),
          ),
          const SizedBox(height: 2),
          Row(
            crossAxisAlignment: CrossAxisAlignment.end,
            children: [
              Text(
                label,
                style: TextStyle(
                  color: color,
                  fontSize: 14,
                  fontWeight: FontWeight.bold,
                  height: 1,
                  decoration: TextDecoration.none,
                ),
              ),
              const SizedBox(width: 5),
              Expanded(
                child: Padding(
                  padding: const EdgeInsets.only(bottom: 1),
                  child: Text(
                    caption,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                      color: Colors.white60,
                      fontSize: 9,
                      decoration: TextDecoration.none,
                    ),
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: 5),
          _directionBar(net == null ? 0.5 : 0.5 + 0.5 * net,
              height: 3, color: color),
        ],
      ),
    );
  }

  Widget _summaryTile(String label, double value, String? caption) {
    final color = JevTooltipData.qualityColor(value);
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 6),
      decoration: BoxDecoration(
        color: Colors.white.withValues(alpha: 0.05),
        borderRadius: BorderRadius.circular(6),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(
            label,
            style: const TextStyle(
              color: Colors.white54,
              fontSize: 8.5,
              letterSpacing: 0.5,
              decoration: TextDecoration.none,
            ),
          ),
          const SizedBox(height: 2),
          Row(
            crossAxisAlignment: CrossAxisAlignment.end,
            children: [
              Text(
                '${(value * 100).round()}%',
                style: TextStyle(
                  color: color,
                  fontSize: 15,
                  fontWeight: FontWeight.bold,
                  height: 1,
                  decoration: TextDecoration.none,
                ),
              ),
              if (caption != null)
                Expanded(
                  child: Padding(
                    padding: const EdgeInsets.only(left: 5, bottom: 1),
                    child: Text(
                      caption,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(
                        color: Colors.white60,
                        fontSize: 9,
                        decoration: TextDecoration.none,
                      ),
                    ),
                  ),
                ),
            ],
          ),
          const SizedBox(height: 5),
          _valueBar(value, color, height: 3),
        ],
      ),
    );
  }

  Widget _skepticStrip(JevTooltipData data) {
    final net = data.direcaoNet;
    final cons = data.direcaoConsenso;
    final cov = data.direcaoCobertura;
    if (net == null && cons == null && cov == null) {
      return const SizedBox.shrink();
    }
    final netColor = net == null
        ? Colors.white54
        : JevTooltipData.directionColor(0.5 + 0.5 * net);
    final netText = net == null
        ? '—'
        : '${net >= 0 ? '+' : ''}${net.toStringAsFixed(2)}';
    final metrics = <String>[
      if (cons != null) 'CONSENSO ${(cons * 100).round()}%',
      if (cov != null) 'COBERTURA ${(cov * 100).round()}%',
    ].join(' · ');
    final lateral = JevTooltipData.normalizeSide(data.vies) == 'lateral';
    return Padding(
      padding: const EdgeInsets.only(top: 7),
      child: Container(
        padding: const EdgeInsets.fromLTRB(8, 6, 8, 5),
        decoration: BoxDecoration(
          color: Colors.white.withValues(alpha: 0.04),
          borderRadius: BorderRadius.circular(6),
          border: Border.all(color: Colors.white.withValues(alpha: 0.08)),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          mainAxisSize: MainAxisSize.min,
          children: [
            Row(
              children: [
                const Text(
                  'SALDO',
                  style: TextStyle(
                    color: Colors.white54,
                    fontSize: 8.5,
                    letterSpacing: 0.5,
                    decoration: TextDecoration.none,
                  ),
                ),
                const SizedBox(width: 6),
                Text(
                  netText,
                  style: TextStyle(
                    color: netColor,
                    fontSize: 13,
                    fontWeight: FontWeight.bold,
                    height: 1,
                    decoration: TextDecoration.none,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 4),
            _directionBar(net == null ? 0.5 : 0.5 + 0.5 * net, height: 5),
            const SizedBox(height: 3),
            Row(
              children: [
                const Text(
                  'VENDA',
                  style: TextStyle(
                    color: Color(0xFFEF5350),
                    fontSize: 8,
                    fontWeight: FontWeight.bold,
                    decoration: TextDecoration.none,
                  ),
                ),
                const Spacer(),
                if (metrics.isNotEmpty)
                  Text(
                    metrics,
                    style: const TextStyle(
                      color: Colors.white54,
                      fontSize: 8,
                      decoration: TextDecoration.none,
                    ),
                  ),
                const Spacer(),
                const Text(
                  'COMPRA',
                  style: TextStyle(
                    color: Color(0xFF42A5F5),
                    fontSize: 8,
                    fontWeight: FontWeight.bold,
                    decoration: TextDecoration.none,
                  ),
                ),
              ],
            ),
            if (lateral && data.direcaoMotivo.isNotEmpty)
              Padding(
                padding: const EdgeInsets.only(top: 2),
                child: Text(
                  'LATERAL · ${data.direcaoMotivo}',
                  style: const TextStyle(
                    color: Colors.white60,
                    fontSize: 8.5,
                    decoration: TextDecoration.none,
                  ),
                ),
              ),
          ],
        ),
      ),
    );
  }

  Widget _indicatorSection(String title, List<JevIndicator> items,
      {String? subtitle, bool directional = false}) {
    if (items.isEmpty) return const SizedBox.shrink();
    return Padding(
      padding: const EdgeInsets.only(top: 9),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(
            '$title (${items.length})',
            style: const TextStyle(
              color: Colors.white54,
              fontSize: 8.5,
              fontWeight: FontWeight.bold,
              letterSpacing: 0.7,
              decoration: TextDecoration.none,
            ),
          ),
          if (subtitle != null) ...[
            const SizedBox(height: 1),
            Text(
              subtitle,
              style: const TextStyle(
                color: Colors.white38,
                fontSize: 8.5,
                decoration: TextDecoration.none,
              ),
            ),
          ],
          const SizedBox(height: 4),
          for (final item in items)
            _indicatorRow(item, directional: directional),
        ],
      ),
    );
  }

  Widget _indicatorRow(JevIndicator item, {bool directional = false}) {
    final paired = directional && item.net != null;
    final color = directional
        ? (paired
            ? JevTooltipData.directionColor(item.value)
            : (item.bull != null
                ? const Color(0xFF42A5F5)
                : item.bear != null
                    ? const Color(0xFFEF5350)
                    : Colors.white54))
        : JevTooltipData.qualityColor(item.value);
    final label = paired
        ? '${item.label} ↑${(item.bull! * 100).round()} '
            '↓${(item.bear! * 100).round()}'
        : item.bull != null
            ? '${item.label} ↑${(item.bull! * 100).round()}'
            : item.bear != null
                ? '${item.label} ↓${(item.bear! * 100).round()}'
                : item.label;
    final right = directional
        ? paired
            ? '${item.net! >= 0 ? '+' : ''}${(item.net! * 100).round()}'
            : item.bull != null
                ? '↑${(item.bull! * 100).round()}%'
                : item.bear != null
                    ? '↓${(item.bear! * 100).round()}%'
                    : '${(item.value * 100).round()}%'
        : '${(item.value * 100).round()}%';
    final barValue = directional && !paired &&
            (item.bull != null || item.bear != null)
        ? (item.bull ?? item.bear)!
        : item.value;
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 2),
      child: Row(
        children: [
          SizedBox(
            width: 122,
            child: Text(
              label,
              overflow: TextOverflow.ellipsis,
              style: const TextStyle(
                color: Colors.white70,
                fontSize: 10,
                decoration: TextDecoration.none,
              ),
            ),
          ),
          Expanded(
            child: paired
                ? _directionBar(item.value, height: 5)
                : _valueBar(barValue, color, height: 5),
          ),
          const SizedBox(width: 6),
          SizedBox(
            width: 38,
            child: Text(
              right,
              textAlign: TextAlign.right,
              style: TextStyle(
                color: color,
                fontSize: 10,
                fontWeight: FontWeight.bold,
                decoration: TextDecoration.none,
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _directionBar(double value,
      {required double height, Color? color}) {
    final v = value.clamp(0.0, 1.0);
    final fillColor = color ?? JevTooltipData.directionColor(v);
    final delta = (v - 0.5).abs();
    return SizedBox(
      height: height,
      child: LayoutBuilder(
        builder: (context, constraints) {
          final w = constraints.maxWidth;
          final fill = w * delta;
          return Stack(
            children: [
              Positioned.fill(
                child: Container(
                  decoration: BoxDecoration(
                    color: Colors.white.withValues(alpha: 0.09),
                    borderRadius: BorderRadius.circular(height / 2),
                  ),
                ),
              ),
              Positioned(
                left: w / 2 - 0.5,
                top: 0,
                bottom: 0,
                width: 1,
                child: Container(color: Colors.white24),
              ),
              if (fill > 0.5)
                Positioned(
                  left: v >= 0.5 ? w / 2 : w / 2 - fill,
                  top: 0,
                  bottom: 0,
                  width: fill,
                  child: Container(
                    decoration: BoxDecoration(
                      color: fillColor,
                      borderRadius: BorderRadius.circular(height / 2),
                    ),
                  ),
                ),
            ],
          );
        },
      ),
    );
  }

  Widget _valueBar(double value, Color color, {required double height}) {
    final v = value.clamp(0.0, 1.0);
    return Container(
      height: height,
      decoration: BoxDecoration(
        color: Colors.white.withValues(alpha: 0.09),
        borderRadius: BorderRadius.circular(height / 2),
      ),
      child: FractionallySizedBox(
        alignment: Alignment.centerLeft,
        widthFactor: v,
        heightFactor: 1,
        child: Container(
          decoration: BoxDecoration(
            color: color,
            borderRadius: BorderRadius.circular(height / 2),
          ),
        ),
      ),
    );
  }

  String _eventLabel(String event) {
    switch (event) {
      case 'bubble':
        return 'bolha';
      case 'candle_close':
        return 'fechamento';
      case 'exec':
        return 'execução';
      case 'virada':
        return 'virada do dia';
      default:
        return event.isEmpty ? 'evento' : event;
    }
  }

  @override
  Widget build(BuildContext context) {
    final m = widget.controller.value;
    final scaleX =
        (m[0].isFinite && m[0] > 0) ? m[0].toDouble() : 1.0;
    final tx = m.getTranslation().x.isFinite ? m.getTranslation().x : 0.0;
    // Sem GestureDetector de propósito: o detalhe abre no hover (igual ao
    // bubble info) e o tap atravessa para o gráfico abaixo. O tooltip vive
    // em Overlay (fora do layout) para nunca empurrar o splitter.
    // Guarda a posição p/ revalidar o hover quando o pan/zoom move os ícones.
    return MouseRegion(
      onHover: (e) {
        _lastLocal = e.localPosition;
        _updateHover(e.localPosition);
      },
      onExit: (_) => _clearHover(),
      child: CustomPaint(
        painter: _TimelinePainter(
          groups: _groups,
          scaleX: scaleX,
          tx: tx,
          width: widget.width,
          stepX: widget.stepX,
          onLayout: (l) {
            _layout = l;
            if (_lastLocal != null && _hovered != null) {
              _scheduleRevalidate();
            }
          },
        ),
      ),
    );
  }
}

class _TimelinePainter extends CustomPainter {
  final List<_LaneGroup> groups;
  final double scaleX;
  final double tx;
  final double width;
  final double stepX;
  final void Function(List<({double x, _LaneGroup group})>) onLayout;

  _TimelinePainter({
    required this.groups,
    required this.scaleX,
    required this.tx,
    required this.width,
    required this.stepX,
    required this.onLayout,
  });

  @override
  void paint(Canvas canvas, Size size) {
    // Fundo translúcido da faixa.
    canvas.drawRect(
      Offset.zero & size,
      Paint()..color = const Color(0x73000000),
    );
    // Linha do tempo.
    canvas.drawLine(
      const Offset(0, 13),
      Offset(size.width, 13),
      Paint()
        ..color = const Color(0x33ffffff)
        ..strokeWidth = 1,
    );
    final layout = <({double x, _LaneGroup group})>[];
    for (final g in groups) {
      final childX = g.candleIndex * stepX + stepX / 2;
      final x = childX * scaleX + tx;
      if (x < -12 || x > width + 12) continue;
      layout.add((x: x, group: g));
      final color = strategyPointColor(g.items.first);
      // Círculo + letra i.
      canvas.drawCircle(
          Offset(x, 13), 8, Paint()..color = color);
      canvas.drawCircle(
          Offset(x, 13),
          8,
          Paint()
            ..color = Colors.white70
            ..style = PaintingStyle.stroke
            ..strokeWidth = 1);
      final tp = TextPainter(
        text: const TextSpan(
          text: 'i',
          style: TextStyle(
            color: Colors.white,
            fontSize: 10,
            fontWeight: FontWeight.bold,
          ),
        ),
        textDirection: TextDirection.ltr,
      )..layout();
      tp.paint(canvas, Offset(x - tp.width / 2, 13 - tp.height / 2));
      // Badge de contagem quando há >1 chamada no candle.
      if (g.items.length > 1) {
        final label = g.items.length > 9 ? '9+' : '${g.items.length}';
        final bp = TextPainter(
          text: TextSpan(
            text: label,
            style: const TextStyle(
              color: Colors.white,
              fontSize: 8,
              fontWeight: FontWeight.bold,
            ),
          ),
          textDirection: TextDirection.ltr,
        )..layout();
        final c = Offset(x + 7, 20);
        canvas.drawCircle(c, 7, Paint()..color = const Color(0xFF444444));
        canvas.drawCircle(
            c,
            7,
            Paint()
              ..color = Colors.white54
              ..style = PaintingStyle.stroke
              ..strokeWidth = 0.8);
        bp.paint(canvas, Offset(c.dx - bp.width / 2, c.dy - bp.height / 2));
      }
    }
    onLayout(layout);
  }

  @override
  bool shouldRepaint(covariant _TimelinePainter old) =>
      old.groups != groups ||
      old.scaleX != scaleX ||
      old.tx != tx ||
      old.width != width ||
      old.stepX != stepX;
}
