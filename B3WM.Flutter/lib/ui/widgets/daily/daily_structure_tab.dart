import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../models/defaults.dart';
import '../../../models/structure_change_item.dart';
import '../../../services/state_service.dart';
import '../drawer_controls.dart';
import 'daily_info.dart';

/// Aba Estruturas do widget diário (issue #12): Range 1D + visibilidade da
/// estrutura 1440 + lista de viradas do 1440. Escopo 100% diário.
class DailyStructureTab extends StatefulWidget {
  const DailyStructureTab({super.key});

  @override
  State<DailyStructureTab> createState() => _DailyStructureTabState();
}

class _DailyStructureTabState extends State<DailyStructureTab>
    with AutomaticKeepAliveClientMixin {
  @override
  bool get wantKeepAlive => true;

  bool _isConfirmLoading = false;

  @override
  Widget build(BuildContext context) {
    super.build(context);
    return Consumer<StateService>(builder: (context, state, _) {
      final changes = state.dailyStructureChanges;
      return Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          ExpandableSection(
            icon: Icons.stacked_line_chart,
            title: 'Estrutura 1D',
            defaultExpanded: true,
            child: Column(
              children: [
                ToggleRow('Structure (Solid)', state.dailyStructureVisible,
                    (v) => state.setDailyStructureVisible(v)),
                ToggleRow('Aux Lines (Dashed)',
                    state.dailyStructureAuxVisible,
                    (v) => state.setDailyStructureAuxVisible(v)),
                SliderRow('Opacity', state.dailyStructureOpacity, 0, 1,
                    (v) => state.setDailyStructureOpacity(v)),
                SliderRow(
                  'Range 1D (estruturas)',
                  state.dailyStructureRangeUpd,
                  0,
                  Defaults.structureRangeUpdDailyMax(state.symbol),
                  (v) => state.setDailyStructureRangeUpd(v),
                  decimals: 0,
                  step: Defaults.structureRangeUpdDailyStep(state.symbol),
                  onChangeEnd: (_) =>
                      state.scheduleDailyStructureConfirm(immediate: true),
                  trailing: state.isDailyStructureConfirmRunning || _isConfirmLoading
                      ? const Padding(
                          padding: EdgeInsets.only(left: 4),
                          child: SizedBox(
                              width: 20,
                              height: 20,
                              child: CircularProgressIndicator(
                                  strokeWidth: 2)))
                      : state.isDailyStructureUpdating
                          ? IconButton(
                              icon: const Icon(Icons.upload,
                                  color: Colors.orange),
                              tooltip:
                                  'Aplicar Range 1D (só 1440)',
                              onPressed: () async {
                                setState(() => _isConfirmLoading = true);
                                await state.confirmStructureRangeUpdDaily();
                                if (mounted) {
                                  setState(() => _isConfirmLoading = false);
                                }
                              },
                              constraints: const BoxConstraints(),
                              padding: EdgeInsets.zero,
                              visualDensity: VisualDensity.compact,
                            )
                          : null,
                ),
                Padding(
                  padding:
                      const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
                  child: Text(
                    dailyWindowText(state),
                    style: const TextStyle(fontSize: 11, color: Colors.grey),
                  ),
                ),
                Padding(
                  padding:
                      const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
                  child: Text(
                    dailyStructureStatusText(state),
                    style: const TextStyle(fontSize: 11, color: Colors.grey),
                  ),
                ),
              ],
            ),
          ),
          const Divider(height: 1, color: Color(0xFF3d3d3d)),
          Padding(
            padding: const EdgeInsets.all(12),
            child: Row(
              children: [
                const Icon(Icons.swap_vert, size: 20),
                const SizedBox(width: 8),
                Expanded(
                  child: Text(
                      'Viradas 1440 (${changes.length})',
                      style: const TextStyle(
                          fontSize: 14, fontWeight: FontWeight.bold)),
                ),
              ],
            ),
          ),
          if (changes.isEmpty)
            const Padding(
              padding: EdgeInsets.all(24),
              child: Center(
                child: Text('Nenhuma virada 1440 no histórico',
                    style: TextStyle(color: Colors.grey)),
              ),
            )
          else
            for (final c in changes) _DailyChangeTile(change: c),
        ],
      );
    });
  }
}

class _DailyChangeTile extends StatelessWidget {
  final StructureChangeItem change;
  const _DailyChangeTile({required this.change});

  @override
  Widget build(BuildContext context) {
    final state = context.read<StateService>();
    final color =
        change.isUp ? state.colorBuyer : state.colorSeller;
    return Column(
      children: [
        ListTile(
          dense: true,
          contentPadding:
              const EdgeInsets.symmetric(horizontal: 12, vertical: 0),
          leading: Chip(
            label: Text(
              fmtDailyDate(change.date),
              style: const TextStyle(fontSize: 10, color: Colors.white),
            ),
            backgroundColor: Colors.grey.shade800,
            materialTapTargetSize: MaterialTapTargetSize.shrinkWrap,
            visualDensity: VisualDensity.compact,
            padding: EdgeInsets.zero,
          ),
          title: Text.rich(
            TextSpan(
              text: change.isUpMove ? '\u2191' : '\u2193',
              style: TextStyle(
                fontSize: 16,
                fontWeight: FontWeight.bold,
                color: change.isUpMove ? Colors.green : Colors.red,
              ),
              children: [
                TextSpan(
                  text:
                      '${change.oldValue.toStringAsFixed(2)} \u2192 ${change.newValue.toStringAsFixed(2)}',
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
              change.isUp ? 'UP' : 'BT',
              style: const TextStyle(
                  fontSize: 12,
                  color: Colors.white,
                  fontWeight: FontWeight.bold),
            ),
          ),
        ),
        const Divider(height: 1, color: Color(0xFF3d3d3d)),
      ],
    );
  }
}
