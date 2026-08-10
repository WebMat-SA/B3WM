import 'package:flutter/material.dart';
import 'bubble_drawer.dart';
import 'structure_drawer.dart';
import 'volume_profile_drawer.dart';
import 'trading_config_drawer.dart';
import 'verifier_drawer.dart';

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
    _tabController = TabController(length: 5, vsync: this, initialIndex: widget.initialTab);
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
                Tab(text: 'Bubbles', icon: Icon(Icons.bubble_chart, size: 18)),
                Tab(text: 'Estrutura', icon: Icon(Icons.stacked_line_chart, size: 18)),
                Tab(text: 'Volume Profile', icon: Icon(Icons.align_horizontal_right, size: 18)),
                Tab(text: 'Trading Data', icon: Icon(Icons.show_chart, size: 18)),
                Tab(text: 'Verifier', icon: Icon(Icons.verified, size: 18)),
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
                VerifierDrawer(noDrawer: true),
              ],
            ),
          ),
        ],
      ),
      ),
    );
  }
}
