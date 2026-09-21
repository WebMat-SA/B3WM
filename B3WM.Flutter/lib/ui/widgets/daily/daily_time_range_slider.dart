import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:intl/intl.dart';
import '../../../services/state_service.dart';

/// Slider de período do Volume 1D (paridade com `TimeRangeSlider` intraday).
///
/// Em auto-mode exibe o badge informativo em vez do slider; em manual,
/// preview local durante o arrasto e aplicação só em `onChangeEnd`
/// (recarrega perfil + topos via rede, sem rajada por pixel).
/// Labels em `dd/MM/yyyy` (escala diária, sem hora).
class DailyTimeRangeSlider extends StatefulWidget {
  const DailyTimeRangeSlider({super.key});

  @override
  State<DailyTimeRangeSlider> createState() => _DailyTimeRangeSliderState();
}

class _DailyTimeRangeSliderState extends State<DailyTimeRangeSlider> {
  RangeValues? _localValues;

  @override
  Widget build(BuildContext context) {
    return Consumer<StateService>(builder: (context, state, _) {
      final bars = List.of(state.dailyBars)
        ..sort((a, b) => a.date.compareTo(b.date));
      final count = bars.length;
      if (count == 0) return const SizedBox.shrink();

      final start = state.dailyRangeStart;
      final end = state.dailyRangeEnd;
      final autoMode = state.dailyProfileAutoByPriceStructure;

      final safeStart = start.clamp(0, count - 1);
      final safeEnd = end.clamp(safeStart + 1, count);

      // Initialize local values from state on first build
      _localValues ??= RangeValues(safeStart.toDouble(), safeEnd.toDouble());

      // If state changed externally (e.g., auto mode), sync local values
      if (!autoMode &&
          (_localValues!.start != safeStart || _localValues!.end != safeEnd)) {
        _localValues = RangeValues(safeStart.toDouble(), safeEnd.toDouble());
      }

      String formatDate(int idx) {
        if (idx < 0 || idx >= bars.length) return '';
        return DateFormat('dd/MM/yyyy').format(bars[idx].date);
      }

      void onChangeEnd(RangeValues v) {
        final newStart = v.start.round().clamp(0, count - 2);
        final newEnd = v.end.round().clamp(newStart + 1, count);
        if (newStart == safeStart && newEnd == safeEnd) {
          return;
        }
        // Aplica a janela e recarrega perfil + topos juntos (sem debounce:
        // só dispara no release, e o reload é 1 request por gesto).
        // ignore: discarded_futures
        state.applyDailyVolumeFilterAndSyncExtremes(newStart, newEnd);
        // Atualiza o estado local para refletir a mudança
        setState(() {
          _localValues = v;
        });
      }

      if (autoMode) {
        return _buildAutoModeLabel(formatDate, safeEnd);
      }

      return _buildManualSlider(count, formatDate, safeStart, safeEnd, onChangeEnd);
    });
  }

  Widget _buildAutoModeLabel(String Function(int) formatDate, int safeEnd) {
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
            child: Text(formatDate(safeEnd),
                style: const TextStyle(fontSize: 10, color: Colors.grey)),
          ),
          Container(
            padding: const EdgeInsets.all(8),
            decoration: BoxDecoration(
              color: Colors.blue.withOpacity(0.1),
              borderRadius: BorderRadius.circular(4),
              border: Border.all(color: Colors.blue.withOpacity(0.3)),
            ),
            child: const Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                Icon(Icons.auto_mode, size: 14, color: Colors.blue),
                SizedBox(width: 6),
                Text('Auto Mode (por Estrutura)',
                    style: TextStyle(fontSize: 12, color: Colors.blue)),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildManualSlider(int count, String Function(int) formatDate,
      int safeStart, int safeEnd, void Function(RangeValues) onChangeEnd) {
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
            values: _localValues!,
            labels: RangeLabels(formatDate(safeStart), formatDate(safeEnd)),
            onChanged: (v) {
              // Apenas atualiza UI local durante o arrasto, sem recarregar
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
