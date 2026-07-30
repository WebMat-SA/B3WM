import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../services/state_service.dart';

class StructureDrawer extends StatefulWidget {
  final bool noDrawer;
  const StructureDrawer({super.key, this.noDrawer = false});

  @override
  State<StructureDrawer> createState() => _StructureDrawerState();
}

class _StructureDrawerState extends State<StructureDrawer> {
  @override
  Widget build(BuildContext context) {
    return Consumer<StateService>(builder: (context, state, _) {
      final changes = state.visibleStructureChanges;
      final body = Column(
        children: [
          Expanded(
            child: changes.isEmpty
                ? const Center(
                    child: Text('Nenhuma alteração de estrutura',
                        style: TextStyle(color: Colors.grey)))
                : ListView.separated(
                    padding: const EdgeInsets.symmetric(vertical: 4),
                    itemCount: changes.length,
                    separatorBuilder: (_, __) => const Divider(
                        height: 1, color: Color(0xFF3d3d3d)),
                    itemBuilder: (context, index) {
                      final c = changes[index];
                      final color =
                          c.isUp ? state.colorBuyer : state.colorSeller;
                      final label = c.isUp ? 'UP' : 'BOTTOM';
                      return ListTile(
                        dense: true,
                        contentPadding: const EdgeInsets.symmetric(
                            horizontal: 12, vertical: 0),
                        leading: Chip(
                          label: Text(
                            '${c.date.hour.toString().padLeft(2, '0')}:${c.date.minute.toString().padLeft(2, '0')}:${c.date.second.toString().padLeft(2, '0')}',
                            style: const TextStyle(
                                fontSize: 10, color: Colors.white),
                          ),
                          backgroundColor: Colors.grey.shade800,
                          materialTapTargetSize:
                              MaterialTapTargetSize.shrinkWrap,
                          visualDensity: VisualDensity.compact,
                          padding: EdgeInsets.zero,
                        ),
                        title: Text.rich(
                          TextSpan(
                            text: c.newValue > c.oldValue ? '\u2191' : '\u2193',
                            style: TextStyle(
                              fontSize: 16,
                              fontWeight: FontWeight.bold,
                              color: c.newValue > c.oldValue
                                  ? Colors.green
                                  : Colors.red,
                            ),
                            children: [
                              TextSpan(
                                text: '${c.oldValue.toStringAsFixed(2)} \u2192 ${c.newValue.toStringAsFixed(2)}',
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
                          padding: const EdgeInsets.symmetric(
                              horizontal: 8, vertical: 2),
                          decoration: BoxDecoration(
                            color: _parseColor(color),
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
                    },
                  ),
          ),
        ],
      );
      if (widget.noDrawer) return body;
      return Drawer(width: 360, child: body);
    });
  }

  Color _parseColor(String hex) {
    hex = hex.replaceAll('#', '');
    if (hex.length == 6) hex = 'FF$hex';
    return Color(int.parse(hex, radix: 16));
  }
}
