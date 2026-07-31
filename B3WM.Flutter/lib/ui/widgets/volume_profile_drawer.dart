import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../services/state_service.dart';
import 'drawer_controls.dart';
import 'time_range_slider.dart';

class VolumeProfileDrawer extends StatefulWidget {
  final bool noDrawer;
  const VolumeProfileDrawer({super.key, this.noDrawer = false});

  @override
  State<VolumeProfileDrawer> createState() => _VolumeProfileDrawerState();
}

class _VolumeProfileDrawerState extends State<VolumeProfileDrawer> {
  @override
  Widget build(BuildContext context) {
    return Consumer<StateService>(builder: (context, state, _) {
      final body = ListView(
        padding: EdgeInsets.zero,
        children: [
          const TimeRangeSlider(),
          ExpandableSection(
            icon: Icons.align_horizontal_right,
            title: 'Configurações',
            defaultExpanded: false,
            child: Column(
              children: [
                ToggleRow('Show on Chart', state.profileVisible,
                    (v) => state.setProfileVisible(v)),
                ToggleRow('Auto Mode (por Estrutura)',
                    state.profileAutoByPriceStructure,
                    (v) => state.setProfileAutoByPriceStructure(v)),
                SliderRow('Size (Horizontal)', state.profileSizeH, 0, 3,
                    (v) => state.setProfileSizeH(v)),
                SliderRow('Size (Vertical)', state.profileSizeV, 0, 10,
                    (v) => state.setProfileSizeV(v)),
                SliderRow('Opacity', state.profileOpacity, 0, 1,
                    (v) => state.setProfileOpacity(v)),
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
