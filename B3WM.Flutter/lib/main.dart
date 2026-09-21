import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'services/api_service.dart';
import 'services/signalr_service.dart';
import 'services/preferences_service.dart';
import 'services/state_service.dart';
import 'services/trading_service.dart';
import 'services/audio_service.dart';
import 'ui/widgets/app_bar_widget.dart';
import 'ui/widgets/app_drawer.dart';
import 'ui/widgets/daily/daily_panel.dart';
import 'ui/widgets/trading_drawer.dart';
import 'ui/widgets/chart/map_flow_chart.dart';
import 'ui/widgets/chart/chart_data.dart';

void main() async {
  WidgetsFlutterBinding.ensureInitialized();

  final baseUrl = 'https://localhost:5002';
  final apiService = ApiService(baseUrl: baseUrl);
  final tradingApiService = TradingApiService(baseUrl: baseUrl);
  final signalRService = SignalRService(
    hubUrl: '$baseUrl/api/datahub',
    apiService: apiService,
  );
  final preferencesService = PreferencesService();
  await preferencesService.init();
  final audioService = AudioService();

  runApp(
    MultiProvider(
      providers: [
        Provider<ApiService>.value(value: apiService),
        Provider<TradingApiService>.value(value: tradingApiService),
        Provider<SignalRService>.value(value: signalRService),
        Provider<PreferencesService>.value(value: preferencesService),
        Provider<AudioService>.value(value: audioService),
        ChangeNotifierProvider<StateService>(
          create: (_) => StateService(
            apiService: apiService,
            signalRService: signalRService,
            preferencesService: preferencesService,
            audioService: audioService,
          ),
        ),
      ],
      child: const B3WMApp(),
    ),
  );
}

class B3WMApp extends StatelessWidget {
  const B3WMApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'B3WM Map Flow',
      debugShowCheckedModeBanner: false,
      theme: ThemeData.dark(useMaterial3: true).copyWith(
        colorScheme: ColorScheme.dark(
          surface: const Color(0xFF1e1e1e),
          primary: Colors.blueGrey,
          secondary: Colors.amber,
        ),
        scaffoldBackgroundColor: const Color(0xFF1e1e1e),
        appBarTheme: const AppBarTheme(
          backgroundColor: Color(0xFF2d2d2d),
          elevation: 0,
        ),
        drawerTheme: const DrawerThemeData(
          backgroundColor: Color(0xFF2d2d2d),
        ),
      ),
      home: const NewMapFlowPage(),
    );
  }
}

class NewMapFlowPage extends StatefulWidget {
  const NewMapFlowPage({super.key});

  @override
  State<NewMapFlowPage> createState() => _NewMapFlowPageState();
}

class _NewMapFlowPageState extends State<NewMapFlowPage> {
  final _scaffoldKey = GlobalKey<ScaffoldState>();
  int _drawerTabIndex = 0;

  @override
  Widget build(BuildContext context) {
    return Consumer<StateService>(
      builder: (context, state, _) {
        return Scaffold(
          key: _scaffoldKey,
          appBar: MapFlowAppBar(
            onBubblesTap: () {
              setState(() => _drawerTabIndex = 0);
              _scaffoldKey.currentState?.openDrawer();
            },
            onStructureTap: () {
              setState(() => _drawerTabIndex = 1);
              _scaffoldKey.currentState?.openDrawer();
            },
            onVolumeProfileTap: () {
              setState(() => _drawerTabIndex = 2);
              _scaffoldKey.currentState?.openDrawer();
            },
            onExtremeTap: () {
              setState(() => _drawerTabIndex = 3);
              _scaffoldKey.currentState?.openDrawer();
            },
            onVwapTap: () {
              setState(() => _drawerTabIndex = 4);
              _scaffoldKey.currentState?.openDrawer();
            },
            onDateRangeTap: () {
              setState(() => _drawerTabIndex = 5);
              _scaffoldKey.currentState?.openDrawer();
            },
            // onVerifierTap: () {
            //   setState(() => _drawerTabIndex = 4);
            //   _scaffoldKey.currentState?.openDrawer();
            // },
            onTradingTap: () =>
                state.setTradingPanelVisible(!state.tradingPanelVisible),
            tradingActive: state.tradingPanelVisible,
          ),
          drawer: AppDrawer(initialTab: _drawerTabIndex),
          body: SafeArea(
            child: state.isLoading
                ? const Center(child: CircularProgressIndicator())
                : state.symbol.isEmpty
                    ? const Center(
                        child: Text('Selecione um símbolo para começar',
                            style: TextStyle(color: Colors.grey)))
                    : LayoutBuilder(
                        builder: (context, constraints) {
                          final totalH = constraints.maxHeight;
                          final panelH = (totalH *
                                  state.dailyPanelFraction)
                              .clamp(220.0, totalH - 200.0);
                          return Row(
                            crossAxisAlignment: CrossAxisAlignment.stretch,
                            children: [
                              Expanded(
                                child: Column(
                                  children: [
                                    Expanded(
                                      child: MapFlowChart(
                                        key: ValueKey(
                                            'chart_${state.symbol}_${state.timeFrame}'),
                                        data: buildChartData(state),
                                      ),
                                    ),
                                    if (state.dailyPanelVisible) ...[
                                      _DailyDivider(
                                        // A divisória é a borda superior do painel:
                                        // arrastar p/ baixo diminui, p/ cima aumenta.
                                        onDelta: (dy) => state
                                            .previewDailyPanelFraction(
                                          state.dailyPanelFraction -
                                              dy / totalH,
                                        ),
                                        onDragEnd: () =>
                                            state.commitDailyPanelFraction(),
                                        onCollapse: () => state
                                            .setDailyPanelVisible(false),
                                      ),
                                      SizedBox(
                                        height: panelH,
                                        child: const DailyPanel(),
                                      ),
                                    ] else
                                      _ExpandDailyBar(
                                        onExpand: () => state
                                            .setDailyPanelVisible(true),
                                      ),
                                  ],
                                ),
                              ),
                              // Painel de trading com animação de abertura/
                              // fechamento: desliza da direita enquanto a
                              // largura ancora de 320 -> 0, de modo que o
                              // gráfico redimensiona junto em vez de pular.
                              AnimatedSwitcher(
                                duration: const Duration(milliseconds: 300),
                                switchInCurve: Curves.easeInOut,
                                switchOutCurve: Curves.easeInOut,
                                transitionBuilder: (child, animation) {
                                  return ClipRect(
                                    child: SizeTransition(
                                      sizeFactor: animation,
                                      axis: Axis.horizontal,
                                      axisAlignment: -1,
                                      child: SlideTransition(
                                        position: Tween<Offset>(
                                          begin: const Offset(1, 0),
                                          end: Offset.zero,
                                        ).animate(animation),
                                        child: child,
                                      ),
                                    ),
                                  );
                                },
                                child: state.tradingPanelVisible
                                    ? TradingDrawer(
                                        key: ValueKey(
                                            'trading_${state.symbol}'),
                                      )
                                    : const SizedBox.shrink(
                                        key: ValueKey('trading_hidden')),
                              ),
                            ],
                          );
                        },
                      ),
          ),
        );
      },
    );
  }
}

/// Divisória arrastável entre o gráfico principal e o painel diário.
/// Arrastar para cima aumenta o painel; para baixo diminui. Mostra o mesmo
/// rótulo da barra colapsada ("Análise diária (1D)") para manter o slider
/// idêntico aberto/fechado; clique (centro) recolhe o painel.
class _DailyDivider extends StatelessWidget {
  final ValueChanged<double> onDelta;
  final VoidCallback onCollapse;
  final Future<void> Function() onDragEnd;
  const _DailyDivider(
      {required this.onDelta,
      required this.onCollapse,
      required this.onDragEnd});

  @override
  Widget build(BuildContext context) {
    return MouseRegion(
      cursor: SystemMouseCursors.resizeUpDown,
      child: GestureDetector(
        behavior: HitTestBehavior.opaque,
        onTap: onCollapse,
        onVerticalDragUpdate: (d) => onDelta(d.delta.dy),
        onVerticalDragEnd: (_) async {
          await onDragEnd();
        },
        child: Container(
          height: 24,
          color: const Color(0xFF2d2d2d),
          child: Row(
            children: [
              const Expanded(
                child: Divider(
                    color: Color(0xFF3d3d3d), thickness: 1, height: 1),
              ),
              IconButton(
                icon: const Icon(Icons.keyboard_arrow_down,
                    size: 20, color: Colors.grey),
                onPressed: onCollapse,
                tooltip: 'Ocultar análise diária',
                visualDensity: VisualDensity.compact,
                padding: EdgeInsets.zero,
                constraints: const BoxConstraints(),
              ),
              const Text('Análise diária (1D)',
                  style: TextStyle(fontSize: 11, color: Colors.grey)),
              const SizedBox(width: 4),
              const Expanded(
                child: Divider(
                    color: Color(0xFF3d3d3d), thickness: 1, height: 1),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// Barra fina acima da borda inferior que expande o painel diário.
/// Substitui o antigo botão da AppBar: o controle fica junto ao splitter.
class _ExpandDailyBar extends StatelessWidget {
  final VoidCallback onExpand;
  const _ExpandDailyBar({required this.onExpand});

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      behavior: HitTestBehavior.opaque,
      onTap: onExpand,
      child: Container(
        height: 24,
        color: const Color(0xFF2d2d2d),
        child: Row(
          children: [
            const Expanded(
              child:
                  Divider(color: Color(0xFF3d3d3d), thickness: 1, height: 1),
            ),
            IconButton(
              icon: const Icon(Icons.keyboard_arrow_up,
                  size: 20, color: Colors.grey),
              onPressed: onExpand,
              tooltip: 'Mostrar análise diária (1D)',
              visualDensity: VisualDensity.compact,
              padding: EdgeInsets.zero,
              constraints: const BoxConstraints(),
            ),
            const Text('Análise diária (1D)',
                style: TextStyle(fontSize: 11, color: Colors.grey)),
            const SizedBox(width: 4),
            const Expanded(
              child:
                  Divider(color: Color(0xFF3d3d3d), thickness: 1, height: 1),
            ),
          ],
        ),
      ),
    );
  }
}
