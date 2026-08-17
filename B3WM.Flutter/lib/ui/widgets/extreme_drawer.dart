import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../services/state_service.dart';
import '../../models/defaults.dart';
import '../../models/extreme_storage_item.dart';
import 'drawer_controls.dart';

class ExtremeDrawer extends StatefulWidget {
  final bool noDrawer;
  const ExtremeDrawer({super.key, this.noDrawer = false});

  @override
  State<ExtremeDrawer> createState() => _ExtremeDrawerState();
}

class _ExtremeDrawerState extends State<ExtremeDrawer>
    with AutomaticKeepAliveClientMixin {
  @override
  bool get wantKeepAlive => true;

  @override
  Widget build(BuildContext context) {
    super.build(context);
    return Consumer<StateService>(builder: (context, state, _) {
      final ex = state.extremes;
      final body = ListView(
        padding: EdgeInsets.zero,
        children: [
          ExpandableSection(
            icon: Icons.linear_scale,
            title: 'Topos/Vales',
            defaultExpanded: true,
            child: Column(
              children: [
                ToggleRow('Show on Chart', state.extremeVisible,
                    (v) => state.setExtremeVisible(v)),
                SliderRow('Opacity', state.extremeOpacity, 0, 1,
                    (v) => state.setExtremeOpacity(v)),
                SliderRow(
                  'Noise Sensitivity',
                  state.extremeNoiseSensitivity,
                  Defaults.extremeNoiseSensitivityMin,
                  Defaults.extremeNoiseSensitivityMax,
                  (v) => state.setExtremeNoiseSensitivity(v),
                  decimals: 1,
                  step: Defaults.extremeNoiseSensitivityStep,
                ),
                SliderRow(
                  'Min Prominence',
                  state.extremeMinimumProminence,
                  Defaults.extremeMinimumProminenceMin,
                  Defaults.extremeMinimumProminenceMax,
                  (v) => state.setExtremeMinimumProminence(v),
                  decimals: 2,
                  step: Defaults.extremeMinimumProminenceStep,
                ),
              ],
            ),
          ),
          if (ex != null && ex.extremes.isNotEmpty)
            ExpandableSection(
              icon: Icons.list_alt,
              title:
                  'Detectados (${ex.topCount} topo(s) / ${ex.valleyCount} vale(s))',
              defaultExpanded: true,
              child: Column(
                children: [
                  for (final e in ex.extremes)
                    _ExtremeRow(point: e),
                ],
              ),
            ),
          if (ex == null)
            const Padding(
              padding: EdgeInsets.all(16),
              child: Text('Nenhuma detecção carregada.',
                  style: TextStyle(fontSize: 13, color: Colors.grey)),
            ),
        ],
      );
      if (widget.noDrawer) return body;
      return Drawer(width: 360, child: body);
    });
  }
}

class _ExtremeRow extends StatelessWidget {
  final ExtremePoint point;
  const _ExtremeRow({required this.point});

  @override
  Widget build(BuildContext context) {
    final isTop = point.type == ExtremeType.top;
    final color = isTop
        ? const Color(0xFF69F0AE)
        : point.type == ExtremeType.valley
            ? const Color(0xFFFF5252)
            : Colors.grey;
    final label = isTop
        ? 'Topo'
        : point.type == ExtremeType.valley
            ? 'Vale'
            : 'Indeterminado';

    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 3),
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
            child: Text(label,
                style: TextStyle(fontSize: 12, color: color)),
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