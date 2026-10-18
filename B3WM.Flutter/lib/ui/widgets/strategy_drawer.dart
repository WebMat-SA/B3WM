import 'dart:async';
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../services/state_service.dart';
import '../../services/strategy_service.dart';
import 'drawer_controls.dart';
import 'locked_badge.dart';
import 'strategy_details_md.dart';
import '../../app_log.dart';

/// Aba Estratégia: escolhe dentre todas as IStrategy do backend e dá PLAY.
/// O PLAY envia a foto da tela (snapshot) e TRAVA os filtros funcionais —
/// para alterar: PAUSE/STOP, edita, PLAY de novo. Execução toda no backend
/// (pode fechar o app). Acompanhamento na faixa timeline ⓘ sob o gráfico
/// (toque no ícone mostra o detalhe da chamada).
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

  @override
  Widget build(BuildContext context) {
    super.build(context);
    return Consumer<StateService>(builder: (context, state, _) {
      final info =
          _strategies.where((s) => s.name == _selected).firstOrNull;
      final listView = ListView(
        padding: EdgeInsets.zero,
        children: [
          ExpandableSection(
            icon: Icons.visibility,
            title: 'Exibição',
            defaultExpanded: true,
            child: Column(
              children: [
                ToggleRow('Show on Chart',
                    state.strategyTimelineVisible,
                    (v) => state.setStrategyTimelineVisible(v)),
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
                Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    const Text('Estratégia',
                        style: TextStyle(fontSize: 13)),
                    LockedBadge(locked: state.configLocked),
                  ],
                ),
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
                if (info != null) ...[
                  Text(info.description,
                      style:
                          const TextStyle(fontSize: 11, color: Colors.grey)),
                  const SizedBox(height: 4),
                  StrategyDetailsMd(markdown: info.detailsMd),
                ],
                const SizedBox(height: 8),
                const Text(
                  'Gatilho e parâmetros moram no código da strategy. '
                  'O PLAY congela a foto dos filtros da tela.',
                  style: TextStyle(fontSize: 11, color: Colors.grey),
                ),
                const SizedBox(height: 8),
                if (!state.configLocked)
                  SizedBox(
                    width: double.infinity,
                    child: FilledButton.icon(
                      onPressed: _busy ? null : _play,
                      icon: const Icon(Icons.play_arrow, size: 18),
                      label: const Text('PLAY'),
                    ),
                  )
                else
                  const Text(
                    'Sessão em andamento — PAUSE/STOP nas sessões abaixo para armar outra.',
                    style: TextStyle(fontSize: 11, color: Colors.grey),
                  ),
                const Padding(
                  padding: EdgeInsets.only(top: 4),
                  child: Text(
                    'PLAY envia a foto dos filtros e trava os funcionais. Pode fechar o app.',
                    style: TextStyle(fontSize: 11, color: Colors.grey),
                  ),
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
                              child: ListTile(
                                dense: true,
                                title: Text(
                                    '${s.strategy} ${s.paused ? '(pausada)' : ''}',
                                    style:
                                        const TextStyle(fontSize: 13)),
                                subtitle: Text(
                                  'pos=${s.position ?? 'flat'} dec=${s.decisions} pts=${s.realizedPts.toStringAsFixed(0)} snap=${s.snapshotHash}\n'
                                  '${s.lastDecision ?? ''}'
                                  '${s.reportItems.isNotEmpty && s.reportItems.first.reason.isNotEmpty ? '\n${s.reportItems.first.reason}' : ''}',
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
                            ))
                        .toList(),
                  ),
          ),
          ExpandableSection(
            icon: Icons.timeline,
            title: 'Timeline',
            defaultExpanded: true,
            child: Text(
              _timelineSummary(state),
              style: const TextStyle(fontSize: 11, color: Colors.grey),
            ),
          ),
        ],
      );
      if (widget.noDrawer) return listView;
      return Drawer(width: 360, child: listView);
    });
  }

  /// Sessões do símbolo atual (mesmo filtro de antes, agora do state central).
  List<StrategySession> _visibleSessions(StateService state) =>
      state.strategySessions
          .where((s) =>
              s.symbol == state.symbol || state.symbol.isEmpty)
          .toList();

  /// Resumo da faixa timeline (diagnóstico: quantos itens chegaram e o span).
  String _timelineSummary(StateService state) {
    final items = state.strategyTimelineItems;
    if (items.isEmpty) return 'Nenhum item (sem At ou sem log/sessão).';
    String hm(DateTime at) {
      final w = b3Wall(at);
      return '${w.day.toString().padLeft(2, '0')}/${w.month.toString().padLeft(2, '0')} '
          '${w.hour.toString().padLeft(2, '0')}:${w.minute.toString().padLeft(2, '0')}';
    }
    final off = DateTime.now().timeZoneOffset;
    final sign = off.isNegative ? '-' : '+';
    final tz =
        'tz$sign${off.inHours.abs().toString().padLeft(2, '0')}:${(off.inMinutes.abs() % 60).toString().padLeft(2, '0')}';
    return '${items.length} itens (${hm(items.first.at)} → ${hm(items.last.at)}) [$tz]';
  }
}

extension<T> on Iterable<T> {
  T? get firstOrNull => isEmpty ? null : first;
}
