import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../services/state_service.dart';
import 'drawer_controls.dart';

class VwapDrawer extends StatefulWidget {
  final bool noDrawer;
  const VwapDrawer({super.key, this.noDrawer = false});

  @override
  State<VwapDrawer> createState() => _VwapDrawerState();
}

class _VwapDrawerState extends State<VwapDrawer>
    with AutomaticKeepAliveClientMixin {
  @override
  bool get wantKeepAlive => true;

  @override
  Widget build(BuildContext context) {
    super.build(context);
    return Consumer<StateService>(builder: (context, state, _) {
      final body = ListView(
        padding: EdgeInsets.zero,
        children: [
          ExpandableSection(
            icon: Icons.trending_up,
            title: 'VWAP Diário',
            defaultExpanded: true,
            child: Column(
              children: [
                ToggleRow('Show on Chart', state.vwapVisible,
                    (v) => state.setVwapVisible(v)),
                SliderRow('Opacity', state.vwapOpacity, 0, 1,
                    (v) => state.setVwapOpacity(v)),
                ColorPickerRow('Color', state.vwapColor,
                    (c) => state.setVwapColor(c)),
              ],
            ),
          ),
        ],
      );
      if (widget.noDrawer) return body;
      return Drawer(width: 360, child: body);
    });
  }
}