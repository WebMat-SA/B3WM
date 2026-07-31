import 'package:flutter/material.dart';
import 'package:flex_color_picker/flex_color_picker.dart';

class SectionHeader extends StatelessWidget {
  final String title;
  const SectionHeader({super.key, required this.title});

  @override
  Widget build(BuildContext context) {
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
}

class ExpandableSection extends StatelessWidget {
  final String title;
  final IconData icon;
  final Widget child;
  final bool defaultExpanded;
  const ExpandableSection({
    super.key,
    required this.title,
    required this.icon,
    required this.child,
    this.defaultExpanded = false,
  });

  @override
  Widget build(BuildContext context) {
    return ExpansionTile(
      leading: Icon(icon, size: 20),
      title: Text(title, style: const TextStyle(fontSize: 14)),
      initiallyExpanded: defaultExpanded,
      tilePadding: const EdgeInsets.symmetric(horizontal: 16),
      childrenPadding: const EdgeInsets.only(left: 16, right: 16, bottom: 8),
      children: [child],
    );
  }
}

class ToggleRow extends StatelessWidget {
  final String label;
  final bool value;
  final ValueChanged<bool> onChanged;
  const ToggleRow(this.label, this.value, this.onChanged, {super.key});

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Expanded(child: Text(label, style: const TextStyle(fontSize: 13))),
        Switch(
          value: value,
          onChanged: onChanged,
          materialTapTargetSize: MaterialTapTargetSize.shrinkWrap,
        ),
      ],
    );
  }
}

class SliderRow extends StatelessWidget {
  final String label;
  final double value;
  final double min;
  final double max;
  final ValueChanged<double> onChanged;
  final int decimals;
  final double step;
  final Widget? trailing;
  const SliderRow(
    this.label,
    this.value,
    this.min,
    this.max,
    this.onChanged, {
    super.key,
    this.decimals = 2,
    this.step = 0.01,
    this.trailing,
  });

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            Expanded(child: Text(label, style: const TextStyle(fontSize: 13))),
            Text(value.toStringAsFixed(decimals),
                style: const TextStyle(fontSize: 12, color: Colors.grey)),
            if (trailing != null) trailing!,
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
}

Color parseHexColor(String hex) {
  hex = hex.replaceAll('#', '');
  if (hex.length == 6) hex = 'FF$hex';
  return Color(int.parse(hex, radix: 16));
}

class ColorPickerRow extends StatelessWidget {
  final String label;
  final String hexColor;
  final ValueChanged<String> onChanged;
  const ColorPickerRow(this.label, this.hexColor, this.onChanged,
      {super.key});

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        GestureDetector(
          onTap: () async {
            final Color picked = await showColorPickerDialog(
              context,
              parseHexColor(hexColor),
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
            final hex =
                '#${picked.toARGB32().toRadixString(16).padLeft(8, '0').substring(2)}';
            onChanged(hex);
          },
          child: Container(
            width: 32,
            height: 32,
            decoration: BoxDecoration(
              color: parseHexColor(hexColor),
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
}
