import 'package:flutter/material.dart';

import '../../models/defaults.dart';

/// Bandeira do ativo como imagem (asset oficial), usada no seletor de
/// símbolo e no cabeçalho do painel diário.
class SymbolFlag extends StatelessWidget {
  final String symbol;
  final double height;

  const SymbolFlag({super.key, required this.symbol, this.height = 14});

  @override
  Widget build(BuildContext context) {
    final asset = Defaults.flagFor(symbol);
    if (asset.isEmpty) return const SizedBox.shrink();
    return Image.asset(
      asset,
      height: height,
      fit: BoxFit.contain,
      filterQuality: FilterQuality.high,
    );
  }
}
