import 'dart:async';
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../services/state_service.dart';
import '../../services/strategy_service.dart';
import 'drawer_controls.dart';
import 'strategy_report_list.dart';
import '../../app_log.dart';

/// Aba Estratégia: escolhe dentre todas as IStrategy do backend, configura o
/// gatilho (fechamento de candle / bubble / proximidade) + params e dá PLAY.
/// O PLAY envia a foto da tela (snapshot) e TRAVA os filtros — para alterar:
/// PAUSE/STOP, edita, PLAY de novo. Execução toda no backend (pode fechar o
/// app). Tudo log-only (envio real comentado no runner).
class StrategyDrawer extends StatefulWidget {
  final bool noDrawer;
  const StrategyDrawer({super.key, this.noDrawer = false});

  @override
  State<StrategyDrawer> createState() => _StrategyDrawerState();
}

class _StrategyDrawerState extends State<StrategyDrawer>
    with AutomaticKeepAliveClientMixin {
  @override
  bool get wantKeepAlive => true;

  List<StrategyInfo> _strategies = [];
  String? _selected;
  bool _busy = false;
  String? _evalResult;

  late final StrategyService _svc = StrategyService();

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => _refreshAll());
  }

  @override
  void dispose() {
    _svc.dispose();
    super.dispose();
  }

  Future<void> _refreshAll() async {
    final list = await _svc.list();
    if (!mounted) return;
    setState(() {
      _strategies = list;
      _selected ??= list.isNotEmpty ? list.first.name : null;
    });
    if (mounted) context.read<StateService>().refreshStrategySessions();
  }

  Future<void> _play() async {
    final state = context.read<StateService>();
    if (_selected == null || state.symbol.isEmpty) return;
    setState(() => _busy = true);
    try {
      // Tab só manda strategy + foto da tela; config 100% no código.
      final res = await _svc.arm(
        strategy: _selected!,
        symbol: state.symbol,
        screenConfig: state.currentScreenConfig,
        displayDate: state.displayDate,
      );
      if (res != null) {
        logD(
            '[strategy] PLAY $_selected snap=${res['snapshotHash']} sessão=${res['sessionId']}');
      } else {
        logD('[strategy] PLAY falhou (ver backend)');
      }
      await _refreshSessions();
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _refreshSessions() async {
    if (!mounted) return;
    await context.read<StateService>().refreshStrategySessions();
  }

  Future<void> _pause(String id, bool paused) async {
    await _svc.pause(id, paused);
    await _refreshSessions();
  }

  Future<void> _stop(String id) async {
    final log = await _svc.stop(id);
    if (log != null) {
      logD(
          '[strategy] STOP trades=${(log['paperTrades'] as List?)?.length} pts=${log['realizedPts']}');
    }
    await _refreshSessions();
  }

  Future<void> _evaluateNow() async {
    final state = context.read<StateService>();
    if (_selected == null || state.symbol.isEmpty) return;
    setState(() {
      _busy = true;
      _evalResult = null;
    });
    try {
      final res = await _svc.evaluate(
        strategy: _selected!,
        symbol: state.symbol,
        screenConfig: state.currentScreenConfig,
      );
      final side = res?['side'] ?? res?['Side'] ?? '?';
      final conf = res?['confidence'] ?? res?['Confidence'] ?? 0;
      setState(() => _evalResult = '$side conf=$conf (só log)');
      logD('[strategy] avulso $side conf=$conf');
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    super.build(context);
    return Consumer<StateService>(builder: (context, state, _) {
      final info =
          _strategies.where((s) => s.name == _selected).firstOrNull;
      final body = ListView(
        padding: EdgeInsets.zero,
        children: [
          if (state.configLocked)
            Container(
              padding: const EdgeInsets.all(12),
              color: Colors.orange.withValues(alpha: 0.12),
              child: const Row(
                children: [
                  Icon(Icons.lock, size: 18, color: Colors.orange),
                  SizedBox(width: 8),
                  Expanded(
                    child: Text(
                      'Estratégia rodando — filtros travados. PAUSE/STOP para alterar.',
                      style: TextStyle(fontSize: 12, color: Colors.orange),
                    ),
                  ),
                ],
              ),
            ),
          ExpandableSection(
            icon: Icons.play_arrow,
            title: 'Rodar estratégia',
            defaultExpanded: true,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Text('Estratégia', style: TextStyle(fontSize: 13)),
                DropdownButton<String>(
                  value: _selected,
                  isExpanded: true,
                  dropdownColor: const Color(0xFF2d2d2d),
                  items: _strategies
                      .map((s) => DropdownMenuItem(
                          value: s.name, child: Text(s.name)))
                      .toList(),
                  onChanged: state.configLocked
                      ? null
                      : (v) {
                          setState(() => _selected = v);
                        },
                ),
                if (info != null)
                  Text(info.description,
                      style:
                          const TextStyle(fontSize: 11, color: Colors.grey)),
                const SizedBox(height: 8),
                const Text(
                  'Gatilho e parâmetros moram no código da strategy. '
                  'O PLAY congela a foto dos filtros da tela.',
                  style: TextStyle(fontSize: 11, color: Colors.grey),
                ),
                const SizedBox(height: 8),
                if (state.configLocked)
                  Container(
                    width: double.infinity,
                    padding: const EdgeInsets.all(12),
                    decoration: BoxDecoration(
                      color: Colors.green.withValues(alpha: 0.10),
                      borderRadius: BorderRadius.circular(4),
                      border: Border.all(
                          color: Colors.green.withValues(alpha: 0.35)),
                    ),
                    child: Text(
                      '▶ ${_runningDesc(state)} — acompanhando abaixo e no gráfico, sem precisar fazer nada.\nPara armar outra sessão, pare a atual.',
                      style:
                          const TextStyle(fontSize: 12, color: Colors.green),
                    ),
                  ),
                if (!state.configLocked)
                  Row(
                    children: [
                      Expanded(
                        child: FilledButton.icon(
                          onPressed: _busy ? null : _play,
                          icon: const Icon(Icons.play_arrow, size: 18),
                          label: const Text('PLAY'),
                        ),
                      ),
                      const SizedBox(width: 8),
                      Expanded(
                        child: OutlinedButton.icon(
                          onPressed: _busy ? null : _evaluateNow,
                          icon: const Icon(Icons.analytics, size: 18),
                          label: const Text('Avaliar agora'),
                        ),
                      ),
                    ],
                  ),
                if (state.configLocked)
                  Padding(
                    padding: const EdgeInsets.only(top: 8),
                    child: SizedBox(
                      width: double.infinity,
                      child: OutlinedButton.icon(
                        onPressed: _busy ? null : _evaluateNow,
                        icon: const Icon(Icons.analytics, size: 18),
                        label: const Text('Avaliar agora'),
                      ),
                    ),
                  ),
                if (_evalResult != null)
                  Padding(
                    padding: const EdgeInsets.only(top: 4),
                    child: Text(_evalResult!,
                        style: const TextStyle(
                            fontSize: 12, color: Colors.lightBlue)),
                  ),
                const Padding(
                  padding: EdgeInsets.only(top: 4),
                  child: Text(
                    'PLAY envia a foto dos filtros e trava tudo. Pode fechar o app.',
                    style: TextStyle(fontSize: 11, color: Colors.grey),
                  ),
                ),
              ],
            ),
          ),
          ExpandableSection(
            icon: Icons.visibility,
            title: 'Exibição no gráfico',
            defaultExpanded: true,
            child: Column(
              children: [
                SliderRow(
                    'Opacidade do relatório',
                    state.strategyOverlayOpacity,
                    0.01,
                    1.0, (v) => state.setStrategyOverlayOpacity(v)),
                const Text(
                  'Vale com sessão rodando: só muda a exibição local.',
                  style: TextStyle(fontSize: 11, color: Colors.grey),
                ),
              ],
            ),
          ),
          ExpandableSection(
            icon: Icons.list,
            title: 'Sessões (${_visibleSessions(state).length})',
            defaultExpanded: true,
            child: _visibleSessions(state).isEmpty
                ? const Text('Nenhuma sessão.',
                    style: TextStyle(fontSize: 12, color: Colors.grey))
                : Column(
                    children: _visibleSessions(state)
                        .map((s) => Card(
                              color: const Color(0xFF1e1e1e),
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  ListTile(
                                    dense: true,
                                    title: Text(
                                        '${s.strategy} ${s.paused ? '(pausada)' : ''}',
                                        style:
                                            const TextStyle(fontSize: 13)),
                                    subtitle: Text(
                                      'pos=${s.position ?? 'flat'} dec=${s.decisions} pts=${s.realizedPts.toStringAsFixed(0)} snap=${s.snapshotHash}\n${s.lastDecision ?? ''}',
                                      style: const TextStyle(
                                          fontSize: 11, color: Colors.grey),
                                    ),
                                    trailing: Row(
                                      mainAxisSize: MainAxisSize.min,
                                      children: [
                                        IconButton(
                                          icon: Icon(
                                              state.isStrategySessionVisible(
                                                      s.sessionId)
                                                  ? Icons.visibility
                                                  : Icons.visibility_off,
                                              size: 20,
                                              color: state
                                                      .isStrategySessionVisible(
                                                          s.sessionId)
                                                  ? Colors.grey
                                                  : Colors.grey.shade700),
                                          tooltip:
                                              'Mostrar/ocultar no gráfico',
                                          onPressed: () => state
                                              .setStrategySessionVisible(
                                                  s.sessionId,
                                                  !state.isStrategySessionVisible(
                                                      s.sessionId)),
                                        ),
                                        IconButton(
                                          icon: Icon(
                                              s.paused
                                                  ? Icons.play_arrow
                                                  : Icons.pause,
                                              size: 20),
                                          tooltip: s.paused
                                              ? 'Retomar'
                                              : 'Pausar',
                                          onPressed: () =>
                                              _pause(s.sessionId, !s.paused),
                                        ),
                                        IconButton(
                                          icon:
                                              const Icon(Icons.stop, size: 20),
                                          tooltip:
                                              'Parar (encerra + relatório)',
                                          onPressed: () =>
                                              _stop(s.sessionId),
                                        ),
                                      ],
                                    ),
                                  ),
                                  if (s.reportItems.isNotEmpty)
                                    Container(
                                      margin: const EdgeInsets.only(
                                          left: 12, right: 12, bottom: 8),
                                      padding: const EdgeInsets.all(8),
                                      decoration: BoxDecoration(
                                        color: const Color(0xFF141414),
                                        borderRadius: BorderRadius.circular(4),
                                        border: Border.all(
                                            color: const Color(0xFF3d3d3d)),
                                      ),
                                      child: Column(
                                        crossAxisAlignment:
                                            CrossAxisAlignment.start,
                                        children: [
                                          const Text('Relatório do dia',
                                              style: TextStyle(
                                                  fontSize: 11,
                                                  color: Colors.grey,
                                                  fontWeight:
                                                      FontWeight.bold)),
                                          const SizedBox(height: 4),
                                          StrategyReportList(
                                              items: s.reportItems),
                                        ],
                                      ),
                                    ),
                                ],
                              ),
                            ))
                        .toList(),
                  ),
          ),
        ],
      );
      if (widget.noDrawer) return body;
      return Drawer(width: 360, child: body);
    });
  }

  /// Sessões do símbolo atual (mesmo filtro de antes, agora do state central).
  /// Descreve as sessões ativas p/ o aviso de "já rodando".
  String _runningDesc(StateService state) {
    final active = state.strategySessions.where((s) => !s.paused).toList();
    if (active.isEmpty) return 'Nenhuma sessão ativa';
    return active.map((s) => '${s.strategy} em ${s.symbol}').join(' • ');
  }

  List<StrategySession> _visibleSessions(StateService state) =>
      state.strategySessions
          .where((s) =>
              s.symbol == state.symbol || state.symbol.isEmpty)
          .toList();
}

extension<T> on Iterable<T> {
  T? get firstOrNull => isEmpty ? null : first;
}
