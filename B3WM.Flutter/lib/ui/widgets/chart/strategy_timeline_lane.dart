import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../../services/state_service.dart';
import '../../../services/strategy_service.dart' show b3Wall;
import 'chart_data.dart';

/// Cor do ícone ⓘ: a mesma do relatório antigo (comprar azul, vender
/// vermelho, execução laranja, resto cinza).
Color strategyPointColor(StrategyPoint p) {
  if (p.kind == 'execucao') return Colors.orange;
  switch (p.side) {
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
    const cardW = 250.0;
    final left =
        (global.dx + 12).clamp(8.0, (screen.width - cardW - 8).clamp(8.0, double.infinity));
    // Card acima do ícone (ancorado pela base): 34px acima do ponto.
    final bottom = (screen.height - global.dy + 34)
        .clamp(8.0, screen.height - 8);
    _hideTip();
    _tipEntry = OverlayEntry(
      builder: (_) => Positioned(
        left: left,
        bottom: bottom,
        width: cardW,
        // Material transparente: sem ele o texto no Overlay ganha
        // sublinhado amarelo (estilo default de debug).
        child: IgnorePointer(
          child: Material(
            color: Colors.transparent,
            child: _buildHoverCard(group),
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
      constraints: const BoxConstraints(maxWidth: 250),
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
              _ddmmhhmm(group.items.first.at),
              style: const TextStyle(
                  color: Colors.white,
                  fontSize: 11,
                  fontWeight: FontWeight.bold,
                  decoration: TextDecoration.none),
            ),
          ),
          for (final p in group.items)
            Padding(
              padding: const EdgeInsets.symmetric(vertical: 2),
              child: Text(
                '${_hhmm(p.at)} ${p.side}${p.confidence > 0 ? ' ${p.confidence.toStringAsFixed(2)}' : ''} → ${p.action}',
                style: TextStyle(
                  color: strategyPointColor(p),
                  fontSize: 10,
                  fontFamily: 'monospace',
                  fontWeight: FontWeight.bold,
                  decoration: TextDecoration.none,
                ),
              ),
            ),
        ],
      ),
    );
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
