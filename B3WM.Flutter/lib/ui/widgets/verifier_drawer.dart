import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../models/signal_event.dart';
import '../../models/verifier_config.dart';
import '../../models/verifier_log_day.dart';
import '../../services/api_service.dart';
import '../../services/state_service.dart';
import 'drawer_controls.dart';

class VerifierDrawer extends StatefulWidget {
  final bool noDrawer;
  const VerifierDrawer({super.key, this.noDrawer = false});

  @override
  State<VerifierDrawer> createState() => _VerifierDrawerState();
}

class _VerifierDrawerState extends State<VerifierDrawer>
    with AutomaticKeepAliveClientMixin {
  @override
  bool get wantKeepAlive => true;
  late VerifierConfig _config;
  late TimeOfDay _closeTime;
  bool _busy = false;

  @override
  void initState() {
    super.initState();
    _config = VerifierConfig();
    _closeTime = _parseTime(_config.dayTradeCloseTime);
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted) return;
      final state = context.read<StateService>();
      if (state.verifierState?.config != null) {
        _config = state.verifierState!.config!.copyWith();
        _closeTime = _parseTime(_config.dayTradeCloseTime);
      }
      setState(() {});
    });
  }

  static TimeOfDay _parseTime(String value) {
    final parts = value.split(':');
    final h = parts.isNotEmpty ? int.tryParse(parts[0]) : null;
    final m = parts.length > 1 ? int.tryParse(parts[1]) : null;
    return TimeOfDay(
      hour: (h ?? 13).clamp(0, 23),
      minute: (m ?? 0).clamp(0, 59),
    );
  }

  static String _formatTime(TimeOfDay t) =>
      '${t.hour.toString().padLeft(2, '0')}:${t.minute.toString().padLeft(2, '0')}';

  Future<void> _pickCloseTime() async {
    final picked = await showTimePicker(
      context: context,
      initialTime: _closeTime,
      builder: (context, child) => MediaQuery(
        data: MediaQuery.of(context).copyWith(alwaysUse24HourFormat: true),
        child: child!,
      ),
    );
    if (picked != null) {
      setState(() => _closeTime = picked);
    }
  }

  VerifierConfig _buildConfig() {
    final state = context.read<StateService>();
    return _config.copyWith(
      symbol: state.symbol,
      timeFrame: state.timeFrame,
      dayTradeCloseTime: _formatTime(_closeTime),
    );
  }

  Future<void> _start(StateService state) async {
    setState(() => _busy = true);
    final config = _buildConfig();
    config.smartAgents =
        state.selectedAgents.isEmpty ? null : List.from(state.selectedAgents);
    config.bubbleThreshold = state.thresholdBubble;
    config.agentThresholds = state.agentThresholds.isEmpty
        ? null
        : Map<int, int>.from(state.agentThresholds);
    config.minDistance = state.structureRangeUpd.round();
    final status = await state.startVerifier(config);
    if (mounted) {
      setState(() => _busy = false);
      _showStatus(status, success: 'Verifier iniciado.', failure: 'Falha ao iniciar o verifier.');
    }
  }

  Future<void> _stop(StateService state) async {
    setState(() => _busy = true);
    final status = await state.stopVerifier();
    if (mounted) {
      setState(() => _busy = false);
      _showStatus(status, success: 'Verifier parado.', failure: 'Falha ao parar o verifier.');
    }
  }

  Future<void> _reset(StateService state) async {
    setState(() => _busy = true);
    final status = await state.resetVerifier();
    if (mounted) {
      setState(() => _busy = false);
      _showStatus(status, success: 'Verifier resetado.', failure: 'Falha ao resetar o verifier.');
    }
  }

  Future<void> _export(StateService state) async {
    final vState = state.verifierState;
    final symbol = vState?.symbol.isNotEmpty == true
        ? vState!.symbol
        : state.symbol;
    final timeFrame = vState?.timeFrame ?? state.timeFrame;
    final api = context.read<ApiService>();
    final days = await api.exportVerifier(symbol, timeFrame);
    if (!mounted) return;
    if (days.isEmpty) {
      _showStatus(0, failure: 'Nenhum log encontrado no período.');
      return;
    }
    _showExportDialog(days);
  }

  void _showStatus(int status, {String success = '', String? failure}) {
    if (!mounted) return;
    final messenger = ScaffoldMessenger.of(context);
    messenger.hideCurrentSnackBar();
    final String message;
    if (status == 200) {
      message = success;
    } else if (status == 0) {
      message = failure ?? 'Falha na conexão com o servidor.';
    } else {
      message = '${failure ?? 'Falha'} (HTTP $status)';
    }
    messenger.showSnackBar(SnackBar(
      content: Text(message),
      duration: const Duration(seconds: 3),
    ));
  }

  void _showExportDialog(List<VerifierLogDay> days) {
    final totalTrades = days.fold<int>(0, (sum, d) => sum + d.totalTrades);
    final netProfit = days.fold<double>(0, (sum, d) => sum + d.netProfit);
    final wins = days.fold<int>(0, (sum, d) => sum + d.winCount);

    showDialog<void>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Export Verifier'),
        content: SizedBox(
          width: 360,
          child: days.isEmpty
              ? const Text('Nenhum log encontrado no período.')
              : SingleChildScrollView(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      for (final d in days)
                        Padding(
                          padding: const EdgeInsets.symmetric(vertical: 4),
                          child: Text(
                            '${d.date.day.toString().padLeft(2, '0')}/${d.date.month.toString().padLeft(2, '0')} - '
                            '${d.totalTrades} trades, win ${(d.winRate * 100).toStringAsFixed(0)}%, '
                            'PL ${d.netProfit.toStringAsFixed(2)}',
                            style: const TextStyle(fontSize: 12),
                          ),
                        ),
                      const Divider(),
                      Text(
                        'Total: $totalTrades trades, $wins wins, '
                        'PL ${netProfit.toStringAsFixed(2)}',
                        style: const TextStyle(
                            fontSize: 13, fontWeight: FontWeight.bold),
                      ),
                    ],
                  ),
                ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context),
            child: const Text('Fechar'),
          ),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    super.build(context);
    return Consumer<StateService>(builder: (context, state, _) {
      final running = state.verifierRunning;
      final vState = state.verifierState;

      final body = ListView(
        padding: EdgeInsets.zero,
        children: [
          ExpandableSection(
            icon: Icons.verified,
            title: 'Configuração do Verificador',
            defaultExpanded: true,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    const Text('Estratégia',
                        style: TextStyle(fontSize: 13)),
                    const SizedBox(width: 8),
                    Expanded(
                      child: DropdownButton<String>(
                        value: _config.strategyName,
                        isExpanded: true,
                        items: VerifierConfig.strategyTypes
                            .map((s) => DropdownMenuItem(
                                value: s, child: Text(s)))
                            .toList(),
                        onChanged: (v) => setState(
                            () => _config.strategyName = v ?? 'SmartBreakout'),
                      ),
                    ),
                  ],
                ),
                const Divider(height: 16),
                Row(
                  children: [
                    const Text('TimeFrame (do gráfico)',
                        style: TextStyle(fontSize: 13)),
                    const SizedBox(width: 8),
                    Expanded(
                      child: Consumer<StateService>(
                        builder: (context, state, _) => Text(
                          '${state.timeFrame} min',
                          style: const TextStyle(
                              fontSize: 13, fontWeight: FontWeight.bold),
                        ),
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 8),
                ToggleRow('Day Trade', _config.isDayTrade,
                    (v) => setState(() => _config.isDayTrade = v)),
                InkWell(
                  onTap: _config.isDayTrade ? _pickCloseTime : null,
                  child: Padding(
                    padding: const EdgeInsets.symmetric(vertical: 8),
                    child: Row(
                      children: [
                        Icon(Icons.schedule,
                            size: 16,
                            color: _config.isDayTrade
                                ? Colors.grey
                                : Colors.grey.shade700),
                        const SizedBox(width: 8),
                        const Expanded(
                          child: Text('Horário de fechamento',
                              style: TextStyle(fontSize: 13)),
                        ),
                        Text(
                          _formatTime(_closeTime),
                          style: TextStyle(
                            fontSize: 13,
                            color: _config.isDayTrade
                                ? Colors.blue
                                : Colors.grey.shade600,
                            fontWeight: FontWeight.bold,
                          ),
                        ),
                        Icon(Icons.arrow_drop_down,
                            color: _config.isDayTrade
                                ? Colors.grey
                                : Colors.grey.shade700),
                      ],
                    ),
                  ),
                ),
                if (_config.strategyName == 'SmartBreakout') ...[
                  const Divider(height: 16),
                  const SizedBox(height: 8),
                  SliderRow(
                      'Volume % (bubble) - Bubbles em Volumeprofile com baixo volume são descartados', _config.smartVolumePct, 0, 1,
                      (v) => setState(() => _config.smartVolumePct = v),
                      step: 0.05,
                      decimals: 2),
                  SliderRow(
                      'Buffer Estrutura %', _config.smartStructureBufferPct, 0,
                      0.5,
                      (v) =>
                          setState(() => _config.smartStructureBufferPct = v),
                      step: 0.01,
                      decimals: 2),
                ],
                if (_config.strategyName == 'Breakout') ...[
                  const Divider(height: 16),
                  SliderRow(
                      'Lookback (candles)', _config.lookbackPeriod.toDouble(),
                      1, 200,
                      (v) => setState(() => _config.lookbackPeriod = v.toInt()),
                      step: 1,
                      decimals: 0),
                ],
              ],
            ),
          ),
          Padding(
            padding: const EdgeInsets.all(16),
            child: Row(
              children: [
                Expanded(
                  child: FilledButton.icon(
                    icon: const Icon(Icons.play_arrow, size: 18),
                    label: const Text('Start'),
                    onPressed: running || _busy ? null : () => _start(state),
                  ),
                ),
                const SizedBox(width: 8),
                Expanded(
                  child: OutlinedButton.icon(
                    icon: const Icon(Icons.stop, size: 18),
                    label: const Text('Stop'),
                    onPressed: !running || _busy ? null : () => _stop(state),
                  ),
                ),
                const SizedBox(width: 8),
                IconButton(
                  icon: const Icon(Icons.refresh),
                  tooltip: 'Reset',
                  onPressed: _busy ? null : () => _reset(state),
                ),
              ],
            ),
          ),
          if (running)
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 16),
              child: Row(
                children: [
                  const Icon(Icons.circle, size: 10, color: Colors.green),
                  const SizedBox(width: 6),
                  Text(
                    'Verificando ${vState?.symbol ?? ''} ${vState?.timeFrame ?? _config.timeFrame}min',
                    style:
                        const TextStyle(fontSize: 12, color: Colors.greenAccent),
                  ),
                ],
              ),
            ),
          _ActionPanel(state: state),
          _MetricsSection(state: state),
          _SignalsSection(signals: vState?.signals ?? []),
          Padding(
            padding: const EdgeInsets.all(16),
            child: OutlinedButton.icon(
              icon: const Icon(Icons.download, size: 18),
              label: const Text('Exportar Logs'),
              onPressed: _busy ? null : () => _export(state),
            ),
          ),
        ],
      );

      if (widget.noDrawer) return body;
      return Drawer(width: 360, child: body);
    });
  }
}

class _ActionPanel extends StatelessWidget {
  final StateService state;
  const _ActionPanel({required this.state});

  @override
  Widget build(BuildContext context) {
    final vs = state.verifierState;

    Color bg;
    Color fg;
    IconData icon;
    String title;
    String subtitle;

    if (vs == null || !vs.isRunning) {
      bg = const Color(0xFF242424);
      fg = Colors.grey;
      icon = Icons.info_outline;
      title = 'Inicie o verifier';
      subtitle = 'Para ver a recomendação da estratégia em tempo real.';
    } else if (vs.openPosition != null) {
      bg = const Color(0xFF332b00);
      fg = Colors.amberAccent;
      icon = Icons.hourglass_top;
      title = 'AGUARDAR';
      subtitle = 'Posição em aberto — aguardar o Stop ou o Alvo.';
    } else if (vs.pendingSignal != null) {
      final p = vs.pendingSignal!;
      final buy = p.isBuy;
      bg = buy ? const Color(0xFF002b1c) : const Color(0xFF330000);
      fg = buy ? Colors.greenAccent : Colors.redAccent;
      icon = buy ? Icons.arrow_upward : Icons.arrow_downward;
      title = buy ? 'COMPRAR' : 'VENDER';
      final reason = p.reason == null || p.reason!.isEmpty
          ? ''
          : ' • ${p.reason}';
      subtitle = 'Entrar na próxima barra • Stop ${p.stopLossPrice.toStringAsFixed(2)} • '
          'Alvo ${p.takeProfitPrice.toStringAsFixed(2)}$reason';
    } else {
      bg = const Color(0xFF242424);
      fg = Colors.grey;
      icon = Icons.do_not_disturb_on_outlined;
      title = 'NADA A FAZER';
      subtitle = 'Sem sinal — aguardando o setup da estratégia.';
    }

    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 8, 16, 4),
      child: Container(
        padding: const EdgeInsets.all(10),
        decoration: BoxDecoration(
          color: bg,
          borderRadius: BorderRadius.circular(6),
          border: Border.all(color: fg.withValues(alpha: 0.4)),
        ),
        child: Row(
          children: [
            Icon(icon, color: fg, size: 26),
            const SizedBox(width: 10),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    title,
                    style: TextStyle(
                      fontSize: 15,
                      fontWeight: FontWeight.bold,
                      color: fg,
                    ),
                  ),
                  const SizedBox(height: 2),
                  Text(
                    subtitle,
                    style: const TextStyle(fontSize: 11, color: Colors.grey),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _MetricsSection extends StatelessWidget {
  final StateService state;
  const _MetricsSection({required this.state});

  @override
  Widget build(BuildContext context) {
    final vs = state.verifierState;
    final position = vs?.openPosition;

    Widget metric(String label, String value, {Color? color}) => Padding(
          padding: const EdgeInsets.symmetric(vertical: 2),
          child: Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text(label, style: const TextStyle(fontSize: 12)),
              Text(value,
                  style: TextStyle(
                      fontSize: 12,
                      fontWeight: FontWeight.bold,
                      color: color)),
            ],
          ),
        );

    return ExpandableSection(
      icon: Icons.assessment,
      title: 'Métricas',
      defaultExpanded: true,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          if (position != null) ...[
            Container(
              padding: const EdgeInsets.all(8),
              decoration: BoxDecoration(
                color: const Color(0xFF1e1e1e),
                borderRadius: BorderRadius.circular(4),
                border: Border.all(color: const Color(0xFF3d3d3d)),
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    'Posição ${position.side == 'Sell' ? 'VENDIDA' : 'COMPRADA'} '
                    '${position.quantity}x',
                    style: const TextStyle(
                        fontSize: 12, fontWeight: FontWeight.bold),
                  ),
                  const SizedBox(height: 2),
                  Text(
                    'Entrada ${position.entryPrice.toStringAsFixed(2)} | '
                    'Stop ${position.stopPrice.toStringAsFixed(2)} | '
                    'Alvo ${position.targetPrice.toStringAsFixed(2)}',
                    style: const TextStyle(fontSize: 11, color: Colors.grey),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 8),
          ],
          metric('Trades', '${vs?.totalTrades ?? 0}'),
          metric('Win Rate',
              '${((vs?.winRate ?? 0) * 100).toStringAsFixed(0)}%'),
          metric('Net Profit', (vs?.netProfit ?? 0).toStringAsFixed(2),
              color: (vs?.netProfit ?? 0) >= 0 ? Colors.greenAccent : Colors.redAccent),
          metric('Gross Profit', (vs?.grossProfit ?? 0).toStringAsFixed(2),
              color: Colors.greenAccent),
          metric('Gross Loss', (vs?.grossLoss ?? 0).toStringAsFixed(2),
              color: Colors.redAccent),
          metric('Max Drawdown', (vs?.maxDrawdown ?? 0).toStringAsFixed(2),
              color: Colors.orangeAccent),
        ],
      ),
    );
  }
}

class _SignalsSection extends StatelessWidget {
  final List<SignalEvent> signals;
  const _SignalsSection({required this.signals});

  @override
  Widget build(BuildContext context) {
    return ExpandableSection(
      icon: Icons.notifications_active,
      title: 'Sinais (${signals.length})',
      defaultExpanded: true,
      child: signals.isEmpty
          ? const Padding(
              padding: EdgeInsets.symmetric(vertical: 8),
              child: Text('Nenhum sinal ainda.',
                  style: TextStyle(fontSize: 12, color: Colors.grey)),
            )
          : Column(
              children: [
                for (final s in signals.take(50))
                  Padding(
                    padding: const EdgeInsets.symmetric(vertical: 2),
                    child: Row(
                      children: [
                        Icon(
                          s.type.contains('Buy')
                              ? Icons.arrow_upward
                              : s.type.contains('Sell')
                                  ? Icons.arrow_downward
                                  : Icons.close,
                          size: 14,
                          color: s.type.contains('Buy')
                              ? Colors.greenAccent
                              : s.type.contains('Sell')
                                  ? Colors.redAccent
                                  : Colors.grey,
                        ),
                        const SizedBox(width: 6),
                        Expanded(
                          child: Text(
                            '${s.type.replaceAll('Exit', 'X-').replaceAll('Entry', '')} '
                            '${s.entryPrice != 0 ? s.entryPrice.toStringAsFixed(1) : ''}',
                            style: const TextStyle(fontSize: 11),
                            overflow: TextOverflow.ellipsis,
                          ),
                        ),
                        Text(
                          s.isEntry
                              ? ''
                              : '${s.profitLoss >= 0 ? '+' : ''}${s.profitLoss.toStringAsFixed(1)}',
                          style: TextStyle(
                            fontSize: 11,
                            fontWeight: FontWeight.bold,
                            color: s.profitLoss >= 0
                                ? Colors.greenAccent
                                : Colors.redAccent,
                          ),
                        ),
                      ],
                    ),
                  ),
              ],
            ),
    );
  }
}
