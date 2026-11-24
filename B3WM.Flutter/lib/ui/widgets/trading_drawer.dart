import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../models/trade_models.dart';
import '../../services/trading_service.dart';
import '../../services/state_service.dart';
import 'chart/measure_ruler.dart';

class TradingDrawer extends StatefulWidget {
  const TradingDrawer({super.key});

  @override
  State<TradingDrawer> createState() => _TradingDrawerState();
}

class _TradingDrawerState extends State<TradingDrawer> {
  AccountInfo? _account;
  bool _loadingAccount = false;
  bool _loadingPositions = false;
  bool _loadingOrders = false;
  bool _loadingHistory = false;
  bool _sendingOrder = false;
  final Set<int> _closingTickets = {};
  final Set<int> _cancellingTickets = {};

  int _volume = 1;
  final _volumeController = TextEditingController(text: '1');
  final _volumeFocusNode = FocusNode();
  OrderResult? _lastResult;

  @override
  void initState() {
    super.initState();
    final savedVolume = context.read<StateService>().tradingVolume;
    _volume = savedVolume;
    _volumeController.text = savedVolume.toString();
    _volumeFocusNode.addListener(() {
      if (!_volumeFocusNode.hasFocus) _commitVolume();
    });
    _refreshAll();
  }

  @override
  void dispose() {
    _volumeController.dispose();
    _volumeFocusNode.dispose();
    super.dispose();
  }

  void _commitVolume() {
    final v = int.tryParse(_volumeController.text);
    setState(() {
      if (v == null || v < 1) {
        _volume = 1;
        _volumeController.text = '1';
      } else {
        _volume = v;
        _volumeController.text = v.toString();
      }
    });
    context.read<StateService>().setTradingVolume(_volume);
  }

  Future<void> _refreshAll() async {
    await Future.wait([
      _refreshPositions(),
      _refreshOrders(),
      _refreshHistory(),
    ]);
    if (mounted) setState(() {});
  }

  Future<void> _refreshAccount() async {
    try {
      _loadingAccount = true;
      setState(() {});
      final api = context.read<TradingApiService>();
      _account = await api.getAccountInfo();
    } catch (_) {
      _account = null;
    } finally {
      _loadingAccount = false;
    }
  }

  Future<void> _refreshHistory() async {
    try {
      _loadingHistory = true;
      if (mounted) setState(() {});
      await context.read<StateService>().refreshTradingHistory();
    } finally {
      _loadingHistory = false;
      if (mounted) setState(() {});
    }
  }

  Future<void> _refreshPositions() async {
    try {
      _loadingPositions = true;
      await context.read<StateService>().refreshTradingData();
    } finally {
      _loadingPositions = false;
    }
  }

  Future<void> _refreshOrders() async {
    try {
      _loadingOrders = true;
      await context.read<StateService>().refreshTradingData();
    } finally {
      _loadingOrders = false;
      if (mounted) setState(() {});
    }
  }

  Future<void> _cancelOrder(int ticket) async {
    if (!_cancellingTickets.add(ticket)) return;
    setState(() {});

    try {
      final api = context.read<TradingApiService>();
      final result = await api.cancelOrder(ticket);
      if (result != null && mounted && !result.success) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
              content: Text('Failed to cancel: ${result.message}'),
              backgroundColor: Colors.red),
        );
      }
      await _refreshAll();
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Error: $e'), backgroundColor: Colors.red),
        );
      }
    } finally {
      _cancellingTickets.remove(ticket);
      if (mounted) setState(() {});
    }
  }

  Future<void> _executeOrder({required bool isBuy}) async {
    if (_sendingOrder) return;
    final state = context.read<StateService>();
    _sendingOrder = true;
    _lastResult = null;
    setState(() {});

    try {
      final api = context.read<TradingApiService>();
      final request = MarketOrderRequest(
        symbol: state.symbol,
        volume: _volume.toDouble(),
        type: isBuy ? 'buy' : 'sell',
      );
      final result = await api.placeMarketOrder(request);
      if (result != null && mounted) {
        _lastResult = result;
        if (!result.success) {
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(
                content: Text('Order failed: ${result.message}'),
                backgroundColor: Colors.red),
          );
        }
      }
      await _refreshAll();
    } catch (e) {
      _lastResult = OrderResult(success: false, message: e.toString());
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Error: $e'), backgroundColor: Colors.red),
        );
      }
    } finally {
      _sendingOrder = false;
      if (mounted) setState(() {});
    }
  }

  Future<void> _closePosition(int ticket) async {
    if (!_closingTickets.add(ticket)) return;
    setState(() {});

    try {
      final api = context.read<TradingApiService>();
      final result = await api.closePosition(ticket);
      if (result != null && mounted && !result.success) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
              content: Text('Failed to close: ${result.message}'),
              backgroundColor: Colors.red),
        );
      }
      await _refreshAll();
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Error: $e'), backgroundColor: Colors.red),
        );
      }
    } finally {
      _closingTickets.remove(ticket);
      if (mounted) setState(() {});
    }
  }

  Future<void> _closeAllPositions() async {
    if (_sendingOrder) return;
    _sendingOrder = true;
    setState(() {});

    try {
      final api = context.read<TradingApiService>();
      final positions = context.read<StateService>().positions;
      for (final pos in positions) {
        final result = await api.closePosition(pos.ticket);
        if (result != null && mounted && !result.success) {
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(
                content:
                    Text('Failed to close ${pos.symbol}: ${result.message}'),
                backgroundColor: Colors.red),
          );
        }
      }
      await _refreshAll();
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Error: $e'), backgroundColor: Colors.red),
        );
      }
    } finally {
      _sendingOrder = false;
      if (mounted) setState(() {});
    }
  }

  @override
  Widget build(BuildContext context) {
    final state = context.watch<StateService>();
    final positions = state.positions;
    final orders = state.orders;
    return Container(
      width: 320,
      constraints: BoxConstraints(
        maxHeight: MediaQuery.of(context).size.height,
      ),
      decoration: BoxDecoration(
        color: const Color(0xFF1e1e1e),
        border: Border(
            left: BorderSide(color: Colors.grey.shade800.withOpacity(0.3))),
      ),
      child: Material(
        type: MaterialType.transparency,
        child: ListTileTheme(
          tileColor: const Color(0xFF1e1e1e),
          child: Column(
            children: [
              Expanded(
                child: Column(
                  children: [
                    // // Header
                    // _section(
                    //   child: Row(
                    //     children: [
                    //       const Icon(Icons.travel_explore, size: 18),
                    //       const SizedBox(width: 8),
                    //       const Expanded(
                    //         child: Text('Trading',
                    //             style: TextStyle(
                    //                 fontSize: 16, fontWeight: FontWeight.bold)),
                    //       ),
                    //       IconButton(
                    //         icon: const Icon(Icons.refresh, size: 18),
                    //         onPressed: _refreshAll,
                    //       ),
                    //     ],
                    //   ),
                    // ),

                    // Configurações (visibilidade dos overlays de trading no gráfico)
                    _section(
                      child: Theme(
                        data: Theme.of(context).copyWith(
                          dividerColor: Colors.transparent,
                          unselectedWidgetColor: Colors.grey,
                        ),
                        child: ExpansionTile(
                          tilePadding: const EdgeInsets.symmetric(vertical: 0),
                          childrenPadding: const EdgeInsets.only(top: 4),
                          title: const Row(children: [
                            Icon(Icons.settings, size: 16),
                            SizedBox(width: 4),
                            Text('Configurações',
                                style: TextStyle(
                                    fontSize: 13, fontWeight: FontWeight.w600)),
                          ]),
                          initiallyExpanded: state.tradingConfigExpanded,
                          onExpansionChanged: (expanded) {
                            context
                                .read<StateService>()
                                .setTradingConfigExpanded(expanded);
                          },
                          children: [
                            _configToggle(
                              'Mostrar Histórico de Trades',
                              state.tradingHistoryVisible,
                              (v) => context
                                  .read<StateService>()
                                  .setTradingHistoryVisible(v),
                            ),
                            _configToggle(
                              'Mostrar Posições',
                              state.positionVisible,
                              (v) => context
                                  .read<StateService>()
                                  .setPositionVisible(v),
                            ),
                            _configToggle(
                              'Mostrar Ordens em Aberto',
                              state.openOrdersVisible,
                              (v) => context
                                  .read<StateService>()
                                  .setOpenOrdersVisible(v),
                            ),
                          ],
                        ),
                      ),
                    ),

                    // Account Info
                    _section(
                      child: Theme(
                        data: Theme.of(context).copyWith(
                          dividerColor: Colors.transparent,
                          unselectedWidgetColor: Colors.grey,
                        ),
                        child: ExpansionTile(
                          tilePadding: const EdgeInsets.symmetric(vertical: 0),
                          childrenPadding: const EdgeInsets.only(top: 4),
                          title: const Row(children: [
                            Icon(Icons.account_balance_wallet, size: 16),
                            SizedBox(width: 4),
                            Text('Account',
                                style: TextStyle(
                                    fontSize: 13, fontWeight: FontWeight.w600)),
                          ]),
                          initiallyExpanded: state.tradingAccountExpanded,
                          onExpansionChanged: (expanded) {
                            context
                                .read<StateService>()
                                .setTradingAccountExpanded(expanded);
                            if (expanded) _refreshAccount();
                          },
                          children: [
                            if (_account != null) ...[
                              _infoRow('Login', '${_account!.login}'),
                              _infoRow('Balance',
                                  _account!.balance.toStringAsFixed(2)),
                              _infoRow(
                                'Equity',
                                _account!.equity.toStringAsFixed(2),
                                color: _account!.equity >= _account!.balance
                                    ? Colors.green
                                    : Colors.red,
                              ),
                              _infoRow('Margin',
                                  _account!.margin.toStringAsFixed(2)),
                              _infoRow('Free',
                                  _account!.marginFree.toStringAsFixed(2)),
                            ] else
                              Padding(
                                padding:
                                    const EdgeInsets.symmetric(vertical: 4),
                                child: _loadingAccount
                                    ? const SizedBox(
                                        width: 16,
                                        height: 16,
                                        child: CircularProgressIndicator(
                                            strokeWidth: 2))
                                    : const Text('Tap to load',
                                        style: TextStyle(
                                            color: Colors.grey, fontSize: 12)),
                              ),
                          ],
                        ),
                      ),
                    ),

                    // Open Orders
                    _section(
                      child: Theme(
                        data: Theme.of(context).copyWith(
                          dividerColor: Colors.transparent,
                          unselectedWidgetColor: Colors.grey,
                        ),
                        child: ExpansionTile(
                          tilePadding: const EdgeInsets.symmetric(vertical: 0),
                          childrenPadding: const EdgeInsets.only(top: 4),
                          title: Row(children: [
                            const Icon(Icons.pending, size: 16),
                            const SizedBox(width: 4),
                            Text(
                                orders.isEmpty
                                    ? 'Open Orders'
                                    : 'Open Orders (${orders.length})',
                                style: const TextStyle(
                                    fontSize: 13, fontWeight: FontWeight.w600)),
                          ]),
                          initiallyExpanded: state.tradingOrdersExpanded,
                          onExpansionChanged: (expanded) {
                            context
                                .read<StateService>()
                                .setTradingOrdersExpanded(expanded);
                            if (expanded) _refreshOrders();
                          },
                          children: [
                            orders.isEmpty
                                ? Padding(
                                    padding: const EdgeInsets.symmetric(
                                        vertical: 20),
                                    child: Center(
                                      child: Text(
                                        _loadingOrders
                                            ? 'Loading...'
                                            : 'No open orders',
                                        style: const TextStyle(
                                            color: Colors.grey, fontSize: 12),
                                      ),
                                    ),
                                  )
                                : ListView.separated(
                                    shrinkWrap: true,
                                    itemCount: orders.length,
                                    separatorBuilder: (_, __) => const Divider(
                                        height: 1, color: Color(0xFF3d3d3d)),
                                    itemBuilder: (context, index) {
                                      final order = orders[index];
                                      final isBuy =
                                          order.type.startsWith('buy');
                                      return ListTile(
                                        dense: true,
                                        contentPadding:
                                            const EdgeInsets.symmetric(
                                                horizontal: 8, vertical: 0),
                                        leading: Chip(
                                          label: Text(isBuy ? 'B' : 'S',
                                              style: const TextStyle(
                                                  fontSize: 10,
                                                  color: Colors.white)),
                                          backgroundColor:
                                              isBuy ? Colors.green : Colors.red,
                                          materialTapTargetSize:
                                              MaterialTapTargetSize.shrinkWrap,
                                          visualDensity: VisualDensity.compact,
                                          padding: EdgeInsets.zero,
                                        ),
                                        title: Text(order.symbol,
                                            style: const TextStyle(
                                                fontSize: 13,
                                                fontWeight: FontWeight.bold)),
                                        subtitle: Text(
                                          '${order.volume.toStringAsFixed(2)} @ ${order.priceOpen.toStringAsFixed(2)}',
                                          style: const TextStyle(fontSize: 11),
                                        ),
                                        trailing: Row(
                                          mainAxisSize: MainAxisSize.min,
                                          children: [
                                            Text(
                                              _formatOrderType(order.type),
                                              style: const TextStyle(
                                                fontSize: 10,
                                                color: Colors.grey,
                                              ),
                                            ),
                                            const SizedBox(width: 4),
                                            _cancellingTickets
                                                    .contains(order.ticket)
                                                ? const SizedBox(
                                                    width: 14,
                                                    height: 14,
                                                    child:
                                                        CircularProgressIndicator(
                                                            strokeWidth: 2))
                                                : IconButton(
                                                    icon: const Icon(
                                                        Icons.close,
                                                        size: 14,
                                                        color: Colors.red),
                                                    onPressed: () =>
                                                        _cancelOrder(
                                                            order.ticket),
                                                  ),
                                          ],
                                        ),
                                      );
                                    },
                                  ),
                          ],
                        ),
                      ),
                    ),

                    // Positions
                    _section(
                      child: Theme(
                        data: Theme.of(context).copyWith(
                          dividerColor: Colors.transparent,
                          unselectedWidgetColor: Colors.grey,
                        ),
                        child: ExpansionTile(
                          tilePadding: const EdgeInsets.symmetric(vertical: 0),
                          childrenPadding: const EdgeInsets.only(top: 4),
                          title: Row(children: [
                            const Icon(Icons.assignment, size: 16),
                            const SizedBox(width: 4),
                            Text(
                                positions.isEmpty
                                    ? 'Positions'
                                    : 'Positions (${positions.length})',
                                style: const TextStyle(
                                    fontSize: 13, fontWeight: FontWeight.w600)),
                          ]),
                          initiallyExpanded: state.tradingPositionsExpanded,
                          onExpansionChanged: (expanded) {
                            context
                                .read<StateService>()
                                .setTradingPositionsExpanded(expanded);
                            if (expanded) _refreshPositions();
                          },
                          children: [
                            positions.isEmpty
                                ? Padding(
                                    padding: const EdgeInsets.symmetric(
                                        vertical: 20),
                                    child: Center(
                                      child: Text(
                                        _loadingPositions
                                            ? 'Loading...'
                                            : 'No open positions',
                                        style: const TextStyle(
                                            color: Colors.grey, fontSize: 12),
                                      ),
                                    ),
                                  )
                                : ListView.separated(
                                    shrinkWrap: true,
                                    itemCount: positions.length,
                                    separatorBuilder: (_, __) => const Divider(
                                        height: 1, color: Color(0xFF3d3d3d)),
                                    itemBuilder: (context, index) {
                                      final pos = positions[index];
                                      final isProfitable = pos.profit >= 0;
                                      final isBuy = pos.type == 'buy';
                                      final last = pos.priceCurrent;
                                      final points =
                                          last > 0 ? pos.pointsAt(last) : 0.0;
                                      final pointsColor = points >= 0
                                          ? Colors.green
                                          : Colors.red;
                                      return ListTile(
                                        dense: true,
                                        contentPadding:
                                            const EdgeInsets.symmetric(
                                                horizontal: 8, vertical: 0),
                                        leading: Chip(
                                          label: Text(isBuy ? 'B' : 'S',
                                              style: const TextStyle(
                                                  fontSize: 10,
                                                  color: Colors.white)),
                                          backgroundColor:
                                              isBuy ? Colors.green : Colors.red,
                                          materialTapTargetSize:
                                              MaterialTapTargetSize.shrinkWrap,
                                          visualDensity: VisualDensity.compact,
                                          padding: EdgeInsets.zero,
                                        ),
                                        title: Text(pos.symbol,
                                            style: const TextStyle(
                                                fontSize: 13,
                                                fontWeight: FontWeight.bold)),
                                        subtitle: Text(
                                          '${pos.volume.toStringAsFixed(2)} @ ${pos.priceOpen.toStringAsFixed(2)}',
                                          style: const TextStyle(fontSize: 11),
                                        ),
                                        trailing: Row(
                                          mainAxisSize: MainAxisSize.min,
                                          children: [
                                            Column(
                                              mainAxisSize: MainAxisSize.min,
                                              crossAxisAlignment:
                                                  CrossAxisAlignment.end,
                                              children: [
                                                if (last > 0)
                                                  Text(
                                                    formatTradePoints(
                                                        points, pos.symbol,
                                                        showSign: true),
                                                    style: TextStyle(
                                                      fontSize: 12,
                                                      fontWeight:
                                                          FontWeight.bold,
                                                      color: pointsColor,
                                                    ),
                                                  ),
                                                Text(
                                                  '${isProfitable ? "+" : ""}${pos.profit.toStringAsFixed(2)}',
                                                  style: TextStyle(
                                                    fontSize: 9,
                                                    color: isProfitable
                                                        ? Colors.green.shade300
                                                        : Colors.red.shade300,
                                                  ),
                                                ),
                                              ],
                                            ),
                                            const SizedBox(width: 4),
                                            _closingTickets.contains(pos.ticket)
                                                ? const SizedBox(
                                                    width: 14,
                                                    height: 14,
                                                    child:
                                                        CircularProgressIndicator(
                                                            strokeWidth: 2))
                                                : IconButton(
                                                    icon: const Icon(
                                                        Icons.close,
                                                        size: 14,
                                                        color: Colors.red),
                                                    onPressed: () =>
                                                        _closePosition(
                                                            pos.ticket),
                                                  ),
                                          ],
                                        ),
                                      );
                                    },
                                  ),
                          ],
                        ),
                      ),
                    ),

                    // History
                    Expanded(
                      child: _buildHistorySection(),
                    ),
                  ],
                ),
              ),

              // Market Order
              _section(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(children: [
                      const Icon(Icons.swap_vert, size: 16),
                      const SizedBox(width: 4),
                      const Text('Market Order',
                          style: TextStyle(
                              fontSize: 13, fontWeight: FontWeight.w600)),
                      const Spacer(),
                      Text(state.symbol,
                          style: const TextStyle(
                              fontSize: 11,
                              fontWeight: FontWeight.bold,
                              color: Colors.grey)),
                    ]),
                    const SizedBox(height: 8),
                    Row(
                      children: [
                        SizedBox(
                          width: 32,
                          height: 32,
                          child: TextButton(
                            style: TextButton.styleFrom(
                              padding: EdgeInsets.zero,
                              backgroundColor: const Color(0xFF3d3d3d),
                              foregroundColor: Colors.white,
                              shape: RoundedRectangleBorder(
                                  borderRadius: BorderRadius.circular(4)),
                            ),
                            onPressed:
                                (int.tryParse(_volumeController.text) ?? 1) > 1
                                    ? () {
                                        _commitVolume();
                                        setState(() {
                                          _volume--;
                                          _volumeController.text =
                                              _volume.toString();
                                        });
                                        context
                                            .read<StateService>()
                                            .setTradingVolume(_volume);
                                      }
                                    : null,
                            child: const Icon(Icons.remove, size: 18),
                          ),
                        ),
                        const SizedBox(width: 8),
                        Expanded(
                          child: TextField(
                            controller: _volumeController,
                            focusNode: _volumeFocusNode,
                            style: const TextStyle(fontSize: 14),
                            textAlign: TextAlign.center,
                            keyboardType: TextInputType.number,
                            onChanged: (text) {
                              final v = int.tryParse(text);
                              if (v != null && v >= 1) {
                                context
                                    .read<StateService>()
                                    .setTradingVolume(v);
                              }
                            },
                            onEditingComplete: _commitVolume,
                            onSubmitted: (_) => _commitVolume(),
                            decoration: InputDecoration(
                              isDense: true,
                              border: OutlineInputBorder(
                                borderRadius: BorderRadius.circular(4),
                              ),
                              contentPadding: const EdgeInsets.symmetric(
                                  horizontal: 8, vertical: 10),
                            ),
                          ),
                        ),
                        const SizedBox(width: 8),
                        SizedBox(
                          width: 32,
                          height: 32,
                          child: TextButton(
                            style: TextButton.styleFrom(
                              padding: EdgeInsets.zero,
                              backgroundColor: const Color(0xFF3d3d3d),
                              foregroundColor: Colors.white,
                              shape: RoundedRectangleBorder(
                                  borderRadius: BorderRadius.circular(4)),
                            ),
                            onPressed: () {
                              _commitVolume();
                              setState(() {
                                _volume++;
                                _volumeController.text = _volume.toString();
                              });
                              context
                                  .read<StateService>()
                                  .setTradingVolume(_volume);
                            },
                            child: const Icon(Icons.add, size: 18),
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: 8),
                    Row(
                      children: [
                        Expanded(
                          child: ElevatedButton(
                            style: ElevatedButton.styleFrom(
                              backgroundColor: Colors.green,
                              foregroundColor: Colors.white,
                              shape: RoundedRectangleBorder(
                                  borderRadius: BorderRadius.circular(4)),
                              padding: const EdgeInsets.symmetric(vertical: 10),
                              textStyle: const TextStyle(
                                  fontSize: 11, fontWeight: FontWeight.bold),
                            ),
                            onPressed: _sendingOrder
                                ? null
                                : () => _executeOrder(isBuy: true),
                            child: _sendingOrder
                                ? const SizedBox(
                                    width: 16,
                                    height: 16,
                                    child: CircularProgressIndicator(
                                        strokeWidth: 2, color: Colors.white))
                                : const Text('Comprar'),
                          ),
                        ),
                        const SizedBox(width: 4),
                        Expanded(
                          child: ElevatedButton(
                            style: ElevatedButton.styleFrom(
                              backgroundColor: Colors.orange.shade700,
                              foregroundColor: Colors.white,
                              shape: RoundedRectangleBorder(
                                  borderRadius: BorderRadius.circular(4)),
                              padding: const EdgeInsets.symmetric(vertical: 10),
                              textStyle: const TextStyle(
                                  fontSize: 11, fontWeight: FontWeight.bold),
                            ),
                            onPressed:
                                _sendingOrder ? null : _closeAllPositions,
                            child: _sendingOrder
                                ? const SizedBox(
                                    width: 16,
                                    height: 16,
                                    child: CircularProgressIndicator(
                                        strokeWidth: 2, color: Colors.white))
                                : const Text('Zerar tudo'),
                          ),
                        ),
                        const SizedBox(width: 4),
                        Expanded(
                          child: ElevatedButton(
                            style: ElevatedButton.styleFrom(
                              backgroundColor: Colors.red,
                              foregroundColor: Colors.white,
                              shape: RoundedRectangleBorder(
                                  borderRadius: BorderRadius.circular(4)),
                              padding: const EdgeInsets.symmetric(vertical: 10),
                              textStyle: const TextStyle(
                                  fontSize: 11, fontWeight: FontWeight.bold),
                            ),
                            onPressed: _sendingOrder
                                ? null
                                : () => _executeOrder(isBuy: false),
                            child: _sendingOrder
                                ? const SizedBox(
                                    width: 16,
                                    height: 16,
                                    child: CircularProgressIndicator(
                                        strokeWidth: 2, color: Colors.white))
                                : const Text('Vender'),
                          ),
                        ),
                      ],
                    ),
                    if (_lastResult != null)
                      Padding(
                        padding: const EdgeInsets.only(top: 4),
                        child: Text(
                          _lastResult!.success
                              ? 'Ticket: ${_lastResult!.orderTicket} @ ${_lastResult!.price.toStringAsFixed(2)}'
                              : 'Error: ${_lastResult!.message}',
                          style: TextStyle(
                            fontSize: 11,
                            color: _lastResult!.success
                                ? Colors.green
                                : Colors.red,
                          ),
                        ),
                      ),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildHistorySection() {
    final state = context.watch<StateService>();
    final history = state.history;
    final historyTotal = history.fold(0.0, (sum, d) => sum + d.profit);
    return LayoutBuilder(
      builder: (context, constraints) {
        final maxListH =
            (constraints.maxHeight - 66).clamp(0.0, double.infinity);
        return _section(
          child: Theme(
            data: Theme.of(context).copyWith(
              dividerColor: Colors.transparent,
              unselectedWidgetColor: Colors.grey,
            ),
            child: ExpansionTile(
              tilePadding: const EdgeInsets.symmetric(vertical: 0),
              childrenPadding: const EdgeInsets.only(top: 4),
              title: Row(children: [
                const Icon(Icons.history, size: 16),
                const SizedBox(width: 4),
                Text('Histórico (${history.length})',
                    style: const TextStyle(
                        fontSize: 13, fontWeight: FontWeight.w600)),
                const SizedBox(width: 8),
                history.isEmpty
                    ? const SizedBox.shrink()
                    : Text(
                        '${historyTotal >= 0 ? "+" : ""}${historyTotal.toStringAsFixed(2)}',
                        style: TextStyle(
                          fontSize: 11,
                          fontWeight: FontWeight.bold,
                          color: historyTotal >= 0 ? Colors.green : Colors.red,
                        ),
                      ),
                const Spacer(),
                SizedBox(
                  height: 24,
                  child: Checkbox(
                    value: state.historyTodayOnly,
                    onChanged: (v) {
                      state.setHistoryTodayOnly(v ?? false);
                    },
                    materialTapTargetSize: MaterialTapTargetSize.shrinkWrap,
                    visualDensity: VisualDensity.compact,
                  ),
                ),
                const Text('Hoje',
                    style: TextStyle(fontSize: 11, color: Colors.grey)),
              ]),
              initiallyExpanded: state.tradingHistoryExpanded,
              onExpansionChanged: (expanded) {
                context
                    .read<StateService>()
                    .setTradingHistoryExpanded(expanded);
                if (expanded) _refreshHistory();
              },
              children: [
                history.isEmpty
                    ? Padding(
                        padding: const EdgeInsets.symmetric(vertical: 20),
                        child: Center(
                          child: Text(
                            _loadingHistory ? 'Loading...' : 'Nenhum histórico',
                            style: const TextStyle(
                                color: Colors.grey, fontSize: 12),
                          ),
                        ),
                      )
                    : ConstrainedBox(
                        constraints: BoxConstraints(maxHeight: maxListH),
                        child: ListView.separated(
                          physics: const AlwaysScrollableScrollPhysics(),
                          shrinkWrap: true,
                          itemCount: history.length,
                          separatorBuilder: (_, __) => const Divider(
                              height: 1, color: Color(0xFF3d3d3d)),
                          itemBuilder: (context, index) {
                            final deal = history[index];
                            final isProfitable = deal.profit >= 0;
                            final isBuy = deal.type == 'buy';
                            return ListTile(
                              dense: true,
                              contentPadding: const EdgeInsets.symmetric(
                                  horizontal: 8, vertical: 0),
                              leading: Chip(
                                label: Text(isBuy ? 'B' : 'S',
                                    style: const TextStyle(
                                        fontSize: 10, color: Colors.white)),
                                backgroundColor:
                                    isBuy ? Colors.green : Colors.red,
                                materialTapTargetSize:
                                    MaterialTapTargetSize.shrinkWrap,
                                visualDensity: VisualDensity.compact,
                                padding: EdgeInsets.zero,
                              ),
                              title: Text(
                                  deal.symbol + ' (${deal.volume.toInt()})',
                                  style: const TextStyle(
                                      fontSize: 13,
                                      fontWeight: FontWeight.bold)),
                              subtitle: Text(
                                '${_formatDealTime(deal.time, state.historyTodayOnly)}  |  ${deal.price.toStringAsFixed(2)}',
                                style: const TextStyle(fontSize: 11),
                              ),
                              trailing: Text(
                                '${isProfitable ? "+" : ""}${deal.profit.toStringAsFixed(2)}',
                                style: TextStyle(
                                  fontSize: 12,
                                  fontWeight: FontWeight.bold,
                                  color:
                                      isProfitable ? Colors.green : Colors.red,
                                ),
                              ),
                            );
                          },
                        ),
                      ),
              ],
            ),
          ),
        );
      },
    );
  }

  Widget _section({required Widget child}) {
    return Container(
      padding: const EdgeInsets.fromLTRB(8, 4, 8, 4),
      decoration: BoxDecoration(
        border: Border(
            bottom: BorderSide(color: Colors.grey.shade800.withOpacity(0.3))),
      ),
      child: child,
    );
  }

  Widget _configToggle(String label, bool value, ValueChanged<bool> onChanged) {
    return Row(
      children: [
        Expanded(
          child: Text(label, style: const TextStyle(fontSize: 12)),
        ),
        Switch(
          value: value,
          onChanged: onChanged,
          materialTapTargetSize: MaterialTapTargetSize.shrinkWrap,
        ),
      ],
    );
  }

  Widget _infoRow(String label, String value, {Color? color}) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 1),
      child: Row(
        children: [
          Expanded(
            child: Text(label,
                style: const TextStyle(fontSize: 11, color: Colors.grey)),
          ),
          Text(value,
              style: TextStyle(
                  fontSize: 12, fontWeight: FontWeight.bold, color: color)),
        ],
      ),
    );
  }

  Widget _buildTextField(
      String label, String initialValue, ValueChanged<String> onChanged) {
    return TextField(
      controller: TextEditingController(text: initialValue),
      style: const TextStyle(fontSize: 12),
      decoration: InputDecoration(
        isDense: true,
        labelText: label,
        labelStyle: const TextStyle(fontSize: 12),
        border: const OutlineInputBorder(),
        contentPadding: const EdgeInsets.symmetric(horizontal: 8, vertical: 6),
      ),
      onChanged: onChanged,
    );
  }

  String _formatDealTime(String time, bool todayOnly) {
    if (!todayOnly) return time;
    final parts = time.split(' ');
    return parts.length > 1 ? parts[1] : time;
  }

  String _formatOrderType(String type) {
    switch (type) {
      case 'buy_limit':
        return 'Limit';
      case 'sell_limit':
        return 'Limit';
      case 'buy_stop':
        return 'Stop';
      case 'sell_stop':
        return 'Stop';
      case 'buy_stop_limit':
        return 'Stop Limit';
      case 'sell_stop_limit':
        return 'Stop Limit';
      default:
        return type;
    }
  }
}
