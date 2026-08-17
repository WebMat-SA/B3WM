import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:intl/intl.dart';
import '../../services/state_service.dart';

class TimeRangeSlider extends StatelessWidget {
  const TimeRangeSlider({super.key});

  @override
  Widget build(BuildContext context) {
    return Consumer<StateService>(builder: (context, state, _) {
      final bars = state.barsTimeFrameFilter;
      final count = bars.length;
      if (count == 0) return const SizedBox.shrink();

      final start = state.dateRangeStart;
      final end = state.dateRangeEnd;
      final autoMode = state.profileAutoByPriceStructure;

      final safeStart = start.clamp(0, count - 1);
      final safeEnd = end.clamp(safeStart + 1, count);

      String formatDate(int idx) {
        final bar = bars.elementAtOrNull(idx);
        if (bar == null) return '';
        final startDate = bars.elementAtOrNull(state.dateRangeStart);
        final fmt = startDate != null &&
                bar.date.day == startDate.date.day &&
                bar.date.month == startDate.date.month
            ? DateFormat('HH:mm')
            : DateFormat('dd/MM HH:mm');
        return fmt.format(bar.date);
      }

      void applyRange(double startVal, double endVal) {
        final newStart = startVal.round().clamp(0, safeEnd - 1);
        final newEnd = endVal.round().clamp(newStart + 1, count);
        if (newStart == safeStart && newEnd == safeEnd) return;
        state.applyVolumeFilter(newStart, newEnd);
      }

      // No release do arrasto garante um sincronismo final da análise de
      // extremos com a janela exata selecionada.
      void applyRangeEnd(double startVal, double endVal) {
        final newStart = startVal.round().clamp(0, safeEnd - 1);
        final newEnd = endVal.round().clamp(newStart + 1, count);
        if (newStart == safeStart && newEnd == safeEnd) return;
        state.applyVolumeFilter(newStart, newEnd);
      }

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
              child: Text(formatDate(safeEnd),
                  style: const TextStyle(fontSize: 10, color: Colors.grey)),
            ),
            // Range filter
            RangeSlider(
              min: 0,
              max: count.toDouble(),
              divisions: count,
              values: RangeValues(safeStart.toDouble(), safeEnd.toDouble()),
              labels: RangeLabels(formatDate(safeStart), formatDate(safeEnd)),
              onChanged: autoMode
                  ? null
                  : (v) => applyRange(v.start, v.end),
              onChangeEnd: autoMode
                  ? null
                  : (v) => applyRangeEnd(v.start, v.end),
            ),
          ],
        ),
      );
    });
  }
}

extension _ListElementAtOrNull<T> on List<T> {
  T? elementAtOrNull(int index) {
    if (index < 0 || index >= length) return null;
    return this[index];
  }
}
