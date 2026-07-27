import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../models/trade_models.dart';
import '../../services/trading_service.dart';
import '../../services/state_service.dart';

class TradingDrawer extends StatefulWidget {
  const TradingDrawer({super.key});

  @override
  State<TradingDrawer> createState() => _TradingDrawerState();
}

class _TradingDrawerState extends State<TradingDrawer> {
  AccountInfo? _account;
  List<PositionInfo> _positions = [];
  List<HistoryDeal> _history = [];
  double _historyTotal = 0;
  bool _loadingAccount = false;
  bool _loadingPositions = false;
  bool _loadingHistory = false;
  bool _historyTodayOnly = true;
  bool _sendingOrder = false;
  bool _closingPosition = false;

  int _volume = 1;
  final _volumeController = TextEditingController(text: '1');
  final _volumeFocusNode = FocusNode();
  OrderResult? _lastResult;

  @override
  void initState() {
    super.initState();
    _volumeFocusNode.addListener(() {
      if (!_volumeFocusNode.hasFocus) _commitVolume();
    });
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
  }

  Future<void> _refreshAll() async {
    await Future.wait([
      _refreshPositions(),
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
      setState(() {});
      final api = context.read<TradingApiService>();
      final state = context.read<StateService>();
      final today = DateTime.now();
      final fromDate = _historyTodayOnly
          ? '${today.year}-${today.month.toString().padLeft(2, '0')}-${today.day.toString().padLeft(2, '0')}'
          : '';
      _history = (await api.getHistory(state.symbol, fromDate: fromDate))
          .reversed
          .toList();
      _historyTotal = _history.fold(0.0, (sum, d) => sum + d.profit);
    } catch (_) {
      _history = [];
      _historyTotal = 0;
    } finally {
      _loadingHistory = false;
      if (mounted) setState(() {});
    }
  }

  Future<void> _refreshPositions() async {
    try {
      _loadingPositions = true;
      final api = context.read<TradingApiService>();
      _positions = await api.getPositions();
    } catch (_) {
      _positions = [];
    } finally {
      _loadingPositions = false;
    }
  }

  Future<void> _executeOrder({required bool isBuy}) async {
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
        if (result.success) {
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(
                content: Text(
                    'Order executed: ticket ${result.orderTicket} @ ${result.price.toStringAsFixed(2)}'),
                backgroundColor: Colors.green),
          );
        } else {
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
    _closingPosition = true;
    setState(() {});

    try {
      final api = context.read<TradingApiService>();
      final result = await api.closePosition(ticket);
      if (result != null && mounted) {
        if (result.success) {
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(
                content: Text(
                    'Position $ticket closed @ ${result.price.toStringAsFixed(2)}'),
                backgroundColor: Colors.green),
          );
        } else {
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(
                content: Text('Failed to close: ${result.message}'),
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
      _closingPosition = false;
      if (mounted) setState(() {});
    }
  }

  Future<void> _closeAllPositions() async {
    _closingPosition = true;
    setState(() {});

    try {
      final api = context.read<TradingApiService>();
      for (final pos in _positions) {
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
      if (mounted && _positions.isNotEmpty) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
              content: Text('All positions closed'),
              backgroundColor: Colors.green),
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
      _closingPosition = false;
      if (mounted) setState(() {});
    }
  }

  @override
  Widget build(BuildContext context) {
    final state = context.watch<StateService>();
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
                    initiallyExpanded: false,
                    onExpansionChanged: (expanded) {
                      if (expanded) _refreshAccount();
                    },
                    children: [
                      if (_account != null) ...[
                        _infoRow('Login', '${_account!.login}'),
                        _infoRow(
                            'Balance', _account!.balance.toStringAsFixed(2)),
                        _infoRow(
                          'Equity',
                          _account!.equity.toStringAsFixed(2),
                          color: _account!.equity >= _account!.balance
                              ? Colors.green
                              : Colors.red,
                        ),
                        _infoRow('Margin', _account!.margin.toStringAsFixed(2)),
                        _infoRow(
                            'Free', _account!.marginFree.toStringAsFixed(2)),
                      ] else
                        Padding(
                          padding: const EdgeInsets.symmetric(vertical: 4),
                          child: _loadingAccount
                              ? const SizedBox(
                                  width: 16,
                                  height: 16,
                                  child:
                                      CircularProgressIndicator(strokeWidth: 2))
                              : const Text('Tap to load',
                                  style: TextStyle(
                                      color: Colors.grey, fontSize: 12)),
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
                      Text('Positions (${_positions.length})',
                          style: const TextStyle(
                              fontSize: 13, fontWeight: FontWeight.w600)),
                    ]),
                    initiallyExpanded: false,
                    onExpansionChanged: (expanded) {
                      if (expanded) _refreshPositions();
                    },
                    children: [
                      _positions.isEmpty
                          ? Padding(
                              padding: const EdgeInsets.symmetric(vertical: 20),
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
                              itemCount: _positions.length,
                              separatorBuilder: (_, __) => const Divider(
                                  height: 1, color: Color(0xFF3d3d3d)),
                              itemBuilder: (context, index) {
                                final pos = _positions[index];
                                final isProfitable = pos.profit >= 0;
                                final isBuy = pos.type == 'buy';
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
                                      Text(
                                        '${isProfitable ? "+" : ""}${pos.profit.toStringAsFixed(2)}',
                                        style: TextStyle(
                                          fontSize: 12,
                                          fontWeight: FontWeight.bold,
                                          color: isProfitable
                                              ? Colors.green
                                              : Colors.red,
                                        ),
                                      ),
                                      const SizedBox(width: 4),
                                      IconButton(
                                        icon: const Icon(Icons.close,
                                            size: 14, color: Colors.red),
                                        onPressed: _closingPosition
                                            ? null
                                            : () => _closePosition(pos.ticket),
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
        fontSize: 11, fontWeight: FontWeight.bold, color: Colors.grey)),
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
                                _closingPosition ? null : _closeAllPositions,
                            child: _closingPosition
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
                Text('Histórico (${_history.length})',
                    style: const TextStyle(
                        fontSize: 13, fontWeight: FontWeight.w600)),
                const SizedBox(width: 8),
                _history.isEmpty
                    ? const SizedBox.shrink()
                    : Text(
                        '${_historyTotal >= 0 ? "+" : ""}${_historyTotal.toStringAsFixed(2)}',
                        style: TextStyle(
                          fontSize: 11,
                          fontWeight: FontWeight.bold,
                          color: _historyTotal >= 0 ? Colors.green : Colors.red,
                        ),
                      ),
                const Spacer(),
                SizedBox(
                  height: 24,
                  child: Checkbox(
                    value: _historyTodayOnly,
                    onChanged: (v) {
                      setState(() => _historyTodayOnly = v ?? false);
                      _refreshHistory();
                    },
                    materialTapTargetSize: MaterialTapTargetSize.shrinkWrap,
                    visualDensity: VisualDensity.compact,
                  ),
                ),
                const Text('Hoje',
                    style: TextStyle(fontSize: 11, color: Colors.grey)),
              ]),
              initiallyExpanded: false,
              onExpansionChanged: (expanded) {
                if (expanded) _refreshHistory();
              },
              children: [
                _history.isEmpty
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
                          itemCount: _history.length,
                          separatorBuilder: (_, __) => const Divider(
                              height: 1, color: Color(0xFF3d3d3d)),
                          itemBuilder: (context, index) {
                            final deal = _history[index];
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
                              title: Text(deal.symbol + ' (${deal.volume.toInt()})',
                                  style: const TextStyle(
                                      fontSize: 13,
                                      fontWeight: FontWeight.bold)),
                              subtitle: Text(
                                '${_formatDealTime(deal.time)}  |  ${deal.price.toStringAsFixed(2)}',
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

  String _formatDealTime(String time) {
    if (!_historyTodayOnly) return time;
    final parts = time.split(' ');
    return parts.length > 1 ? parts[1] : time;
  }
}
