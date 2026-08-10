import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../services/state_service.dart';

class TimeframeSelector extends StatelessWidget {
  final ValueChanged<int>? onSelected;
  const TimeframeSelector({super.key, this.onSelected});

  @override
  Widget build(BuildContext context) {
    final timeframes = [1, 2, 5, 15, 30, 60];
    return Consumer<StateService>(builder: (context, state, _) {
      return Wrap(
        spacing: 4,
        children: timeframes
            .map((tf) => ChoiceChip(
                  label: Text('$tf', style: const TextStyle(fontSize: 12)),
                  selected: state.timeFrame == tf,
                  onSelected: (_) {
                    state.setTimeFrame(tf);
                    onSelected?.call(tf);
                  },
                  showCheckmark: false,
                  visualDensity: VisualDensity.compact,
                  materialTapTargetSize: MaterialTapTargetSize.shrinkWrap,
                  padding: const EdgeInsets.symmetric(horizontal: 6),
                ))
            .toList(),
      );
    });
  }
}
