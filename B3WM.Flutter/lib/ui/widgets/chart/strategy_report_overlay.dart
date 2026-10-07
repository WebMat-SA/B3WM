import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../../services/state_service.dart';
import '../../../services/strategy_service.dart';
import '../strategy_report_list.dart';
import 'chart_fixed_painter.dart';

/// Relatório da sessão sobreposto ao gráfico (canto superior esquerdo).
///
/// Aparece só com sessão ativa (não pausada e marcada como visível);
/// altura máxima = altura do gráfico, com rolagem própria; ancorado no
/// topo. Dispensa a aba Estratégia aberta para acompanhar o retorno.
/// Uma “aba” por sessão ativa (seletor simples, sem TabController para
/// não criar/descartar tickers durante o build do gráfico).
class StrategyReportOverlay extends StatefulWidget {
  final double maxHeight;
  const StrategyReportOverlay({super.key, required this.maxHeight});

  @override
  State<StrategyReportOverlay> createState() =>
      _StrategyReportOverlayState();
}

class _StrategyReportOverlayState extends State<StrategyReportOverlay> {
  int _tabIndex = 0;
  int _lastSeenSessions = -1;
  int _lastSeenItems = -1;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) context.read<StateService>().ensureStrategyPolling();
    });
  }

  @override
  Widget build(BuildContext context) {
    return Consumer<StateService>(builder: (context, state, _) {
      final sessions = state.strategySessions
          .where((s) =>
              !s.paused &&
              state.isStrategySessionVisible(s.sessionId) &&
              (s.symbol == state.symbol || state.symbol.isEmpty))
          .toList();
      if (sessions.isEmpty) return const SizedBox.shrink();
      if (_tabIndex >= sessions.length) _tabIndex = 0;
      final session = sessions[_tabIndex];

      // Diagnóstico (só quando muda): confirma que o overlay enxerga dados.
      final totalItems =
          sessions.fold<int>(0, (n, s) => n + s.reportItems.length);
      if (sessions.length != _lastSeenSessions ||
          totalItems != _lastSeenItems) {
        _lastSeenSessions = sessions.length;
        _lastSeenItems = totalItems;
        debugPrint(
            '[overlay] sessões=${sessions.length} itens=$totalItems');
      }

      final maxH = widget.maxHeight.isFinite && widget.maxHeight > 80
          ? widget.maxHeight
          : 200.0;

      return Positioned(
        left: ChartFixedPainter.marginLeft + 8,
        top: ChartFixedPainter.marginTop + 8,
        child: ConstrainedBox(
          constraints: BoxConstraints(
            maxWidth: 340,
            maxHeight: maxH,
          ),
          // Largura se adequa aos dados (encolhe quando as linhas são
          // curtas); o teto evita ocupar o gráfico inteiro.
          child: IntrinsicWidth(
            child: Material(
              color: Colors.black.withValues(
                  alpha: state.strategyOverlayOpacity),
              borderRadius: BorderRadius.circular(6),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                    Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        for (var i = 0; i < sessions.length; i++)
                          GestureDetector(
                            onTap: () => setState(() => _tabIndex = i),
                            child: Container(
                              padding: const EdgeInsets.symmetric(
                                  horizontal: 8, vertical: 8),
                              decoration: BoxDecoration(
                                border: Border(
                                  bottom: BorderSide(
                                    color: i == _tabIndex
                                        ? Colors.blue
                                        : Colors.transparent,
                                    width: 2,
                                  ),
                                ),
                              ),
                              child: Text(
                                '${sessions[i].strategy} ${sessions[i].position ?? 'flat'}',
                                style: TextStyle(
                                  fontSize: 11,
                                  color: i == _tabIndex
                                      ? Colors.white
                                      : Colors.grey,
                                  fontWeight: i == _tabIndex
                                      ? FontWeight.bold
                                      : FontWeight.normal,
                                ),
                              ),
                            ),
                          ),
                      ],
                    ),
                    Flexible(
                      child: SingleChildScrollView(
                        padding: const EdgeInsets.all(8),
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              if (session.reportItems.isEmpty)
                              const Text('Sem itens ainda.',
                                  style: TextStyle(
                                      fontSize: 11, color: Colors.grey))
                            else
                              StrategyReportList(
                                  items: session.reportItems),
                          ],
                        ),
                      ),
                    ),
                ],
              ),
            ),
          ),
        )
      );
    });
  }
}
