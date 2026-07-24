import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../services/state_service.dart';

class MapFlowAppBar extends StatelessWidget implements PreferredSizeWidget {
  final VoidCallback onSettingsTap;
  final VoidCallback onBubblesTap;
  final VoidCallback onTradingTap;
  final bool tradingActive;

  const MapFlowAppBar({
    super.key,
    required this.onSettingsTap,
    required this.onBubblesTap,
    required this.onTradingTap,
    this.tradingActive = false,
  });

  @override
  Size get preferredSize => const Size.fromHeight(48);

  @override
  Widget build(BuildContext context) {
    return Consumer<StateService>(
      builder: (context, state, _) {
        final isLoading =
            state.isLoading;
        final connected = state.isConnected;

        return AppBar(
          toolbarHeight: 48,
          leadingWidth: 150,
          leading: Row(
            children: [
              const SizedBox(width: 4),
              IconButton(
                icon: const Icon(Icons.settings, size: 20),
                onPressed: onSettingsTap,
                tooltip: 'Configurações',
              ),
              IconButton(
                icon: const Icon(Icons.bubble_chart, size: 20),
                onPressed: onBubblesTap,
                tooltip: 'Notificações de bubbles',
              ),
              IconButton(
                icon: Icon(Icons.travel_explore,
                    size: 20,
                    color: tradingActive ? Colors.amber : Colors.grey),
                onPressed: onTradingTap,
                tooltip: 'Trading Panel',
              ),
            ],
          ),
          actions: [
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
                        value: 'WINFUT', child: Text('WINFUT (Profit)')),
                    DropdownMenuItem(
                        value: 'WDOFUT', child: Text('WDOFUT (Profit)')),
                  ],
                  onChanged: isLoading
                      ? null
                      : (value) {
                          if (value != null) state.setSymbol(value);
                        },
                ),
              ),
            ),
          ],
        );
      },
    );
  }
}
