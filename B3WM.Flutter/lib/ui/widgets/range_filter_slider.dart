import 'package:flutter/material.dart';

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

  const RangeFilterSlider({
    super.key,
    required this.count,
    required this.start,
    required this.end,
    required this.autoMode,
    required this.formatDate,
    required this.onApply,
    this.autoLabel = 'Auto Mode (por Estrutura)',
  });

  @override
  State<RangeFilterSlider> createState() => _RangeFilterSliderState();
}

class _RangeFilterSliderState extends State<RangeFilterSlider> {
  RangeValues? _localValues;

  @override
  Widget build(BuildContext context) {
    final count = widget.count;
    if (count == 0) return const SizedBox.shrink();

    final safeStart = widget.start.clamp(0, count - 1);
    final safeEnd = widget.end.clamp(safeStart + 1, count);

    // Initialize local values from state on first build
    _localValues ??= RangeValues(safeStart.toDouble(), safeEnd.toDouble());

    // If state changed externally (e.g., auto mode), sync local values
    if (!widget.autoMode &&
        (_localValues!.start != safeStart ||
            _localValues!.end != safeEnd)) {
      _localValues = RangeValues(safeStart.toDouble(), safeEnd.toDouble());
    }

    void onChangeEnd(RangeValues v) {
      final newStart = v.start.round().clamp(0, count - 2);
      final newEnd = v.end.round().clamp(newStart + 1, count);
      if (newStart == safeStart && newEnd == safeEnd) {
        return;
      }
      widget.onApply(newStart, newEnd);
      setState(() {
        _localValues = v;
      });
    }

    if (widget.autoMode) {
      return _buildAutoModeLabel(safeEnd);
    }

    return _buildManualSlider(count, safeStart, safeEnd, onChangeEnd);
  }

  Widget _buildAutoModeLabel(int safeEnd) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 4),
      decoration: BoxDecoration(
        border: Border(
            top: BorderSide(color: Colors.grey.shade800.withOpacity(0.3))),
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Align(
            alignment: Alignment.centerRight,
            child: Text(widget.formatDate(safeEnd),
                style: const TextStyle(fontSize: 10, color: Colors.grey)),
          ),
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

  Widget _buildManualSlider(int count, int safeStart, int safeEnd,
      void Function(RangeValues) onChangeEnd) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 4),
      decoration: BoxDecoration(
        border: Border(
            top: BorderSide(color: Colors.grey.shade800.withOpacity(0.3))),
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          // End date label
          Align(
            alignment: Alignment.centerRight,
            child: Text(widget.formatDate(safeEnd),
                style: const TextStyle(fontSize: 10, color: Colors.grey)),
          ),
          // Range filter
          RangeSlider(
            min: 0,
            max: count.toDouble(),
            divisions: count,
            values: _localValues!,
            labels: RangeLabels(
                widget.formatDate(safeStart), widget.formatDate(safeEnd)),
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
