import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../models/defaults.dart';
import '../../../models/extreme_storage_item.dart';
import '../../../services/state_service.dart';
import '../drawer_controls.dart';

/// Aba Topos/Vales do widget diário (issue #12): detecção sobre o perfil
/// agregado diário (`getExtremeDaily` na mesma janela do volume profile).
class DailyExtremeTab extends StatefulWidget {
  const DailyExtremeTab({super.key});

  @override
  State<DailyExtremeTab> createState() => _DailyExtremeTabState();
}

class _DailyExtremeTabState extends State<DailyExtremeTab>
    with AutomaticKeepAliveClientMixin {
  @override
  bool get wantKeepAlive => true;

  @override
  Widget build(BuildContext context) {
    super.build(context);
    return Consumer<StateService>(builder: (context, state, _) {
      final dex = state.dailyExtremes;
      return Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          ExpandableSection(
            icon: Icons.terrain,
            title: 'Topos/Vales (1D)',
            defaultExpanded: true,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                ToggleRow('Show on Chart', state.dailyExtremeVisible,
                    (v) => state.setDailyExtremeVisible(v)),
                SliderRow('Opacity', state.dailyExtremeOpacity, 0, 1,
                    (v) => state.setDailyExtremeOpacity(v)),
                SliderRow(
                  'Noise Sensitivity',
                  state.dailyExtremeNoiseSensitivity,
                  Defaults.extremeNoiseSensitivityMin,
                  Defaults.extremeNoiseSensitivityMax,
                  (v) => state.setDailyExtremeNoiseSensitivity(v),
                  decimals: 1,
                  step: Defaults.extremeNoiseSensitivityStep,
                  locked: state.configLocked,
                ),
                SliderRow(
                  'Min Prominence',
                  state.dailyExtremeMinimumProminence,
                  Defaults.extremeMinimumProminenceMin,
                  Defaults.extremeMinimumProminenceMax,
                  (v) => state.setDailyExtremeMinimumProminence(v),
                  decimals: 2,
                  step: Defaults.extremeMinimumProminenceStep,
                  locked: state.configLocked,
                ),
                if (dex != null)
                  Padding(
                    padding:
                        const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
                    child: Row(
                      children: [
                        Expanded(
                          child: Text(
                            dex.extremes.isNotEmpty
                                ? 'Diário: ${dex.topCount} topo(s) / ${dex.valleyCount} vale(s)'
                                    '${dex.profilePointCount > 0 ? ' • ${dex.profilePointCount} níveis no perfil' : ''}'
                                : 'Nenhuma detecção carregada.',
                            style: dex.extremes.isNotEmpty
                                ? const TextStyle(
                                    fontSize: 12, fontWeight: FontWeight.bold)
                                : const TextStyle(
                                    fontSize: 11, color: Colors.grey),
                          ),
                        ),
                        IconButton(
                          tooltip: 'Limpar',
                          icon: const Icon(Icons.clear, size: 18),
                          onPressed: () => state.clearDailyExtremes(),
                        ),
                      ],
                    ),
                  ),
                if (dex != null)
                  for (final e in dex.extremes) _DailyExtremeRow(point: e),
                if (dex == null)
                  Padding(
                    padding:
                        const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
                    child: Text(
                      state.isDailyExtremeLoading
                          ? 'Carregando níveis diários...'
                          : 'Nenhuma detecção carregada.',
                      style: const TextStyle(fontSize: 11, color: Colors.grey),
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

class _DailyExtremeRow extends StatelessWidget {
  final ExtremePoint point;
  const _DailyExtremeRow({required this.point});

  @override
  Widget build(BuildContext context) {
    final isTop = point.type == ExtremeType.top;
    final color = isTop
        ? const Color(0xFF43A047)
        : point.type == ExtremeType.valley
            ? const Color(0xFFE53935)
            : Colors.grey;
    final label = isTop
        ? 'Topo'
        : point.type == ExtremeType.valley
            ? 'Vale'
            : 'Indeterminado';

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
            child: Text(label, style: TextStyle(fontSize: 12, color: color)),
          ),
          Expanded(
            child: Text(point.position.toStringAsFixed(1),
                style: const TextStyle(fontSize: 12)),
          ),
          Text('imp ${point.prominence.toStringAsFixed(2)}',
              style: const TextStyle(fontSize: 11, color: Colors.grey)),
        ],
      ),
    );
  }
}
