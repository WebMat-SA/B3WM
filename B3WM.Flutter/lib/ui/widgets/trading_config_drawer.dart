import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../services/state_service.dart';
import 'drawer_controls.dart';

class TradingConfigDrawer extends StatefulWidget {
  final bool noDrawer;
  const TradingConfigDrawer({super.key, this.noDrawer = false});

  @override
  State<TradingConfigDrawer> createState() => _TradingConfigDrawerState();
}

class _TradingConfigDrawerState extends State<TradingConfigDrawer>
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
            icon: Icons.show_chart,
            title: 'Configurações',
            defaultExpanded: true,
            child: Column(
              children: [
                ToggleRow('Mostrar Histórico de Trades',
                    state.tradingHistoryVisible,
                    (v) => state.setTradingHistoryVisible(v)),
                ToggleRow('Mostrar Posições', state.positionVisible,
                    (v) => state.setPositionVisible(v)),
                ToggleRow('Mostrar Ordens em Aberto', state.openOrdersVisible,
                    (v) => state.setOpenOrdersVisible(v)),
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
