import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../models/defaults.dart';
import '../../../models/extreme_storage_item.dart';
import '../../../services/state_service.dart';
import '../drawer_controls.dart';
import 'daily_info.dart';

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
                ),
                SliderRow(
                  'Min Prominence',
                  state.dailyExtremeMinimumProminence,
                  Defaults.extremeMinimumProminenceMin,
                  Defaults.extremeMinimumProminenceMax,
                  (v) => state.setDailyExtremeMinimumProminence(v),
                  decimals: 2,
                  step: Defaults.extremeMinimumProminenceStep,
                ),
                Padding(
                  padding:
                      const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
                  child: Text(
                    dailyWindowText(state),
                    style: const TextStyle(fontSize: 11, color: Colors.grey),
                  ),
                ),
                Padding(
                  padding:
                      const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
                  child: Row(
                    children: [
                      Expanded(
                        child: ElevatedButton.icon(
                          icon: state.isDailyExtremeLoading
                              ? const SizedBox(
                                  width: 14,
                                  height: 14,
                                  child: CircularProgressIndicator(
                                      strokeWidth: 2))
                              : const Icon(Icons.refresh, size: 16),
                          label: Text(state.isDailyExtremeLoading
                              ? 'Atualizando...'
                              : 'Atualizar diário'),
                          onPressed: state.isDailyExtremeLoading
                              ? null
                              : () => state.loadDailyExtremes(),
                        ),
                      ),
                      if (dex != null) ...[
                        const SizedBox(width: 8),
                        IconButton(
                          tooltip: 'Limpar',
                          icon: const Icon(Icons.clear, size: 18),
                          onPressed: () => state.clearDailyExtremes(),
                        ),
                      ],
                    ],
                  ),
                ),
                if (dex != null && dex.extremes.isNotEmpty)
                  Padding(
                    padding: const EdgeInsets.symmetric(
                        horizontal: 12, vertical: 4),
                    child: Text(
                      'Diário: ${dex.topCount} topo(s) / ${dex.valleyCount} vale(s)'
                      '${dex.profilePointCount > 0 ? ' • ${dex.profilePointCount} níveis no perfil' : ''}',
                      style: const TextStyle(
                          fontSize: 12, fontWeight: FontWeight.bold),
                    ),
                  ),
                if (dex != null)
                  for (final e in dex.extremes)
                    _DailyExtremeRow(point: e),
                if (dex == null)
                  Padding(
                    padding: const EdgeInsets.symmetric(
                        horizontal: 12, vertical: 4),
                    child: Text(
                      state.isDailyExtremeLoading
                          ? 'Carregando níveis diários...'
                          : 'Toque em Atualizar após mudar Noise/Prominence.',
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
            child:
                Text(label, style: TextStyle(fontSize: 12, color: color)),
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
