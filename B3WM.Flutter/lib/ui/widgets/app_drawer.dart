import 'package:flutter/material.dart';
import 'config_drawer.dart';
import 'bubble_drawer.dart';

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
    _tabController = TabController(length: 2, vsync: this, initialIndex: widget.initialTab);
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
      width: 360,
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
                Tab(text: 'Configurações', icon: Icon(Icons.settings, size: 18)),
                Tab(text: 'Bubbles', icon: Icon(Icons.bubble_chart, size: 18)),
              ],
            ),
          ),
          Expanded(
            child: TabBarView(
              controller: _tabController,
              children: const [
                ConfigDrawer(noDrawer: true),
                BubbleDrawer(noDrawer: true),
              ],
            ),
          ),
        ],
      ),
      ),
    );
  }
}
