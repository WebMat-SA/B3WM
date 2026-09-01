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
            onTradingConfigTap: () {
              setState(() => _drawerTabIndex = 3);
              _scaffoldKey.currentState?.openDrawer();
            },
            onExtremeTap: () {
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
                    : Column(
                        children: [
                          Expanded(
                            child: Row(
                              children: [
                                Expanded(
                                  child: MapFlowChart(
                                    key: ValueKey('chart_${state.symbol}_${state.timeFrame}'),
                                    data: buildChartData(state),
                                  ),
                                ),
                                if (state.tradingPanelVisible)
                                  TradingDrawer(
                                    key: ValueKey(
                                        'trading_${state.symbol}'),
                                  ),
                              ],
                            ),
                          ),
                        ],
                      ),
          ),
        );
      },
    );
  }
}
