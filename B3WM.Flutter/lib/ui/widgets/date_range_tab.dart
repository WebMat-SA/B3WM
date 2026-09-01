import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../services/state_service.dart';
import '../../models/symbol_config.dart';

class DateRangeTab extends StatelessWidget {
  final bool noDrawer;
  const DateRangeTab({super.key, this.noDrawer = false});

  @override
  Widget build(BuildContext context) {
    return Consumer<StateService>(builder: (context, state, _) {
      return ListView(
        padding: const EdgeInsets.all(16),
        children: [
          _buildSectionTitle('Modo de Exibição'),
          SegmentedButton<DateRangeMode>(
            segments: const [
              ButtonSegment(
                value: DateRangeMode.intraday,
                label: Text('Intraday'),
                icon: Icon(Icons.today, size: 18),
              ),
              ButtonSegment(
                value: DateRangeMode.multiDay,
                label: Text('Múltiplos Dias'),
                icon: Icon(Icons.calendar_view_month, size: 18),
              ),
            ],
            selected: {state.dateRangeMode},
            onSelectionChanged: (s) => state.setDateRangeMode(s.first),
          ),

          if (state.dateRangeMode == DateRangeMode.multiDay) ...[
            const SizedBox(height: 16),
            _buildSectionTitle('Período (Últimos N dias)'),
            Wrap(
              spacing: 8,
              children: [1, 3, 5, 10, 20, 30].map((d) => FilterChip(
                label: Text('$d dias'),
                selected: state.lookbackDays == d,
                onSelected: (_) => state.setLookbackDays(d),
              )).toList(),
            ),

            const SizedBox(height: 16),
            _buildSectionTitle('Período Carregado'),
            Text(
              '${state.rangeStartDate?.toLocal().toString().split(' ')[0]} a ${state.rangeEndDate?.toLocal().toString().split(' ')[0]}',
              style: const TextStyle(fontSize: 13),
            ),
            const SizedBox(height: 4),
            Text(
              '${state.barsTimeFrameFilter.length} candles',
              style: const TextStyle(fontSize: 12, color: Colors.grey),
            ),

            const SizedBox(height: 16),
            _buildWarningCard(),
          ],
        ],
      );
    });
  }

  Widget _buildSectionTitle(String t) => Padding(
    padding: const EdgeInsets.only(bottom: 8),
    child: Text(t, style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 14)),
  );

  Widget _buildWarningCard() => Container(
    padding: const EdgeInsets.all(12),
    decoration: BoxDecoration(
      color: Colors.orange.withValues(alpha: 0.1),
      borderRadius: BorderRadius.circular(8),
      border: Border.all(color: Colors.orange.withValues(alpha: 0.3)),
    ),
    child: const Row(
      children: [
        Icon(Icons.info_outline, color: Colors.orange, size: 20),
        SizedBox(width: 8),
        Expanded(child: Text(
          'Modo Múltiplos Dias: dados históricos são estáticos. '
          'Apenas o dia atual recebe atualizações em tempo real (velas, bubbles, volume).',
          style: TextStyle(fontSize: 11, color: Colors.orange),
        )),
      ],
    ),
  );
}