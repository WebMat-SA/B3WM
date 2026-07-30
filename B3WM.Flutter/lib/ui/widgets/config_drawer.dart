import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:flex_color_picker/flex_color_picker.dart';
import '../../models/defaults.dart';
import '../../services/state_service.dart';
import '../../services/extensions.dart';

class ConfigDrawer extends StatefulWidget {
  final bool noDrawer;
  const ConfigDrawer({super.key, this.noDrawer = false});

  @override
  State<ConfigDrawer> createState() => _ConfigDrawerState();
}

class _ConfigDrawerState extends State<ConfigDrawer> {
  int? _editingAgent;
  bool _isStructureRangeLoading = false;
  final Map<int, TextEditingController> _thresholdControllers = {};

  @override
  void dispose() {
    for (final c in _thresholdControllers.values) {
      c.dispose();
    }
    super.dispose();
  }
  @override
  Widget build(BuildContext context) {
    return Consumer<StateService>(builder: (context, state, _) {
      final body = ListView(
        padding: EdgeInsets.zero,
        children: [
          // _sectionHeader('Configurações'),
          _timeframeSection(state),
          _volumeProfileSection(state),
          _structureSection(state),
          _bubbleSection(state),
        ],
      );
      if (widget.noDrawer) return body;
      return Drawer(width: 360, child: body);
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
      {int decimals = 2, double step = 0.01, Widget? trailing}) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            Expanded(child: Text(label, style: const TextStyle(fontSize: 13))),
            Text(value.toStringAsFixed(decimals),
                style: const TextStyle(fontSize: 12, color: Colors.grey)),
            if (trailing != null) trailing,
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
          _toggleRow('Structure (Solid)', state.structureVisible,
              (v) => state.setStructureVisible(v)),
          _toggleRow('Aux Lines (Dashed)', state.structureAuxVisible,
              (v) => state.setStructureAuxVisible(v)),
          _sliderRow('Opacity', state.structureOpacity, 0, 1,
              (v) => state.setStructureOpacity(v)),
          _sliderRow('Range to Update', state.structureRangeUpd, 0,
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

  Widget _bubbleSection(StateService state) {
    return _expandableSection(
      icon: Icons.bubble_chart,
      title: 'Bubbles',
      defaultExpanded: false,
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
          _sliderRow('Min Size (px)', state.bubbleSizeMin, 5, 200,
              (v) => state.setBubbleSizeMin(v), decimals: 0, step: 1),
          _sliderRow('Max Size (px)', state.bubbleSizeMax, 5, 200,
              (v) => state.setBubbleSizeMax(v), decimals: 0, step: 1),
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
        GestureDetector(
          onTap: () async {
            final currentColor = _parseColor(hexColor);
            final Color? picked = await showColorPickerDialog(
              context,
              currentColor,
              title: Text('Selecionar cor - $label',
                  style: const TextStyle(fontSize: 16)),
              width: 40,
              height: 40,
              borderRadius: 4,
              spacing: 5,
              runSpacing: 5,
              wheelDiameter: 180,
              enableOpacity: false,
              showColorCode: true,
              colorCodeHasColor: true,
              pickersEnabled: const <ColorPickerType, bool>{
                ColorPickerType.wheel: true,
                ColorPickerType.primary: true,
                ColorPickerType.accent: true,
                ColorPickerType.both: false,
                ColorPickerType.bw: false,
                ColorPickerType.custom: false,
              },
              actionButtons: const ColorPickerActionButtons(
                okButton: true,
                closeButton: true,
              ),
              constraints: const BoxConstraints(
                minHeight: 420,
                minWidth: 300,
                maxWidth: 320,
              ),
            );
            if (picked != null) {
              final hex =
                  '#${picked.toARGB32().toRadixString(16).padLeft(8, '0').substring(2)}';
              onChanged(hex);
            }
          },
          child: Container(
            width: 32,
            height: 32,
            decoration: BoxDecoration(
              color: _parseColor(hexColor),
              borderRadius: BorderRadius.circular(4),
              border: Border.all(color: Colors.grey.shade600),
            ),
          ),
        ),
        const SizedBox(width: 8),
        Text(label, style: const TextStyle(fontSize: 13)),
      ],
    );
  }

  Color _parseColor(String hex) {
    hex = hex.replaceAll('#', '');
    if (hex.length == 6) hex = 'FF$hex';
    return Color(int.parse(hex, radix: 16));
  }

  Widget _agentsSection(StateService state) {
    final allAgents = state.allBubbleAgents.toList()
      ..sort((a, b) => agentsDescription(a).compareTo(agentsDescription(b)));
    if (allAgents.isEmpty) return const SizedBox.shrink();

    return ExpansionTile(
      leading: const Icon(Icons.people, size: 20),
      title: const Text('Agents', style: const TextStyle(fontSize: 13)),
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
}
