import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../services/state_service.dart';
import '../chart/daily_chart_data.dart';
import '../chart/map_flow_chart.dart';
import 'daily_drawer.dart';

/// Painel inferior do widget de análise diária (issue #12): divide a tela
/// com o gráfico principal (intraday em cima, 1D embaixo).
///
/// Espelha o padrão do intraday: toolbar própria com botões que abrem o
/// `DailyDrawer` (drawer à esquerda, confinado ao split) na aba correspondente.
/// O gráfico 1D (timeframe fixo 1440, sem seletor) ocupa o restante.
///
/// A altura é controlada pela divisória arrastável no `main.dart`
/// (`StateService.dailyPanelFraction`).
class DailyPanel extends StatefulWidget {
  const DailyPanel({super.key});

  @override
  State<DailyPanel> createState() => _DailyPanelState();
}

class _DailyPanelState extends State<DailyPanel> {
  final _scaffoldKey = GlobalKey<ScaffoldState>();
  int _drawerTab = 0;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) context.read<StateService>().loadDailyAll();
    });
  }

  void _openTab(int tab) {
    setState(() => _drawerTab = tab);
    _scaffoldKey.currentState?.openDrawer();
  }

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      decoration: const BoxDecoration(
        border: Border(top: BorderSide(color: Color(0xFF3d3d3d))),
      ),
        child: Scaffold(
          key: _scaffoldKey,
          backgroundColor: const Color(0xFF2d2d2d),
          drawer: DailyDrawer(initialTab: _drawerTab),
          body: Consumer<StateService>(builder: (context, state, _) {
            return Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                SizedBox(
                  height: 40,
                  // Barra de controles sem título: o rótulo "Análise diária
                  // (1D)" vive no slider (_DailyDivider/_ExpandDailyBar),
                  // idêntico aberto/fechado. Fechamento só pelo slider.
                  child: Row(
                    children: [
                      const SizedBox(width: 4),
                      IconButton(
                        icon: const Icon(Icons.stacked_line_chart,
                            size: 20, color: Colors.grey),
                        onPressed: () => _openTab(0),
                        tooltip: 'Estrutura 1D',
                        visualDensity: VisualDensity.compact,
                      ),
                      IconButton(
                        icon: const Icon(Icons.align_horizontal_right,
                            size: 20, color: Colors.grey),
                        onPressed: () => _openTab(1),
                        tooltip: 'Volume Profile 1D',
                        visualDensity: VisualDensity.compact,
                      ),
                      IconButton(
                        icon: const Icon(Icons.terrain,
                            size: 20, color: Colors.grey),
                        onPressed: () => _openTab(2),
                        tooltip: 'Topos/Vales 1D',
                        visualDensity: VisualDensity.compact,
                      ),
                      const Spacer(),
                      Chip(
                        label: Text(state.symbol,
                            style: const TextStyle(
                                fontSize: 11, color: Colors.white)),
                        backgroundColor: Colors.grey.shade800,
                        materialTapTargetSize:
                            MaterialTapTargetSize.shrinkWrap,
                        visualDensity: VisualDensity.compact,
                        padding: EdgeInsets.zero,
                      ),
                      const SizedBox(width: 8),
                    ],
                  ),
                ),
                const Divider(
                    height: 1, color: Color(0xFF3d3d3d)),
                Expanded(
                  child: (state.isDailyLoading &&
                          state.dailyBars.isEmpty)
                      ? const Center(
                          child: CircularProgressIndicator())
                      : state.dailyBars.isEmpty
                          ? const Center(
                              child: Text(
                                  'Sem candles 1D — abra uma aba de configuração e toque em Atualizar',
                                  style: TextStyle(
                                      color: Colors.grey,
                                      fontSize: 12)))
                          : MapFlowChart(
                              key: ValueKey(
                                  'daily_${state.symbol}'),
                              data: buildDailyChartData(state),
                            ),
                ),
              ],
            );
          }),
        ),
    );
  }
}
