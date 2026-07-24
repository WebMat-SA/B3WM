import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../models/defaults.dart';
import '../../services/state_service.dart';
import '../../services/extensions.dart';

class ConfigDrawer extends StatefulWidget {
  const ConfigDrawer({super.key});

  @override
  State<ConfigDrawer> createState() => _ConfigDrawerState();
}

class _ConfigDrawerState extends State<ConfigDrawer> {
  int? _editingAgent;

  @override
  Widget build(BuildContext context) {
    return Consumer<StateService>(builder: (context, state, _) {
      return Drawer(
        width: 360,
        child: ListView(
          padding: EdgeInsets.zero,
          children: [
            _sectionHeader('Configurações'),
            _timeframeSection(state),
            _volumeProfileSection(state),
            _structureSection(state),
            _forecastSection(state),
            _bubbleSection(state),
          ],
        ),
      );
    });
  }

  Widget _sectionHeader(String title) {
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: const BoxDecoration(
        color: Color(0xFF2d2d2d),
        border: Border(bottom: BorderSide(color: Color(0xFF3d3d3d))),
      ),
      child: Text(title,
          style: const TextStyle(fontSize: 16, fontWeight: FontWeight.bold)),
    );
  }

  Widget _expandableSection({
    required String title,
    required IconData icon,
    required Widget child,
    bool defaultExpanded = false,
  }) {
    return ExpansionTile(
      leading: Icon(icon, size: 20),
      title: Text(title, style: const TextStyle(fontSize: 14)),
      initiallyExpanded: defaultExpanded,
      tilePadding: const EdgeInsets.symmetric(horizontal: 16),
      childrenPadding: const EdgeInsets.only(left: 16, right: 16, bottom: 8),
      children: [child],
    );
  }

  Widget _toggleRow(String label, bool value, ValueChanged<bool> onChanged) {
    return Row(
      children: [
        Expanded(
            child: Text(label, style: const TextStyle(fontSize: 13))),
        Switch(
          value: value,
          onChanged: onChanged,
          materialTapTargetSize: MaterialTapTargetSize.shrinkWrap,
        ),
      ],
    );
  }

  Widget _sliderRow(String label, double value, double min, double max,
      ValueChanged<double> onChanged,
      {int decimals = 3, double step = 0.001}) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            Expanded(child: Text(label, style: const TextStyle(fontSize: 13))),
            Text(value.toStringAsFixed(decimals),
                style: const TextStyle(fontSize: 12, color: Colors.grey)),
          ],
        ),
        Slider(
          value: value,
          min: min,
          max: max,
          divisions: ((max - min) / step).round().clamp(1, 1000),
          onChanged: onChanged,
        ),
      ],
    );
  }

  Widget _timeframeSection(StateService state) {
    final timeframes = [1, 2, 5, 15, 30, 60];
    return _expandableSection(
      icon: Icons.timelapse,
      title: 'TimeFrame (Min)',
      child: Wrap(
        spacing: 4,
        children: timeframes
            .map((tf) => ChoiceChip(
                  label: Text('$tf', style: const TextStyle(fontSize: 12)),
                  selected: state.timeFrame == tf,
                  onSelected: (_) => state.setTimeFrame(tf),
                ))
            .toList(),
      ),
    );
  }

  Widget _volumeProfileSection(StateService state) {
    return _expandableSection(
      icon: Icons.align_horizontal_right,
      title: 'Volume Profile',
      child: Column(
        children: [
          _toggleRow('Show on Chart', state.profileVisible,
              (v) => state.setProfileVisible(v)),
          _sliderRow('Size (Horizontal)', state.profileSizeH, 0, 3,
              (v) => state.setProfileSizeH(v)),
          _sliderRow('Size (Vertical)', state.profileSizeV, 0, 10,
              (v) => state.setProfileSizeV(v)),
          _sliderRow('Opacity', state.profileOpacity, 0, 1,
              (v) => state.setProfileOpacity(v)),
        ],
      ),
    );
  }

  Widget _structureSection(StateService state) {
    return _expandableSection(
      icon: Icons.stacked_line_chart,
      title: 'Price Structure',
      child: Column(
        children: [
          _toggleRow('Show on Chart', state.structureVisible,
              (v) => state.setStructureVisible(v)),
          _toggleRow('Show Aux on Chart', state.structureAuxVisible,
              (v) => state.setStructureAuxVisible(v)),
          _sliderRow('Opacity', state.structureOpacity, 0, 1,
              (v) => state.setStructureOpacity(v)),
          _sliderRow('Range to Update', state.structureRangeUpd, 0, 3000,
              (v) => state.setStructureRangeUpd(v),
              decimals: 1, step: 0.5),
        ],
      ),
    );
  }

  Widget _forecastSection(StateService state) {
    return _expandableSection(
      icon: Icons.ssid_chart,
      title: 'Ajuste Previsto',
      child: Column(
        children: [
          _toggleRow('Show on Chart', state.forecastVisible,
              (v) => state.setForecastVisible(v)),
          const Padding(
            padding: EdgeInsets.only(top: 4),
            child: Text('VWAP Diário (acumulado do pregão)',
                style: TextStyle(fontSize: 11, color: Colors.grey)),
          ),
        ],
      ),
    );
  }

  Widget _bubbleSection(StateService state) {
    return _expandableSection(
      icon: Icons.bubble_chart,
      title: 'Bubbles',
      defaultExpanded: true,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          _toggleRow('Show on Chart', state.bubbleVisible,
              (v) => state.setBubbleVisible(v)),
          _sliderRow('Amount Threshold',
              state.thresholdBubble.toDouble(),
              Defaults.thresholdBubbleSize(state.symbol).toDouble(),
              5000,
              (v) => state.setThresholdBubble(v.round()),
              decimals: 0,
              step: 25),
          _sliderRow('Size', state.bubbleSize, 0, 1,
              (v) => state.setBubbleSize(v)),
          _sliderRow('Opacity', state.bubbleOpacity, 0, 1,
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
      childrenPadding:
          const EdgeInsets.only(left: 16, right: 16, bottom: 8),
      children: [
        _colorPicker('Buy', state.colorBuyer, (c) => state.setColorBuyer(c)),
        const SizedBox(height: 4),
        _colorPicker(
            'Sell', state.colorSeller, (c) => state.setColorSeller(c)),
      ],
    );
  }

  Widget _colorPicker(
      String label, String hexColor, ValueChanged<String> onChanged) {
    return Row(
      children: [
        SizedBox(
          width: 32,
          height: 32,
          child: Container(
            decoration: BoxDecoration(
              color: _parseColor(hexColor),
              borderRadius: BorderRadius.circular(4),
              border: Border.all(color: Colors.grey.shade600),
            ),
          ),
        ),
        const SizedBox(width: 8),
        Text(label, style: const TextStyle(fontSize: 13)),
        const Spacer(),
        SizedBox(
          width: 80,
          child: TextField(
            controller: TextEditingController(text: hexColor),
            style: const TextStyle(fontSize: 12),
              decoration: const InputDecoration(
              isDense: true,
              contentPadding:
                  EdgeInsets.symmetric(horizontal: 8, vertical: 4),
              border: OutlineInputBorder(),
            ),
            onSubmitted: onChanged,
          ),
        ),
      ],
    );
  }

  Color _parseColor(String hex) {
    hex = hex.replaceAll('#', '');
    if (hex.length == 6) hex = 'FF$hex';
    return Color(int.parse(hex, radix: 16));
  }

  Widget _agentsSection(StateService state) {
    final allAgents = state.allBubbleAgents.toList();
    if (allAgents.isEmpty) return const SizedBox.shrink();

    return ExpansionTile(
      leading: const Icon(Icons.people, size: 20),
      title: const Text('Agents', style: const TextStyle(fontSize: 13)),
      initiallyExpanded: true,
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

  Widget _agentTile(StateService state, int agentValue) {
    final agentName = agentsDescription(agentValue);
    final isSelected = state.selectedAgents.contains(agentValue);
    final threshold = state.getThreshold(agentValue);
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
                    color: threshold != state.thresholdBubble
                        ? Colors.amber
                        : Colors.grey,
                  ),
                  onPressed: () =>
                      setState(() => _editingAgent = isEditing ? null : agentValue),
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
                    controller: TextEditingController(
                        text: threshold.toString()),
                    style: const TextStyle(fontSize: 12),
                    keyboardType: TextInputType.number,
                    decoration: const InputDecoration(
                      isDense: true,
                      labelText: 'Threshold',
                      border: OutlineInputBorder(),
                    ),
                    onSubmitted: (v) {
                      final val = int.tryParse(v);
                      state.setAgentThreshold(agentValue, val);
                    },
                  ),
                ),
                const SizedBox(width: 8),
                if (threshold != state.thresholdBubble)
                  IconButton(
                    icon:
                        const Icon(Icons.clear, size: 16, color: Colors.red),
                    onPressed: () {
                      state.setAgentThreshold(agentValue, null);
                      setState(() => _editingAgent = null);
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
}
