import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../services/state_service.dart';
import 'drawer_controls.dart';

/// Aba Pivot do drawer principal (só intraday, issue #14).
///
/// Pivot Tradicional do Profit — fonte HLC de D-1, linhas só sobre a sessão
/// exibida (dia atual ou último pregão). Opções da aba: só
/// visible/opacity/lineCount (2–5, default 2), sem escolha de fórmula.
class PivotDrawer extends StatefulWidget {
  final bool noDrawer;
  const PivotDrawer({super.key, this.noDrawer = false});

  @override
  State<PivotDrawer> createState() => _PivotDrawerState();
}

class _PivotDrawerState extends State<PivotDrawer>
    with AutomaticKeepAliveClientMixin {
  @override
  bool get wantKeepAlive => true;

  @override
  Widget build(BuildContext context) {
    super.build(context);
    return Consumer<StateService>(builder: (context, state, _) {
      final pv = state.pivotIntraday;
      final body = ListView(
        padding: EdgeInsets.zero,
        children: [
          ExpandableSection(
            icon: Icons.drag_handle,
            title: 'Pivot Tradicional (D-1)',
            defaultExpanded: true,
            child: Column(
              children: [
                ToggleRow('Show on Chart', state.pivotVisible,
                    (v) => state.setPivotVisible(v)),
                SliderRow('Opacity', state.pivotOpacity, 0, 1,
                    (v) => state.setPivotOpacity(v)),
                SliderRow(
                  'Linhas (pares por lado)',
                  state.pivotLineCount.toDouble(),
                  2,
                  5,
                  (v) => state.setPivotLineCount(v.round()),
                  decimals: 0,
                  step: 1,
                ),
              ],
            ),
          ),
          if (pv != null && pv.levels.isNotEmpty)
            ExpandableSection(
              icon: Icons.list_alt,
              title: 'Níveis (${pv.source} • ${pv.levels.length})',
              defaultExpanded: true,
              child: Column(
                children: [
                  for (final l in pv.levels)
                    _PivotRow(levelKey: l.key, value: l.value),
                ],
              ),
            ),
          if (pv == null)
            Padding(
              padding: const EdgeInsets.all(16),
              child: Text(
                  state.isPivotIntradayLoading
                      ? 'Carregando pivot do dia...'
                      : 'Sem pivot para a sessão (verifique as barras 1440 de D-1).',
                  style: const TextStyle(fontSize: 13, color: Colors.grey)),
            ),
        ],
      );
      if (widget.noDrawer) return body;
      return Drawer(width: 360, child: body);
    });
  }
}

class _PivotRow extends StatelessWidget {
  final String levelKey;
  final double value;
  const _PivotRow({required this.levelKey, required this.value});

  @override
  Widget build(BuildContext context) {
    final color = levelKey == 'P'
        ? const Color(0xFFBDBDBD)
        : levelKey.startsWith('R')
            ? const Color(0xFFCE93D8)
            : const Color(0xFF4DD0E1);
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
