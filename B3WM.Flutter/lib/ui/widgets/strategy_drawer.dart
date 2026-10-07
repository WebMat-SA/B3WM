import 'dart:async';
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../services/state_service.dart';
import '../../services/strategy_service.dart';
import 'drawer_controls.dart';
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
  List<StrategySession> _sessions = [];
  bool _busy = false;
  String? _evalResult;
  Timer? _poll;

  late final StrategyService _svc = StrategyService();

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => _refreshAll());
    _poll = Timer.periodic(const Duration(seconds: 5), (_) {
      if (mounted) _refreshSessions(silent: true);
    });
  }

  @override
  void dispose() {
    _poll?.cancel();
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
    await _refreshSessions(silent: true);
    _syncLock();
  }

  Future<void> _refreshSessions({bool silent = false}) async {
    final sessions = await _svc.state();
    if (!mounted) return;
    setState(() => _sessions = sessions
        .where((s) =>
            s.symbol == context.read<StateService>().symbol ||
            context.read<StateService>().symbol.isEmpty)
        .toList());
    _syncLock();
  }

  void _syncLock() {
    final state = context.read<StateService>();
    final armed = _sessions.any((s) => !s.paused);
    if (state.strategiesArmed != armed) {
      state.setStrategiesArmed(armed);
    }
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
                Row(
                  children: [
                    Expanded(
                      child: FilledButton.icon(
                        onPressed: (_busy || state.configLocked) ? null : _play,
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
            icon: Icons.list,
            title: 'Sessões (${_sessions.length})',
            defaultExpanded: true,
            child: _sessions.isEmpty
                ? const Text('Nenhuma sessão.',
                    style: TextStyle(fontSize: 12, color: Colors.grey))
                : Column(
                    children: _sessions
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
                                  if (s.recentDecisions.isNotEmpty)
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
                                          const Text('Relatório (recentes)',
                                              style: TextStyle(
                                                  fontSize: 11,
                                                  color: Colors.grey,
                                                  fontWeight:
                                                      FontWeight.bold)),
                                          const SizedBox(height: 4),
                                          ...s.recentDecisions.map(
                                              (d) => Padding(
                                                    padding: const EdgeInsets
                                                        .symmetric(vertical: 1),
                                                    child: Text(
                                                      _formatItem(d),
                                                      style: TextStyle(
                                                          fontSize: 11,
                                                          fontFamily:
                                                              'monospace',
                                                          color: _itemColor(
                                                              d)),
                                                    ),
                                                  )),
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

  String _formatItem(StrategyDecisionItem d) {
    if (d.kind == 'execucao') {
      return '${d.time} ⚙ ${d.action}';
    }
    final conf =
        d.confidence > 0 ? ' ${d.confidence.toStringAsFixed(2)}' : '';
    return '${d.time} ${d.side}$conf → ${d.action}';
  }

  Color _itemColor(StrategyDecisionItem d) {
    if (d.kind == 'execucao') return Colors.orange;
    switch (d.side) {
      case 'comprar':
        return Colors.lightBlue;
      case 'vender':
        return Colors.redAccent;
      default:
        return Colors.grey;
    }
  }
}

extension<T> on Iterable<T> {
  T? get firstOrNull => isEmpty ? null : first;
}
