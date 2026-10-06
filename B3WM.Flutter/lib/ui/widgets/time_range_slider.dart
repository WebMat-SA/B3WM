import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:intl/intl.dart';
import '../../services/state_service.dart';
import 'range_filter_slider.dart';

/// Slider de período intraday (adaptador fino sobre `RangeFilterSlider`).
class TimeRangeSlider extends StatelessWidget {
  const TimeRangeSlider({super.key});

  @override
  Widget build(BuildContext context) {
    return Consumer<StateService>(builder: (context, state, _) {
      final bars = state.barsTimeFrameFilter;

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

      return RangeFilterSlider(
        count: bars.length,
        start: state.dateRangeStart,
        end: state.dateRangeEnd,
        autoMode: state.profileAutoByPriceStructure,
        formatDate: formatDate,
        // Aplica o filtro e sincroniza extremos imediatamente (sem debounce)
        onApply: state.applyVolumeFilterAndSyncExtremes,
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
