import 'dart:async';
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../services/state_service.dart';
import 'timeframe_selector.dart';

class MapFlowAppBar extends StatefulWidget implements PreferredSizeWidget {
  final VoidCallback onBubblesTap;
  final VoidCallback onStructureTap;
  final VoidCallback onVolumeProfileTap;
  final VoidCallback onTradingTap;
  final VoidCallback onTradingConfigTap;
  final VoidCallback onExtremeTap;
  final VoidCallback onDateRangeTap;
  final bool tradingActive;

  const MapFlowAppBar({
    super.key,
    required this.onBubblesTap,
    required this.onStructureTap,
    required this.onVolumeProfileTap,
    required this.onTradingTap,
    required this.onTradingConfigTap,
    required this.onExtremeTap,
    required this.onDateRangeTap,
    this.tradingActive = false,
  });

  @override
  Size get preferredSize => const Size.fromHeight(48);

  @override
  State<MapFlowAppBar> createState() => _MapFlowAppBarState();
}

class _MapFlowAppBarState extends State<MapFlowAppBar> {
  bool _showTimeframe = false;
  final _clockKey = GlobalKey();
  OverlayEntry? _overlayEntry;
  Timer? _hoverTimer;

  @override
  void dispose() {
    _hoverTimer?.cancel();
    _overlayEntry?.remove();
    super.dispose();
  }

  void _closeTimeframe() {
    _hoverTimer?.cancel();
    _hoverTimer = null;
    _overlayEntry?.remove();
    _overlayEntry = null;
    if (_showTimeframe) setState(() => _showTimeframe = false);
  }

  void _startHoverTimer() {
    _hoverTimer?.cancel();
    _hoverTimer = Timer(const Duration(seconds: 3), _closeTimeframe);
  }

  void _cancelHoverTimer() {
    _hoverTimer?.cancel();
    _hoverTimer = null;
  }

  void _toggleTimeframe() {
    if (_showTimeframe) {
      _closeTimeframe();
      return;
    }
    final box =
        _clockKey.currentContext?.findRenderObject() as RenderBox?;
    if (box == null || !box.hasSize) return;
    final offset = box.localToGlobal(Offset.zero);
    _overlayEntry = OverlayEntry(
      builder: (context) => Positioned(
        left: offset.dx,
        top: offset.dy + box.size.height + 4,
        child: MouseRegion(
          onEnter: (_) => _cancelHoverTimer(),
          onExit: (_) => _startHoverTimer(),
          child: Material(
            color: const Color(0xFF2d2d2d),
            elevation: 4,
            shape: RoundedRectangleBorder(
              side: const BorderSide(color: Color(0xFF3d3d3d)),
              borderRadius: BorderRadius.circular(4),
            ),
            child: Padding(
              padding: const EdgeInsets.all(8),
              child: TimeframeSelector(
                onSelected: (_) => _closeTimeframe(),
              ),
            ),
          ),
        ),
      ),
    );
    Overlay.of(context).insert(_overlayEntry!);
    setState(() => _showTimeframe = true);
    _startHoverTimer();
  }

  void _handleBarPointerDown(PointerDownEvent event) {
    if (!_showTimeframe) return;
    final box = _clockKey.currentContext?.findRenderObject() as RenderBox?;
    if (box != null && box.hasSize) {
      final local = box.globalToLocal(event.position);
      if ((Offset.zero & box.size).contains(local)) return;
    }
    _closeTimeframe();
  }

  @override
  Widget build(BuildContext context) {
    return Consumer<StateService>(
      builder: (context, state, _) {
        final isLoading = state.isLoading;
        final connected = state.isConnected;

        return Listener(
          onPointerDown: _handleBarPointerDown,
          child: AppBar(
            toolbarHeight: 48,
            leadingWidth: 260,
          leading: SingleChildScrollView(
            scrollDirection: Axis.horizontal,
            child: Row(
              children: [
                const SizedBox(width: 8),
                IconButton(
                  key: _clockKey,
                  icon: Icon(
                    _showTimeframe ? Icons.schedule : Icons.access_time,
                    size: 20,
                    color: _showTimeframe ? Colors.blue : Colors.grey,
                  ),
                  onPressed: _toggleTimeframe,
                  tooltip: 'TimeFrame',
                  visualDensity: VisualDensity.compact,
                ),
                const SizedBox(width: 4),
                IconButton(
                  icon: const Icon(Icons.bubble_chart, size: 20),
                  onPressed: widget.onBubblesTap,
                  tooltip: 'Notificações de bubbles',
                  visualDensity: VisualDensity.compact,
                ),
                IconButton(
                  icon: const Icon(Icons.stacked_line_chart, size: 20),
                  onPressed: widget.onStructureTap,
                  tooltip: 'Notificações de estrutura',
                  visualDensity: VisualDensity.compact,
                ),
                IconButton(
                  icon: const Icon(Icons.align_horizontal_right, size: 20),
                  onPressed: widget.onVolumeProfileTap,
                  tooltip: 'Volume Profile',
                  visualDensity: VisualDensity.compact,
                ),
                IconButton(
                  icon: const Icon(Icons.show_chart, size: 20),
                  onPressed: widget.onTradingConfigTap,
                  tooltip: 'Trading Data',
                  visualDensity: VisualDensity.compact,
                ),
                IconButton(
                  icon: const Icon(Icons.linear_scale, size: 20),
                  onPressed: widget.onExtremeTap,
                  tooltip: 'Topos/Vales',
                  visualDensity: VisualDensity.compact,
                ),
                IconButton(
                  icon: const Icon(Icons.date_range, size: 20),
                  onPressed: widget.onDateRangeTap,
                  tooltip: 'Período / Dados Históricos',
                  visualDensity: VisualDensity.compact,
                ),
                // IconButton(
                //   icon: const Icon(Icons.verified, size: 20),
                //   onPressed: widget.onVerifierTap,
                //   tooltip: 'Verificador',
                //   visualDensity: VisualDensity.compact,
                // ),
              ],
            ),
          ),
          actions: [
            Padding(
              padding: const EdgeInsets.only(right: 8),
              child: DropdownButtonHideUnderline(
                child: DropdownButton<String>(
                  value: state.symbol.isEmpty ? null : state.symbol,
                  isDense: true,
                  dropdownColor: const Color(0xFF2d2d2d),
                  hint: const Text('Symbol',
                      style: TextStyle(color: Colors.grey)),
                  items: const [
                    DropdownMenuItem(
                        value: 'WINFUT', child: Text('WINFUT')),
                    DropdownMenuItem(
                        value: 'WDOFUT', child: Text('WDOFUT')),
                  ],
                  onChanged: isLoading
                      ? null
                      : (value) {
                          if (value != null) state.setSymbol(value);
                        },
                ),
              ),
            ),
            if (isLoading)
              const Padding(
                padding: EdgeInsets.symmetric(horizontal: 8),
                child: SizedBox(
                    width: 16,
                    height: 16,
                    child: CircularProgressIndicator(strokeWidth: 2)),
              )
            else
              IconButton(
                icon: Icon(
                  connected ? Icons.play_circle_filled : Icons.play_circle_outline,
                  size: 20,
                  color: connected ? Colors.green : Colors.grey,
                ),
                onPressed: () => state.loadData(),
                tooltip: connected ? 'Conectado' : 'Conectar',
              ),
            IconButton(
              icon: Icon(Icons.monetization_on,
                  size: 20,
                  color: widget.tradingActive ? Colors.blue : Colors.grey),
              onPressed: widget.onTradingTap,
              tooltip: 'Trading Panel',
            ),
          ],
          ),
        );
      },
    );
  }
}
