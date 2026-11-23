import 'dart:async';

import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:provider/provider.dart';

import '../../models/defaults.dart';
import '../../models/trade_models.dart';
import '../../services/state_service.dart';
import '../../services/trading_service.dart';
import 'chart/measure_ruler.dart';

/// Aba "Book de Ofertas" do Trading Drawer (issue #51).
///
/// - Polling curto (400 ms) apenas quando [active] (aba visível).
/// - Clique em nível de ask compra / bid vende, com o volume atual
///   (`StateService.tradingVolume`) e `kind` coerente com a regra do gráfico
///   (preço abaixo/acima da referência = limit/stop).
/// - Ordens limite próprias aparecem no nível `price_open`, destacadas, e
///   podem ser arrastadas: soltar em nível válido modifica o preço; soltar na
///   zona de execução (spread / além do bid/ask) cancela e executa a mercado
///   com o volume da própria ordem.
class OrderBookPanel extends StatefulWidget {
  const OrderBookPanel({
    super.key,
    required this.active,
    this.onOrderPlaced,
  });

  /// true somente quando esta aba está visível; pausa o polling caso contrário.
  final bool active;

  /// Chamado após qualquer envio/modificação para atualizar
  /// Open Orders/Positions/Histórico no drawer.
  final Future<void> Function()? onOrderPlaced;

  @override
  State<OrderBookPanel> createState() => _OrderBookPanelState();
}

class _OrderBookPanelState extends State<OrderBookPanel> {
  static const Color _bg = Color(0xFF1e1e1e);
  static const Color _line = Color(0xFF3d3d3d);
  static const double _priceColumnWidth = 72;
  static const int _maxBookLevels = 30;
  static const Duration _pollInterval = Duration(milliseconds: 400);

  OrderBook? _book;
  Timer? _timer;
  bool _fetching = false;
  bool _sending = false;
  int? _busyTicket;
  String? _error;
  double? _highlightPrice;

  @override
  void initState() {
    super.initState();
    if (widget.active) _startPolling();
  }

  @override
  void didUpdateWidget(covariant OrderBookPanel oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (widget.active && !oldWidget.active) {
      _startPolling();
    } else if (!widget.active && oldWidget.active) {
      _stopPolling();
    }
  }

  @override
  void dispose() {
    _stopPolling();
    super.dispose();
  }

  void _startPolling() {
    _stopPolling();
    _fetch();
    _timer = Timer.periodic(_pollInterval, (_) => _fetch());
  }

  void _stopPolling() {
    _timer?.cancel();
    _timer = null;
  }

  Future<void> _fetch() async {
    if (!mounted || _fetching) return;
    final state = context.read<StateService>();
    final symbol = state.symbol;
    if (symbol.isEmpty) return;
    _fetching = true;
    try {
      final api = context.read<TradingApiService>();
      final book = await api.getOrderBook(symbol);
      if (!mounted) return;
      setState(() {
        _book = book;
        _error = book == null ? 'Falha ao carregar o book.' : null;
      });
    } catch (e) {
      if (mounted) setState(() => _error = 'Erro no book: $e');
    } finally {
      _fetching = false;
    }
  }

  double _referencePrice(OrderBook book) {
    final bid = book.bestBid;
    final ask = book.bestAsk;
    if (bid > 0 && ask > 0) return (bid + ask) / 2;
    return bid > 0 ? bid : ask;
  }

  Future<bool> _confirm(String title, String detail) async {
    if (!context.read<StateService>().orderBookConfirmOrders) return true;
    final result = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        backgroundColor: const Color(0xFF2a2a2a),
        title: Text(title, style: const TextStyle(fontSize: 15)),
        content: Text(detail, style: const TextStyle(fontSize: 13)),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(ctx).pop(false),
            child: const Text('Cancelar'),
          ),
          FilledButton(
            onPressed: () => Navigator.of(ctx).pop(true),
            child: const Text('Confirmar'),
          ),
        ],
      ),
    );
    return result == true;
  }

  void _snack(String message, {bool error = false}) {
    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(
      content: Text(message),
      backgroundColor: error ? Colors.red : Colors.green,
    ));
  }

  Future<void> _afterOrder() async {
    await _fetch();
    await widget.onOrderPlaced?.call();
  }

  void _highlight(double price) {
    if (!mounted) return;
    setState(() => _highlightPrice = price);
    Future.delayed(const Duration(milliseconds: 1200), () {
      if (mounted && _highlightPrice == price) {
        setState(() => _highlightPrice = null);
      }
    });
  }

  Future<void> _placeLimitAt(
      {required bool isAsk, required double price}) async {
    if (_sending) return;
    final state = context.read<StateService>();
    final book = _book;
    if (book == null || !book.available) return;
    final reference = _referencePrice(book);
    if (reference <= 0) {
      _snack('Referência de preço indisponível.', error: true);
      return;
    }

    final isBuy = isAsk;
    final isLimit = isBuy ? price < reference : price > reference;
    final kind = isLimit ? 'limit' : 'stop';
    final volume = state.tradingVolume;
    final kindLabel = isLimit ? 'limite' : 'stop';

    _sending = true;
    setState(() {});
    try {
      final ok = await _confirm(
        '${isBuy ? 'Comprar' : 'Vender'} $volume',
        'Ordem $kindLabel @ ${price.toStringAsFixed(2)} '
            '(${state.symbol})?',
      );
      if (!ok || !mounted) return;

      final api = context.read<TradingApiService>();
      final result = await api.placeLimitOrder(LimitOrderRequest(
        symbol: state.symbol,
        volume: volume.toDouble(),
        type: isBuy ? 'buy' : 'sell',
        price: price,
        kind: kind,
      ));
      if (!mounted) return;
      if (result == null) {
        _snack('Falha ao enviar a ordem.', error: true);
        return;
      }
      if (!result.success) {
        final reason = result.message.isNotEmpty
            ? result.message
            : (result.retcodeName.isNotEmpty
                ? result.retcodeName
                : 'erro desconhecido');
        _snack('Ordem rejeitada: $reason', error: true);
        return;
      }
      _highlight(price);
      await _afterOrder();
    } catch (e) {
      _snack('Erro ao enviar ordem: $e', error: true);
    } finally {
      _sending = false;
      if (mounted) setState(() {});
    }
  }

  Future<void> _modifyPending(OrderInfo order, double price) async {
    if (_sending) return;
    _sending = true;
    _busyTicket = order.ticket;
    setState(() {});
    try {
      final ok = await _confirm(
        'Modificar ordem ${order.ticket}',
        'Novo preço: ${price.toStringAsFixed(2)} '
            '(volume ${order.volume.toStringAsFixed(2)})?',
      );
      if (!ok || !mounted) return;

      final api = context.read<TradingApiService>();
      final result = await api.modifyPendingOrder(ModifyPendingOrderRequest(
        orderTicket: order.ticket,
        price: price,
      ));
      if (!mounted) return;
      if (result == null || !result.success) {
        final reason = result?.message.isNotEmpty == true
            ? result!.message
            : (result?.retcodeName.isNotEmpty == true
                ? result!.retcodeName
                : 'erro desconhecido');
        _snack('Falha ao modificar ordem ${order.ticket}: $reason',
            error: true);
        return;
      }
      _highlight(price);
      await _afterOrder();
    } catch (e) {
      _snack('Erro ao modificar ordem: $e', error: true);
    } finally {
      _sending = false;
      _busyTicket = null;
      if (mounted) setState(() {});
    }
  }

  Future<void> _cancelAndExecuteMarket(OrderInfo order) async {
    if (_sending) return;
    final isBuy = order.type.startsWith('buy');
    _sending = true;
    _busyTicket = order.ticket;
    setState(() {});
    try {
      final ok = await _confirm(
        'Executar a mercado',
        'Cancelar a ordem ${order.ticket} e '
            '${isBuy ? 'comprar' : 'vender'} '
            '${order.volume.toStringAsFixed(2)} a mercado?',
      );
      if (!ok || !mounted) return;

      final api = context.read<TradingApiService>();
      final state = context.read<StateService>();

      final cancel = await api.cancelOrder(order.ticket);
      if (cancel == null || !cancel.success) {
        final reason = cancel?.message.isNotEmpty == true
            ? cancel!.message
            : (cancel?.retcodeName.isNotEmpty == true
                ? cancel!.retcodeName
                : 'erro desconhecido');
        _snack(
            'Cancelamento da ordem ${order.ticket} falhou ($reason). '
            'Nada foi enviado a mercado.',
            error: true);
        return;
      }

      final result = await api.placeMarketOrder(MarketOrderRequest(
        symbol: state.symbol,
        volume: order.volume,
        type: isBuy ? 'buy' : 'sell',
      ));
      if (!mounted) return;
      if (result == null || !result.success) {
        final reason = result?.message.isNotEmpty == true
            ? result!.message
            : (result?.retcodeName.isNotEmpty == true
                ? result!.retcodeName
                : 'erro desconhecido');
        final newTicket = result?.orderTicket ?? 0;
        _snack(
            'Ordem ${order.ticket} cancelada, mas a ordem a mercado '
            'falhou ($reason). Tickets: cancelada=${order.ticket}, '
            'nova=$newTicket.',
            error: true);
        return;
      }
      await _afterOrder();
    } catch (e) {
      _snack('Erro ao cancelar/executar ordem: $e', error: true);
    } finally {
      _sending = false;
      _busyTicket = null;
      if (mounted) setState(() {});
    }
  }

  bool _symbolMatches(String orderSymbol, String current) {
    final a = orderSymbol.toUpperCase();
    final b = current.toUpperCase();
    if (a.isEmpty || b.isEmpty) return false;
    return a == b || a.startsWith(b) || b.startsWith(a);
  }

  List<OrderInfo> _ownLimitOrders(StateService state) {
    return state.orders
        .where((o) =>
            (o.type == 'buy_limit' || o.type == 'sell_limit') &&
            _symbolMatches(o.symbol, state.symbol))
        .toList();
  }

  bool _canDrop(OrderInfo order, double price) {
    if (_sending || price <= 0) return false;
    return (order.priceOpen - price).abs() > 0.0000001;
  }

  void _handleDrop(OrderInfo order, double price) {
    if (!_canDrop(order, price)) return;
    final book = _book;
    if (book == null) return;
    final isBuy = order.type.startsWith('buy');
    final bestAsk = book.bestAsk;
    final bestBid = book.bestBid;
    final market = isBuy
        ? (bestAsk > 0 && price >= bestAsk)
        : (bestBid > 0 && price <= bestBid);
    if (market) {
      _cancelAndExecuteMarket(order);
    } else {
      _modifyPending(order, price);
    }
  }

  @override
  Widget build(BuildContext context) {
    final state = context.watch<StateService>();
    final book = _book;

    return Container(
      color: _bg,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          _buildHeader(state, book),
          const Divider(height: 1, color: _line),
          if (book == null)
            const Expanded(
              child: Center(
                child: SizedBox(
                  width: 18,
                  height: 18,
                  child: CircularProgressIndicator(strokeWidth: 2),
                ),
              ),
            )
          else if (!book.available)
            Expanded(child: _buildUnavailable(book))
          else
            Expanded(child: _buildBook(state, book)),
          if (_error != null)
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
              child: Text(
                _error!,
                style: const TextStyle(fontSize: 10, color: Colors.redAccent),
              ),
            ),
        ],
      ),
    );
  }

  Widget _buildHeader(StateService state, OrderBook? book) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(8, 6, 4, 4),
      child: Row(
        children: [
          const Icon(Icons.menu_book, size: 16),
          const SizedBox(width: 4),
          const Text('Book de Ofertas',
              style: TextStyle(fontSize: 13, fontWeight: FontWeight.w600)),
          const Spacer(),
          if (book != null && book.source == 'tick_fallback')
            const Tooltip(
              message: 'Book indisponível: exibindo Nível 1 (bid/ask).',
              child: Icon(Icons.info_outline, size: 14, color: Colors.orange),
            ),
          const SizedBox(width: 2),
          Text(state.symbol,
              style: const TextStyle(
                  fontSize: 11,
                  fontWeight: FontWeight.bold,
                  color: Colors.grey)),
          IconButton(
            icon: const Icon(Icons.refresh, size: 16),
            padding: EdgeInsets.zero,
            constraints: const BoxConstraints(minWidth: 26, minHeight: 26),
            onPressed: _fetch,
          ),
        ],
      ),
    );
  }

  Widget _buildUnavailable(OrderBook book) {
    return Padding(
      padding: const EdgeInsets.all(16),
      child: Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(Icons.cloud_off, size: 28, color: Colors.grey),
            const SizedBox(height: 8),
            Text(
              book.message.isNotEmpty
                  ? book.message
                  : 'Book indisponível para este símbolo.',
              textAlign: TextAlign.center,
              style: const TextStyle(fontSize: 12, color: Colors.grey),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildBook(StateService state, OrderBook book) {
    final symbol = state.symbol;
    final tick = Defaults.tickSize(symbol);
    final tolerance = tick > 0 ? tick / 2 : 0.0001;

    final askLevels = book.asks.take(_maxBookLevels).toList();
    final bidLevels = book.bids.take(_maxBookLevels).toList();

    final ownOrders = _ownLimitOrders(state);
    final ownBuys = ownOrders.where((o) => o.type == 'buy_limit').toList();
    final ownSells = ownOrders.where((o) => o.type == 'sell_limit').toList();

    final askVolumes = {for (final l in askLevels) l.price: l.volume};
    final bidVolumes = {for (final l in bidLevels) l.price: l.volume};

    final askPrices = <double>{
      ...askLevels.map((l) => l.price),
      ...ownSells.map((o) => o.priceOpen),
    }.toList()
      ..sort();
    final bidPrices = <double>{
      ...bidLevels.map((l) => l.price),
      ...ownBuys.map((o) => o.priceOpen),
    }.toList()
      ..sort((a, b) => b.compareTo(a));

    final totalAskVolume =
        askVolumes.values.fold<double>(0, (total, v) => total + v);
    final totalBidVolume =
        bidVolumes.values.fold<double>(0, (total, v) => total + v);

    // O Profit destaca em amarelo as ofertas "relevantes". Usamos um limiar
    // relativo (bem acima da média do lado) para reproduzir o efeito.
    final askHighlight = askLevels.isEmpty
        ? 0.0
        : (totalAskVolume / askLevels.length) * 1.6;
    final bidHighlight = bidLevels.isEmpty
        ? 0.0
        : (totalBidVolume / bidLevels.length) * 1.6;

    bool isHighlighted(double volume, double threshold) =>
        volume >= 50 && volume >= threshold;

    return SingleChildScrollView(
      physics: const AlwaysScrollableScrollPhysics(),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          _buildColumnHeader(),
          if (askPrices.isEmpty)
            const Padding(
              padding: EdgeInsets.symmetric(vertical: 8),
              child: Center(
                child: Text('Sem asks',
                    style: TextStyle(fontSize: 11, color: Colors.grey)),
              ),
            ),
          for (final price in askPrices.reversed)
            _buildLevelRow(
              symbol: symbol,
              price: price,
              isAsk: true,
              volume: askVolumes[price] ?? 0,
              totalVolume: totalAskVolume,
              highlightVolume:
                  isHighlighted(askVolumes[price] ?? 0, askHighlight),
              ownOrders: ownSells
                  .where((o) => (o.priceOpen - price).abs() <= tolerance)
                  .toList(),
            ),
          _buildSpreadRow(book, symbol),
          for (final price in bidPrices)
            _buildLevelRow(
              symbol: symbol,
              price: price,
              isAsk: false,
              volume: bidVolumes[price] ?? 0,
              totalVolume: totalBidVolume,
              highlightVolume:
                  isHighlighted(bidVolumes[price] ?? 0, bidHighlight),
              ownOrders: ownBuys
                  .where((o) => (o.priceOpen - price).abs() <= tolerance)
                  .toList(),
            ),
          if (bidPrices.isEmpty)
            const Padding(
              padding: EdgeInsets.symmetric(vertical: 8),
              child: Center(
                child: Text('Sem bids',
                    style: TextStyle(fontSize: 11, color: Colors.grey)),
              ),
            ),
        ],
      ),
    );
  }

  Widget _buildColumnHeader() {
    const bidStyle = TextStyle(
      fontSize: 9,
      fontWeight: FontWeight.w600,
      color: Color(0xFF9CC7FF),
    );
    const askStyle = TextStyle(
      fontSize: 9,
      fontWeight: FontWeight.w600,
      color: Color(0xFFFFA0A0),
    );
    const priceStyle = TextStyle(
      fontSize: 9,
      fontWeight: FontWeight.w600,
      color: Color(0xFFD0D0D0),
    );
    return Container(
      height: 20,
      decoration: const BoxDecoration(
        color: Color(0xFF1E1E1E),
        border: Border(
          bottom: BorderSide(color: Colors.black),
        ),
      ),
      child: const Row(
        children: [
          Expanded(
            child: Padding(
              padding: EdgeInsets.only(left: 6),
              child: Text('Qtd. Compra',
                  textAlign: TextAlign.left, style: bidStyle),
            ),
          ),
          SizedBox(
            width: _priceColumnWidth,
            child: Text('Preço', textAlign: TextAlign.center,
                style: priceStyle),
          ),
          Expanded(
            child: Padding(
              padding: EdgeInsets.only(right: 6),
              child: Text('Qtd. Venda',
                  textAlign: TextAlign.right, style: askStyle),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildLevelRow({
    required String symbol,
    required double price,
    required bool isAsk,
    required double volume,
    required double totalVolume,
    required bool highlightVolume,
    required List<OrderInfo> ownOrders,
  }) {
    final highlighted =
        _highlightPrice != null && (_highlightPrice! - price).abs() < 0.0000001;

    return DragTarget<OrderInfo>(
      onWillAcceptWithDetails: (details) => _canDrop(details.data, price),
      onAcceptWithDetails: (details) => _handleDrop(details.data, price),
      builder: (context, candidate, rejected) {
        final hovering = candidate.isNotEmpty;
        final ownHighlight = ownOrders.isNotEmpty;
        Color rowBg = const Color(0xFF161616);
        if (hovering) rowBg = Colors.cyan.shade900;

        final fraction =
            totalVolume > 0 ? (volume / totalVolume).clamp(0.0, 1.0) : 0.0;
        final hasLevel = volume > 0;

        final priceBg = hovering
            ? const Color(0xFF9FD6EA)
            : highlighted
                ? const Color(0xFFFFE082)
                : const Color(0xFFE9E9E9);

        return Material(
          color: rowBg,
          child: InkWell(
            onTap: _sending
                ? null
                : () => _placeLimitAt(isAsk: isAsk, price: price),
            child: Container(
              height: 18,
              decoration: BoxDecoration(
                border: Border(
                  bottom: BorderSide(
                      color: Colors.black.withValues(alpha: 0.45)),
                  left: ownHighlight
                      ? BorderSide(color: Colors.amber.shade400, width: 3)
                      : BorderSide.none,
                ),
              ),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Expanded(
                    child: isAsk
                        ? const SizedBox.shrink()
                        : _buildQtyCell(
                            isAsk: false,
                            qtyText: _formatQty(volume),
                            fraction: fraction,
                            hasLevel: hasLevel,
                            highlightVolume: highlightVolume,
                            ownOrders: ownOrders,
                          ),
                  ),
                  Container(
                    width: _priceColumnWidth,
                    alignment: Alignment.center,
                    decoration: BoxDecoration(
                      color: priceBg,
                      border: const Border(
                        left: BorderSide(color: Colors.black54),
                        right: BorderSide(color: Colors.black54),
                      ),
                    ),
                    child: Text(
                      formatRulerPrice(price, symbol),
                      style: const TextStyle(
                        fontSize: 11,
                        fontWeight: FontWeight.w500,
                        color: Colors.black,
                      ),
                    ),
                  ),
                  Expanded(
                    child: isAsk
                        ? _buildQtyCell(
                            isAsk: true,
                            qtyText: _formatQty(volume),
                            fraction: fraction,
                            hasLevel: hasLevel,
                            highlightVolume: highlightVolume,
                            ownOrders: ownOrders,
                          )
                        : const SizedBox.shrink(),
                  ),
                ],
              ),
            ),
          ),
        );
      },
    );
  }

  /// Célula de quantidade no estilo Profit: célula azul/vermelha cheia, texto
  /// preto e uma faixa mais escura proporcional ao volume. Ofertas relevantes
  /// (volume bem acima da média do lado) ficam amarelas, como no Profit.
  Widget _buildQtyCell({
    required bool isAsk,
    required String qtyText,
    required double fraction,
    required bool hasLevel,
    required bool highlightVolume,
    required List<OrderInfo> ownOrders,
  }) {
    final baseColor =
        isAsk ? const Color(0xFFE02424) : const Color(0xFF1F6ED6);
    const highlightColor = Color(0xFFF4C51C);

    final background = !hasLevel
        ? Colors.transparent
        : highlightVolume
            ? highlightColor
            : baseColor;

    final qty = Text(
      qtyText,
      style: const TextStyle(
        fontSize: 11,
        fontWeight: FontWeight.w600,
        color: Colors.black,
      ),
    );

    return Container(
      color: background,
      child: Stack(
        alignment: isAsk ? Alignment.centerLeft : Alignment.centerRight,
        children: [
          if (hasLevel && !highlightVolume && fraction > 0)
            Align(
              alignment:
                  isAsk ? Alignment.centerLeft : Alignment.centerRight,
              child: FractionallySizedBox(
                alignment:
                    isAsk ? Alignment.centerLeft : Alignment.centerRight,
                widthFactor: fraction,
                heightFactor: 1,
                child: ColoredBox(
                  color: Colors.black.withValues(alpha: 0.18),
                ),
              ),
            ),
          Align(
            alignment: isAsk ? Alignment.centerLeft : Alignment.centerRight,
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: 5),
              child: Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  if (isAsk) qty,
                  for (final order in ownOrders)
                    Padding(
                      padding: const EdgeInsets.symmetric(horizontal: 2),
                      child: _buildOwnBadge(order),
                    ),
                  if (!isAsk) qty,
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }

  String _formatQty(double value) {
    if (value <= 0) return '';
    if (value >= 1000) {
      final text = NumberFormat('#,##0.00', 'pt_BR').format(value / 1000);
      return '${text}k';
    }
    return NumberFormat('#,##0', 'pt_BR').format(value);
  }

  Widget _buildOwnBadge(OrderInfo order) {
    final busy = _busyTicket == order.ticket;
    final isBuy = order.type.startsWith('buy');
    final badge = Container(
      padding: const EdgeInsets.symmetric(horizontal: 3, vertical: 1),
      decoration: BoxDecoration(
        color: Colors.black.withValues(alpha: 0.72),
        borderRadius: BorderRadius.circular(3),
      ),
      child: busy
          ? const SizedBox(
              width: 9,
              height: 9,
              child: CircularProgressIndicator(
                  strokeWidth: 1.5, color: Colors.amber),
            )
          : Text(
              '${isBuy ? 'C' : 'V'}${order.volume.toStringAsFixed(0)}',
              style: const TextStyle(
                  fontSize: 8,
                  fontWeight: FontWeight.bold,
                  color: Colors.amber),
            ),
    );

    if (_sending || busy) return Opacity(opacity: 0.5, child: badge);

    return GestureDetector(
      onTap: () {},
      child: Draggable<OrderInfo>(
        data: order,
        axis: Axis.vertical,
        feedback: Material(
          color: Colors.transparent,
          child: Chip(
            backgroundColor: Colors.amber.shade700,
            label: Text(
              '#${order.ticket} ${isBuy ? 'compra' : 'venda'} '
              '${order.volume.toStringAsFixed(2)}',
              style: const TextStyle(fontSize: 10, color: Colors.black),
            ),
          ),
        ),
        childWhenDragging: Opacity(opacity: 0.3, child: badge),
        child: Tooltip(
          message: 'Arraste para mover ou executar a mercado',
          child: badge,
        ),
      ),
    );
  }

  Widget _buildSpreadRow(OrderBook book, String symbol) {
    final bid = book.bestBid;
    final ask = book.bestAsk;
    final spread = (ask > 0 && bid > 0) ? ask - bid : 0.0;
    final dec = decimalPlacesForSymbol(symbol);
    final spreadText = spread > 0
        ? spread.toStringAsFixed(dec).replaceAll('.', ',')
        : '—';

    return DragTarget<OrderInfo>(
      onWillAcceptWithDetails: (_) => !_sending,
      onAcceptWithDetails: (details) => _cancelAndExecuteMarket(details.data),
      builder: (context, candidate, rejected) {
        final hovering = candidate.isNotEmpty;
        return Container(
          height: 16,
          decoration: BoxDecoration(
            color: const Color(0xFF161616),
            border: Border.symmetric(
              horizontal: BorderSide(
                color: Colors.black.withValues(alpha: 0.5),
              ),
            ),
          ),
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const Expanded(child: SizedBox.shrink()),
              Container(
                width: _priceColumnWidth,
                alignment: Alignment.center,
                decoration: BoxDecoration(
                  color: hovering
                      ? Colors.orange.shade700
                      : const Color(0xFFC6D9F2),
                  border: const Border(
                    left: BorderSide(color: Colors.black54),
                    right: BorderSide(color: Colors.black54),
                  ),
                ),
                child: Tooltip(
                  message: 'Spread',
                  child: Text(
                    hovering ? 'EXECUTAR' : spreadText,
                    textAlign: TextAlign.center,
                    style: TextStyle(
                      fontSize: hovering ? 8 : 9,
                      fontWeight: FontWeight.w600,
                      color: hovering ? Colors.white : Colors.black87,
                    ),
                  ),
                ),
              ),
              const Expanded(child: SizedBox.shrink()),
            ],
          ),
        );
      },
    );
  }
}
