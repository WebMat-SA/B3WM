import 'package:flutter/material.dart';

import 'daily_extreme_tab.dart';
import 'daily_pivot_tab.dart';
import 'daily_structure_tab.dart';
import 'daily_volume_tab.dart';

/// Drawer de configurações do widget diário (issue #12), espelhando o
/// padrão do intraday (`AppDrawer`): toolbar do split abre aqui na aba
/// correspondente (0=Estruturas, 1=Volume, 2=Topos/Vales, 3=Pivot).
class DailyDrawer extends StatefulWidget {
  final int initialTab;
  const DailyDrawer({super.key, this.initialTab = 0});

  @override
  State<DailyDrawer> createState() => _DailyDrawerState();
}

class _DailyDrawerState extends State<DailyDrawer>
    with SingleTickerProviderStateMixin {
  late TabController _tabController;

  @override
  void initState() {
    super.initState();
    _tabController =
        TabController(length: 4, vsync: this, initialIndex: widget.initialTab);
  }

  @override
  void didUpdateWidget(DailyDrawer oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (widget.initialTab != oldWidget.initialTab) {
      _tabController.animateTo(widget.initialTab);
    }
  }

  @override
  void dispose() {
    _tabController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Drawer(
      width: 380,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.zero,
        side: BorderSide(color: Color(0xFF3d3d3d)),
      ),
      child: ListTileTheme(
        tileColor: const Color(0xFF2d2d2d),
        child: Column(
          children: [
            Container(
              color: const Color(0xFF2d2d2d),
              child: TabBar(
                controller: _tabController,
                indicatorColor: Colors.blue,
                labelColor: Colors.blue,
                unselectedLabelColor: Colors.grey,
                tabs: const [
                  Tab(
                    icon: Tooltip(
                      message: 'Estrutura 1D',
                      child: Icon(Icons.stacked_line_chart, size: 18),
                    ),
                  ),
                  Tab(
                    icon: Tooltip(
                      message: 'Volume Profile 1D',
                      child:
                          Icon(Icons.align_horizontal_right, size: 18),
                    ),
                  ),
                  Tab(
                    icon: Tooltip(
                      message: 'Topos/Vales 1D',
                      child: Icon(Icons.terrain, size: 18),
                    ),
                  ),
                  Tab(
                    icon: Tooltip(
                      message: 'Pivot Tradicional 1D',
                      child: Icon(Icons.drag_handle, size: 18),
                    ),
                  ),
                ],
              ),
            ),
            Expanded(
              child: TabBarView(
                controller: _tabController,
                children: const [
                  SingleChildScrollView(child: DailyStructureTab()),
                  SingleChildScrollView(child: DailyVolumeTab()),
                  SingleChildScrollView(child: DailyExtremeTab()),
                  SingleChildScrollView(child: DailyPivotTab()),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}
