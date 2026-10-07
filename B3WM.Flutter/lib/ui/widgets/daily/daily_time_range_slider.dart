import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:intl/intl.dart';
import '../../../services/state_service.dart';
import '../range_filter_slider.dart';

/// Slider de período do Volume 1D (adaptador fino sobre `RangeFilterSlider`).
///
/// Em auto-mode exibe o badge informativo em vez do slider; em manual,
/// preview local durante o arrasto e aplicação só em `onChangeEnd`
/// (recarrega perfil + topos via rede, sem rajada por pixel).
/// Labels em `dd/MM/yyyy` (escala diária, sem hora).
class DailyTimeRangeSlider extends StatelessWidget {
  const DailyTimeRangeSlider({super.key});

  @override
  Widget build(BuildContext context) {
    return Consumer<StateService>(builder: (context, state, _) {
      final bars = List.of(state.dailyBars)
        ..sort((a, b) => a.date.compareTo(b.date));

      String formatDate(int idx) {
        if (idx < 0 || idx >= bars.length) return '';
        return DateFormat('dd/MM/yyyy').format(bars[idx].date);
      }

      return RangeFilterSlider(
        count: bars.length,
        start: state.dailyRangeStart,
        end: state.dailyRangeEnd,
        autoMode: state.dailyProfileAutoByPriceStructure,
        formatDate: formatDate,
        // Aplica a janela e recarrega perfil + topos juntos (sem debounce:
        // só dispara no release, e o reload é 1 request por gesto).
        // ignore: discarded_futures
        onApply: state.applyDailyVolumeFilterAndSyncExtremes,
      );
    });
  }
}
