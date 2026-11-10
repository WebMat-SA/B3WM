import 'package:flutter/material.dart';
import 'locked_badge.dart';

/// Slider de período compartilhado (intraday + diário).
///
/// Comportamento único: preview local durante o arrasto, aplicação só em
/// `onChangeEnd` (sem rajada por pixel) e badge informativo em auto-mode.
/// Os wrappers (`TimeRangeSlider`, `DailyTimeRangeSlider`) só adaptam fonte
/// de dados, formato de data e ação de aplicação.
class RangeFilterSlider extends StatefulWidget {
  final int count;
  final int start;
  final int end;
  final bool autoMode;
  final String Function(int idx) formatDate;
  final void Function(int newStart, int newEnd) onApply;
  final String autoLabel;
  final bool locked;

  const RangeFilterSlider({
    super.key,
    required this.count,
    required this.start,
    required this.end,
    required this.autoMode,
    required this.formatDate,
    required this.onApply,
    this.autoLabel = 'Auto Mode (por Estrutura)',
    this.locked = false,
  });

  @override
  State<RangeFilterSlider> createState() => _RangeFilterSliderState();
}

class _RangeFilterSliderState extends State<RangeFilterSlider> {
  RangeValues? _localValues;

  @override
  void didUpdateWidget(covariant RangeFilterSlider oldWidget) {
    super.didUpdateWidget(oldWidget);
    // Sincroniza só quando o estado EXTERNO mudou (novo candle, auto mode,
    // troca de símbolo…). Durante o arrasto o widget.onChanged atualiza
    // apenas `_localValues`, então não podemos reancorar a cada rebuild —
    // isso fazia o thumb voltar e escondia a data de destino.
    if (oldWidget.count != widget.count ||
        oldWidget.start != widget.start ||
        oldWidget.end != widget.end) {
      final count = widget.count;
      if (count > 0) {
        final safeStart = widget.start.clamp(0, count - 1);
        final safeEnd = widget.end.clamp(safeStart + 1, count);
        _localValues = RangeValues(safeStart.toDouble(), safeEnd.toDouble());
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final count = widget.count;
    if (count == 0) return const SizedBox.shrink();

    final safeStart = widget.start.clamp(0, count - 1);
    final safeEnd = widget.end.clamp(safeStart + 1, count);

    // Initialize local values from state on first build
    _localValues ??= RangeValues(safeStart.toDouble(), safeEnd.toDouble());

    final localStart = _localValues!.start.round().clamp(0, count - 1);
    final localEnd = _localValues!.end.round().clamp(localStart + 1, count);

    void onChangeEnd(RangeValues v) {
      final newStart = v.start.round().clamp(0, count - 2);
      final newEnd = v.end.round().clamp(newStart + 1, count);
      if (newStart == safeStart && newEnd == safeEnd) {
        return;
      }
      widget.onApply(newStart, newEnd);
      setState(() {
        _localValues = RangeValues(newStart.toDouble(), newEnd.toDouble());
      });
    }

    if (widget.autoMode) {
      return _buildAutoModeLabel(safeEnd - 1);
    }

    return _buildManualSlider(count, localStart, localEnd, onChangeEnd);
  }

  Widget _headerEnd(String text) {
    return Align(
      alignment: Alignment.centerRight,
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(text, style: const TextStyle(fontSize: 10, color: Colors.grey)),
          LockedBadge(locked: widget.locked),
        ],
      ),
    );
  }

  Widget _buildAutoModeLabel(int lastIndex) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 4),
      decoration: BoxDecoration(
        border: Border(
            top: BorderSide(color: Colors.grey.shade800.withOpacity(0.3))),
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          _headerEnd(widget.formatDate(lastIndex)),
          Container(
            padding: const EdgeInsets.all(8),
            decoration: BoxDecoration(
              color: Colors.blue.withOpacity(0.1),
              borderRadius: BorderRadius.circular(4),
              border: Border.all(color: Colors.blue.withOpacity(0.3)),
            ),
            child: Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                const Icon(Icons.auto_mode, size: 14, color: Colors.blue),
                const SizedBox(width: 6),
                Text(widget.autoLabel,
                    style:
                        const TextStyle(fontSize: 12, color: Colors.blue)),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildManualSlider(int count, int localStart, int localEnd,
      void Function(RangeValues) onChangeEnd) {
    // `end` é exclusivo: a data exibida é a do último candle incluído.
    final startLabel = widget.formatDate(localStart);
    final endLabel = widget.formatDate(localEnd - 1);
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 4),
      decoration: BoxDecoration(
        border: Border(
            top: BorderSide(color: Colors.grey.shade800.withOpacity(0.3))),
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          // Mostra a data de destino durante o arrasto (antes só aparecia
          // depois de soltar).
          _headerEnd('$startLabel → $endLabel'),
          // Range filter
          RangeSlider(
            min: 0,
            max: count.toDouble(),
            divisions: count,
            values: RangeValues(localStart.toDouble(), localEnd.toDouble()),
            labels: RangeLabels(startLabel, endLabel),
            onChanged: (v) {
              // Apenas atualiza UI local durante o arrasto, sem aplicar
              setState(() {
                _localValues = v;
              });
            },
            onChangeEnd: onChangeEnd,
          ),
        ],
      ),
    );
  }
}
