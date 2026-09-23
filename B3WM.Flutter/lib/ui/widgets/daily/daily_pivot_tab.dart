import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../services/state_service.dart';
import '../drawer_controls.dart';

/// Aba Pivot do widget diário (issue #14): Pivot Tradicional do Profit com
/// fonte HLC da semana anterior (fiel ao Profit), exibido só no dia
/// atual/último pregão. Opções: só visible/opacity/lineCount (2–5).
class DailyPivotTab extends StatefulWidget {
  const DailyPivotTab({super.key});

  @override
  State<DailyPivotTab> createState() => _DailyPivotTabState();
}

class _DailyPivotTabState extends State<DailyPivotTab>
    with AutomaticKeepAliveClientMixin {
  @override
  bool get wantKeepAlive => true;

  @override
  Widget build(BuildContext context) {
    super.build(context);
    return Consumer<StateService>(builder: (context, state, _) {
      final pv = state.dailyPivot;
      return Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          ExpandableSection(
            icon: Icons.drag_handle,
            title: 'Pivot Tradicional (1D)',
            defaultExpanded: true,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                ToggleRow('Show on Chart', state.dailyPivotVisible,
                    (v) => state.setDailyPivotVisible(v)),
                SliderRow('Opacity', state.dailyPivotOpacity, 0, 1,
                    (v) => state.setDailyPivotOpacity(v)),
                SliderRow(
                  'Linhas (pares por lado)',
                  state.dailyPivotLineCount.toDouble(),
                  2,
                  5,
                  (v) => state.setDailyPivotLineCount(v.round()),
                  decimals: 0,
                  step: 1,
                ),
                if (pv != null && pv.levels.isNotEmpty)
                  Padding(
                    padding: const EdgeInsets.symmetric(
                        horizontal: 12, vertical: 4),
                    child: Text(
                      'Diário (${pv.source}): ${pv.levels.length} níveis',
                      style: const TextStyle(
                          fontSize: 12, fontWeight: FontWeight.bold),
                    ),
                  ),
                if (pv != null)
                  for (final l in pv.levels)
                    _DailyPivotRow(levelKey: l.key, value: l.value),
                if (pv == null)
                  Padding(
                    padding: const EdgeInsets.symmetric(
                        horizontal: 12, vertical: 4),
                    child: Text(
                      state.isDailyPivotLoading
                          ? 'Carregando pivot diário...'
                          : 'Sem pivot para a sessão (verifique as barras 1440).',
                      style:
                          const TextStyle(fontSize: 11, color: Colors.grey),
                    ),
                  ),
              ],
            ),
          ),
        ],
      );
    });
  }
}

class _DailyPivotRow extends StatelessWidget {
  final String levelKey;
  final double value;
  const _DailyPivotRow({required this.levelKey, required this.value});

  @override
  Widget build(BuildContext context) {
    final color = levelKey == 'P'
        ? const Color(0xFFBDBDBD)
        : levelKey.startsWith('R')
            ? const Color(0xFFCE93D8)
            : const Color(0xFF4DD0E1);
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 3),
      child: Row(
        children: [
          Container(
            width: 10,
            height: 10,
            decoration: BoxDecoration(color: color, shape: BoxShape.circle),
          ),
          const SizedBox(width: 8),
          SizedBox(
            width: 84,
            child: Text(levelKey,
                style: TextStyle(
                    fontSize: 12, color: color, fontWeight: FontWeight.bold)),
          ),
          Expanded(
            child: Text(value.toStringAsFixed(1),
                style: const TextStyle(fontSize: 12)),
          ),
        ],
      ),
    );
  }
}
