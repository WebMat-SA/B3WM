import 'dart:async';
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
  bool _loadingAccount = false;
  bool _loadingPositions = false;
  bool _sendingOrder = false;
  bool _closingPosition = false;

  double _volume = 1.0;
  bool _isBuy = true;
  double? _sl;
  double? _tp;
  String _comment = '';
  int _deviation = 10;
  OrderResult? _lastResult;

  Timer? _refreshTimer;

  @override
  void initState() {
    super.initState();
    _refreshTimer = Timer.periodic(const Duration(seconds: 5), (_) {
      _refreshAll();
    });
    WidgetsBinding.instance.addPostFrameCallback((_) => _refreshAll());
  }

  @override
  void dispose() {
    _refreshTimer?.cancel();
    super.dispose();
  }

  Future<void> _refreshAll() async {
    await Future.wait([
      _refreshAccount(),
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

  Future<void> _executeOrder() async {
    final state = context.read<StateService>();
    _sendingOrder = true;
    _lastResult = null;
    setState(() {});

    try {
      final api = context.read<TradingApiService>();
      final request = MarketOrderRequest(
        symbol: state.symbol,
        volume: _volume,
        type: _isBuy ? 'buy' : 'sell',
        sl: _sl,
        tp: _tp,
        comment: _comment,
        deviation: _deviation,
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
      _lastResult = OrderResult(
          success: false, message: e.toString());
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
              content: Text('Error: $e'),
              backgroundColor: Colors.red),
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
                content: Text('Position $ticket closed @ ${result.price.toStringAsFixed(2)}'),
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
          SnackBar(
              content: Text('Error: $e'),
              backgroundColor: Colors.red),
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
      decoration: BoxDecoration(
        color: const Color(0xFF1e1e1e),
        border: Border(
            left: BorderSide(color: Colors.grey.shade800.withOpacity(0.3))),
      ),
      child: Column(
        children: [
          // Header
          _section(
            child: Row(
              children: [
                const Icon(Icons.travel_explore, size: 18),
                const SizedBox(width: 8),
                const Expanded(
                  child: Text('Trading',
                      style: TextStyle(
                          fontSize: 16, fontWeight: FontWeight.bold)),
                ),
                IconButton(
                  icon: const Icon(Icons.refresh, size: 18),
                  onPressed: _refreshAll,
                ),
              ],
            ),
          ),

          // Account Info
          _section(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Row(children: [
                  Icon(Icons.account_balance_wallet, size: 16),
                  SizedBox(width: 4),
                  Text('Account',
                      style: TextStyle(
                          fontSize: 13, fontWeight: FontWeight.w600)),
                ]),
                const SizedBox(height: 4),
                if (_account != null) ...[
                  _infoRow('Login', '${_account!.login}'),
                  _infoRow('Balance', _account!.balance.toStringAsFixed(2)),
                  _infoRow(
                    'Equity',
                    _account!.equity.toStringAsFixed(2),
                    color: _account!.equity >= _account!.balance
                        ? Colors.green
                        : Colors.red,
                  ),
                  _infoRow('Margin', _account!.margin.toStringAsFixed(2)),
                  _infoRow('Free', _account!.marginFree.toStringAsFixed(2)),
                  _infoRow('Level', '${_account!.marginLevel.toStringAsFixed(2)}%'),
                ] else
                  Text(_loadingAccount ? 'Loading...' : 'No data',
                      style: const TextStyle(color: Colors.grey, fontSize: 12)),
              ],
            ),
          ),

          // Market Order
          _section(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Row(children: [
                  Icon(Icons.swap_vert, size: 16),
                  SizedBox(width: 4),
                  Text('Market Order',
                      style: TextStyle(
                          fontSize: 13, fontWeight: FontWeight.w600)),
                ]),
                const SizedBox(height: 8),
                _infoRow('Symbol', state.symbol),
                const SizedBox(height: 4),
                Row(
                  children: [
                    Expanded(
                      child: ElevatedButton(
                        style: ElevatedButton.styleFrom(
                          backgroundColor: _isBuy ? Colors.green : Colors.grey.shade800,
                          foregroundColor: Colors.white,
                          shape: RoundedRectangleBorder(
                              borderRadius: BorderRadius.circular(4)),
                          padding: const EdgeInsets.symmetric(vertical: 8),
                        ),
                        onPressed: () => setState(() => _isBuy = true),
                        child: const Text('COMPRAR',
                            style: TextStyle(
                                fontSize: 12, fontWeight: FontWeight.bold)),
                      ),
                    ),
                    const SizedBox(width: 4),
                    Expanded(
                      child: ElevatedButton(
                        style: ElevatedButton.styleFrom(
                          backgroundColor: !_isBuy ? Colors.red : Colors.grey.shade800,
                          foregroundColor: Colors.white,
                          shape: RoundedRectangleBorder(
                              borderRadius: BorderRadius.circular(4)),
                          padding: const EdgeInsets.symmetric(vertical: 8),
                        ),
                        onPressed: () => setState(() => _isBuy = false),
                        child: const Text('VENDER',
                            style: TextStyle(
                                fontSize: 12, fontWeight: FontWeight.bold)),
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 8),
                _buildTextField('Volume', _volume.toString(),
                    (v) => _volume = double.tryParse(v) ?? 1.0),
                const SizedBox(height: 4),
                Row(
                  children: [
                    Expanded(
                      child: _buildTextField('Stop Loss', _sl?.toString() ?? '',
                          (v) => _sl = double.tryParse(v)),
                    ),
                    const SizedBox(width: 4),
                    Expanded(
                      child: _buildTextField('Take Profit', _tp?.toString() ?? '',
                          (v) => _tp = double.tryParse(v)),
                    ),
                  ],
                ),
                const SizedBox(height: 4),
                _buildTextField('Deviation (pts)', '$_deviation',
                    (v) => _deviation = int.tryParse(v) ?? 10),
                const SizedBox(height: 4),
                _buildTextField('Comment', _comment, (v) => _comment = v),
                const SizedBox(height: 8),
                SizedBox(
                  width: double.infinity,
                  child: ElevatedButton(
                    style: ElevatedButton.styleFrom(
                      backgroundColor: _isBuy ? Colors.green : Colors.red,
                      foregroundColor: Colors.white,
                      shape: RoundedRectangleBorder(
                          borderRadius: BorderRadius.circular(4)),
                      padding: const EdgeInsets.symmetric(vertical: 10),
                    ),
                    onPressed: _sendingOrder ? null : _executeOrder,
                    child: _sendingOrder
                        ? const SizedBox(
                            width: 16,
                            height: 16,
                            child: CircularProgressIndicator(
                                strokeWidth: 2, color: Colors.white))
                        : Text(
                            '${_isBuy ? "COMPRAR" : "VENDER"} ${state.symbol}',
                            style: const TextStyle(
                                fontSize: 13, fontWeight: FontWeight.bold)),
                  ),
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
                        color: _lastResult!.success ? Colors.green : Colors.red,
                      ),
                    ),
                  ),
              ],
            ),
          ),


          // Positions
          Expanded(
            child: _section(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(children: [
                    const Icon(Icons.assignment, size: 16),
                    const SizedBox(width: 4),
                    Text('Positions (${_positions.length})',
                        style: const TextStyle(
                            fontSize: 13, fontWeight: FontWeight.w600)),
                  ]),
                  const SizedBox(height: 4),
                  if (_positions.isEmpty)
                    Expanded(
                      child: Center(
                        child: Text(
                          _loadingPositions ? 'Loading...' : 'No open positions',
                          style:
                              const TextStyle(color: Colors.grey, fontSize: 12),
                        ),
                      ),
                    )
                  else
                    Expanded(
                      child: ListView.separated(
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
                                style:
                                    const TextStyle(fontSize: 13, fontWeight: FontWeight.bold)),
                            subtitle: Text(
                              'Vol: ${pos.volume.toStringAsFixed(2)} @ ${pos.priceOpen.toStringAsFixed(2)}',
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
                    ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _section({required Widget child}) {
    return Container(
      padding: const EdgeInsets.all(8),
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
        contentPadding:
            const EdgeInsets.symmetric(horizontal: 8, vertical: 6),
      ),
      onChanged: onChanged,
    );
  }
}
