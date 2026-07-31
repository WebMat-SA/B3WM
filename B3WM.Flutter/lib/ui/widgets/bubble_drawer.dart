import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../models/bubble_storage_item.dart';
import '../../models/defaults.dart';
import '../../models/ticks2.dart';
import '../../services/state_service.dart';
import '../../services/extensions.dart';
import 'drawer_controls.dart';

class BubbleDrawer extends StatefulWidget {
  final bool noDrawer;
  const BubbleDrawer({super.key, this.noDrawer = false});

  @override
  State<BubbleDrawer> createState() => _BubbleDrawerState();
}

class _BubbleDrawerState extends State<BubbleDrawer> {
  int? _editingAgent;
  final Map<int, TextEditingController> _thresholdControllers = {};

  @override
  void dispose() {
    for (final c in _thresholdControllers.values) {
      c.dispose();
    }
    super.dispose();
  }

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
    return filtered.toList();
  }

  @override
  Widget build(BuildContext context) {
    return Consumer<StateService>(builder: (context, state, _) {
      final bubbles = _filteredBubbles(state);
      final body = ListView(
        padding: EdgeInsets.zero,
        children: [
          _configSection(state),
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
          if (state.bubbleSoundEnabled)
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
              child: Row(
                children: [
                  const Icon(Icons.volume_up, size: 16, color: Colors.grey),
                  Expanded(
                    child: Slider(
                      value: state.bubbleSoundVolume,
                      min: 0.0,
                      max: 1.0,
                      label: '${(state.bubbleSoundVolume * 100).round()}%',
                      onChanged: (v) => state.setBubbleSoundVolume(v),
                    ),
                  ),
                  SizedBox(
                    width: 32,
                    child: Text(
                      '${(state.bubbleSoundVolume * 100).round()}%',
                      style: const TextStyle(fontSize: 11, color: Colors.grey),
                    ),
                  ),
                ],
              ),
            ),
          const Divider(height: 1, color: Color(0xFF3d3d3d)),
          if (bubbles.isEmpty)
            const Center(
              child: Padding(
                padding: EdgeInsets.all(24),
                child: Text('Nenhuma bubble encontrada',
                    style: TextStyle(color: Colors.grey)),
              ),
            )
          else
            ...bubbles.expand((b) => [
                  _bubbleTile(state, b),
                  const Divider(height: 1, color: Color(0xFF3d3d3d)),
                ]),
        ],
      );
      if (widget.noDrawer) return body;
      return Drawer(width: 360, child: body);
    });
  }

  Widget _configSection(StateService state) {
    return ExpandableSection(
      icon: Icons.bubble_chart,
      title: 'Configurações',
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          ToggleRow('Show on Chart', state.bubbleVisible,
              (v) => state.setBubbleVisible(v)),
          SliderRow('Amount Threshold',
              state.thresholdBubble.toDouble(),
              Defaults.thresholdBubbleSize(state.symbol).toDouble(),
              5000,
              (v) => state.setThresholdBubble(v.round()),
              decimals: 0,
              step: 25),
          SliderRow('Size', state.bubbleSize, 0, 1,
              (v) => state.setBubbleSize(v)),
          SliderRow('Min Size (px)', state.bubbleSizeMin, 5, 200,
              (v) => state.setBubbleSizeMin(v), decimals: 0, step: 1),
          SliderRow('Max Size (px)', state.bubbleSizeMax, 5, 200,
              (v) => state.setBubbleSizeMax(v), decimals: 0, step: 1),
          SliderRow('Opacity', state.bubbleOpacity, 0, 1,
              (v) => state.setBubbleOpacity(v)),
          _colorsSection(state),
          _agentsSection(state),
        ],
      ),
    );
  }

  Widget _colorsSection(StateService state) {
    return ExpansionTile(
      leading: const Icon(Icons.palette, size: 20),
      title: const Text('Colors', style: TextStyle(fontSize: 13)),
      tilePadding: const EdgeInsets.symmetric(horizontal: 8),
      childrenPadding: const EdgeInsets.only(left: 16, right: 16, bottom: 8),
      children: [
        ColorPickerRow('Buy', state.colorBuyer, (c) => state.setColorBuyer(c)),
        const SizedBox(height: 4),
        ColorPickerRow(
            'Sell', state.colorSeller, (c) => state.setColorSeller(c)),
      ],
    );
  }

  Widget _agentsSection(StateService state) {
    final allAgents = state.allBubbleAgents.toList()
      ..sort((a, b) => agentsDescription(a).compareTo(agentsDescription(b)));
    if (allAgents.isEmpty) return const SizedBox.shrink();

    return ExpansionTile(
      leading: const Icon(Icons.people, size: 20),
      title: const Text('Agents', style: TextStyle(fontSize: 13)),
      initiallyExpanded: false,
      tilePadding: const EdgeInsets.symmetric(horizontal: 8),
      childrenPadding: const EdgeInsets.only(bottom: 8),
      children: [
        CheckboxListTile(
          dense: true,
          title: const Text('Selecionar Todos',
              style: TextStyle(fontSize: 12)),
          value: state.selectedAgents.length == allAgents.length,
          onChanged: (_) => state.selectAllAgents(),
          controlAffinity: ListTileControlAffinity.leading,
        ),
        const Divider(height: 1),
        ...allAgents.map((agent) => _agentTile(state, agent)),
      ],
    );
  }

  TextEditingController _controllerFor(int agent, int currentValue) {
    return _thresholdControllers.putIfAbsent(
        agent, () => TextEditingController(text: currentValue.toString()));
  }

  Widget _agentTile(StateService state, int agentValue) {
    final agentName = agentsDescription(agentValue);
    final isSelected = state.selectedAgents.contains(agentValue);
    final threshold = state.getThreshold(agentValue);
    final isCustom = state.agentThresholds.containsKey(agentValue);
    final isEditing = _editingAgent == agentValue;

    return Column(
      children: [
        ListTile(
          dense: true,
          leading: Checkbox(
            value: isSelected,
            onChanged: (_) => state.toggleAgent(agentValue),
            materialTapTargetSize: MaterialTapTargetSize.shrinkWrap,
          ),
          title: Text('$agentName ($agentValue)',
              style: const TextStyle(fontSize: 12)),
          trailing: isSelected
              ? IconButton(
                  icon: Icon(
                    Icons.tune,
                    size: 18,
                    color: isCustom ? Colors.blue : Colors.grey,
                  ),
                  onPressed: () => setState(() {
                    _editingAgent = isEditing ? null : agentValue;
                    _thresholdControllers.remove(agentValue);
                  }),
                )
              : null,
        ),
        if (isEditing && isSelected)
          Padding(
            padding: const EdgeInsets.only(left: 48, right: 16, bottom: 4),
            child: Row(
              children: [
                SizedBox(
                  width: 120,
                  child: TextField(
                    controller: _controllerFor(agentValue, isCustom ? threshold : state.thresholdBubble),
                    style: const TextStyle(fontSize: 12),
                    keyboardType: TextInputType.number,
                    decoration: InputDecoration(
                      isDense: true,
                      labelText: isCustom ? 'Threshold' : 'Threshold (opcional)',
                      border: const OutlineInputBorder(),
                    ),
                    onSubmitted: (v) {
                      final val = int.tryParse(v);
                      if (val != null && val >= Defaults.thresholdBubbleSize(state.symbol)) {
                        state.setAgentThreshold(agentValue, val);
                      }
                    },
                  ),
                ),
                const SizedBox(width: 8),
                if (isCustom)
                  IconButton(
                    icon:
                        const Icon(Icons.clear, size: 16, color: Colors.red),
                    onPressed: () {
                      state.setAgentThreshold(agentValue, null);
                      setState(() {
                        _editingAgent = null;
                        _thresholdControllers.remove(agentValue);
                      });
                    },
                  )
                else
                  Text('padrão: ${state.thresholdBubble}',
                      style: const TextStyle(fontSize: 11, color: Colors.grey)),
              ],
            ),
          ),
      ],
    );
  }

  Widget _bubbleTile(StateService state, BubbleStorageItem b) {
    final isBuy = b.actionType == ActionType.buy;
    final color = isBuy ? state.colorBuyer : state.colorSeller;
    final agentName = agentsDescription(b.agent);
    return ListTile(
      dense: true,
      contentPadding:
          const EdgeInsets.symmetric(horizontal: 12, vertical: 0),
      leading: Chip(
        label: Text(
          '${b.date.hour.toString().padLeft(2, '0')}:${b.date.minute.toString().padLeft(2, '0')}:${b.date.second.toString().padLeft(2, '0')}',
          style: const TextStyle(fontSize: 10, color: Colors.white),
        ),
        backgroundColor: Colors.grey.shade800,
        materialTapTargetSize: MaterialTapTargetSize.shrinkWrap,
        visualDensity: VisualDensity.compact,
        padding: EdgeInsets.zero,
      ),
      title: Text(
        agentName,
        style: const TextStyle(fontSize: 12),
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
          b.amount.toStringAsFixed(0),
          style: const TextStyle(
              fontSize: 12,
              color: Colors.white,
              fontWeight: FontWeight.bold),
        ),
      ),
    );
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
        color: active ? Colors.blue : Colors.grey,
        onPressed: onPressed,
      ),
    );
  }
}
