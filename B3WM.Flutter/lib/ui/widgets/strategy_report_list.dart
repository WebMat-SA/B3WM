import 'package:flutter/material.dart';
import '../../services/strategy_service.dart';

/// Formato único da linha do relatório (aba + overlay do gráfico).
String formatStrategyItem(StrategyDecisionItem d) {
  if (d.kind == 'execucao') {
    return '${d.time} ⚙ ${d.action}';
  }
  final conf = d.confidence > 0 ? ' ${d.confidence.toStringAsFixed(2)}' : '';
  return '${d.time} ${d.side}$conf → ${d.action}';
}

/// Cor por lado/execução (aba + overlay do gráfico).
Color strategyItemColor(StrategyDecisionItem d) {
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

/// Lista mono-espaçada dos itens (o scroll fica por conta do chamador).
class StrategyReportList extends StatelessWidget {
  final List<StrategyDecisionItem> items;
  const StrategyReportList({super.key, required this.items});

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        for (final d in items)
          Padding(
            padding: const EdgeInsets.symmetric(vertical: 1),
            child: Text(
              formatStrategyItem(d),
              style: TextStyle(
                  fontSize: 11,
                  fontFamily: 'monospace',
                  color: strategyItemColor(d)),
            ),
          ),
      ],
    );
  }
}
