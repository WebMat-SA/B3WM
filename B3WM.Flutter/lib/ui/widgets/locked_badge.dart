import 'package:flutter/material.dart';

/// Mensagem padrão do cadeado de lock da estratégia (issue #22, problema 6).
const String kLockedTooltipMessage =
    'Bloqueado pela execução da estratégia neste símbolo. '
    'PAUSE/STOP para alterar.';

/// Badge reutilizável de lock (cadeado warning + tooltip + semantics).
/// Visível só quando [locked] == true; quando false ocupa zero espaço
/// (não polui o layout destravado). Tooltip cobre hover desktop +
/// long-press mobile.
class LockedBadge extends StatelessWidget {
  final bool locked;
  final double size;
  const LockedBadge({super.key, required this.locked, this.size = 14});

  @override
  Widget build(BuildContext context) {
    if (!locked) return const SizedBox.shrink();
    return Semantics(
      label: kLockedTooltipMessage,
      child: Tooltip(
        message: kLockedTooltipMessage,
        child: Padding(
          padding: const EdgeInsets.only(left: 6),
          child: Icon(Icons.lock, size: size, color: Colors.orange),
        ),
      ),
    );
  }
}

/// Dot de lock p/ botões/tabs (issue #22, P5 sem footer): bolinha laranja
/// com cadeado, posicionada via `Stack` + `Positioned` sobre o ícone.
/// Estático, visível só quando [locked] (zero custo vertical/horizontal
/// extra quando destravado). Tooltip + Semantics com a mensagem padrão.
class StrategyLockDot extends StatelessWidget {
  final bool locked;
  const StrategyLockDot({super.key, required this.locked});

  @override
  Widget build(BuildContext context) {
    if (!locked) return const SizedBox.shrink();
    return Semantics(
      label: kLockedTooltipMessage,
      child: Tooltip(
        message: kLockedTooltipMessage,
        child: Container(
          width: 14,
          height: 14,
          decoration: BoxDecoration(
            color: Colors.orange,
            shape: BoxShape.circle,
            border: Border.all(color: const Color(0xFF2d2d2d), width: 1.5),
          ),
          child: const Icon(Icons.lock, size: 9, color: Colors.white),
        ),
      ),
    );
  }
}
