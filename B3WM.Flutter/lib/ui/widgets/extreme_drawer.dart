import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../services/state_service.dart';
import '../../models/defaults.dart';
import '../../models/extreme_storage_item.dart';
import 'drawer_controls.dart';

/// Aba Topos/Vales do drawer principal (só intraday).
///
/// A análise diária (Range 1D, perfil agregado, topos/vales 1D) vive no
/// widget próprio (issue #12): `daily/daily_panel.dart`.
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
            icon: Icons.terrain,
            title: 'Topos/Vales',
            defaultExpanded: true,
            child: Column(
              children: [
                ToggleRow('Show on Chart', state.extremeVisible,
                    (v) => state.setExtremeVisible(v)),
                ToggleRow('Incluir Topos/Vales 1D', state.extremeIncludeDaily,
                    (v) => state.setExtremeIncludeDaily(v)),
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
                  locked: state.configLocked,
                ),
                SliderRow(
                  'Min Prominence',
                  state.extremeMinimumProminence,
                  Defaults.extremeMinimumProminenceMin,
                  Defaults.extremeMinimumProminenceMax,
                  (v) => state.setExtremeMinimumProminence(v),
                  decimals: 2,
                  step: Defaults.extremeMinimumProminenceStep,
                  locked: state.configLocked,
                ),
                _colorsSection(state),
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
                    _ExtremeRow(
                      point: e,
                      topColor: parseHexColor(state.extremeTopColor),
                      valleyColor: parseHexColor(state.extremeValleyColor),
                    ),
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

  Widget _colorsSection(StateService state) {
    return ExpansionTile(
      leading: const Icon(Icons.palette, size: 20),
      title: const Text('Colors', style: TextStyle(fontSize: 13)),
      tilePadding: const EdgeInsets.symmetric(horizontal: 8),
      childrenPadding: const EdgeInsets.only(left: 16, right: 16, bottom: 8),
      children: [
        ColorPickerRow(
            'Topo', state.extremeTopColor, (c) => state.setExtremeTopColor(c)),
        const SizedBox(height: 4),
        ColorPickerRow('Vale', state.extremeValleyColor,
            (c) => state.setExtremeValleyColor(c)),
      ],
    );
  }
}

class _ExtremeRow extends StatelessWidget {
  final ExtremePoint point;
  final Color topColor;
  final Color valleyColor;
  const _ExtremeRow(
      {required this.point, required this.topColor, required this.valleyColor});

  @override
  Widget build(BuildContext context) {
    final isTop = point.type == ExtremeType.top;
    final color = isTop
        ? topColor
        : point.type == ExtremeType.valley
            ? valleyColor
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
