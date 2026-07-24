import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../models/bubble_storage_item.dart';
import '../../models/ticks2.dart';
import '../../services/state_service.dart';
import '../../services/extensions.dart';

class BubbleDrawer extends StatefulWidget {
  const BubbleDrawer({super.key});

  @override
  State<BubbleDrawer> createState() => _BubbleDrawerState();
}

class _BubbleDrawerState extends State<BubbleDrawer> {
  List<BubbleStorageItem> _filteredBubbles(StateService state) {
    final all = state.bubbles;
    var filtered = all;
    if (state.bubbleAmountFilter) {
      filtered = filtered.where((b) {
        final threshold = state.getThreshold(b.agent);
        return b.amount >= threshold;
      }).toList();
    }
    if (state.bubbleAgentsFilter) {
      filtered = filtered
          .where((b) => state.selectedAgents.contains(b.agent))
          .toList();
    }
    filtered.sort((a, b) => b.date.compareTo(a.date));
    return filtered.take(45).toList();
  }

  @override
  Widget build(BuildContext context) {
    return Consumer<StateService>(builder: (context, state, _) {
      final bubbles = _filteredBubbles(state);
      return Drawer(
        width: 360,
        child: Column(
          children: [
            Container(
              padding: const EdgeInsets.all(12),
              decoration: const BoxDecoration(
                color: Color(0xFF2d2d2d),
                border: Border(
                    bottom: BorderSide(color: Color(0xFF3d3d3d))),
              ),
              child: Row(
                children: [
                  const Icon(Icons.filter_alt, size: 20),
                  const SizedBox(width: 8),
                  const Expanded(
                    child: Text('Notificação de Bubbles',
                        style: TextStyle(
                            fontSize: 14, fontWeight: FontWeight.bold)),
                  ),
                  _filterIcon(
                    icon: Icons.bar_chart,
                    active: state.bubbleAmountFilter,
                    tooltip:
                        'Quantidade: ${state.thresholdBubble} (${state.bubbleAmountFilter ? "Ligado" : "Desligado"})',
                    onPressed: () =>
                        state.setBubbleAmountFilter(!state.bubbleAmountFilter),
                  ),
                  const SizedBox(width: 4),
                  _filterIcon(
                    icon: Icons.people,
                    active: state.bubbleAgentsFilter,
                    tooltip:
                        'Agents (${state.bubbleAgentsFilter ? "Ligado" : "Desligado"})',
                    onPressed: () =>
                        state.setBubbleAgentsFilter(!state.bubbleAgentsFilter),
                  ),
                  const SizedBox(width: 4),
                  _filterIcon(
                    icon: Icons.notifications_active,
                    active: state.bubbleSoundEnabled,
                    tooltip:
                        'Notificações Sonoras (${state.bubbleSoundEnabled ? "Ligado" : "Desligado"})',
                    onPressed: () =>
                        state.setBubbleSoundEnabled(!state.bubbleSoundEnabled),
                  ),
                ],
              ),
            ),
            Expanded(
              child: bubbles.isEmpty
                  ? const Center(
                      child: Text('Nenhuma bubble encontrada',
                          style: TextStyle(color: Colors.grey)))
                  : ListView.separated(
                      padding: const EdgeInsets.symmetric(vertical: 4),
                      itemCount: bubbles.length,
                      separatorBuilder: (_, __) => const Divider(
                          height: 1,
                          color: Color(0xFF3d3d3d)),
                      itemBuilder: (context, index) {
                        final b = bubbles[index];
                        final isBuy =
                            b.actionType == ActionType.buy;
                        final color = isBuy ? state.colorBuyer : state.colorSeller;
                        final agentName = agentsDescription(b.agent);
                        return ListTile(
                          dense: true,
                          contentPadding: const EdgeInsets.symmetric(
                              horizontal: 12, vertical: 0),
                          leading: Chip(
                            label: Text(
                              '${b.date.hour.toString().padLeft(2, '0')}:${b.date.minute.toString().padLeft(2, '0')}:${b.date.second.toString().padLeft(2, '0')}',
                              style:
                                  const TextStyle(fontSize: 10, color: Colors.white),
                            ),
                            backgroundColor: Colors.grey.shade800,
                            materialTapTargetSize:
                                MaterialTapTargetSize.shrinkWrap,
                            visualDensity: VisualDensity.compact,
                            padding: EdgeInsets.zero,
                          ),
                          title: Text(
                            agentName,
                            style: const TextStyle(fontSize: 12),
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
                              b.amount.toStringAsFixed(0),
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
        ),
      );
    });
  }

  Widget _filterIcon({
    required IconData icon,
    required bool active,
    required String tooltip,
    required VoidCallback onPressed,
  }) {
    return Tooltip(
      message: tooltip,
      child: IconButton(
        icon: Icon(icon, size: 18),
        color: active ? Colors.amber : Colors.grey,
        onPressed: onPressed,
      ),
    );
  }

  Color _parseColor(String hex) {
    hex = hex.replaceAll('#', '');
    if (hex.length == 6) hex = 'FF$hex';
    return Color(int.parse(hex, radix: 16));
  }
}
