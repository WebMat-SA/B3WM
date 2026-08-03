import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../models/defaults.dart';
import '../../models/structure_change_item.dart';
import '../../services/state_service.dart';
import 'drawer_controls.dart';

class StructureDrawer extends StatefulWidget {
  final bool noDrawer;
  const StructureDrawer({super.key, this.noDrawer = false});

  @override
  State<StructureDrawer> createState() => _StructureDrawerState();
}

class _StructureDrawerState extends State<StructureDrawer> {
  bool _isStructureRangeLoading = false;

  @override
  Widget build(BuildContext context) {
    return Consumer<StateService>(builder: (context, state, _) {
      final changes = state.visibleStructureChanges;
      final body = Column(
        children: [
          Expanded(
            child: ListView(
              padding: const EdgeInsets.symmetric(vertical: 4),
              children: [
                _configSection(state),
                const Divider(height: 1, color: Color(0xFF3d3d3d)),
                Container(
                  padding: const EdgeInsets.all(12),
                  decoration: const BoxDecoration(
                    color: Color(0xFF2d2d2d),
                    border: Border(
                        bottom: BorderSide(color: Color(0xFF3d3d3d))),
                  ),
                  child: const Row(
                    children: [
                      Icon(Icons.swap_vert, size: 20),
                      SizedBox(width: 8),
                      Expanded(
                        child: Text('Alterações de Estrutura',
                            style: TextStyle(
                                fontSize: 14, fontWeight: FontWeight.bold)),
                      ),
                    ],
                  ),
                ),
                if (changes.isEmpty)
                  const Center(
                    child: Padding(
                      padding: EdgeInsets.all(24),
                      child: Text('Nenhuma alteração de estrutura',
                          style: TextStyle(color: Colors.grey)),
                    ),
                  )
                else
                  ...changes.expand((c) => [
                        _changeTile(state, c),
                        const Divider(
                            height: 1, color: Color(0xFF3d3d3d)),
                      ]),
              ],
            ),
          ),
        ],
      );
      if (widget.noDrawer) return body;
      return Drawer(width: 360, child: body);
    });
  }

  Widget _configSection(StateService state) {
    return ExpandableSection(
      icon: Icons.stacked_line_chart,
      title: 'Configurações',
      child: Column(
        children: [
          ToggleRow('Structure (Solid)', state.structureVisible,
              (v) => state.setStructureVisible(v)),
          ToggleRow('Aux Lines (Dashed)', state.structureAuxVisible,
              (v) => state.setStructureAuxVisible(v)),
          SliderRow('Opacity', state.structureOpacity, 0, 1,
              (v) => state.setStructureOpacity(v)),
          SliderRow('Range to Update', state.structureRangeUpd, 0,
              Defaults.structureRangeUpdMax(state.symbol),
              (v) => state.setStructureRangeUpd(v),
              decimals: 1, step: Defaults.structureRangeUpdStep(state.symbol),
              trailing: state.isStructureUpdating
                  ? _isStructureRangeLoading
                      ? const Padding(
                          padding: EdgeInsets.only(left: 4),
                          child: SizedBox(
                              width: 20,
                              height: 20,
                              child: CircularProgressIndicator(strokeWidth: 2)))
                      : IconButton(
                          icon: const Icon(Icons.upload,
                              color: Colors.orange),
                          onPressed: () async {
                            setState(
                                () => _isStructureRangeLoading = true);
                            await state.confirmStructureRangeUpd();
                            if (mounted) {
                              setState(() =>
                                  _isStructureRangeLoading = false);
                            }
                          },
                          constraints: const BoxConstraints(),
                          padding: EdgeInsets.zero,
                          visualDensity: VisualDensity.compact,
                        )
                  : null),
        ],
      ),
    );
  }

  Widget _changeTile(StateService state, StructureChangeItem c) {
    final color = c.isUp ? state.colorBuyer : state.colorSeller;
    return ListTile(
      dense: true,
      contentPadding:
          const EdgeInsets.symmetric(horizontal: 12, vertical: 0),
      leading: Chip(
        label: Text(
          '${c.date.hour.toString().padLeft(2, '0')}:${c.date.minute.toString().padLeft(2, '0')}:${c.date.second.toString().padLeft(2, '0')}',
          style: const TextStyle(fontSize: 10, color: Colors.white),
        ),
        backgroundColor: Colors.grey.shade800,
        materialTapTargetSize: MaterialTapTargetSize.shrinkWrap,
        visualDensity: VisualDensity.compact,
        padding: EdgeInsets.zero,
      ),
      title: Text.rich(
        TextSpan(
          text: c.isUpMove ? '\u2191' : '\u2193',
          style: TextStyle(
            fontSize: 16,
            fontWeight: FontWeight.bold,
            color: c.isUpMove ? Colors.green : Colors.red,
          ),
          children: [
            TextSpan(
              text:
                  '${c.oldValue.toStringAsFixed(2)} \u2192 ${c.newValue.toStringAsFixed(2)}',
              style: const TextStyle(
                fontSize: 12,
                color: Colors.white,
              ),
            ),
          ],
        ),
        overflow: TextOverflow.ellipsis,
      ),
      trailing: Container(
        padding:
            const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
        decoration: BoxDecoration(
          color: parseHexColor(color),
          borderRadius: BorderRadius.circular(12),
        ),
        child: Text(
          c.isUp ? 'UP' : 'BT',
          style: const TextStyle(
              fontSize: 12,
              color: Colors.white,
              fontWeight: FontWeight.bold),
        ),
      ),
    );
  }
}
