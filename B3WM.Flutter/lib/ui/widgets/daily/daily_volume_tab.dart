import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../services/state_service.dart';
import '../drawer_controls.dart';
import 'daily_time_range_slider.dart';

/// Aba Volume Profile do widget diário (issue #12).
///
/// Fonte 100% diária: `GetDailyProfile` (perfil agregado multi-dia somado
/// no backend). Paridade com o intraday: Auto Mode (por Estrutura, default)
/// ou slider manual de período; a sombra da janela aparece no chart diário.
/// Perfil + topos sempre recarregam juntos no mesmo from/to.
class DailyVolumeTab extends StatefulWidget {
  const DailyVolumeTab({super.key});

  @override
  State<DailyVolumeTab> createState() => _DailyVolumeTabState();
}

class _DailyVolumeTabState extends State<DailyVolumeTab>
    with AutomaticKeepAliveClientMixin {
  @override
  bool get wantKeepAlive => true;

  @override
  Widget build(BuildContext context) {
    super.build(context);
    return Consumer<StateService>(builder: (context, state, _) {
      final levels = state.dailyProfileLevels;
      final topLevels = List.of(levels)
        ..sort((a, b) => b.total.compareTo(a.total));
      final poc = topLevels.take(5).toList();
      return Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          ExpandableSection(
            icon: Icons.align_horizontal_right,
            title: 'Volume Profile (1D)',
            defaultExpanded: true,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                ToggleRow('Auto Mode (por Estrutura)',
                    state.dailyProfileAutoByPriceStructure,
                    (v) => state.setDailyProfileAutoByPriceStructure(v)),
                const DailyTimeRangeSlider(),
                ToggleRow('Show on Chart', state.dailyProfileVisible,
                    (v) => state.setDailyProfileVisible(v)),
                SliderRow('Size (Horizontal)', state.dailyProfileSizeH, 0, 3,
                    (v) => state.setDailyProfileSizeH(v)),
                SliderRow('Size (Vertical)', state.dailyProfileSizeV, 0, 10,
                    (v) => state.setDailyProfileSizeV(v)),
                SliderRow('Opacity', state.dailyProfileOpacity, 0, 1,
                    (v) => state.setDailyProfileOpacity(v)),
                Padding(
                  padding:
                      const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
                  child: ElevatedButton.icon(
                    icon: (state.isDailyProfileLoading ||
                            state.isDailyExtremeLoading)
                        ? const SizedBox(
                            width: 14,
                            height: 14,
                            child:
                                CircularProgressIndicator(strokeWidth: 2))
                        : const Icon(Icons.refresh, size: 16),
                    label: Text((state.isDailyProfileLoading ||
                            state.isDailyExtremeLoading)
                        ? 'Atualizando...'
                        : 'Atualizar perfil + topos/vales'),
                    onPressed: (state.isDailyProfileLoading ||
                            state.isDailyExtremeLoading)
                        ? null
                        : () => state.loadDailyProfileAndExtremes(),
                  ),
                ),
              ],
            ),
          ),
          const Divider(height: 1, color: Color(0xFF3d3d3d)),
          Padding(
            padding: const EdgeInsets.all(12),
            child: Text(
                levels.isEmpty
                    ? 'Nenhum nível carregado.'
                    : 'Top concentrações (${levels.length} níveis)',
                style: const TextStyle(
                    fontSize: 14, fontWeight: FontWeight.bold)),
          ),
          if (state.isDailyProfileLoading && levels.isEmpty)
            const Padding(
              padding: EdgeInsets.all(24),
              child: Center(child: CircularProgressIndicator()),
            )
          else
            for (final lvl in poc)
              Padding(
                padding: const EdgeInsets.symmetric(
                    horizontal: 12, vertical: 3),
                child: Row(
                  children: [
                    Container(
                      width: 10,
                      height: 10,
                      decoration: BoxDecoration(
                        color: lvl == poc.first
                            ? Colors.amber
                            : Colors.grey.shade500,
                        shape: BoxShape.circle,
                      ),
                    ),
                    const SizedBox(width: 8),
                    Expanded(
                      child: Text(
                          lvl.price.toStringAsFixed(1),
                          style: const TextStyle(fontSize: 12)),
                    ),
                    Text('${lvl.total} contr.',
                        style: const TextStyle(
                            fontSize: 11, color: Colors.grey)),
                  ],
                ),
              ),
        ],
      );
    });
  }
}
