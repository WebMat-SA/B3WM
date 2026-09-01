import 'package:flutter/material.dart';
import 'bubble_drawer.dart';
import 'structure_drawer.dart';
import 'volume_profile_drawer.dart';
import 'trading_config_drawer.dart';
import 'extreme_drawer.dart';
import 'date_range_tab.dart';
// Verifier desabilitado. Para reativar, re-importe 'verifier_drawer.dart',
// volte o TabController para 6 e re-adicione a Tab/VerifierDrawer abaixo.

class AppDrawer extends StatefulWidget {
  final int initialTab;
  const AppDrawer({super.key, this.initialTab = 0});

  @override
  State<AppDrawer> createState() => _AppDrawerState();
}

class _AppDrawerState extends State<AppDrawer>
    with SingleTickerProviderStateMixin {
  late TabController _tabController;

  @override
  void initState() {
    super.initState();
    _tabController = TabController(length: 6, vsync: this, initialIndex: widget.initialTab);
  }

  @override
  void didUpdateWidget(AppDrawer oldWidget) {
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
                    message: 'Bubbles',
                    child: Icon(Icons.bubble_chart, size: 18),
                  ),
                ),
                Tab(
                  icon: Tooltip(
                    message: 'Estrutura',
                    child: Icon(Icons.stacked_line_chart, size: 18),
                  ),
                ),
                Tab(
                  icon: Tooltip(
                    message: 'Volume Profile',
                    child: Icon(Icons.align_horizontal_right, size: 18),
                  ),
                ),
                Tab(
                  icon: Tooltip(
                    message: 'Trading Data',
                    child: Icon(Icons.show_chart, size: 18),
                  ),
                ),
                Tab(
                  icon: Tooltip(
                    message: 'Topos/Vales',
                    child: Icon(Icons.linear_scale, size: 18),
                  ),
                ),
                Tab(
                  icon: Tooltip(
                    message: 'Período',
                    child: Icon(Icons.date_range, size: 18),
                  ),
                ),
              ],
            ),
          ),
          Expanded(
            child: TabBarView(
              controller: _tabController,
children: const [
                BubbleDrawer(noDrawer: true),
                StructureDrawer(noDrawer: true),
                VolumeProfileDrawer(noDrawer: true),
                TradingConfigDrawer(noDrawer: true),
                ExtremeDrawer(noDrawer: true),
                DateRangeTab(noDrawer: true),
              ],
            ),
          ),
        ],
      ),
      ),
    );
  }
}
